using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services
{
    // New since the fork: live review (docs/live-review-design.md). Tracking remembers the document as it was when it was
    // switched on (the baseline); as the user edits, the baseline and the document are compared again (ReviewDiff) and what
    // differs is shown: as a list, and as the text of a review that the editor's preview draws in colour. Nothing is written
    // into the document by this: the review text exists only to be shown, until the user chooses to write it down.
    //
    // One change can be accepted (the baseline takes its new text, so it stops being a difference) or rejected (the document
    // gets its old text back). Both are made from the review text itself, without a second record of where each change is:
    // every change of the review is either "old side" or "new side" in the text that is rebuilt, so accepting change k is the
    // text with only k on the new side, and rejecting it is the text with every change but k on the new side.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class LiveReview
    {
        // One change: its number in the order of the text, what it is, what the list shows (for a replacement "old -> new"),
        // and where it is in the document (a position in the text with the lines ended by "\n"; the length is that of the new
        // text, 0 for a deletion, which is at the place where the text was).
        internal sealed record Change(int Index, ChangeKind Kind, string Text, int Offset, int Length);

        internal enum ChangeKind { Added, Deleted, Replaced }

        // `Marked`: the baseline compared with the document, as a review in CriticMarkup. `Changes`: the number of marks
        // written. `Unmarked`: differences that could not be marked (ReviewDiff). `List`: the changes in the order of the text.
        // `Exact`: rejecting every change gives the baseline and accepting every change gives the document; when it does not
        // (a block of code that changed cannot be marked), one change cannot be accepted or rejected on its own.
        internal sealed record Result(string Marked, int Changes, int Unmarked, IReadOnlyList<Change> List, bool Exact, string Stamp, string Own, string Review, string Baseline, string Current, IReadOnlyList<string> Days, IReadOnlyList<ReviewDates.Seen> Seen);

        // The comparison marks its changes with a stamp of its own, one no document holds, so that a mark that was in the text before
        // (even one by the same author on the same day) is never taken for a change of this review. `Marked` has the stamp of the
        // author (what is shown or written); `Review` and `Own` are what the list, accept and reject work on.
        private const string OwnAuthor = "caret-live-review";

        private static readonly DateTime OwnDay = new(1, 1, 1);

        // The most characters of a change shown in the list.
        private const int ListText = 80;

        // `Days`: the day each change of the list was first seen; `Seen`: what to save to know them again (ReviewDates). `seen` is what was
        // saved before (null: every change is new today, `when`).
        public static Result Compare(string baseline, string current, string author, DateTime when, string codeNote, IReadOnlyList<ReviewDates.Seen> seen = null)
        {
            baseline ??= "";
            current ??= "";
            var marked = ReviewDiff.Mark(baseline, current, OwnAuthor, OwnDay, codeNote);
            var own = ReviewMarks.Stamp(OwnAuthor, OwnDay);
            var stamp = ReviewMarks.Stamp(author, when);
            var review = Unix(marked.Text);
            var oldText = Unix(baseline);
            var newText = Unix(current);
            // a word deleted between two spaces is a replacement by spaces in the text, and is listed as the deletion it is
            var matches = Own(review, own).Select(m =>
                m.Kind == ChangeKind.Replaced && m.New.Trim().Length == 0 && m.Old.Trim().Length > 0 ? (ChangeKind.Deleted, m.Old, "")
                : m.Kind == ChangeKind.Replaced && m.Old.Trim().Length == 0 && m.New.Trim().Length > 0 ? (ChangeKind.Added, "", m.New)
                : m).ToList();
            var rebuilt = Rebuild(review, own, _ => true, out var before);
            // One by one needs the document to be exactly the changes made new (what accept and reject build on), and every difference to be
            // in the list: a changed block of code has no entry. A difference that could not be marked (a comment added while tracking
            // holds what ends a mark) is in the document as it is, and accepting or rejecting another change leaves it alone.
            var exact = SameText(rebuilt, newText) && matches.Count == marked.Changes;
            var offsets = Places(rebuilt, before, matches);
            var list = new List<Change>(matches.Count);
            for (var i = 0; i < matches.Count; i++)
            {
                var (kind, old, text) = matches[i];
                var shown = kind switch
                {
                    ChangeKind.Added => Short(text),
                    ChangeKind.Deleted => Short(old),
                    _ => Short(old) + " \u2192 " + Short(text),
                };
                list.Add(new Change(i, kind, shown, offsets[i], kind == ChangeKind.Deleted ? 0 : text.Length));
            }
            // the day of each change: the day it was first seen (ReviewDates)
            var found = ReviewDates.Probes(rebuilt, matches.Select(m => m.Item2).ToList(), matches.Select(m => m.Item3).ToList(),
                list.Select(c => c.Offset).ToList(), list.Select(c => c.Length).ToList());
            var days = ReviewDates.Assign(seen, found, ReviewMarks.Day(when));
            // the notes of changed blocks of code (counted among the changes, but not marks of the list) are of the day of the comparison
            var displayed = Displayed(marked.Text, own, "{>>@" + ReviewMarks.Author(author) + " ", days,
                marked.Changes - matches.Count, ReviewMarks.Stamp(OwnAuthor, OwnDay, codeNote), ReviewMarks.Stamp(author, when, codeNote));
            return new Result(displayed, marked.Changes, marked.Unmarked, list, exact, stamp, own, review, oldText, newText, days, ReviewDates.Remember(found, days));
        }

        // Blank lines left behind where text was cleared do not matter: the clean-up of a change removes them.
        private static bool SameText(string a, string b) => Squash(a) == Squash(b);

        private static string Squash(string s) => Regex.Replace(s, "\n{3,}", "\n\n");

        // The review text with the stamp of each change shown as the author's, with the day that change was first seen. Only the stamp
        // right after a mark this comparison made is taken (text of the document that happens to look like one is left as it is). The note
        // of a changed block of code is written by the comparison on a line of its own, between blank lines, and only as many as it made
        // (`codeNotes`): only such a line is taken, so the same text in the document (in a block of code) is left as it is too.
        private static string Displayed(string markedText, string own, string authorPrefix, IReadOnlyList<string> days, int codeNotes, string ownNote, string shownNote)
        {
            var output = new StringBuilder(markedText.Length);
            var position = 0;
            var index = 0;
            foreach (Match m in OwnPattern(own).Matches(markedText))
            {
                var end = m.Index + m.Length;
                output.Append(markedText, position, end - position);
                if (index < days.Count) output.Append(authorPrefix).Append(days[index]).Append("<<}");
                else output.Append(own);
                index++;
                position = end + own.Length;
            }
            output.Append(markedText, position, markedText.Length - position);
            if (codeNotes <= 0) return output.ToString();
            var lines = output.ToString().Split(LineFeed);
            var replaced = 0;
            for (var i = 0; i < lines.Length && replaced < codeNotes; i++)
            {
                var blankBefore = i == 0 || lines[i - 1].Trim().Length == 0;
                var blankAfter = i == lines.Length - 1 || lines[i + 1].Trim().Length == 0;
                if (!blankBefore || !blankAfter || lines[i].TrimEnd('\r') != ownNote) continue;
                lines[i] = shownNote + (lines[i].EndsWith('\r') ? "\r" : "");
                replaced++;
            }
            return string.Join(LineFeed, lines);
        }

        private const char LineFeed = (char)10;

        // Which change of `seen` (the last comparison) a card in the Visual view was about, or -1 when that cannot be told. The card knows the
        // old and the new text of its change, a little of the text around it, and whether it was the only change with that old and new
        // text. By the time it is used the document may have changed: a change by its text alone could be another one of the same words
        // (the first of two identical changes went and the second took its place), so with more than one the text around it must pick
        // exactly one; a change that was alone with its words is found by them, and otherwise it needs its context.
        public static int Reidentify(IReadOnlyList<ReviewDates.Seen> seen, string old, string @new, string before, string after, bool unique)
        {
            var same = Enumerable.Range(0, seen.Count).Where(i => seen[i].Old == old && seen[i].New == @new).ToList();
            if (same.Count == 0) return -1;
            bool Context(int i) => seen[i].Before == before && seen[i].After == after;
            if (same.Count == 1) return unique || Context(same[0]) ? same[0] : -1;
            var matching = same.Where(Context).ToList();
            return matching.Count == 1 ? matching[0] : -1;
        }

        // The baseline after change `index` was accepted: the old text with that one change made.
        public static string AcceptOne(Result result, int index) => Rebuild(result.Review, result.Own, i => i == index, out _);

        // The document after change `index` was rejected: the new text with that one change taken back.
        public static string RejectOne(Result result, int index) => Rebuild(result.Review, result.Own, i => i != index, out _);

        // The marks of this review: a change followed by its own stamp ({>>@Name day<<}). A mark that was in the text before has
        // another stamp, or none, and is part of the text.
        private static Regex OwnPattern(string stamp)
        {
            var after = Regex.Escape(stamp);
            return new Regex(
                @"\{\+\+(?<add>(?:(?!\+\+\})[\s\S])*)\+\+\}(?=" + after + @")" +
                @"|\{--(?<del>(?:(?!--\})[\s\S])*)--\}(?=" + after + @")" +
                @"|\{~~(?<old>(?:(?!~>|~~\})[\s\S])*)~>(?<new>(?:(?!~~\})[\s\S])*)~~\}(?=" + after + @")",
                RegexOptions.CultureInvariant);
        }

        private static List<(ChangeKind Kind, string Old, string New)> Own(string review, string stamp)
        {
            var found = new List<(ChangeKind, string, string)>();
            foreach (Match m in OwnPattern(stamp).Matches(review))
            {
                if (m.Groups["add"].Success) found.Add((ChangeKind.Added, "", m.Groups["add"].Value));
                else if (m.Groups["del"].Success) found.Add((ChangeKind.Deleted, m.Groups["del"].Value, ""));
                else found.Add((ChangeKind.Replaced, m.Groups["old"].Value, m.Groups["new"].Value));
            }
            return found;
        }

        // The review text with each of its own changes on one side: `useNew(i)` is true for the new text of change i, false for its
        // old text; the stamps of the changes go; everything else stays. A line whose only text was removed goes too, and no blank
        // line is left doubled where a paragraph went (the same clean-up as accepting or rejecting in the document, ReviewMarks).
        // `places[i]` is the text just before change i (as far as it is made), to find where the change is in the result.
        private static string Rebuild(string review, string stamp, Func<int, bool> useNew, out List<string> places)
        {
            places = new List<string>();
            var output = new StringBuilder(review.Length);
            var position = 0;
            var index = 0;
            var anyRemoved = false;
            foreach (Match m in OwnPattern(stamp).Matches(review))
            {
                output.Append(review, position, m.Index - position);
                places.Add(output.ToString(Math.Max(0, output.Length - Context), Math.Min(Context, output.Length)).Replace(ReviewMarks.Removed.ToString(), ""));
                string side;
                if (m.Groups["add"].Success) side = useNew(index) ? m.Groups["add"].Value : "";
                else if (m.Groups["del"].Success) side = useNew(index) ? "" : m.Groups["del"].Value;
                else side = useNew(index) ? m.Groups["new"].Value : m.Groups["old"].Value;
                if (side.Length == 0)
                {
                    anyRemoved = true;
                    output.Append(ReviewMarks.Removed);
                }
                else output.Append(side);
                position = m.Index + m.Length + stamp.Length;
                index++;
            }
            output.Append(review, position, review.Length - position);
            var text = output.ToString();
            return anyRemoved ? ReviewMarks.DropEmptyLines(text) : text;
        }

        // How much of the text before a change is kept to find the change again in the rebuilt text.
        private const int Context = 40;

        // Where each change is in `text` (the document): an addition or replacement where its new text is, a deletion where the text
        // before it ends. Looked for in order, so equal pieces of text find their own place.
        private static List<int> Places(string text, List<string> before, IReadOnlyList<(ChangeKind Kind, string Old, string New)> changes)
        {
            var places = new List<int>(changes.Count);
            var from = 0;
            for (var i = 0; i < changes.Count; i++)
            {
                var at = from;
                var tail = before[i].TrimEnd('\n');
                if (changes[i].Kind != ChangeKind.Deleted && changes[i].New.Length > 0)
                {
                    var found = text.IndexOf(changes[i].New, from, StringComparison.Ordinal);
                    if (found >= 0) at = found;
                }
                else if (tail.Length > 0)
                {
                    // the text before a deletion may start before the change that came before it, and ends at or after where that one ends
                    var found = text.IndexOf(tail, Math.Max(0, from - tail.Length), StringComparison.Ordinal);
                    while (found >= 0 && found + tail.Length < from) found = text.IndexOf(tail, found + 1, StringComparison.Ordinal);
                    if (found >= 0) at = found + tail.Length;
                }
                places.Add(at);
                from = changes[i].Kind != ChangeKind.Deleted ? Math.Min(text.Length, at + changes[i].New.Length) : at;
            }
            return places;
        }
        private static string Unix(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        private static string Short(string text)
        {
            var one = Regex.Replace(text, @"\s+", " ").Trim();
            return one.Length <= ListText ? one : one.Substring(0, ListText - 1) + "\u2026";
        }
    }
}