using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Typedown.WinUI.Services
{
    // New since the fork: the folders of the Home page (the page shown when no note is open): the ones that were pinned, and the ones that were opened
    // last. A small JSON file next to Settings.json, like the favourite files. Every call reads the file again and writes it back, so a window never saves
    // over what another one just changed; a file that is missing or damaged is an empty list. Plain .NET, tested without the window.
    public sealed class FolderHistory
    {
        public const int MaxRecent = 12;

        private sealed class Data
        {
            public List<string> Pinned { get; set; } = new();
            public List<string> Recent { get; set; } = new();
        }

        private readonly string file;

        public FolderHistory(string file) => this.file = file;

        private static string Normal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())); }
            catch { return path.Trim(); }
        }

        private static bool Same(string a, string b) => string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

        private static bool SameOrUnder(string path, string folder)
        {
            var p = Normal(path);
            var f = Normal(folder);
            return string.Equals(p, f, StringComparison.OrdinalIgnoreCase)
                || p.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private Data Read()
        {
            try
            {
                var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Data();
                data.Pinned = Clean(data.Pinned);
                data.Recent = Clean(data.Recent);
                return data;
            }
            catch
            {
                return new Data();
            }
        }

        // Only real paths, each once.
        private static List<string> Clean(IEnumerable<string> list)
        {
            var result = new List<string>();
            foreach (var path in list ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(path) && !result.Any(p => Same(p, path))) result.Add(Normal(path));
            return result;
        }

        private void Write(Data data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                // straight into the file, as the favourites do: a temporary file next to it did not survive in a folder that OneDrive syncs
                File.WriteAllText(file, JsonSerializer.Serialize(data));
            }
            catch (Exception ex)
            {
                // a list that cannot be saved is lost with the session, not a reason to fail (the reason is there for the log)
                LastError = ex.Message;
            }
        }

        // Why the last write failed, or null.
        public string LastError { get; private set; }

        public IReadOnlyList<string> Pinned => Read().Pinned;

        public IReadOnlyList<string> Recent => Read().Recent;

        public bool IsPinned(string folder) => Read().Pinned.Any(p => Same(p, folder));

        // A folder that was opened: first among the recent ones.
        public void Record(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            var data = Read();
            data.Recent.RemoveAll(p => Same(p, folder));
            data.Recent.Insert(0, Normal(folder));
            if (data.Recent.Count > MaxRecent) data.Recent.RemoveRange(MaxRecent, data.Recent.Count - MaxRecent);
            Write(data);
        }

        // True when the folder is pinned now.
        public bool TogglePinned(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return false;
            var data = Read();
            if (data.Pinned.RemoveAll(p => Same(p, folder)) > 0)
            {
                Write(data);
                return false;
            }
            data.Pinned.Insert(0, Normal(folder));
            Write(data);
            return true;
        }

        // The folder is gone (deleted, or a folder above it): off both lists.
        public void Remove(string folder)
        {
            var data = Read();
            var removed = data.Pinned.RemoveAll(p => SameOrUnder(p, folder)) + data.Recent.RemoveAll(p => SameOrUnder(p, folder));
            if (removed > 0) Write(data);
        }

        // The folder was renamed or moved: it, and the folders under it, keep their place and their pin.
        public void Rename(string oldPath, string newPath)
        {
            var data = Read();
            var changed = false;
            string Move(string p)
            {
                if (!SameOrUnder(p, oldPath)) return p;
                changed = true;
                return Normal(newPath) + Normal(p).Substring(Normal(oldPath).Length);
            }
            data.Pinned = Clean(data.Pinned.Select(Move));
            data.Recent = Clean(data.Recent.Select(Move));
            if (changed) Write(data);
        }

        // What the Home page lists: the pinned folders first (the last one pinned first), then the recent ones that are not pinned, only the folders
        // that are still there, at most `max`.
        public IReadOnlyList<(string Path, bool Pinned)> Shelf(Func<string, bool> exists, int max = 8)
        {
            var data = Read();
            return data.Pinned.Where(exists).Select(p => (p, true))
                .Concat(data.Recent.Where(p => !data.Pinned.Any(q => Same(q, p))).Where(exists).Select(p => (p, false)))
                .Take(max).ToList();
        }
    }
}
