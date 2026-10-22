using System;

namespace Typedown.WinUI.Services
{
    // New since the fork: the small facts the status bar shows about a note (the live status bar, MainWindow.StatusBar.cs).
    // Plain .NET, tested without the window.
    internal static class StatusFacts
    {
        // The reading speed the reading time is worked out at (words a minute): a rough, careful figure, and the tooltip says it.
        public const int ReadingWordsPerMinute = 200;

        // Whole minutes to read `words` words, rounded to the nearest; 0 means "less than a minute" (fewer than 100 words).
        public static int ReadingMinutes(int words) =>
            words <= 0 ? 0 : (int)Math.Round(words / (double)ReadingWordsPerMinute, MidpointRounding.AwayFromZero);

        // The line ends a text was read with, when they are not the LF Caret writes: "CRLF" (Windows), "CR" (old Mac), else null.
        public static string LineEndingOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Contains("\r\n", StringComparison.Ordinal)) return "CRLF";
            return text.Contains('\r') ? "CR" : null;
        }
    }
}
