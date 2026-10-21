using System.IO;

namespace Typedown.WinUI.Services
{
    // The help topics that ship with the app (Help/<language>/<topic>.md, opened from the Help menu: MainWindow.Help.cs). Plain .NET
    // (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class HelpTopics
    {
        // The file names (without .md); the Help menu has one item for each.
        public static readonly string[] Names = { "whats-new", "getting-started", "review", "speech-marks", "teleprompter", "convert", "word-export", "punctuation" };

        // The file of a topic in the language, or the English one when the language has none.
        public static string PathOf(string root, string language, string topic)
        {
            var own = Path.Combine(root, "Help", language ?? "", topic + ".md");
            return !string.IsNullOrEmpty(language) && File.Exists(own) ? own : Path.Combine(root, "Help", "en", topic + ".md");
        }
    }
}
