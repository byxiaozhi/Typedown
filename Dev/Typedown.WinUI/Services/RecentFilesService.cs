using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Typedown.WinUI.Services
{
    // Reimplemented, not ported: the original AccessHistory (Typedown.Core\Services\AccessHistory.cs)
    // persisted recent files (and folders) to a SQLite database via EF Core, with an AccessTime column
    // and a full migrations setup. That's a lot of infrastructure for "remember the last 10 paths" —
    // this does the same job with a small JSON file next to Settings.json, same maxCount=10 and
    // most-recently-used ordering the original used. Folder history isn't covered — there's no folder
    // browsing pane yet (still #6's FolderPage gap) for it to matter to.
    public class RecentFilesService
    {
        private const int MaxCount = 10;
        private readonly string historyFile = Path.Combine(Config.GetLocalFolderPath(), "RecentFiles.json");

        public List<string> Files { get; private set; } = new();

        public RecentFilesService()
        {
            try
            {
                Files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(historyFile)) ?? new List<string>();
            }
            catch
            {
                Files = new List<string>();
            }
        }

        public void Record(string filePath)
        {
            Files.Remove(filePath);
            Files.Insert(0, filePath);
            while (Files.Count > MaxCount) Files.RemoveAt(Files.Count - 1);
            Save();
        }

        public void Remove(string filePath)
        {
            Files.Remove(filePath);
            Save();
        }

        // A file or folder renamed in the folder tree: its entries, and those of files under a renamed
        // folder, keep pointing at it. Reread first, as another window may have changed the list.
        public void RenamePath(string oldPath, string newPath)
        {
            try { Files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(historyFile)) ?? Files; } catch { }
            var root = oldPath.TrimEnd('\\', '/');
            var changed = false;
            for (var i = 0; i < Files.Count; i++)
            {
                if (!Files[i].Equals(root, System.StringComparison.OrdinalIgnoreCase)
                    && !Files[i].StartsWith(root + "\\", System.StringComparison.OrdinalIgnoreCase)) continue;
                Files[i] = newPath + Files[i].Substring(root.Length);
                changed = true;
            }
            if (changed) Save();
        }

        public void Clear()
        {
            Files.Clear();
            Save();
        }

        private void Save()
        {
            try { File.WriteAllText(historyFile, JsonConvert.SerializeObject(Files)); } catch { /* Ignore, matches SettingsViewModel.SaveAllSettings */ }
        }
    }
}
