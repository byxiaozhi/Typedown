using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Typedown.WinUI.Services;

namespace Typedown.WinUI.Utilities
{
    // The one local history of this app: the versions of every note it saves, in the folder History of the app's data folder
    // (see Services/NoteHistory.cs for what is kept and for how long). Static, like AutoBackup next to it: a single folder, no
    // state of its own per window.
    internal static class LocalHistory
    {
        public static NoteHistory Notes { get; } = new(Path.Combine(Config.GetLocalFolderPath(), "History"));

        private static int pruned;

        // Once per run of the app, off the UI thread: the versions that have grown too old are removed, also those of notes that are
        // not saved again (several windows start, only the first one does it).
        public static void PruneOnce()
        {
            if (Interlocked.Exchange(ref pruned, 1) == 0) _ = Task.Run(Notes.PruneAll);
        }
    }
}
