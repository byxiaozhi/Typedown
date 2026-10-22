using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Typedown.WinUI.Services
{
    // New in the sidebar restructure (Phase 2 of the warm-autumn reskin) — nothing in the original
    // Typedown had a favorites/starred-files concept. Same shape as RecentFilesService: a small JSON
    // file next to Settings.json, no database. Unlike Recent, favorites are user-curated (Toggle, not
    // an MRU eviction), so there's no MaxCount trim.
    public class FavoritesService
    {
        private readonly string favoritesFile = Path.Combine(Config.GetLocalFolderPath(), "Favorites.json");

        public List<string> Files { get; private set; } = new();

        public FavoritesService() => Reload();

        // Every window has its own instance; reading the file again before a change or a list build
        // keeps one window from saving over what another just added or removed.
        public void Reload()
        {
            try
            {
                Files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(favoritesFile)) ?? new List<string>();
            }
            catch
            {
                Files = new List<string>();
            }
        }

        public bool Contains(string filePath) => !string.IsNullOrEmpty(filePath) && Files.Contains(filePath);

        // Returns the new favorite state (true = now favorited) — lets a single call drive a toggle
        // button's checked state without the caller needing its own Contains check first.
        public bool Toggle(string filePath)
        {
            Reload();
            if (Files.Remove(filePath))
            {
                Save();
                return false;
            }
            Files.Insert(0, filePath);
            Save();
            return true;
        }

        // The path itself, and everything under it when it's a deleted folder.
        public void Remove(string path)
        {
            Reload();
            if (Files.RemoveAll(f => IsSameOrUnder(f, path)) > 0) Save();
        }

        // Called after a rename/move so a favorited file doesn't silently fall out of the list —
        // mirrors FileViewModel.RenamePathOnly's role in keeping RecentFiles in sync.
        // A renamed folder carries the favorites inside it along.
        public void RenamePath(string oldPath, string newPath)
        {
            Reload();
            var changed = false;
            for (var i = 0; i < Files.Count; i++)
            {
                if (!IsSameOrUnder(Files[i], oldPath)) continue;
                Files[i] = newPath + Files[i].Substring(oldPath.Length);
                changed = true;
            }
            if (changed) Save();
        }

        private static bool IsSameOrUnder(string path, string root)
        {
            root = root.TrimEnd('\\', '/');
            return path.Equals(root, System.StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + "\\", System.StringComparison.OrdinalIgnoreCase);
        }

        // Called after Delete so a favorited file doesn't linger as a dead entry pointing at a
        // recycled path.
        public void RemoveMissing() => Files.RemoveAll(f => !File.Exists(f));

        private void Save()
        {
            try { File.WriteAllText(favoritesFile, JsonConvert.SerializeObject(Files)); } catch { /* Ignore, matches SettingsViewModel.SaveAllSettings */ }
        }
    }
}
