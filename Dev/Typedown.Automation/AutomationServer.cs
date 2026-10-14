using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    /// <summary>A transport that hands over one connected client stream at a time (a named pipe, a Unix socket).</summary>
    public interface IConnectionListener : IDisposable
    {
        /// <summary>Waits for the next client. Throws <see cref="OperationCanceledException"/> when stopped.</summary>
        Task<Stream> AcceptAsync(CancellationToken cancellationToken);
    }

    /// <summary>What the application shows about automation: who is connected and what was just written.</summary>
    public sealed class AutomationActivity
    {
        public int ConnectionCount { get; set; }
        /// <summary>Client names as they declared them (self-reported, trimmed and shortened for display).</summary>
        public IReadOnlyList<string> Clients { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// The local automation service: accepts clients from a listener, gives each its own session and method table,
    /// and reports connections so every window can show that automation is connected (a marker clients cannot hide,
    /// docs/automation-api-spec.md section 5.1). Off until <see cref="Start"/>; <see cref="StopAsync"/> closes the
    /// listener and every connection, so no endpoint is left while the switch is off.
    /// </summary>
    public sealed class AutomationServer
    {
        public const int DefaultMaxConnections = 8;

        private readonly Func<IConnectionListener> listenerFactory;
        private readonly Func<AutomationSession> sessionFactory;
        private readonly long maxMessageBytes;
        private readonly int maxConnections;
        private readonly object gate = new();
        private readonly List<(JsonRpcConnection connection, AutomationSession session, Stream stream)> connections = new();
        private CancellationTokenSource? stopping;
        private IConnectionListener? listener;
        private Task? acceptLoop;

        public AutomationServer(Func<IConnectionListener> listenerFactory, Func<AutomationSession> sessionFactory, long maxMessageBytes, int maxConnections = DefaultMaxConnections)
        {
            this.listenerFactory = listenerFactory;
            this.sessionFactory = sessionFactory;
            this.maxMessageBytes = maxMessageBytes;
            this.maxConnections = maxConnections;
        }

        /// <summary>Raised on the thread that noticed the change; handlers marshal to their UI themselves.</summary>
        public event Action<AutomationActivity>? ActivityChanged;

        /// <summary>
        /// The listener failed for good (for example another process already holds the endpoint name): the service
        /// stops listening and says why. Connections already open are not affected.
        /// </summary>
        public event Action<Exception>? ListenerFailed;

        public bool IsRunning
        {
            get { lock (gate) return stopping != null; }
        }

        public void Start()
        {
            lock (gate)
            {
                if (stopping != null) return;
                stopping = new CancellationTokenSource();
                listener = listenerFactory();
                var token = stopping.Token;
                var current = listener;
                acceptLoop = Task.Run(() => AcceptLoopAsync(current, token));
            }
        }

        public async Task StopAsync()
        {
            Task? loop;
            List<Stream> streams;
            lock (gate)
            {
                if (stopping == null) return;
                stopping.Cancel();
                listener?.Dispose();
                listener = null;
                stopping = null;
                loop = acceptLoop;
                streams = connections.Select(c => c.stream).ToList();
            }
            foreach (var stream in streams)
            {
                try { stream.Dispose(); } catch { }
            }
            if (loop != null)
            {
                try { await loop.ConfigureAwait(false); } catch { }
            }
        }

        public AutomationActivity Activity
        {
            get
            {
                lock (gate)
                    return new AutomationActivity
                    {
                        ConnectionCount = connections.Count,
                        Clients = connections.Select(c => DisplayName(c.session.Client?.Name)).ToList(),
                    };
            }
        }

        /// <summary>A self-reported client name made safe to show: printable, at most 40 characters.</summary>
        public static string DisplayName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var printable = new string(name!.Where(c => !char.IsControl(c) && c != '‮' && c != '‭').ToArray()).Trim();
            return printable.Length <= 40 ? printable : printable.Substring(0, 39) + "…";
        }

        private async Task AcceptLoopAsync(IConnectionListener current, CancellationToken token)
        {
            var clients = new List<Task>();
            while (!token.IsCancellationRequested)
            {
                Stream stream;
                try { stream = await current.AcceptAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (IOException)
                {
                    // A client that went away while connecting; keep listening.
                    continue;
                }
                catch (Exception e)
                {
                    try { ListenerFailed?.Invoke(e); } catch { }
                    break;
                }
                bool full;
                lock (gate) full = connections.Count >= maxConnections;
                if (full)
                {
                    try { stream.Dispose(); } catch { }
                    continue;
                }
                clients.RemoveAll(t => t.IsCompleted);
                clients.Add(Task.Run(() => ServeAsync(stream, token)));
            }
            try { await Task.WhenAll(clients).ConfigureAwait(false); } catch { }
        }

        private async Task ServeAsync(Stream stream, CancellationToken token)
        {
            var session = sessionFactory();
            var connection = new JsonRpcConnection(new MessageFraming(stream, maxMessageBytes), session);
            session.Initialized += () => Notify();
            lock (gate) connections.Add((connection, session, stream));
            Notify();
            try
            {
                await connection.RunAsync(token).ConfigureAwait(false);
            }
            finally
            {
                lock (gate) connections.RemoveAll(c => ReferenceEquals(c.connection, connection));
                connection.Dispose();
                try { stream.Dispose(); } catch { }
                Notify();
            }
        }

        private void Notify()
        {
            var activity = Activity;
            try { ActivityChanged?.Invoke(activity); } catch { }
        }
    }
}
