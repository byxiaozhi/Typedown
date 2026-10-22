using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Export
{
    // What happens to the marks of a review and of a speech before the Markdown is read. CriticMarkup ({++ ++}, {-- --}, {~~ ~> ~~},
    // {== ==}, {>> <<}) and speech marks ({pause 2s}) are text, and Markdig reads them as such. So that they can become tracked changes,
    // comments and gray notes in Word, each mark is first written as sentinel characters (punctuation of a rarely used block, so the
    // emphasis rules around them stay what they were with the braces) that go through Markdig untouched, and the builder turns them
    // into Word elements. The authors, the days and the notes travel in tables; the sentinels carry only their number.
    // Plain .NET (no WinUI).
    public enum WordSpeechMarks
    {
        // Small gray text, as written: the speaker can still see the marks.
        Notes,
        // Left out, as for a text that is only to be read.
        Remove,
    }

    internal sealed class MarkTables
    {
        public List<(string Author, DateTime Date)> Revisions { get; } = new();

        public List<(string Author, DateTime Date, string Note)> Comments { get; } = new();
    }

    internal static partial class WordMarks
    {
        public const char InsertStart = (char)0x2E02, InsertEnd = (char)0x2E03, DeleteStart = (char)0x2E04, DeleteEnd = (char)0x2E05,
            ReplaceStart = (char)0x2E09, ReplaceSplit = (char)0x2E0A, ReplaceEnd = (char)0x2E0C, HighlightStart = (char)0x2E0D, HighlightEnd = (char)0x2E1C,
            CommentStart = (char)0x2E1D, CommentEnd = (char)0x2E20, CommentPoint = (char)0x2E21, Terminator = (char)0x2E22,
            SpeechStart = (char)0x2E23, SpeechEnd = (char)0x2E24;

        private static readonly char[] All =
        {
            InsertStart, InsertEnd, DeleteStart, DeleteEnd, ReplaceStart, ReplaceSplit, ReplaceEnd, HighlightStart, HighlightEnd,
            CommentStart, CommentEnd, CommentPoint, Terminator, SpeechStart, SpeechEnd,
        };

        public static bool IsMark(char c) => Array.IndexOf(All, c) >= 0;

        private static readonly Regex SpeechRule = new(@"\{(/?)([A-Za-z][A-Za-z0-9-]*)(?: ([^{}\n:]*))?(?::[ ]?([^{}\n]*))?\}", RegexOptions.CultureInvariant);
        private static readonly Regex Sentinels = new("[" + new string(All) + "]");
        private static readonly Regex Numbered = new("[" + InsertStart + DeleteStart + ReplaceStart + CommentStart + CommentPoint + "][0-9]*" + Terminator);

        // The text of a heading or a note without any sentinel: what a reader would call the words.
        public static string Strip(string text) => Sentinels.Replace(Numbered.Replace(text ?? "", ""), "");

        public static string Prepare(string text, WordExportOptions options, MarkTables tables)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            text = Sentinels.Replace(text, "");
            text = SpeechMarks(text, options.SpeechMarks);
            return ReviewMarks(text, options, tables);
        }

        // {pause 2s} and the others of the built-in words, outside code.
        private static string SpeechMarks(string text, WordSpeechMarks mode)
        {
            if (text.IndexOf('{') < 0) return text;
            var code = Services.ReviewMarks.CodePlaces(text);
            var sb = new StringBuilder(text.Length);
            var at = 0;
            foreach (Match match in SpeechRule.Matches(text))
            {
                if (code[match.Index] || !SpeechLibrary.BuiltInWords.Contains(match.Groups[2].Value) || (match.Index > 0 && text[match.Index - 1] == (char)92)) continue;
                sb.Append(text, at, match.Index - at);
                at = match.Index + match.Length;
                if (mode == WordSpeechMarks.Notes) sb.Append(SpeechStart).Append(match.Value).Append(SpeechEnd);
            }
            return sb.Append(text, at, text.Length - at).ToString();
        }

        // {++added++}, {--deleted--}, {~~old~>new~~}, {==highlight==}, {>>comment<<}, outside code.
        private static string ReviewMarks(string text, WordExportOptions options, MarkTables tables)
        {
            var found = Services.ReviewMarks.Scan(text);
            if (found.Count == 0) return text;
            var author = string.IsNullOrWhiteSpace(options.ReviewAuthor) ? "Caret" : options.ReviewAuthor;
            var date = options.ReviewDate ?? DateTime.UtcNow;
            var sb = new StringBuilder(text.Length);
            var at = 0;
            // the comment mark that comes right after mark k, with nothing between
            Services.ReviewMarks.Found Following(int k) =>
                k + 1 < found.Count && found[k + 1].Start == at && found[k + 1].Kind == "comment" ? found[k + 1] : null;
            for (var k = 0; k < found.Count; k++)
            {
                var mark = found[k];
                sb.Append(text, at, mark.Start - at);
                at = mark.Start + mark.Length;
                if (mark.Kind is "add" or "del" or "sub")
                {
                    // the stamps right after a change say who made it and when
                    var (who, when) = (author, date);
                    while (Following(k) is { } stamp && Services.ReviewMarks.TryParseStamp(stamp.First, out var a, out var d, out var n) && n.Length == 0)
                    {
                        (who, when) = (string.IsNullOrWhiteSpace(a) ? author : a, d); // a stamp with no name is the reviewer
                        at = stamp.Start + stamp.Length;
                        k++;
                    }
                    tables.Revisions.Add((who, when));
                    var number = tables.Revisions.Count - 1;
                    if (mark.Kind == "add") sb.Append(InsertStart).Append(number).Append(Terminator).Append(mark.First).Append(InsertEnd);
                    else if (mark.Kind == "del") sb.Append(DeleteStart).Append(number).Append(Terminator).Append(mark.First).Append(DeleteEnd);
                    else sb.Append(ReplaceStart).Append(number).Append(Terminator).Append(mark.First).Append(ReplaceSplit).Append(mark.Second).Append(ReplaceEnd);
                }
                else if (mark.Kind == "mark")
                {
                    var following = Following(k);
                    var number = following == null ? null : Note(following.First, author, date, tables);
                    if (following != null) { at = following.Start + following.Length; k++; }
                    if (number != null) sb.Append(CommentStart).Append(number).Append(Terminator).Append(mark.First).Append(CommentEnd);
                    else sb.Append(HighlightStart).Append(mark.First).Append(HighlightEnd);
                }
                else if (Note(mark.First, author, date, tables) is int point)
                {
                    sb.Append(CommentPoint).Append(point).Append(Terminator);
                }
            }
            return sb.Append(text, at, text.Length - at).ToString();
        }

        // A comment of the text as a comment of the file; null for a stamp with nothing said.
        private static int? Note(string raw, string author, DateTime date, MarkTables tables)
        {
            var (who, when, note) = (author, date, raw ?? "");
            if (Services.ReviewMarks.TryParseStamp(note, out var a, out var d, out var n)) (who, when, note) = (string.IsNullOrWhiteSpace(a) ? author : a, d, n);
            note = Strip(note).Trim();
            if (note.Length == 0) return null;
            tables.Comments.Add((who, when, note));
            return tables.Comments.Count - 1;
        }
    }
}
