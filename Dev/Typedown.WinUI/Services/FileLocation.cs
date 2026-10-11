using System;
using System.IO;

namespace Typedown.WinUI.Services
{
    // New since the fork: where a file is, for a person who works from the list of recent files. What Word and Excel offer in the Info page:
    // open the folder with the file selected, copy the path, copy a link to the file. Plain .NET (no WinUI), so the tests in
    // Caret.ConverterTests compile it as it is.
    internal static class FileLocation
    {
        internal enum Outcome
        {
            // Explorer opens with the file selected.
            SelectedFile,
            // The file is not there any more (moved, renamed or deleted): its folder is opened.
            OpenedFolder,
            // Neither the file nor its folder is there (a drive that is not connected, a deleted folder).
            NotFound,
        }

        // What to start explorer.exe with for this path, and what that is. A Windows path cannot contain a double quote.
        internal static (Outcome Outcome, string Arguments) Reveal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return (Outcome.NotFound, null);
            try
            {
                // a path that is not a full one would be taken from the folder Caret was started in: not a file of the user
                if (!Path.IsPathFullyQualified(path)) return (Outcome.NotFound, null);
                var full = Path.GetFullPath(path);
                if (File.Exists(full)) return (Outcome.SelectedFile, $"/select,\"{full}\"");
                var folder = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) return (Outcome.OpenedFolder, $"\"{folder}\"");
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
            {
            }
            return (Outcome.NotFound, null);
        }

        // The link to the file as a file: address (file:///C:/My%20Notes/a.md, file://server/share/a.md), which opens the file where the
        // viewer has it. null for a path that is not a full file path.
        internal static string Link(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!Path.IsPathFullyQualified(path)) return null;
                return new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
            {
                return null;
            }
        }
    }
}
