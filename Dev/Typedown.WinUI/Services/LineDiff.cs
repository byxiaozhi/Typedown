using System;
using System.Collections.Generic;
using System.Linq;

namespace Typedown.WinUI.Services
{
    internal enum DiffKind { Same, Removed, Added }

    internal readonly record struct DiffLine(DiffKind Kind, string Text);

    // New since the fork: the lines that differ between two versions of a text, for the History tab of the inspector pane
    // ("what changed between that version and the text now"). Plain .NET, tested without the window.
    //
    // How: the lines both versions start with and end with are set aside, and what is left in the middle is compared with
    // the longest common run of lines. A very long middle (more than MaxCells comparisons) is not compared line by line:
    // it is shown as all removed, then all added, which is true if less exact.
    internal static class LineDiff
    {
        private const int MaxCells = 4_000_000;

        // The lines of a text; blank lines at its end do not count (the editor ends a text with an empty paragraph the file may not have).
        private static string[] Lines(string text) =>
            (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n', ' ', '\t').Split('\n');

        // Every line of both versions, in the order of the text: kept (Same), only in the old one (Removed), only in the new one (Added).
        public static List<DiffLine> Compute(string older, string newer)
        {
            var x = Lines(older);
            var y = Lines(newer);
            var result = new List<DiffLine>();

            var start = 0;
            while (start < x.Length && start < y.Length && x[start] == y[start]) start++;
            var endX = x.Length;
            var endY = y.Length;
            while (endX > start && endY > start && x[endX - 1] == y[endY - 1]) { endX--; endY--; }

            for (var i = 0; i < start; i++) result.Add(new DiffLine(DiffKind.Same, x[i]));
            Middle(x, start, endX, y, start, endY, result);
            for (var i = endX; i < x.Length; i++) result.Add(new DiffLine(DiffKind.Same, x[i]));
            return result;
        }

        // Only the lines that changed (the Same lines left out), at most `limit` of them; `more` is how many were left out of the end.
        public static List<DiffLine> Changes(string older, string newer, int limit, out int more)
        {
            var changed = new List<DiffLine>();
            more = 0;
            foreach (var line in Compute(older, newer))
            {
                if (line.Kind == DiffKind.Same) continue;
                if (changed.Count < limit) changed.Add(line);
                else more++;
            }
            return changed;
        }

        // The first line that is not the same in the two versions, as the newer one has it (or, when the newer one only lost
        // lines at the end, the first line it lost); one line, the runs of spaces one space, cut with an ellipsis when long.
        public static string FirstChangedLine(string older, string newer, int length = 90)
        {
            var x = Lines(older);
            var y = Lines(newer);
            var i = 0;
            while (i < x.Length && i < y.Length && x[i] == y[i]) i++;
            // a blank line is not worth showing: the first line with words in the part that differs (the newer text's, else the older's)
            var line = y.Skip(i).FirstOrDefault(l => l.Trim().Length > 0) ?? x.Skip(i).FirstOrDefault(l => l.Trim().Length > 0) ?? "";
            var one = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ").Trim();
            return one.Length <= length ? one : one.Substring(0, length - 1).TrimEnd() + "…";
        }

        private static void Middle(string[] x, int fromX, int endX, string[] y, int fromY, int endY, List<DiffLine> result)
        {
            var n = endX - fromX;
            var m = endY - fromY;
            if (n == 0 || m == 0 || (long)n * m > MaxCells)
            {
                for (var i = fromX; i < endX; i++) result.Add(new DiffLine(DiffKind.Removed, x[i]));
                for (var j = fromY; j < endY; j++) result.Add(new DiffLine(DiffKind.Added, y[j]));
                return;
            }

            // lengths[i, j]: the longest common run of the lines from i on in x and from j on in y
            var lengths = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
                for (var j = m - 1; j >= 0; j--)
                    lengths[i, j] = x[fromX + i] == y[fromY + j]
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);

            var a = 0;
            var b = 0;
            while (a < n && b < m)
            {
                if (x[fromX + a] == y[fromY + b])
                {
                    result.Add(new DiffLine(DiffKind.Same, x[fromX + a]));
                    a++;
                    b++;
                }
                else if (lengths[a + 1, b] >= lengths[a, b + 1])
                {
                    result.Add(new DiffLine(DiffKind.Removed, x[fromX + a]));
                    a++;
                }
                else
                {
                    result.Add(new DiffLine(DiffKind.Added, y[fromY + b]));
                    b++;
                }
            }
            for (; a < n; a++) result.Add(new DiffLine(DiffKind.Removed, x[fromX + a]));
            for (; b < m; b++) result.Add(new DiffLine(DiffKind.Added, y[fromY + b]));
        }
    }
}
