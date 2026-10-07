using System;
using System.Globalization;
using System.Text;

namespace Typedown.WinUI.Services
{
    // New since the fork: the text of a review in the document, written in CriticMarkup (criticmarkup.com), which
    // the editor draws in colour (Typedown.Editor, parser/critic). CriticMarkup has no author, so what is written
    // here is a comment `{>>@Name 2026-10-07: the note<<}` after the text it is about, which is itself marked
    // `{==like this==}`: the author and the day travel with the document, as plain text a colleague without Caret
    // (or an AI assistant) can read, and nothing is kept anywhere else.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static partial class ReviewMarks
    {
        // The most that is quoted back in a dialog; the marked text itself is never cut.
        public const int PreviewLength = 140;

        // What goes after `@`: one line, none of the characters the markup or the stamp is made of.
        public static string Author(string name)
        {
            var clean = Flatten(name, "{}<>:@");
            return clean.Length == 0 ? "?" : clean;
        }

        // The note: one line (a paragraph break would end the paragraph the mark is in), and never the closing `<<}`.
        public static string Note(string note)
        {
            var clean = Flatten(note, "");
            while (clean.Contains("<<}", StringComparison.Ordinal)) clean = clean.Replace("<<}", "< <}", StringComparison.Ordinal);
            return clean;
        }

        public static string Day(DateTime when) => when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // `{>>@Name 2026-10-07<<}`, or with a note `{>>@Name 2026-10-07: the note<<}`.
        public static string Stamp(string author, DateTime when, string note = null)
        {
            var text = Note(note);
            return "{>>@" + Author(author) + " " + Day(when) + (text.Length == 0 ? "" : ": " + text) + "<<}";
        }

        // What to write for a comment on `selected` (the text as it is in the file, may be empty). `wraps` says
        // whether the selection is replaced by the markup (it was marked and the note follows it) or the markup is
        // only a comment, to be written after the selection: a selection over more than one line can't be marked
        // (a paragraph break would end the mark), and neither can text that holds the marks themselves.
        public static (string Markup, bool Wraps) Comment(string selected, string note, string author, DateTime when)
        {
            var stamp = Stamp(author, when, note);
            var markable = !string.IsNullOrWhiteSpace(selected)
                && selected.IndexOfAny(new[] { '\r', '\n' }) < 0
                && !selected.Contains("{==", StringComparison.Ordinal)
                && !selected.Contains("==}", StringComparison.Ordinal);
            if (!markable) return (stamp, false);
            // A double-click selects a word with the space after it: the spaces stay outside the mark ("{==word==}{>>...<<} next").
            var core = selected.Trim();
            var lead = selected.Substring(0, selected.Length - selected.TrimStart().Length);
            var tail = selected.Substring(selected.TrimEnd().Length);
            return (lead + "{==" + core + "==}" + stamp + tail, true);
        }

        // A selection quoted in a dialog: one line, cut with an ellipsis.
        public static string Preview(string selected)
        {
            var line = Flatten(selected, "");
            return line.Length <= PreviewLength ? line : line.Substring(0, PreviewLength).TrimEnd() + "…";
        }

        // Line breaks and runs of white space become single spaces, the listed characters are dropped.
        private static string Flatten(string text, string drop)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder(text.Length);
            var space = false;
            foreach (var c in text)
            {
                if (c == '\u200b') continue; // zero-width space: the editor's own marker, not text
                if (char.IsWhiteSpace(c))
                {
                    space = result.Length > 0;
                    continue;
                }
                if (drop.IndexOf(c) >= 0) continue;
                if (space) result.Append(' ');
                space = false;
                result.Append(c);
            }
            return result.ToString();
        }
    }
}
