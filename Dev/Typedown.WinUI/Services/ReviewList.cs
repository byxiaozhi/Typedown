using System;
using System.Collections.Generic;

namespace Typedown.WinUI.Services
{
    // One row of the Review tab of the inspector pane: a change, a highlight or a comment of the note.
    // Kind is the mark's kind (add, del, sub, mark, comment), Snippet what it is about, Meta "author, day" when the mark
    // carries a stamp, Raw the mark as it is written in the text (the editor finds it by that), and Nth which of the
    // identical marks it is (0 for the first), so that two equal marks are told apart.
    internal sealed record ReviewListItem(string Kind, string Snippet, string Meta, string Raw, int Nth);

    // The review marks of a note as a list, in the order of the text. Plain .NET (the marks are read by ReviewMarks, so the
    // rules are the editor's: none in code, none across a paragraph), tested without the window.
    internal static class ReviewList
    {
        private const int SnippetLength = 90;

        public static List<ReviewListItem> Build(string text)
        {
            var items = new List<ReviewListItem>();
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return items;
            var found = ReviewMarks.Scan(text);
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < found.Count; i++)
            {
                var mark = found[i];
                var raw = text.Substring(mark.Start, mark.Length);
                string snippet;
                string meta = "";

                if (mark.Kind == "comment")
                {
                    // `{>>@Name 2026-10-07<<}` alone says nothing; with a note it is a comment, and a comment without a stamp is a note
                    if (ReviewMarks.TryParseStamp(mark.First, out var who, out var day, out var note))
                    {
                        if (string.IsNullOrEmpty(note)) continue;
                        snippet = note;
                        meta = Meta(who, day);
                    }
                    else
                    {
                        snippet = mark.First;
                    }
                }
                else
                {
                    snippet = mark.Kind == "sub" ? mark.First + " → " + mark.Second : mark.First;
                    // the stamp (and the note) right behind a change or a highlight belong to it
                    if (i + 1 < found.Count && found[i + 1].Kind == "comment" && found[i + 1].Start == mark.Start + mark.Length
                        && ReviewMarks.TryParseStamp(found[i + 1].First, out var author, out var when, out var remark))
                    {
                        meta = Meta(author, when);
                        if (!string.IsNullOrEmpty(remark)) snippet += " — " + remark;
                        i++;
                    }
                }

                seen.TryGetValue(raw, out var nth);
                seen[raw] = nth + 1;
                items.Add(new ReviewListItem(mark.Kind, Shorten(snippet), meta, raw, nth));
            }
            return items;
        }

        private static string Meta(string who, DateTime day) => who + ", " + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        // One line: the line breaks and the runs of spaces are one space, and a long text is cut with an ellipsis.
        private static string Shorten(string text)
        {
            var one = System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return one.Length <= SnippetLength ? one : one.Substring(0, SnippetLength - 1).TrimEnd() + "…";
        }
    }
}
