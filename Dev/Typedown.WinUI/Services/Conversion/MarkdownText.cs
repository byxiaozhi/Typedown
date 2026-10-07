using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // Shared Markdown writing helpers for the converters.
    internal static class MarkdownText
    {
        // Escapes characters that would otherwise turn plain document text into Markdown syntax. Kept
        // minimal on purpose: every backslash is a token, and most text needs none.
        public static string EscapeInline(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length + 8);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '\\':
                    case '`':
                    case '*':
                        sb.Append('\\').Append(c);
                        break;
                    case '_':
                        // Only at a word edge; snake_case inside a word never becomes emphasis.
                        var before = i > 0 && char.IsLetterOrDigit(text[i - 1]);
                        var after = i + 1 < text.Length && char.IsLetterOrDigit(text[i + 1]);
                        if (!(before && after)) sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '<':
                        if (i + 1 < text.Length && (char.IsLetter(text[i + 1]) || text[i + 1] == '/' || text[i + 1] == '!')) sb.Append("&lt;");
                        else sb.Append(c);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static readonly Regex BlockStart = new(@"^(\s*)(#{1,6}\s|>|[-+*]\s|\d{1,9}[.)]\s)", RegexOptions.Compiled);

        // For text that starts a paragraph: stops "1. " / "- " / "# " / "> " at the very start from
        // being read as a list, heading or quote.
        public static string EscapeBlockStart(string text)
        {
            var m = BlockStart.Match(text ?? "");
            if (!m.Success) return text;
            var marker = m.Groups[2].Value;
            var escaped = char.IsDigit(marker[0])
                ? Regex.Replace(marker, @"^(\d+)([.)])", "$1\\$2")
                : "\\" + marker;
            return m.Groups[1].Value + escaped + text.Substring(m.Length);
        }

        public static string TableCell(string text) =>
            (text ?? "").Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\n", "<br>").Trim();

        // A GFM pipe table; the first row is the header. Ragged rows are padded.
        public static string Table(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (rows == null || rows.Count == 0) return "";
            var columns = rows.Max(r => r.Count);
            if (columns == 0) return "";
            var sb = new StringBuilder();
            void Row(IReadOnlyList<string> cells)
            {
                sb.Append('|');
                for (var c = 0; c < columns; c++)
                    sb.Append(' ').Append(c < cells.Count ? TableCell(cells[c]) : "").Append(" |");
                sb.Append('\n');
            }
            Row(rows[0]);
            sb.Append('|');
            for (var c = 0; c < columns; c++) sb.Append(" --- |");
            sb.Append('\n');
            foreach (var row in rows.Skip(1)) Row(row);
            return sb.ToString().TrimEnd('\n');
        }

        public static string CodeSpan(string code)
        {
            var longest = Regex.Matches(code, "`+").Select(m => m.Length).DefaultIfEmpty(0).Max();
            var fence = new string('`', longest + 1);
            var pad = code.StartsWith('`') || code.EndsWith('`') ? " " : "";
            return fence + pad + code + pad + fence;
        }

        public static string CodeBlock(string code)
        {
            var longest = Regex.Matches(code, "`{3,}").Select(m => m.Length).DefaultIfEmpty(0).Max();
            var fence = new string('`', Math.Max(3, longest + 1));
            return fence + "\n" + code.TrimEnd('\n') + "\n" + fence;
        }

        // Final clean-up: no trailing spaces except hard breaks, at most one blank line in a row,
        // single trailing newline.
        public static string Tidy(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(markdown.Length);
            var blank = 0;
            var inFence = false;
            foreach (var raw in lines)
            {
                if (raw.TrimStart().StartsWith("```")) inFence = !inFence;
                var line = inFence ? raw : (raw.EndsWith("  ") && raw.Trim().Length > 0 ? raw.TrimEnd() + "  " : raw.TrimEnd());
                if (line.Length == 0 && !inFence)
                {
                    if (++blank > 1 || sb.Length == 0) continue;
                }
                else blank = 0;
                sb.Append(line).Append('\n');
            }
            return sb.ToString().TrimEnd('\n') + "\n";
        }
    }

    // Collects a paragraph's text runs with their formatting and writes them as Markdown, merging
    // neighbouring runs with the same formatting (Word splits text into many runs — without merging,
    // "**Hello** **world**" would come out as "**Hello****world**").
    internal sealed class InlineBuilder
    {
        private sealed record Segment(string Text, bool Bold, bool Italic, bool Strike, bool Code, string Link, bool Raw);

        private readonly List<Segment> segments = new();

        public bool IsEmpty => segments.All(s => string.IsNullOrWhiteSpace(s.Text));

        public string PlainText => string.Concat(segments.Where(s => !s.Raw).Select(s => s.Text));

        public void Add(string text, bool bold = false, bool italic = false, bool strike = false, bool code = false, string link = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var last = segments.Count > 0 ? segments[^1] : null;
            if (last != null && !last.Raw && last.Bold == bold && last.Italic == italic && last.Strike == strike && last.Code == code && last.Link == link)
                segments[^1] = last with { Text = last.Text + text };
            else
                segments.Add(new Segment(text, bold, italic, strike, code, link, false));
        }

        // Already-Markdown content (an image, a line break, a footnote reference).
        public void AddRaw(string markdown, string link = null)
        {
            if (!string.IsNullOrEmpty(markdown)) segments.Add(new Segment(markdown, false, false, false, false, link, true));
        }

        public string Build(bool plainHeading = false)
        {
            var sb = new StringBuilder();
            var i = 0;
            while (i < segments.Count)
            {
                var link = segments[i].Link;
                var group = new List<Segment>();
                while (i < segments.Count && segments[i].Link == link) group.Add(segments[i++]);
                var inner = string.Concat(group.Select(s => Format(s, plainHeading)));
                if (link != null && inner.Trim().Length > 0)
                {
                    var lead = inner.Length - inner.TrimStart().Length;
                    var trail = inner.Length - inner.TrimEnd().Length;
                    sb.Append(inner, 0, lead).Append('[').Append(inner.Trim()).Append("](").Append(link.Replace(" ", "%20").Replace(")", "%29")).Append(')').Append(' ', trail > 0 ? 1 : 0);
                }
                else sb.Append(inner);
            }
            return sb.ToString();
        }

        private static string Format(Segment s, bool plainHeading)
        {
            if (s.Raw) return s.Text;
            if (s.Text.Trim().Length == 0) return s.Text;
            var lead = s.Text.Substring(0, s.Text.Length - s.Text.TrimStart().Length);
            var trail = s.Text.Substring(s.Text.TrimEnd().Length);
            var core = s.Text.Trim();
            if (s.Code) return lead + MarkdownText.CodeSpan(core) + trail;
            core = MarkdownText.EscapeInline(core);
            if (s.Strike) core = "~~" + core + "~~";
            if (s.Italic && !plainHeading) core = "*" + core + "*";
            if (s.Bold && !plainHeading) core = "**" + core + "**";
            return lead + core + trail;
        }
    }
}
