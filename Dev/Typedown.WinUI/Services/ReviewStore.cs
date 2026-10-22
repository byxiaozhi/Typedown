using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Typedown.WinUI.Services
{
    // New since the fork: live review (docs/live-review-design.md, section 5). What tracking needs to go on after Caret was closed
    // (or crashed): the text the document is compared with (the baseline), the name and the start day, the baselines Undo accept can
    // go back to, and the first-seen day of each change (ReviewDates). It is kept in Caret's own data folder, `Review\<key>.md` for the
    // baseline and `Review\<key>.json` for the rest, where the key is made from the full path of the document: never next to the
    // user's file, never in it. Removed when tracking stops, when Caret deletes the file, and after 90 days without being opened.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal sealed class ReviewStore
    {
        // What is kept for one document. `SavedHash`: what the file on disk held when it was last saved by Caret (Hash), to tell on the
        // next open that it was edited elsewhere meanwhile.
        public sealed record Saved(string Path, string Baseline, string Author, string Started, string SavedHash, IReadOnlyList<string> Previous, IReadOnlyList<ReviewDates.Seen> Seen);

        // The most characters of earlier baselines kept (the newest stay), so that a long document does not make the copy huge.
        private const int PreviousLimit = 3_000_000;

        private readonly string folder;

        public ReviewStore(string folder) => this.folder = folder;

        // The same document gives the same key, whatever the case of its path.
        public static string KeyOf(string path)
        {
            var full = Path.GetFullPath(path).ToUpperInvariant();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full))).Substring(0, 32).ToLowerInvariant();
        }

        // Line endings and the newlines at the end do not count (the editor writes LF and may add a final one).
        public static string Hash(string text)
        {
            var plain = (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain))).ToLowerInvariant();
        }

        private string BaselineFile(string path) => System.IO.Path.Combine(folder, KeyOf(path) + ".md");

        private string DataFile(string path) => System.IO.Path.Combine(folder, KeyOf(path) + ".json");

        private sealed class Data
        {
            public int Version { get; set; } = 1;
            public string Path { get; set; }
            public string Author { get; set; }
            public string Started { get; set; }
            public string SavedHash { get; set; }
            public List<string> Previous { get; set; } = new();
            public List<ReviewDates.Seen> Seen { get; set; } = new();
        }

        public void Save(Saved state)
        {
            Directory.CreateDirectory(folder);
            var previous = new List<string>();
            var size = 0;
            foreach (var text in state.Previous.Reverse())
            {
                size += text.Length;
                if (size > PreviousLimit) break;
                previous.Insert(0, text);
            }
            var data = new Data
            {
                Path = state.Path,
                Author = state.Author,
                Started = state.Started,
                SavedHash = state.SavedHash,
                Previous = previous,
                Seen = state.Seen.ToList(),
            };
            Write(BaselineFile(state.Path), state.Baseline ?? "");
            Write(DataFile(state.Path), JsonSerializer.Serialize(data));
        }

        // A file is written whole under another name and then put in place, so a crash does not leave half of it.
        private static void Write(string file, string text)
        {
            var temp = file + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            File.Move(temp, file, overwrite: true);
        }

        // Null when nothing is kept for this document, or what is kept cannot be read.
        public Saved Load(string path)
        {
            try
            {
                var dataFile = DataFile(path);
                var baselineFile = BaselineFile(path);
                if (!File.Exists(dataFile) || !File.Exists(baselineFile)) return null;
                var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(dataFile, Encoding.UTF8));
                if (data == null || data.Version != 1 || string.IsNullOrEmpty(data.Author) || string.IsNullOrEmpty(data.Started)) return null;
                // opened: the 90 days start again
                File.SetLastWriteTimeUtc(dataFile, DateTime.UtcNow);
                return new Saved(path, File.ReadAllText(baselineFile, Encoding.UTF8), data.Author, data.Started, data.SavedHash,
                    data.Previous ?? new List<string>(), data.Seen ?? new List<ReviewDates.Seen>());
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        public void Remove(string path)
        {
            try
            {
                File.Delete(BaselineFile(path));
                File.Delete(DataFile(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        }

        // Removes what is kept for every document under `folder` (it went to the Trash): the copies hold the path of their document, so
        // they are found by it. Returns how many were removed.
        public int RemoveUnder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(this.folder)) return 0;
            var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var removed = 0;
            foreach (var data in Directory.GetFiles(this.folder, "*.json"))
            {
                try
                {
                    var saved = JsonSerializer.Deserialize<Data>(File.ReadAllText(data, Encoding.UTF8));
                    if (saved?.Path == null || !Path.GetFullPath(saved.Path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    File.Delete(data);
                    File.Delete(Path.ChangeExtension(data, ".md"));
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            }
            return removed;
        }

        // Removes what was not opened for `days` days (and a baseline or a temporary file left without its data); returns how many
        // documents' copies went.
        public int Cleanup(DateTime now, int days = 90)
        {
            if (!Directory.Exists(folder)) return 0;
            var removed = 0;
            try
            {
                foreach (var data in Directory.GetFiles(folder, "*.json"))
                {
                    if (File.GetLastWriteTimeUtc(data) > now.ToUniversalTime().AddDays(-days)) continue;
                    File.Delete(data);
                    File.Delete(System.IO.Path.ChangeExtension(data, ".md"));
                    removed++;
                }
                foreach (var other in Directory.GetFiles(folder, "*.md").Concat(Directory.GetFiles(folder, "*.tmp")))
                {
                    var isBaseline = other.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
                    if (isBaseline && File.Exists(System.IO.Path.ChangeExtension(other, ".json"))) continue;
                    File.Delete(other);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return removed;
        }
    }
}