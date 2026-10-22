using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services
{
    // New since the fork: the differences between two versions of a Markdown text, written as a review in CriticMarkup
    // (see ReviewMarks.cs): what the new version adds is `{++added++}`, what it removes `{--deleted--}`, a changed word
    // or phrase `{~~old~>new~~}`, and each is followed by `{>>@Name 2026-10-07<<}` saying who made it. Accepting every
    // change (ReviewMarks.Resolve) gives the new version back, rejecting every change gives the old one.
    //
    // How: the lines are compared first; a changed line that has a similar line on the other side is compared word by
    // word, the others are marked whole (a list marker, heading mark or quote mark stays outside the mark, so the line
    // is still what it was). Links, images, code spans, HTML tags and math count as one word, so a mark never cuts
    // them open. Fenced code is never marked (a mark there would be code): a block whose code changed gets a comment
    // after it and the new code as it is.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class ReviewDiff
    {
        // `Changes`: the marks written. `Unmarked`: differences that could not be marked, because the text holds what ends a
        // mark; they are in the text as the new version has them (accepting is right, rejecting does not undo them).
        internal sealed record Result(string Text, int Changes, int Unmarked);

        // The most differing lines (edit steps) looked at before giving up and calling the middle of the text replaced.
        private const int MaxLineEdits = 2500;
        // The most words a line can hold to be compared word by word.
        private const int MaxWords = 1500;
        // The most line pairs tried for similarity in one changed stretch.
        private const int MaxPairs = 4000;
        private const double SimilarEnough = 0.5;
        // A line of this many words or fewer, replaced by another, is compared word by word whatever they have in common.
        private const int ShortLine = 8;

        private static readonly Regex Words = new(
            @"\{(?:\+\+|--|==|>>|~~)[\s\S]*?(?:\+\+|--|==|<<|~~)\}" + // a mark that is already there
            @"|`+[^`]*`+" +                                           // a code span
            @"|!?\[[^\]]*\]\([^)]*\)" +                               // a link or an image
            @"|\$[^$\s][^$]*\$" +                                     // math
            @"|<[^<>\s][^<>]*>" +                                     // an HTML tag
            @"|\s+|[\p{L}\p{N}_]+|[^\p{L}\p{N}_\s]",
            RegexOptions.CultureInvariant);

        private static readonly Regex Prefix = new(
            @"^(\s*(?:>\s*)*(?:(?:[-*+]|\d{1,9}[.)])\s+(?:\[[ xX]\]\s+)?|#{1,6}\s+)?)",
            RegexOptions.CultureInvariant);

        private static readonly Regex Letters = new(@"[\p{L}\p{N}_]+", RegexOptions.CultureInvariant);
        private static readonly Regex Fence = new(@"^ {0,3}(`{3,}|~{3,})", RegexOptions.CultureInvariant);

        // Text that cannot sit inside a mark: it would end the mark early.
        private static readonly string[] Closers = { "++}", "--}", "~~}", "~>", "==}", "<<}" };

        // `codeNote` is what the comment says after a block of code that changed (the app's language).
        public static Result Mark(string original, string current, string author, DateTime when, string codeNote)
        {
            original ??= "";
            current ??= "";
            var windows = current.Contains("\r\n", StringComparison.Ordinal);
            var builder = new Builder(author, when, codeNote);
            builder.Run(Lines(original), Lines(current));
            var text = builder.Text();
            return new Result(windows ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text, builder.Changes, builder.Unmarked);
        }

        private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        private sealed class Builder
        {
            private readonly string stamp;
            private readonly string codeNote;
            private readonly DateTime when;
            private readonly string author;
            // The lines of the result; a blank line that only separates what was added is marked so that it can be dropped
            // where it would double a blank line.
            private readonly List<(string Text, bool Spare)> output = new();
            private string[] a, b;
            private bool[] blankA, blankB, codeA, codeB, closeB, openAfterB;
            private bool codeChanged, codeNotePending;
            // The lines of the old text that are compared with a line of the new one, in the stretch being written.
            private readonly HashSet<int> compared = new();

            public int Changes { get; private set; }

            public int Unmarked { get; private set; }

            public Builder(string author, DateTime when, string codeNote)
            {
                this.author = author;
                this.when = when;
                this.codeNote = codeNote;
                stamp = ReviewMarks.Stamp(author, when);
            }

            public string Text()
            {
                var lines = new List<string>(output.Count);
                for (var i = 0; i < output.Count; i++)
                {
                    var (text, spare) = output[i];
                    if (spare && text.Length == 0)
                    {
                        var previousBlank = lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0;
                        var nextBlank = i + 1 < output.Count && output[i + 1].Text.Trim().Length == 0;
                        if (previousBlank || nextBlank) continue;
                    }
                    lines.Add(text);
                }
                return string.Join("\n", lines);
            }

            public void Run(string[] original, string[] changed)
            {
                a = original;
                b = changed;
                Classify(a, out blankA, out codeA, out _, out _);
                Classify(b, out blankB, out codeB, out closeB, out openAfterB);
                var keysA = a.Select(l => l.TrimEnd()).ToArray();
                var keysB = b.Select(l => l.TrimEnd()).ToArray();
                var ia = 0;
                var ib = 0;
                foreach (var (ma, mb) in Matches(keysA, keysB))
                {
                    Hunk(ia, ma, ib, mb);
                    Emit(b[mb], mb);
                    ia = ma + 1;
                    ib = mb + 1;
                }
                Hunk(ia, a.Length, ib, b.Length);
            }

            // The changed lines that are alike, in order: pairs (index in the old text, index in the new). A line is alike
            // another when about half of their words are the same, in the same order.
            private List<(int Old, int New)> Pairs(List<int> oldLines, List<int> newLines)
            {
                var pairs = new List<(int Old, int New)>();
                if (oldLines.Count == 0 || newLines.Count == 0) return pairs;
                // one short line for another is an edit in place, however little of it is the same
                if (oldLines.Count == 1 && newLines.Count == 1 && Letters.Matches(a[oldLines[0]]).Count <= ShortLine && Letters.Matches(b[newLines[0]]).Count <= ShortLine)
                {
                    pairs.Add((oldLines[0], newLines[0]));
                    return pairs;
                }
                if ((long)oldLines.Count * newLines.Count > MaxPairs)
                {
                    // too many to compare each with each: the same number of lines are taken as edits in place
                    if (oldLines.Count != newLines.Count) return pairs;
                    for (var k = 0; k < oldLines.Count; k++)
                        if (Similarity(a[oldLines[k]], b[newLines[k]]) >= SimilarEnough) pairs.Add((oldLines[k], newLines[k]));
                    return pairs;
                }
                var n = oldLines.Count;
                var m = newLines.Count;
                var similarity = new double[n, m];
                for (var i = 0; i < n; i++)
                    for (var j = 0; j < m; j++)
                        similarity[i, j] = Similarity(a[oldLines[i]], b[newLines[j]]);
                // the best pairing that keeps the order
                var best = new double[n + 1, m + 1];
                for (var i = n - 1; i >= 0; i--)
                    for (var j = m - 1; j >= 0; j--)
                    {
                        best[i, j] = Math.Max(best[i + 1, j], best[i, j + 1]);
                        if (similarity[i, j] >= SimilarEnough) best[i, j] = Math.Max(best[i, j], best[i + 1, j + 1] + similarity[i, j]);
                    }
                int p = 0, q = 0;
                while (p < n && q < m)
                {
                    if (similarity[p, q] >= SimilarEnough && best[p, q] == best[p + 1, q + 1] + similarity[p, q])
                    {
                        pairs.Add((oldLines[p], newLines[q]));
                        p++;
                        q++;
                    }
                    else if (best[p + 1, q] >= best[p, q + 1]) p++;
                    else q++;
                }
                return pairs;
            }

            // One line of the new version goes out as it is, and the note after a block of code that changed goes after it.
            private void Emit(string line, int index)
            {
                output.Add((line, false));
                if (codeNotePending && closeB[index])
                {
                    codeNotePending = false;
                    CodeNote();
                }
            }

            private void CodeNote()
            {
                output.Add(("", true));
                output.Add((ReviewMarks.Stamp(author, when, codeNote), false));
                output.Add(("", true));
                Changes++;
            }

            // Lines [fromA, toA) of the old text are gone and lines [fromB, toB) of the new text are there instead.
            private void Hunk(int fromA, int toA, int fromB, int toB)
            {
                if (fromA >= toA && fromB >= toB) return;
                codeChanged = false;
                var removed = Enumerable.Range(fromA, toA - fromA).Where(i => !blankA[i] && !codeA[i]).ToList();
                var added = Enumerable.Range(fromB, toB - fromB).Where(j => !blankB[j] && !codeB[j]).ToList();
                var ra = fromA;
                var rb = fromB;
                var pairs = Pairs(removed, added);
                compared.Clear();
                foreach (var (i, j) in pairs) compared.Add(i);
                foreach (var (i, j) in pairs)
                {
                    for (; ra < i; ra++) Removed(ra, fromA, toA);
                    for (; rb < j; rb++) Added(rb);
                    Modified(i, j);
                    ra = i + 1;
                    rb = j + 1;
                }
                for (; ra < toA; ra++) Removed(ra, fromA, toA);
                for (; rb < toB; rb++) Added(rb);

                if (!codeChanged) return;
                // Code changed: a comment after the block it is in (when that block is still open here, the comment waits for
                // its closing fence), or right here when the block is gone.
                var inside = toB > 0 && openAfterB[toB - 1];
                if (inside) codeNotePending = true;
                else CodeNote();
            }

            // `from` and `to`: the lines of the old text that are gone in this stretch.
            private void Removed(int i, int from, int to)
            {
                if (blankA[i])
                {
                    // A blank line that went with a deleted paragraph keeps the deleted text a paragraph of its own. One that
                    // went by itself is not a change this marks: the new version is what accepting gives, as it is.
                    bool Deleted(int k) => k >= from && k < to && !blankA[k] && !codeA[k] && !compared.Contains(k);
                    var besideText = Deleted(i - 1) || Deleted(i + 1);
                    if (besideText) output.Add(("", true));
                }
                else if (codeA[i]) codeChanged = true;
                else Whole("--", a[i]);
            }

            private void Added(int j)
            {
                if (blankB[j]) { output.Add((b[j], false)); return; }
                if (codeB[j])
                {
                    codeChanged = true;
                    Emit(b[j], j);
                    return;
                }
                Whole("++", b[j]);
            }

            // A line marked whole: `- {++the item++}{>>@Name 2026-10-07<<}`.
            private void Whole(string sign, string line)
            {
                var prefix = Prefix.Match(line).Value;
                var rest = line.Substring(prefix.Length).TrimEnd();
                var trailing = line.Substring(prefix.Length + rest.Length);
                if (rest.Length == 0)
                {
                    // nothing to mark (an empty list item): the line is there as it is
                    if (sign == "++") output.Add((line, false));
                    return;
                }
                if (!Safe(rest))
                {
                    // text that would end the mark: the line is there as the new version has it, and the difference is not marked
                    if (sign == "++") output.Add((line, false));
                    Unmarked++;
                    return;
                }
                output.Add((prefix + "{" + sign + rest + sign + "}" + stamp + trailing, false));
                Changes++;
            }

            // Two lines that are alike: the words that differ are marked.
            private void Modified(int i, int j)
            {
                var before = a[i].TrimEnd();
                var after = b[j].TrimEnd();
                var trailing = b[j].Substring(after.Length);
                var prefixBefore = Prefix.Match(before).Value.Trim();
                var prefixAfter = Prefix.Match(after).Value;
                if (prefixBefore != prefixAfter.Trim())
                {
                    output.Add((InlineDiff(before, after) + trailing, false));
                    return;
                }
                output.Add((prefixAfter + InlineDiff(before.Substring(Prefix.Match(before).Value.Length), after.Substring(prefixAfter.Length)) + trailing, false));
            }

            // The words of two lines compared; changed stretches are marked, the rest is the new text.
            private string InlineDiff(string before, string after)
            {
                var x = Tokens(before);
                var y = Tokens(after);
                if (x.Length > MaxWords || y.Length > MaxWords) return Change(before, after);
                // runs of words: the same on both sides, or changed (old words, new words)
                var runs = new List<(string Old, string New, bool Same)>();
                void Add(string oldText, string newText, bool same)
                {
                    if (same)
                    {
                        if (runs.Count > 0 && runs[runs.Count - 1].Same) runs[runs.Count - 1] = (runs[runs.Count - 1].Old + oldText, runs[runs.Count - 1].New + newText, true);
                        else runs.Add((oldText, newText, true));
                        return;
                    }
                    // two changes with only spaces between them are one change: "{~~a b~>c d~~}", not two marks
                    if (runs.Count >= 2 && runs[runs.Count - 1].Same && runs[runs.Count - 1].Old.Trim().Length == 0 && !runs[runs.Count - 2].Same)
                    {
                        var space = runs[runs.Count - 1].Old;
                        var last = runs[runs.Count - 2];
                        runs.RemoveRange(runs.Count - 2, 2);
                        runs.Add((last.Old + space + oldText, last.New + space + newText, false));
                        return;
                    }
                    runs.Add((oldText, newText, false));
                }
                var ix = 0;
                var iy = 0;
                foreach (var (mx, my) in LongestCommon(x, y).Append((x.Length, y.Length)))
                {
                    if (ix < mx || iy < my) Add(string.Concat(x.Skip(ix).Take(mx - ix)), string.Concat(y.Skip(iy).Take(my - iy)), false);
                    if (mx < x.Length) Add(x[mx], y[my], true);
                    ix = mx + 1;
                    iy = my + 1;
                }
                var result = new StringBuilder();
                for (var k = 0; k < runs.Count; k++)
                {
                    var run = runs[k];
                    if (run.Same)
                    {
                        result.Append(run.New);
                        continue;
                    }
                    var left = result.Length > 0 && char.IsWhiteSpace(result[result.Length - 1]);
                    var right = k + 1 < runs.Count && runs[k + 1].New.Length > 0 && char.IsWhiteSpace(runs[k + 1].New[0]);
                    result.Append(Change(run.Old, run.New, left, right));
                }
                return result.ToString();
            }

            // One changed stretch: the old words, the new words (either may be empty). `spaceBefore` and `spaceAfter`: the text
            // around it has a space right there.
            private string Change(string before, string after, bool spaceBefore = false, bool spaceAfter = false)
            {
                var oldWhite = before.Trim().Length == 0;
                var newWhite = after.Trim().Length == 0;
                if (oldWhite && newWhite) return after;
                if (!Safe(before) || !Safe(after))
                {
                    // text that would end the mark: the new text as it is, the difference not marked
                    Unmarked++;
                    return after;
                }
                Changes++;
                // (spaces on the other side, as when a word is deleted between two spaces, make it a replacement: an addition or a
                // deletion would lose them)
                if ((oldWhite && before.Length == 0) || (newWhite && after.Length == 0))
                {
                    // Added or deleted. One space between the words stays outside the mark ("a {--b--} c"), because taking the
                    // mark away (rejecting an addition, accepting a deletion) then leaves one space, as there was; a space
                    // that is not between two spaces stays inside, or taking the mark away would leave one too many.
                    var text = oldWhite ? after : before;
                    var core = text.Trim();
                    var lead = text.Substring(0, text.Length - text.TrimStart().Length);
                    var tail = text.Substring(text.TrimEnd().Length);
                    var outsideLead = lead == " " && tail.Length == 0 && spaceAfter ? " " : "";
                    var outsideTail = tail == " " && lead.Length == 0 && spaceBefore ? " " : "";
                    var sign = oldWhite ? "++" : "--";
                    var inside = lead.Substring(outsideLead.Length) + core + tail.Substring(outsideTail.Length);
                    return outsideLead + "{" + sign + inside + sign + "}" + stamp + outsideTail;
                }
                return "{~~" + before + "~>" + after + "~~}" + stamp;
            }

            // Which lines are blank, which are fenced code (the fences too), which close a fence, and whether a fence is
            // still open after each.
            private static void Classify(string[] lines, out bool[] blank, out bool[] code, out bool[] close, out bool[] openAfter)
            {
                blank = new bool[lines.Length];
                code = new bool[lines.Length];
                close = new bool[lines.Length];
                openAfter = new bool[lines.Length];
                string fence = null;
                for (var i = 0; i < lines.Length; i++)
                {
                    blank[i] = lines[i].Trim().Length == 0;
                    var opens = Fence.Match(lines[i]);
                    if (fence == null && opens.Success)
                    {
                        fence = opens.Groups[1].Value;
                        code[i] = true;
                    }
                    else if (fence != null)
                    {
                        code[i] = true;
                        var trimmed = lines[i].Trim();
                        if (trimmed.Length >= fence.Length && trimmed[0] == fence[0] && trimmed.Trim(fence[0]).Length == 0)
                        {
                            close[i] = true;
                            fence = null;
                        }
                    }
                    openAfter[i] = fence != null;
                }
            }
        }

        private static bool Safe(string text) =>
            text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0 && !Closers.Any(c => text.Contains(c, StringComparison.Ordinal));

        private static string[] Tokens(string text) => Words.Matches(text).Select(m => m.Value).ToArray();

        // How much of two lines' words are the same, in the same order: 0 to 1.
        private static double Similarity(string x, string y)
        {
            var wx = Letters.Matches(x).Select(m => m.Value).ToArray();
            var wy = Letters.Matches(y).Select(m => m.Value).ToArray();
            if (wx.Length == 0 && wy.Length == 0) return x.Trim() == y.Trim() ? 1 : 0;
            if (wx.Length == 0 || wy.Length == 0 || wx.Length > MaxWords || wy.Length > MaxWords) return 0;
            var previous = new int[wy.Length + 1];
            var current = new int[wy.Length + 1];
            for (var i = 0; i < wx.Length; i++)
            {
                for (var j = 0; j < wy.Length; j++)
                    current[j + 1] = wx[i] == wy[j] ? previous[j] + 1 : Math.Max(previous[j + 1], current[j]);
                (previous, current) = (current, previous);
            }
            return 2.0 * previous[wy.Length] / (wx.Length + wy.Length);
        }

        // The longest run of words the two share, as index pairs.
        private static List<(int X, int Y)> LongestCommon(string[] x, string[] y)
        {
            var matches = new List<(int, int)>();
            var start = 0;
            while (start < x.Length && start < y.Length && x[start] == y[start])
            {
                matches.Add((start, start));
                start++;
            }
            var endX = x.Length;
            var endY = y.Length;
            var tail = new List<(int, int)>();
            while (endX > start && endY > start && x[endX - 1] == y[endY - 1])
            {
                endX--;
                endY--;
                tail.Add((endX, endY));
            }
            var n = endX - start;
            var m = endY - start;
            if (n > 0 && m > 0)
            {
                var table = new int[n + 1, m + 1];
                for (var i = n - 1; i >= 0; i--)
                    for (var j = m - 1; j >= 0; j--)
                        table[i, j] = x[start + i] == y[start + j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
                int p = 0, q = 0;
                while (p < n && q < m)
                {
                    if (x[start + p] == y[start + q]) { matches.Add((start + p, start + q)); p++; q++; }
                    else if (table[p + 1, q] >= table[p, q + 1]) p++;
                    else q++;
                }
            }
            tail.Reverse();
            matches.AddRange(tail);
            return matches;
        }

        // The lines the two texts share, in order: pairs (index in old, index in new).
        private static List<(int Old, int New)> Matches(string[] x, string[] y)
        {
            var matches = new List<(int, int)>();
            var start = 0;
            while (start < x.Length && start < y.Length && x[start] == y[start])
            {
                matches.Add((start, start));
                start++;
            }
            var endX = x.Length;
            var endY = y.Length;
            var tail = new List<(int, int)>();
            while (endX > start && endY > start && x[endX - 1] == y[endY - 1])
            {
                endX--;
                endY--;
                tail.Add((endX, endY));
            }
            var middle = Myers(x, y, start, endX, endY);
            if (middle != null) matches.AddRange(middle);
            tail.Reverse();
            matches.AddRange(tail);
            return matches;
        }

        // The shortest edit script between x[from..endX) and y[from..endY) (Myers, O(ND)), as the lines it keeps. Null when
        // the two are too different: then nothing in between is taken as kept.
        private static List<(int, int)> Myers(string[] x, string[] y, int from, int endX, int endY)
        {
            var n = endX - from;
            var m = endY - from;
            if (n == 0 || m == 0) return new List<(int, int)>();
            var max = Math.Min(n + m, MaxLineEdits);
            var offset = max + 1;
            var v = new int[2 * max + 3];
            var trace = new List<int[]>();
            for (var d = 0; d <= max; d++)
            {
                trace.Add((int[])v.Clone());
                for (var k = -d; k <= d; k += 2)
                {
                    int px;
                    if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1])) px = v[offset + k + 1];
                    else px = v[offset + k - 1] + 1;
                    var py = px - k;
                    while (px < n && py < m && x[from + px] == y[from + py])
                    {
                        px++;
                        py++;
                    }
                    v[offset + k] = px;
                    if (px >= n && py >= m) return Backtrack(trace, x, y, from, n, m, offset);
                }
            }
            return null;
        }

        private static List<(int, int)> Backtrack(List<int[]> trace, string[] x, string[] y, int from, int n, int m, int offset)
        {
            var kept = new List<(int, int)>();
            int cx = n, cy = m;
            for (var d = trace.Count - 1; d >= 0; d--)
            {
                var v = trace[d];
                var k = cx - cy;
                int previousK;
                if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1])) previousK = k + 1;
                else previousK = k - 1;
                var previousX = v[offset + previousK];
                var previousY = previousX - previousK;
                while (cx > previousX && cy > previousY)
                {
                    kept.Add((from + cx - 1, from + cy - 1));
                    cx--;
                    cy--;
                }
                if (d > 0)
                {
                    cx = previousX;
                    cy = previousY;
                }
            }
            kept.Reverse();
            return kept;
        }
    }
}
