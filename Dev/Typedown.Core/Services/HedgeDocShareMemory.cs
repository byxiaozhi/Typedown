using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Typedown.Core.Services
{
    /// <summary>
    /// Remembers which HedgeDoc note a local file was shared to, so re-sharing an unchanged document hands back the
    /// same links instead of creating another note (HedgeDoc 1.x has no HTTP API to update an existing note).
    /// </summary>
    public static class HedgeDocShareMemory
    {
        public class Entry
        {
            public string NoteUrl { get; set; }
            public string PublishedUrl { get; set; }
            public ulong ContentHash { get; set; }
            public DateTime Time { get; set; }
        }

        private const int MaxEntries = 500;
        private static readonly object sync = new();
        private static Dictionary<string, Entry> entries;

        private static string StorePath => Path.Combine(Config.GetLocalFolderPath(), "hedgedoc-shares.json");

        private static void EnsureLoaded()
        {
            if (entries != null) return;
            try
            {
                entries = File.Exists(StorePath)
                    ? JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(StorePath)) ?? new()
                    : new();
            }
            catch
            {
                entries = new();
            }
        }

        public static Entry Get(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            lock (sync)
            {
                EnsureLoaded();
                return entries.TryGetValue(Normalize(path), out var entry) ? entry : null;
            }
        }

        public static void Set(string path, HedgeDocShareResult result, ulong contentHash)
        {
            if (string.IsNullOrEmpty(path) || result == null) return;
            Dictionary<string, Entry> snapshot;
            lock (sync)
            {
                EnsureLoaded();
                entries[Normalize(path)] = new Entry { NoteUrl = result.NoteUrl, PublishedUrl = result.PublishedUrl, ContentHash = contentHash, Time = DateTime.UtcNow };
                if (entries.Count > MaxEntries)
                    foreach (var key in entries.OrderBy(x => x.Value.Time).Take(entries.Count - MaxEntries).Select(x => x.Key).ToList())
                        entries.Remove(key);
                snapshot = new(entries);
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath, JsonConvert.SerializeObject(snapshot));
            }
            catch
            {
            }
        }

        private static string Normalize(string path) => Path.GetFullPath(path).ToLowerInvariant();
    }
}
