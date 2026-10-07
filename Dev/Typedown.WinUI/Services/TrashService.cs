using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Typedown.WinUI.Services
{
    public class TrashEntry
    {
        public string Name { get; set; }
        public string OriginalPath { get; set; }
        public DateTime DeletedAt { get; set; }
    }

    // New in the sidebar restructure — a "Trash" nav entry needs something to show. This is NOT a
    // reimplementation of the Windows Recycle Bin (DeleteContext_Click already sends files there via
    // Microsoft.VisualBasic.FileIO.FileSystem's RecycleOption.SendToRecycleBin — that's the real
    // trash, and restoring from it is Explorer's job, not this app's). This is just a small log of
    // what Caret itself deleted and when, so the sidebar has something more useful than a dead end;
    // "Reveal" opens the real Recycle Bin (MainWindow's RevealTrashEntry) to actually restore something.
    public class TrashService
    {
        private const int MaxCount = 50;
        private readonly string trashFile = Path.Combine(Config.GetLocalFolderPath(), "Trash.json");

        public List<TrashEntry> Entries { get; private set; } = new();

        public TrashService() => Reload();

        // Reread before a change or a list build, so one window doesn't save over another's entries.
        public void Reload()
        {
            try
            {
                Entries = JsonConvert.DeserializeObject<List<TrashEntry>>(File.ReadAllText(trashFile)) ?? new List<TrashEntry>();
            }
            catch
            {
                Entries = new List<TrashEntry>();
            }
        }

        public void Record(string filePath)
        {
            Reload();
            Entries.Insert(0, new TrashEntry { Name = Path.GetFileName(filePath), OriginalPath = filePath, DeletedAt = DateTime.Now });
            while (Entries.Count > MaxCount) Entries.RemoveAt(Entries.Count - 1);
            Save();
        }

        public void Clear()
        {
            Reload();
            Entries.Clear();
            Save();
        }

        private void Save()
        {
            try { File.WriteAllText(trashFile, JsonConvert.SerializeObject(Entries)); } catch { /* Ignore, matches SettingsViewModel.SaveAllSettings */ }
        }
    }
}
