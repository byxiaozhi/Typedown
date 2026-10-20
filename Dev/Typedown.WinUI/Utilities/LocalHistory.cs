using System.IO;
using Typedown.WinUI.Services;

namespace Typedown.WinUI.Utilities
{
    // The one local history of this app: the versions of every note it saves, in the folder History of the app's data folder
    // (see Services/NoteHistory.cs for what is kept and for how long). Static, like AutoBackup next to it: a single folder, no
    // state of its own per window.
    internal static class LocalHistory
    {
        public static NoteHistory Notes { get; } = new(Path.Combine(Config.GetLocalFolderPath(), "History"));
    }
}
