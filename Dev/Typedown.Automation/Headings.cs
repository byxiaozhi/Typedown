using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.Automation
{
    public sealed class Heading
    {
        public int Index { get; set; }
        public int Level { get; set; }
        public string Text { get; set; } = "";
        public string Slug { get; set; } = "";
    }

    /// <summary>
    /// The headings of a document text for <c>document.get</c> with <c>include: ["headings"]</c>. Computed from the
    /// text itself, so they belong to exactly the revision that text is. The slug is the API's own stable id for a
    /// heading (GitHub style: lower case, punctuation removed, spaces as '-', repeats numbered -1, -2 ...); it is not
    /// the editor's outline id, which changes with every load.
    /// </summary>
    public static class Headings
    {
        private static readonly Regex Atx = new(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$");
        private static readonly Regex SetextUnderline = new(@"^ {0,3}(=+|-+)[ \t]*$");
        private static readonly Regex Fence = new(@"^ {0,3}(`{3,}|~{3,})");
        private static readonly Regex InlineMarkup = new(@"!?\[([^\]]*)\]\([^)]*\)|[*_~`]");

        public static IReadOnlyList<Heading> Extract(string text)
        {
            var result = new List<Heading>();
            var seen = new Dictionary<string, int>();
            var lines = text.Split('\n');
            var i = 0;
            if (lines.Length > 0 && lines[0] == "---")
            {
                var end = System.Array.IndexOf(lines, "---", 1);
                if (end > 0) i = end + 1;
            }
            string? fence = null;
            for (; i < lines.Length; i++)
            {
                var line = lines[i];
                var fenceMatch = Fence.Match(line);
                if (fence != null)
                {
                    if (fenceMatch.Success && fenceMatch.Groups[1].Value[0] == fence[0] && fenceMatch.Groups[1].Value.Length >= fence.Length && line.Trim().Trim(fence[0]).Length == 0) fence = null;
                    continue;
                }
                if (fenceMatch.Success) { fence = fenceMatch.Groups[1].Value; continue; }

                var atx = Atx.Match(line);
                if (atx.Success)
                {
                    Add(atx.Groups[1].Value.Length, atx.Groups[2].Value);
                    continue;
                }
                if (line.Trim().Length > 0 && i + 1 < lines.Length && !line.StartsWith("    "))
                {
                    var underline = SetextUnderline.Match(lines[i + 1]);
                    if (underline.Success && !IsBlockStart(line))
                    {
                        Add(underline.Groups[1].Value[0] == '=' ? 1 : 2, line.Trim());
                        i++;
                    }
                }
            }
            return result;

            void Add(int level, string raw)
            {
                var plain = InlineMarkup.Replace(raw.Trim(), m => m.Groups[1].Success ? m.Groups[1].Value : "").Trim();
                result.Add(new Heading { Index = result.Count, Level = level, Text = plain, Slug = Slug(plain, seen) });
            }
        }

        private static bool IsBlockStart(string line)
        {
            var t = line.TrimStart();
            return t.StartsWith(">") || t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("+ ") || t.StartsWith("|") || Regex.IsMatch(t, @"^\d+[.)] ");
        }

        private static string Slug(string text, Dictionary<string, int> seen)
        {
            var slug = new StringBuilder();
            foreach (var c in text.ToLowerInvariant().Trim())
            {
                var category = char.GetUnicodeCategory(c);
                if (char.IsWhiteSpace(c)) slug.Append('-');
                else if (c == '-' || c == '_' || char.IsLetterOrDigit(c) || category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark) slug.Append(c);
            }
            var s = slug.ToString();
            if (seen.TryGetValue(s, out var n))
            {
                var original = s;
                do { n++; s = original + "-" + n; } while (seen.ContainsKey(s));
                seen[original] = n;
            }
            seen[s] = 0;
            return s;
        }
    }
}
