using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services
{
    internal enum ReviewAction
    {
        // Keep what the changes say: additions stay, deletions go, a replacement becomes its new text.
        Accept,
        // Undo the changes: additions go, deletions stay, a replacement becomes its old text.
        Reject,
        // Remove the comments (and unwrap the highlights they are about); the changes stay.
        DeleteComments,
    }

    internal enum ReviewKind { None, Change, Comment }

    // Accepting, rejecting and deleting in a text that carries a review in CriticMarkup (see ReviewMarks.cs). The
    // rules are those of the editor that draws the marks (Typedown.Editor, parser/index.js): a mark is on one
    // paragraph, a backslash before `{` keeps it text, and code (fenced blocks and `code spans`) is never a mark.
    internal static partial class ReviewMarks
    {
        private const char Removed = (char)1;

        private static readonly (string Kind, Regex Pattern)[] Rules =
        {
            ("add", new Regex(@"\G\{\+\+([\s\S]*?)\+\+\}", RegexOptions.CultureInvariant)),
            ("del", new Regex(@"\G\{--([\s\S]*?)--\}", RegexOptions.CultureInvariant)),
            ("sub", new Regex(@"\G\{~~([\s\S]*?)~>([\s\S]*?)~~\}", RegexOptions.CultureInvariant)),
            ("mark", new Regex(@"\G\{==([\s\S]*?)==\}", RegexOptions.CultureInvariant)),
            ("comment", new Regex(@"\G\{>>([\s\S]*?)<<\}", RegexOptions.CultureInvariant)),
        };

        private static readonly Regex ParagraphBreak = new(@"\n[ \t\r]*\n", RegexOptions.CultureInvariant);
        // `@Name 2026-10-07` and nothing else: who and when, not a note.
        private static readonly Regex StampOnly = new(@"^@[^:\r\n]+ \d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant);
        // What is left of a line when its only text was removed: nothing, or just a list marker, a quote mark, a heading mark.
        private static readonly Regex EmptyBlock = new(@"^[\s>]*(?:(?:[-*+]|\d{1,9}[.)]|#{1,6})\s*)?$", RegexOptions.CultureInvariant);
        private static readonly Regex Fence = new(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.CultureInvariant);

        private sealed record Token(string Kind, int Length, string First, string Second);

        // What a piece of text starts with: a change (addition, deletion, replacement), a comment or highlight, or neither.
        public static ReviewKind KindOf(string raw)
        {
            if (string.IsNullOrEmpty(raw) || !TryToken(raw, 0, new bool[raw.Length], out var token)) return ReviewKind.None;
            return token.Kind is "add" or "del" or "sub" ? ReviewKind.Change : ReviewKind.Comment;
        }

        // A comment that says something: not a stamp (who and when) and not a change or a highlight.
        public static bool IsNote(string raw) =>
            !string.IsNullOrEmpty(raw) && TryToken(raw, 0, new bool[raw.Length], out var token) && token.Kind == "comment" && !StampOnly.IsMatch(token.First);

        // How many changes and comments (a note, not a stamp) the text holds.
        public static (int Changes, int Comments) Count(string text)
        {
            var changes = 0;
            var comments = 0;
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return (0, 0);
            var protectedText = Protected(text);
            for (var i = 0; i < text.Length;)
            {
                if (!TryToken(text, i, protectedText, out var token)) { i++; continue; }
                if (token.Kind is "add" or "del" or "sub") changes++;
                else if (token.Kind == "mark" || (token.Kind == "comment" && !StampOnly.IsMatch(token.First))) comments++;
                i += token.Length;
            }
            return (changes, comments);
        }

        // The text with every change accepted or rejected, or every comment deleted. The stamps right after a change
        // (`{>>@Name 2026-10-07<<}`) go with it when it is accepted or rejected; notes stay. A line that held only
        // what was removed goes too.
        public static string Resolve(string text, ReviewAction action)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text ?? "";
            var protectedText = Protected(text);
            var output = new StringBuilder(text.Length);
            var anyRemoved = false;
            var i = 0;
            while (i < text.Length)
            {
                if (!TryToken(text, i, protectedText, out var token))
                {
                    output.Append(text[i++]);
                    continue;
                }
                var change = token.Kind is "add" or "del" or "sub";
                var end = i + token.Length;
                string replacement;
                if (change && action != ReviewAction.DeleteComments)
                {
                    replacement = token.Kind switch
                    {
                        "add" => action == ReviewAction.Accept ? token.First : "",
                        "del" => action == ReviewAction.Accept ? "" : token.First,
                        _ => action == ReviewAction.Accept ? token.Second : token.First,
                    };
                    end = SkipStamps(text, end, protectedText);
                }
                else if (change)
                {
                    // Deleting comments leaves the changes, and the stamps that say who made them.
                    end = SkipStamps(text, end, protectedText);
                    output.Append(text, i, end - i);
                    i = end;
                    continue;
                }
                else if (action == ReviewAction.DeleteComments)
                {
                    // A highlight is unwrapped and the notes on it go; a comment goes.
                    replacement = token.Kind == "mark" ? token.First : "";
                    if (token.Kind == "mark")
                        while (TryToken(text, end, protectedText, out var next) && next.Kind == "comment") end += next.Length;
                }
                else
                {
                    output.Append(text, i, token.Length);
                    i = end;
                    continue;
                }

                if (replacement.Length == 0)
                {
                    anyRemoved = true;
                    // "the {--quick--} fox" would be left with two spaces.
                    if (output.Length > 0 && output[output.Length - 1] == ' ' && end < text.Length && text[end] == ' ') end++;
                    output.Append(Removed);
                }
                else
                {
                    output.Append(replacement);
                }
                i = end;
            }
            return anyRemoved ? DropEmptyLines(output.ToString()) : output.ToString();
        }

        // After a change: the stamps (comments that are only who and when) that follow it, up to the next other text.
        private static int SkipStamps(string text, int at, bool[] protectedText)
        {
            while (TryToken(text, at, protectedText, out var next) && next.Kind == "comment" && StampOnly.IsMatch(next.First))
                at += next.Length;
            return at;
        }

        // A mark starting at `at`, as the editor would read it.
        private static bool TryToken(string text, int at, bool[] protectedText, out Token token)
        {
            token = null;
            if (at < 0 || at >= text.Length) return false;
            if (text[at] != '{' || protectedText[at]) return false;
            // `\{` is an escaped brace, not the start of a mark
            var slashes = 0;
            for (var k = at - 1; k >= 0 && text[k] == '\\'; k--) slashes++;
            if (slashes % 2 == 1) return false;
            foreach (var (kind, pattern) in Rules)
            {
                var match = pattern.Match(text, at);
                if (!match.Success || match.Index != at) continue;
                if (protectedText[at + match.Length - 1]) return false;
                var first = match.Groups[1].Value;
                var second = match.Groups.Count > 2 ? match.Groups[2].Value : "";
                // marks are inside a paragraph: a blank line ends it
                if (ParagraphBreak.IsMatch(first) || ParagraphBreak.IsMatch(second)) return false;
                token = new Token(kind, match.Length, first, second);
                return true;
            }
            return false;
        }

        // The places where marks are not marks: fenced code blocks and `code spans`.
        private static bool[] Protected(string text)
        {
            var guarded = new bool[text.Length];
            var position = 0;
            string fence = null;
            while (position < text.Length)
            {
                var lineEnd = text.IndexOf('\n', position);
                var next = lineEnd < 0 ? text.Length : lineEnd + 1;
                var line = text.Substring(position, (lineEnd < 0 ? text.Length : lineEnd) - position).TrimEnd('\r');
                var opens = Fence.Match(line);
                var isFenceLine = false;
                if (fence == null && opens.Success)
                {
                    fence = opens.Groups[1].Value;
                    isFenceLine = true;
                }
                else if (fence != null)
                {
                    isFenceLine = true;
                    var trimmed = line.Trim();
                    if (trimmed.Length >= fence.Length && trimmed.Trim(fence[0]).Length == 0 && trimmed[0] == fence[0]) fence = null;
                }
                if (isFenceLine)
                    for (var k = position; k < next; k++) guarded[k] = true;
                else
                    GuardCodeSpans(text, position, next, guarded);
                position = next;
            }
            return guarded;
        }

        // `code` and ``code with ` inside``: a run of backticks to the next run of the same length, on the line.
        private static void GuardCodeSpans(string text, int start, int end, bool[] guarded)
        {
            var i = start;
            while (i < end)
            {
                if (text[i] != '`') { i++; continue; }
                var run = i;
                while (run < end && text[run] == '`') run++;
                var length = run - i;
                var close = -1;
                for (var k = run; k < end;)
                {
                    if (text[k] != '`') { k++; continue; }
                    var stop = k;
                    while (stop < end && text[stop] == '`') stop++;
                    if (stop - k == length) { close = stop; break; }
                    k = stop;
                }
                if (close < 0) { i = run; continue; }
                for (var k = i; k < close; k++) guarded[k] = true;
                i = close;
            }
        }

        // Lines whose only text was removed (marked with Removed) go; the others lose the marker. No blank line is left
        // doubled where a paragraph went.
        private static string DropEmptyLines(string text)
        {
            var lines = text.Split('\n');
            var kept = new List<string>(lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.IndexOf(Removed) < 0)
                {
                    kept.Add(line);
                    continue;
                }
                var stripped = line.Replace(Removed.ToString(), "");
                if (!EmptyBlock.IsMatch(stripped.TrimEnd('\r')))
                {
                    kept.Add(stripped);
                    continue;
                }
                var previousBlank = kept.Count == 0 || string.IsNullOrWhiteSpace(kept[kept.Count - 1]);
                if (previousBlank && i + 1 < lines.Length && string.IsNullOrWhiteSpace(lines[i + 1])) i++;
            }
            return string.Join("\n", kept);
        }
    }
}
