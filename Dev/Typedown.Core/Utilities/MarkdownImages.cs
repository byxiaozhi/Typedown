using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Where a Markdown text refers to images by an address, and what it looks like with some of those addresses
    /// replaced (File > Upload local images). Plain .NET, so the tests run it on any machine.
    ///
    /// Counted: inline images (![alt](dest "title"), dest also in &lt;&gt;), the definitions images use by reference
    /// (![alt][label], ![label][], ![label] with [label]: dest), and &lt;img src&gt;. Not counted: anything in a fenced
    /// code block or a code span - an example of Markdown is not an image of the document.
    /// </summary>
    public static class MarkdownImages
    {
        /// <summary>An image address in the text: <see cref="Start"/> and <see cref="Length"/> cover the address alone.</summary>
        public sealed class Reference
        {
            public int Start { get; set; }
            public int Length { get; set; }
            public string Address { get; set; } = "";
        }

        private static readonly Regex Fence = new(@"^[ ]{0,3}(`{3,}|~{3,})", RegexOptions.Compiled);
        private static readonly Regex Definition = new(@"^[ ]{0,3}\[((?:[^\]\\]|\\.)+)\]:[ \t]*(<[^>\n]*>|\S+)", RegexOptions.Compiled);
        private static readonly Regex Img = new(@"<img\b[^>]*?\bsrc\s*=\s*(""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<Reference> Find(string text)
        {
            var result = new List<Reference>();
            var code = CodeRanges(text);
            bool InCode(int i) => code.Any(r => i >= r.start && i < r.end);

            // Labels images use by reference, then the definitions of those labels.
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = text.IndexOf("![", StringComparison.Ordinal); i >= 0; i = text.IndexOf("![", i + 2, StringComparison.Ordinal))
            {
                if (InCode(i) || IsEscaped(text, i)) continue;
                var close = ClosingBracket(text, i + 1);
                if (close < 0) continue;
                var alt = text.Substring(i + 2, close - i - 2);
                var next = close + 1;
                if (next < text.Length && text[next] == '(')
                {
                    if (InlineDestination(text, next) is Reference r) result.Add(r);
                }
                else if (next < text.Length && text[next] == '[')
                {
                    var end = text.IndexOf(']', next + 1);
                    if (end < 0) continue;
                    var label = text.Substring(next + 1, end - next - 1);
                    labels.Add(Normalize(label.Length == 0 ? alt : label));
                }
                else
                {
                    labels.Add(Normalize(alt));
                }
            }

            var lineStart = 0;
            foreach (var line in text.Split('\n'))
            {
                var m = Definition.Match(line);
                if (!InCode(lineStart) && m.Success && labels.Contains(Normalize(m.Groups[1].Value)))
                {
                    var dest = m.Groups[2];
                    var bracketed = dest.Value.StartsWith("<");
                    result.Add(new Reference
                    {
                        Start = lineStart + dest.Index + (bracketed ? 1 : 0),
                        Length = dest.Length - (bracketed ? 2 : 0),
                        Address = bracketed ? dest.Value.Substring(1, dest.Length - 2) : dest.Value,
                    });
                }
                lineStart += line.Length + 1;
            }

            foreach (Match m in Img.Matches(text))
            {
                if (InCode(m.Index)) continue;
                var g = m.Groups[2].Success ? m.Groups[2] : m.Groups[3].Success ? m.Groups[3] : m.Groups[4];
                result.Add(new Reference { Start = g.Index, Length = g.Length, Address = g.Value });
            }
            return result.OrderBy(r => r.Start).ToList();
        }

        /// <summary>
        /// An address that names a file on this computer: a relative or absolute path, or file:. Not a web address,
        /// data:, or a protocol-relative //host/... (a drive letter, C:\ or C:/, is a path and not a scheme).
        /// </summary>
        public static bool IsLocal(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address.StartsWith("#") || address.StartsWith("//")) return false;
            if (Regex.IsMatch(address, @"^[A-Za-z]:[\\/]")) return true;
            var scheme = Regex.Match(address, @"^([A-Za-z][A-Za-z0-9+.-]*):");
            return !scheme.Success || scheme.Groups[1].Value.Equals("file", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The text with each reference whose address is in <paramref name="replacements"/> given its new address.</summary>
        public static string Replace(string text, IReadOnlyDictionary<string, string> replacements)
        {
            var builder = new StringBuilder(text);
            foreach (var r in Find(text).OrderByDescending(r => r.Start))
                if (replacements.TryGetValue(r.Address, out var to))
                    builder.Remove(r.Start, r.Length).Insert(r.Start, to);
            return builder.ToString();
        }

        private static string Normalize(string label) => Regex.Replace(label.Trim(), @"\s+", " ");

        private static bool IsEscaped(string text, int i)
        {
            var backslashes = 0;
            for (var j = i - 1; j >= 0 && text[j] == '\\'; j--) backslashes++;
            return backslashes % 2 == 1;
        }

        // The ']' that closes the '[' at open, nested brackets and escapes allowed; -1 when the line ends first.
        private static int ClosingBracket(string text, int open)
        {
            var depth = 0;
            for (var i = open; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\') { i++; continue; }
                if (c == '\n' && i + 1 < text.Length && text[i + 1] == '\n') return -1;
                if (c == '[') depth++;
                else if (c == ']' && --depth == 0) return i;
            }
            return -1;
        }

        // (dest "title") from the '(' at paren: the destination, &lt;...&gt; or up to white space with balanced parentheses.
        private static Reference InlineDestination(string text, int paren)
        {
            var i = paren + 1;
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t' || text[i] == '\n')) i++;
            if (i >= text.Length) return null;
            if (text[i] == '<')
            {
                var end = text.IndexOf('>', i + 1);
                if (end < 0 || text.IndexOf('\n', i, end - i) >= 0) return null;
                return new Reference { Start = i + 1, Length = end - i - 1, Address = text.Substring(i + 1, end - i - 1) };
            }
            var start = i;
            var depth = 0;
            for (; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\') { i++; continue; }
                if (char.IsWhiteSpace(c)) break;
                if (c == '(') depth++;
                else if (c == ')') { if (depth == 0) break; depth--; }
            }
            if (i == start) return null;
            return new Reference { Start = start, Length = i - start, Address = text.Substring(start, i - start) };
        }

        // Fenced code blocks (to the closing fence or the end) and code spans.
        private static List<(int start, int end)> CodeRanges(string text)
        {
            var ranges = new List<(int, int)>();
            var lines = text.Split('\n');
            var pos = 0;
            string fence = null;
            var fenceStart = 0;
            foreach (var line in lines)
            {
                var m = Fence.Match(line);
                if (fence == null && m.Success)
                {
                    fence = m.Groups[1].Value;
                    fenceStart = pos;
                }
                else if (fence != null && m.Success && m.Groups[1].Value[0] == fence[0] && m.Groups[1].Value.Length >= fence.Length && line.Trim().Trim(fence[0]).Length == 0)
                {
                    ranges.Add((fenceStart, pos + line.Length));
                    fence = null;
                }
                pos += line.Length + 1;
            }
            if (fence != null) ranges.Add((fenceStart, text.Length));

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '`' || ranges.Any(r => i >= r.Item1 && i < r.Item2) || IsEscaped(text, i)) continue;
                var run = 1;
                while (i + run < text.Length && text[i + run] == '`') run++;
                var marker = new string('`', run);
                var close = i + run;
                while ((close = text.IndexOf(marker, close, StringComparison.Ordinal)) >= 0)
                {
                    var after = close + run;
                    if ((after >= text.Length || text[after] != '`') && text[close - 1] != '`') break;
                    close = after;
                }
                if (close < 0) { i += run - 1; continue; }
                ranges.Add((i, close + run));
                i = close + run - 1;
            }
            return ranges;
        }
    }
}
