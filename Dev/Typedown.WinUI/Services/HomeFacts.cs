using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services
{
    // New since the fork: the small facts the top of the Home page is made of, with no window needed (tested in Caret.ConverterTests): the line of a
    // note that says what it is about, the tip of the day, and how many comments wait in a note. Everything is read from files on the PC; nothing is
    // sent anywhere and nothing is guessed by a model: plain rules.
    internal static class HomeFacts
    {
        // The first line of the note that says something, as plain text: the front matter, the headings (when anything else follows), code, tables,
        // rules and comments are passed over; list and quote marks, emphasis, links and pictures are taken off. Empty when there is nothing.
        public static string Excerpt(string markdown, int max = 140)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var i = 0;
            if (lines[0].Trim() == "---")
            {
                var end = Array.FindIndex(lines, 1, l => l.Trim() is "---" or "...");
                if (end > 0) i = end + 1;
                else
                {
                    // no closing line (or it is beyond what was read): when what follows reads as properties, it is front matter that is cut short and
                    // has nothing to say; otherwise the line was a rule and the note goes on after it
                    var next = lines.Skip(1).FirstOrDefault(l => l.Trim().Length > 0);
                    if (next != null && Regex.IsMatch(next, @"^[A-Za-z_][\w-]*\s*:")) return "";
                    i = 1;
                }
            }
            string firstHeading = null;
            var inCode = false;
            var inComment = false;
            for (; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (inComment) { if (line.Contains("-->")) inComment = false; continue; }
                if (line.StartsWith("```") || line.StartsWith("~~~")) { inCode = !inCode; continue; }
                if (inCode || line.Length == 0) continue;
                if (line.StartsWith("<!--"))
                {
                    inComment = !line.Contains("-->"); // a comment over several lines is passed over to its end
                    continue;
                }
                if (line.StartsWith("|") || Regex.IsMatch(line, @"^([-*_])(\s*\1){2,}$")) continue;
                if (line.StartsWith("#"))
                {
                    firstHeading ??= Plain(line.TrimStart('#'));
                    continue;
                }
                var text = Plain(line);
                if (text.Length > 0) return Cut(text, max);
            }
            return string.IsNullOrEmpty(firstHeading) ? "" : Cut(firstHeading, max);
        }

        private static string Plain(string line)
        {
            line = Regex.Replace(line, @"^(>\s*)+", "");
            line = Regex.Replace(line, @"^([-*+]|\d+[.)])\s+(\[[ xX]\]\s+)?", "");
            line = Regex.Replace(line, @"!\[[^\]]*\]\([^)]*\)", "");           // pictures
            line = Regex.Replace(line, @"\[([^\]]*)\]\([^)]*\)", "$1");         // links: the words
            line = Regex.Replace(line, @"\{>>.*?<<\}", "");                    // comments
            line = line.Replace("**", "").Replace("__", "").Replace("`", "").Replace("~~", "");
            line = Regex.Replace(line, @"(?<![\w*])\*(?=\S)|(?<=\S)\*(?![\w*])", "");
            line = Regex.Replace(line, @"(?<!\w)_(?=\S)|(?<=\S)_(?!\w)", "");
            line = Regex.Replace(line, @"\s+", " ").Trim();
            return Regex.Replace(line, @"\s+([.,;:!?])", "$1"); // what a removed comment leaves before a full stop
        }

        private static string Cut(string text, int max)
        {
            if (text.Length <= max) return text;
            var cut = text.LastIndexOf(' ', max);
            if (cut < max / 2) cut = max;
            return text.Substring(0, cut).TrimEnd(' ', ',', ';', ':', '.') + "…";
        }

        // The tip of the day: one shortcut of the list, the same all day and another tomorrow (the days are counted from a fixed day, so the
        // tips come round in the order of the list). -1 for an empty list.
        public static int TipIndex(DateTime day, int count)
        {
            if (count <= 0) return -1;
            var days = (int)(day.Date - new DateTime(2026, 1, 1)).TotalDays;
            return ((days % count) + count) % count;
        }

        // The comments of the note that have been left for someone: what the Review tab of the inspector lists as comments.
        public static int CountComments(string text) => ReviewList.Build(text).Count(item => item.Kind == "comment");
    }
}
