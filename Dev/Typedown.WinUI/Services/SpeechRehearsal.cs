using System;
using System.IO;
using System.Text;

namespace Typedown.WinUI.Services
{
    // New since the fork: the file of rehearsals beside a speech (docs/speech-marks-design.md, step 3). The teleprompter's rehearse
    // mode (Typedown.Editor/src/teleprompter/rehearsal.js) works out, from the keys the speaker pressed, a block of Markdown that
    // starts "## Rehearsal <date>"; this class puts it at the end of "<name>.rehearsal.md" next to "<name>.md" (one dated block per
    // run, the file is the speaker's to read, keep or delete) and finds the last block again for "Copy for AI". It never changes
    // the speech itself and touches nothing outside that one file. Plain .NET, so Caret.ConverterTests compiles it as it is.
    internal static class SpeechRehearsal
    {
        public const string Heading = "## Rehearsal ";

        // "talk.md" -> "talk.rehearsal.md" in the same folder; null for a document with no file yet.
        public static string PathFor(string documentPath)
        {
            if (string.IsNullOrWhiteSpace(documentPath)) return null;
            var folder = Path.GetDirectoryName(documentPath) ?? "";
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(documentPath) + ".rehearsal.md");
        }

        // The text of the file with the block added at the end (a new file starts with a title).
        public static string Append(string existing, string block, string title)
        {
            var baseText = (existing ?? "").TrimEnd();
            var added = (block ?? "").Replace("\r\n", "\n").TrimEnd();
            var head = baseText.Length > 0 ? baseText + "\n\n" : "# " + title + "\n\n";
            return head + added + "\n";
        }

        // The last run of such a file: from its last "## Rehearsal" heading to the end, or "" when there is none.
        public static string LastRun(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r\n", "\n");
            var at = text.LastIndexOf("\n" + Heading, StringComparison.Ordinal);
            var start = at >= 0 ? at + 1 : text.StartsWith(Heading, StringComparison.Ordinal) ? 0 : -1;
            return start >= 0 ? text.Substring(start).Trim() : "";
        }

        // Adds the block to the file beside the document; returns the path written. Throws on an unwritable folder.
        public static string Save(string documentPath, string block, string title)
        {
            var path = PathFor(documentPath) ?? throw new InvalidOperationException("The document has no file yet.");
            var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            var text = Append(existing, block, title);
            var temp = path + ".tmp";
            File.WriteAllText(temp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return path;
        }

        // The last run beside the document, or "" when there is no file or no run in it.
        public static string LastRunFor(string documentPath)
        {
            try
            {
                var path = PathFor(documentPath);
                return path != null && File.Exists(path) ? LastRun(File.ReadAllText(path, Encoding.UTF8)) : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
