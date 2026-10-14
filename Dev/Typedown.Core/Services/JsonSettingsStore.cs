using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    /// <summary>
    /// Owns the in-memory settings object and persists coherent snapshots in order. A burst of property changes
    /// is folded into the newest pending snapshot, so two writes never race and an older snapshot can never land
    /// after a newer one.
    ///
    /// The application has one store per settings file (<see cref="Shared"/>), used by every window. A store per
    /// window read the file once and wrote its whole snapshot back, so a window holding an older snapshot put back
    /// values another window had changed since, and no window heard of another's changes. Each window now
    /// listens to <see cref="Changed"/> and applies what others change.
    /// </summary>
    public sealed class JsonSettingsStore
    {
        private static readonly Dictionary<string, JsonSettingsStore> shared = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The one store for <paramref name="path"/> in this process.</summary>
        public static JsonSettingsStore Shared(string path, Action<Exception> onWriteError = null)
        {
            var key = Path.GetFullPath(path);
            lock (shared)
            {
                if (!shared.TryGetValue(key, out var store))
                    shared[key] = store = new JsonSettingsStore(key, onWriteError: onWriteError);
                return store;
            }
        }

        /// <summary>
        /// A value changed: its name (null after <see cref="Reset"/>: all of them) and the origin passed to the call
        /// that changed it, so a window can skip its own changes. Raised outside the store's lock, on the thread
        /// that made the change.
        /// </summary>
        public event Action<string, object> Changed;

        /// <summary>Advances with every change that altered a value.</summary>
        public long Revision { get; private set; }

        // How often each value changed, and how often everything was reset: what RevisionOf adds up.
        private readonly Dictionary<string, long> changes = new(StringComparer.Ordinal);
        private long resets;

        /// <summary>
        /// Advances with every change of one of <paramref name="names"/> (and with a reset): the automation API's
        /// settingsRevision, which counts the exposed settings only.
        /// </summary>
        public long RevisionOf(IEnumerable<string> names)
        {
            lock (sync)
            {
                var revision = resets;
                foreach (var name in names)
                    if (changes.TryGetValue(name, out var count)) revision += count;
                return revision;
            }
        }

        private readonly object sync = new();
        private readonly string path;
        private readonly Func<string, string, Task> writer;
        private readonly Action<Exception> onWriteError;
        private JObject values;
        private bool writeRequested;
        private bool writerRunning;
        private Task writerTask = Task.CompletedTask;

        public Exception LastWriteError { get; private set; }

        public JsonSettingsStore(string path, Func<string, string, Task> writer = null, Action<Exception> onWriteError = null)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.writer = writer ?? ((target, json) => SafeFile.WriteAllTextAtomicAsync(target, json));
            this.onWriteError = onWriteError;
            values = Load(path, out var error);
            LoadError = error;
            LoadedNames = values.Properties().Select(p => p.Name).ToArray();
        }

        /// <summary>The names the file held when the store was made, for the log: a value missing on the next run shows when it went.</summary>
        public IReadOnlyList<string> LoadedNames { get; }

        /// <summary>Why the file that was there could not be read (null: read, or there was none).</summary>
        public Exception LoadError { get; }

        private static JObject Load(string path, out Exception error)
        {
            error = null;
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                if (File.Exists(path)) error = ex;
                // A missing or damaged file must not prevent startup. The next successful setting change replaces
                // it atomically with a valid document; unknown values in a valid old file remain in the JObject.
                return new JObject();
            }
        }

        public T Get<T>(string name, T defaultValue = default)
        {
            lock (sync)
            {
                try
                {
                    var token = values[name];
                    if (token == null || token.Type == JTokenType.Null)
                        return defaultValue;
                    var value = token.ToObject<T>();
                    return value is null ? defaultValue : value;
                }
                catch
                {
                    // Old builds may have stored a different type. One incompatible value should fall back to its
                    // current default without discarding the rest of the settings file.
                    return defaultValue;
                }
            }
        }

        /// <summary>A copy of the stored value (null when there is none), for a window keeping its own copy.</summary>
        public JToken GetToken(string name)
        {
            lock (sync) return values.TryGetValue(name, out var token) ? token.DeepClone() : null;
        }

        public void Set<T>(string name, T value, object origin = null)
        {
            var token = ToToken(value);
            lock (sync)
            {
                if (values.TryGetValue(name, out var old) && JToken.DeepEquals(old, token)) return;
                values[name] = token;
                Revision++;
                changes[name] = (changes.TryGetValue(name, out var count) ? count : 0) + 1;
                QueueWriteLocked();
            }
            RaiseChanged(name, origin);
        }

        public void Reset(object origin = null)
        {
            lock (sync)
            {
                values = new JObject();
                Revision++;
                resets++;
                QueueWriteLocked();
            }
            RaiseChanged(null, origin);
        }

        private void RaiseChanged(string name, object origin)
        {
            var handlers = Changed;
            if (handlers == null) return;
            // One listener failing (a window being torn down) must not keep the others from hearing of the change.
            foreach (Action<string, object> handler in handlers.GetInvocationList())
            {
                try { handler(name, origin); }
                catch (Exception ex) { try { onWriteError?.Invoke(ex); } catch { } }
            }
        }

        private static JToken ToToken<T>(T value)
        {
            if (value == null)
                return JValue.CreateNull();
            if (value is string || value is long || value is int || value is short || value is sbyte || value is ulong ||
                value is uint || value is ushort || value is byte || value is Enum || value is double || value is float ||
                value is decimal || value is DateTime || value is byte[] || value is bool || value is Guid || value is Uri ||
                value is TimeSpan)
                return new JValue(value);
            return JToken.FromObject(value);
        }

        private void QueueWriteLocked()
        {
            writeRequested = true;
            if (writerRunning)
                return;
            writerRunning = true;
            writerTask = Task.Run(DrainWritesAsync);
        }

        private async Task DrainWritesAsync()
        {
            while (true)
            {
                string snapshot;
                lock (sync)
                {
                    if (!writeRequested)
                    {
                        writerRunning = false;
                        return;
                    }
                    writeRequested = false;
                    snapshot = values.ToString(Formatting.Indented);
                }

                try
                {
                    await writer(path, snapshot);
                    lock (sync) LastWriteError = null;
                }
                catch (Exception ex)
                {
                    lock (sync) LastWriteError = ex;
                    try { onWriteError?.Invoke(ex); } catch { }
                }
            }
        }

        /// <summary>Waits until every change queued before or during this call has reached a terminal write.</summary>
        public async Task FlushAsync()
        {
            while (true)
            {
                Task pending;
                lock (sync) pending = writerTask;
                await pending;
                lock (sync)
                {
                    if (!writerRunning && !writeRequested)
                        return;
                }
            }
        }
    }
}
