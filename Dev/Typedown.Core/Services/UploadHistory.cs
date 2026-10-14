using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    /// <summary>
    /// What was uploaded with which configuration: a picture whose content (SHA-256) went up before with the same
    /// configuration gets its earlier address back instead of a second upload - inserted twice, uploaded again after an
    /// undo, or the same picture under two paths. The configuration counts as it is stored (method and every field), so
    /// a changed configuration uploads again. Kept as JSON in the application's local folder, the newest
    /// <see cref="Capacity"/> entries. Plain .NET, so the tests run it on any machine.
    /// </summary>
    public sealed class UploadHistory
    {
        public const int Capacity = 5000;

        public sealed class Entry
        {
            public string Scope { get; set; } = "";
            public string Content { get; set; } = "";
            public string Url { get; set; } = "";
            public DateTime Uploaded { get; set; }
        }

        private readonly string path;
        private readonly SemaphoreSlim gate = new(1, 1);
        private List<Entry> entries;

        public UploadHistory(string path)
        {
            this.path = path;
        }

        /// <summary>The configuration as a key: its method and stored fields (a change to any of them is another key).</summary>
        public static string Scope(string method, string storedConfig) => Hash(Encoding.UTF8.GetBytes(method + "\n" + storedConfig));

        public static string ContentHash(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(stream));
        }

        public async Task<int> CountAsync()
        {
            await gate.WaitAsync();
            try { return Load().Count; }
            finally { gate.Release(); }
        }

        /// <summary>
        /// The address of the file under this configuration: the recorded one, or <paramref name="upload"/>'s, which is
        /// then recorded. <paramref name="reused"/> says which. A failed upload records nothing.
        /// </summary>
        public async Task<(string url, bool reused)> UploadAsync(string scope, string filePath, Func<Task<string>> upload)
        {
            var content = ContentHash(filePath);
            await gate.WaitAsync();
            try
            {
                if (Load().LastOrDefault(e => e.Scope == scope && e.Content == content) is Entry known)
                    return (known.Url, true);
            }
            finally { gate.Release(); }

            var url = await upload();
            if (string.IsNullOrWhiteSpace(url))
                return (url, false);
            await gate.WaitAsync();
            try
            {
                var list = Load();
                list.RemoveAll(e => e.Scope == scope && e.Content == content);
                list.Add(new Entry { Scope = scope, Content = content, Url = url.Trim(), Uploaded = DateTime.UtcNow });
                if (list.Count > Capacity) list.RemoveRange(0, list.Count - Capacity);
                await SaveAsync(list);
            }
            finally { gate.Release(); }
            return (url, false);
        }

        public async Task ClearAsync()
        {
            await gate.WaitAsync();
            try
            {
                entries = new List<Entry>();
                await SaveAsync(entries);
            }
            finally { gate.Release(); }
        }

        private List<Entry> Load()
        {
            if (entries != null) return entries;
            try
            {
                entries = File.Exists(path) ? JsonConvert.DeserializeObject<List<Entry>>(File.ReadAllText(path)) ?? new List<Entry>() : new List<Entry>();
            }
            catch (Exception)
            {
                // A history that cannot be read only costs uploads; it is written fresh next time.
                entries = new List<Entry>();
            }
            return entries;
        }

        private Task SaveAsync(List<Entry> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return SafeFile.WriteAllTextAtomicAsync(path, JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        private static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));
    }
}
