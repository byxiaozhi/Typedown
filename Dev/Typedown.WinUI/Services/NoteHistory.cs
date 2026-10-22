using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Typedown.WinUI.Services
{
    // One earlier version of a note in the History tab. `Id` names the snapshot; `Delta` is the change in length against the
    // version before it (characters); `FirstChange` is the first line that is not as it was.
    internal sealed record HistoryEntry(string Id, DateTime Time, int Length, int Delta, string FirstChange, bool IsFirst);

    // New since the fork: the local history of a note (the History tab of the inspector pane). Each time a note is saved,
    // its text is kept as a snapshot in one folder under the app's data folder, so that an earlier version can be looked at
    // and brought back. Nothing leaves the PC. What is kept is limited: the newest MaxVersions snapshots of a note, none
    // older than MaxAge (the newest is always kept), and a note longer than MaxLength characters is not kept.
    //
    // A snapshot is taken only when the text differs from the last snapshot, and, for the automatic saves, only when the
    // last one is older than MinGap (so that typing with auto save on does not fill the history with near copies); a save
    // the user asked for (a checkpoint) is always kept.
    //
    // The snapshots are plain files in one folder, named "<hash of the path>_<file name>~<time>.md" (no folder per note).
    // Plain .NET (the folder and the clock are given), tested without the window.
    internal sealed class NoteHistory
    {
        public const int DefaultMaxVersions = 50;
        public const int DefaultMaxDays = 30;
        public const int MaxLength = 2_000_000;
        public static readonly TimeSpan DefaultMinGap = TimeSpan.FromMinutes(2);

        private const string Extension = ".md";
        private const char Separator = '~';
        private const string StampFormat = "yyyyMMdd'T'HHmmssfff'Z'";

        private readonly string root;
        private readonly Func<DateTime> utcNow;

        public int MaxVersions { get; }
        public TimeSpan MaxAge { get; }
        public TimeSpan MinGap { get; }

        public NoteHistory(string root, Func<DateTime> utcNow = null, int maxVersions = DefaultMaxVersions, int maxDays = DefaultMaxDays, TimeSpan? minGap = null)
        {
            this.root = root;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
            MaxVersions = maxVersions;
            MaxAge = TimeSpan.FromDays(maxDays);
            MinGap = minGap ?? DefaultMinGap;
        }

        // What the file names of a note start with: a hash of its path (so two notes of the same name apart do not share a history)
        // and its file name.
        private static string PrefixOf(string documentPath)
        {
            var full = Path.GetFullPath(documentPath);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant()))).Substring(0, 12).ToLowerInvariant();
            var invalid = Path.GetInvalidFileNameChars();
            var name = new string(Path.GetFileName(full).ToLowerInvariant().Select(c => Array.IndexOf(invalid, c) >= 0 || c == '.' || c == Separator ? '_' : c).ToArray());
            if (name.Length > 60) name = name.Substring(0, 60);
            return hash + "_" + name + Separator;
        }

        // Keeps `text` as the newest version of the note when it should be kept (see above); says whether it was.
        public bool Record(string documentPath, string text, bool checkpoint = false)
        {
            try
            {
                if (string.IsNullOrEmpty(documentPath) || string.IsNullOrWhiteSpace(text) || text.Length > MaxLength) return false;
                var prefix = PrefixOf(documentPath);
                var files = Snapshots(prefix);
                var now = utcNow();
                if (files.Count > 0)
                {
                    var latest = files[^1];
                    if (!checkpoint && now - TimeOf(latest) < MinGap) return false;
                    if (File.ReadAllText(latest, Encoding.UTF8) == text) return false;
                }
                Directory.CreateDirectory(root);
                var stamp = now;
                string path;
                // two snapshots in the same millisecond: the later one is a millisecond later
                while (File.Exists(path = Path.Combine(root, prefix + stamp.ToString(StampFormat) + Extension))) stamp = stamp.AddMilliseconds(1);
                File.WriteAllText(path, text, new UTF8Encoding(false));
                Prune(prefix);
                return true;
            }
            catch
            {
                // the history is a convenience: a full disk or a locked folder must not stop a save
                return false;
            }
        }

        // The versions of the note, newest first. The text of each is compared with the version before it (the oldest has none).
        public List<HistoryEntry> List(string documentPath)
        {
            var entries = new List<HistoryEntry>();
            try
            {
                var files = Snapshots(PrefixOf(documentPath));
                string previous = null;
                foreach (var file in files)
                {
                    var text = File.ReadAllText(file, Encoding.UTF8);
                    var first = previous == null;
                    entries.Add(new HistoryEntry(
                        IdOf(file),
                        TimeOf(file).ToLocalTime(),
                        text.Length,
                        first ? text.Length : text.Length - previous.Length,
                        first ? "" : LineDiff.FirstChangedLine(previous, text),
                        first));
                    previous = text;
                }
            }
            catch
            {
            }
            entries.Reverse();
            return entries;
        }

        public string Read(string documentPath, string id)
        {
            try
            {
                var path = Path.Combine(root, PrefixOf(documentPath) + id + Extension);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        // The id of the newest version, or null: a cheap way to see whether the list has changed (no file is read).
        public string LatestId(string documentPath)
        {
            try
            {
                var files = Snapshots(PrefixOf(documentPath));
                return files.Count == 0 ? null : IdOf(files[^1]);
            }
            catch
            {
                return null;
            }
        }

        public bool Any(string documentPath) => LatestId(documentPath) != null;

        // The whole history of the note is deleted.
        public void Clear(string documentPath)
        {
            try
            {
                foreach (var file in Snapshots(PrefixOf(documentPath))) File.Delete(file);
            }
            catch
            {
            }
        }

        // A renamed or moved note takes its history along (the file names start with the path's hash and the name).
        public void Move(string oldPath, string newPath)
        {
            try
            {
                var from = PrefixOf(oldPath);
                var to = PrefixOf(newPath);
                if (from == to) return;
                var files = Snapshots(from);
                if (files.Count == 0) return;
                foreach (var old in Snapshots(to)) File.Delete(old);
                foreach (var file in files)
                    File.Move(file, Path.Combine(root, to + IdOf(file) + Extension), overwrite: true);
            }
            catch
            {
            }
        }

        // Every note's old versions are pruned, not only the note being saved: versions expire with time even when a note is never
        // saved again, or the history is off (run when the app starts). The newest version of a note is always kept.
        public void PruneAll()
        {
            try
            {
                if (!Directory.Exists(root)) return;
                var prefixes = Directory.GetFiles(root, "*" + Extension)
                    .Select(Path.GetFileName)
                    .Where(name => name != null && name.IndexOf(Separator) > 0)
                    .Select(name => name.Substring(0, name.LastIndexOf(Separator) + 1))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var prefix in prefixes) Prune(prefix);
            }
            catch
            {
            }
        }

        // The snapshot files of a note, oldest first (the time in the name sorts as text).
        private List<string> Snapshots(string prefix) =>
            Directory.Exists(root)
                ? Directory.GetFiles(root, prefix + "*" + Extension)
                    .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(f => f, StringComparer.Ordinal).ToList()
                : new List<string>();

        // "20261020T123000123Z": the part of the file name after the separator.
        private static string IdOf(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            return name.Substring(name.LastIndexOf(Separator) + 1);
        }

        private static DateTime TimeOf(string file) =>
            DateTime.ParseExact(IdOf(file), StampFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);

        // The oldest snapshots beyond MaxVersions go, and those older than MaxAge, but never the newest.
        private void Prune(string prefix)
        {
            var files = Snapshots(prefix);
            var cutoff = utcNow() - MaxAge;
            for (var i = 0; i < files.Count - 1; i++)
            {
                var beyondCount = files.Count - i > MaxVersions;
                if (beyondCount || TimeOf(files[i]) < cutoff) File.Delete(files[i]);
            }
        }
    }
}
