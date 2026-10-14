using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Typedown.Core.Services
{
    /// <summary>
    /// The set of documents open when the last window closed, so the next start can bring them all back as tabs
    /// (startup action "restore last session"). Cursor positions come from <see cref="CursorMemory"/>, the window
    /// placement from the window itself, so only the file list and the active index live here.
    /// </summary>
    public static class SessionMemory
    {
        public class Session
        {
            public List<string> Files { get; set; } = new();
            public int ActiveIndex { get; set; }
            public DateTime Time { get; set; }
        }

        private static string StorePath => Path.Combine(Config.GetLocalFolderPath(), "session.json");

        public static Session Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return null;
                var session = JsonConvert.DeserializeObject<Session>(File.ReadAllText(StorePath));
                if (session?.Files == null) return null;
                // Files that vanished meanwhile are dropped; the active index follows the surviving list.
                var active = session.ActiveIndex >= 0 && session.ActiveIndex < session.Files.Count ? session.Files[session.ActiveIndex] : null;
                session.Files = session.Files.Where(File.Exists).ToList();
                session.ActiveIndex = Math.Max(0, session.Files.IndexOf(active));
                return session.Files.Count > 0 ? session : null;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(IEnumerable<string> files, int activeIndex)
        {
            try
            {
                var list = files.Where(f => !string.IsNullOrEmpty(f)).ToList();
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                File.WriteAllText(StorePath, JsonConvert.SerializeObject(new Session { Files = list, ActiveIndex = Math.Max(0, activeIndex), Time = DateTime.UtcNow }));
            }
            catch
            {
            }
        }
    }
}
