using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services
{
    // Edit > Copy as WhatsApp text: a note, or the selection of one, as a message WhatsApp draws as it should.
    //
    // WhatsApp has its own small markup: *bold*, _italic_, ~strikethrough~, `inline code`, ```a block of code```,
    // "- " and "1. " lists and "> " quotes. It has no headings, links, images or tables, and it reads a marker
    // only when it touches the text it marks. Everything here is rules, offline, no AI.
    //
    //  - Markdown bold, italic, strikethrough and code become the WhatsApp markers (bold italic is *_text_*).
    //  - A heading becomes a bold line, set off by a blank line.
    //  - A link becomes "text (address)", an image its description (and its address when it is on the web), a table
    //    a block of code with the columns lined up, a rule a line of dashes, a task "✅"/"⬜", a footnote "[1]".
    //  - Review marks (CriticMarkup) are taken as accepted and their comments are dropped; YAML front matter and
    //    HTML tags go. What the editor does with speech marks is done before this, in the page.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class WhatsAppFormat
    {
        // What one WhatsApp message holds (characters); WhatsApp cuts what is longer.
        public const int MessageLimit = 65536;

        // Markers that are not Markdown any more, so a later rule can't read them again; they become * _ ~ at the end.
        private const char Bold = '\uE000', Italic = '\uE001', Strike = '\uE002', Slot = '\uE010', SlotEnd = '\uE011';

        private static readonly Regex Fence = new(@"^\s{0,3}(`{3,}|~{3,})");
        private static readonly Regex Rule = new(@"^\s{0,3}([-*_])(?:\s*\1){2,}\s*$");
        private static readonly Regex Atx = new(@"^\s{0,3}(#{1,6})\s+(.*?)(?:\s+#+)?\s*$");
        private static readonly Regex Setext = new(@"^\s{0,3}(=+|-{2,})\s*$");
        private static readonly Regex Quote = new(@"^\s{0,3}(?:>\s?)+(.*)$");
        private static readonly Regex Task = new(@"^(\s*)[-*+]\s+\[([ xX])\]\s+(.*)$");
        private static readonly Regex Bullet = new(@"^(\s*)[-*+]\s+(.*)$");
        private static readonly Regex Numbered = new(@"^(\s*)(\d{1,9})[.)]\s+(.*)$");
        private static readonly Regex FootnoteDefinition = new(@"^\[\^([^\]]+)\]:\s*(.*)$");
        private static readonly Regex TableSeparator = new(@"^\s*\|?\s*:?-+:?\s*(?:\|\s*:?-+:?\s*)*\|?\s*$");
        private static readonly Regex MathFence = new(@"^\s*\$\$\s*$");
        private static readonly Regex MathLine = new(@"^\s*\$\$(.+)\$\$\s*$");

        // The document as Markdown in, the message out.
        public static string Convert(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return "";
            // Changes are taken as accepted and the comments are dropped: the message is the text as it reads now.
            markdown = ReviewMarks.Resolve(ReviewMarks.Resolve(markdown, ReviewAction.Accept), ReviewAction.DeleteComments);
            var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var output = new List<string>();
            var indents = new List<int>(); // the indentation of the open list levels
            var i = 0;

            // YAML front matter at the very top: a closing line and at least one "key:" between, so that a note which opens
            // with a rule (and has another further down) keeps what lies between them
            if (lines.Length > 1 && lines[0].Trim() == "---")
            {
                var close = Array.FindIndex(lines, 1, l => l.Trim() is "---" or "...");
                if (close > 1 && lines.Skip(1).Take(close - 1).Any(l => Regex.IsMatch(l, @"^[A-Za-z0-9_-]+\s*:")))
                    i = close + 1;
            }

            void Add(string line) => output.Add(line);
            void BlankBefore() { if (output.Count > 0 && output[^1].Length > 0) Add(""); }

            while (i < lines.Length)
            {
                var line = lines[i];

                var fence = Fence.Match(line);
                if (fence.Success)
                {
                    var marker = fence.Groups[1].Value;
                    var code = new List<string>();
                    i++;
                    while (i < lines.Length && !IsClosingFence(lines[i], marker)) code.Add(lines[i++].TrimEnd());
                    i++; // the closing fence
                    CodeBlock(output, code);
                    indents.Clear();
                    continue;
                }
                if (MathFence.IsMatch(line))
                {
                    var math = new List<string>();
                    i++;
                    while (i < lines.Length && !MathFence.IsMatch(lines[i])) math.Add(lines[i++].TrimEnd());
                    i++;
                    CodeBlock(output, math);
                    continue;
                }
                var mathLine = MathLine.Match(line);
                if (mathLine.Success)
                {
                    CodeBlock(output, new List<string> { mathLine.Groups[1].Value.Trim() });
                    i++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    Add("");
                    indents.Clear();
                    i++;
                    continue;
                }
                if (line.Contains('|') && i + 1 < lines.Length && TableSeparator.IsMatch(lines[i + 1]) && Cells(line).Count == Cells(lines[i + 1]).Count)
                {
                    var rows = new List<List<string>> { Cells(line) };
                    i += 2;
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && lines[i].Contains('|')) rows.Add(Cells(lines[i++]));
                    Table(output, rows);
                    indents.Clear();
                    continue;
                }
                if (Rule.IsMatch(line))
                {
                    Add("———");
                    indents.Clear();
                    i++;
                    continue;
                }
                var atx = Atx.Match(line);
                if (atx.Success)
                {
                    BlankBefore();
                    Add(Heading(atx.Groups[2].Value));
                    indents.Clear();
                    i++;
                    continue;
                }
                var quote = Quote.Match(line);
                if (quote.Success)
                {
                    Add("> " + Inline(quote.Groups[1].Value.TrimEnd()));
                    i++;
                    continue;
                }
                var task = Task.Match(line);
                if (task.Success)
                {
                    Add(Indent(task.Groups[1].Value, indents) + (task.Groups[2].Value == " " ? "⬜ " : "✅ ") + Inline(task.Groups[3].Value.TrimEnd()));
                    i++;
                    continue;
                }
                var bullet = Bullet.Match(line);
                if (bullet.Success)
                {
                    Add(Indent(bullet.Groups[1].Value, indents) + "- " + Inline(bullet.Groups[2].Value.TrimEnd()));
                    i++;
                    continue;
                }
                var number = Numbered.Match(line);
                if (number.Success)
                {
                    Add(Indent(number.Groups[1].Value, indents) + number.Groups[2].Value + ". " + Inline(number.Groups[3].Value.TrimEnd()));
                    i++;
                    continue;
                }
                var footnote = FootnoteDefinition.Match(line);
                if (footnote.Success)
                {
                    Add("[" + footnote.Groups[1].Value + "] " + Inline(footnote.Groups[2].Value.TrimEnd()));
                    i++;
                    continue;
                }
                // A line of text over a line of = or - is a heading too
                if (i + 1 < lines.Length && Setext.IsMatch(lines[i + 1]))
                {
                    BlankBefore();
                    Add(Heading(line.Trim()));
                    indents.Clear();
                    i += 2;
                    continue;
                }
                var text = Inline(line.Trim().TrimEnd('\\'));
                // A line that was only a tag or a comment leaves nothing
                if (text.Length > 0 || !Regex.IsMatch(line, @"^\s*<")) Add(indents.Count > 0 ? new string(' ', 2 * indents.Count) + text : text);
                i++;
            }

            var result = string.Join("\n", output.Select(l => l.TrimEnd()));
            result = Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
            return result;
        }

        private static bool IsClosingFence(string line, string marker)
        {
            var t = line.Trim();
            return t.Length >= marker.Length && t.All(c => c == marker[0]);
        }

        private static void CodeBlock(List<string> output, List<string> code)
        {
            if (output.Count > 0 && output[^1].Length > 0) output.Add("");
            output.Add("```");
            output.AddRange(code);
            output.Add("```");
        }

        // The depth of a list item from its indentation: a deeper indentation opens a level, a shallower one closes levels.
        private static string Indent(string leading, List<int> indents)
        {
            var width = leading.Replace("\t", "    ").Length;
            while (indents.Count > 0 && width < indents[^1]) indents.RemoveAt(indents.Count - 1);
            if (indents.Count == 0 || width > indents[^1]) indents.Add(width);
            return new string(' ', 2 * (indents.Count - 1));
        }

        // A heading is a bold line; what is bold in it already needs no second pair of stars.
        private static string Heading(string text)
        {
            var inner = InlineMarked(text).Replace(Bold.ToString(), "").Trim();
            return inner.Length == 0 ? "" : Markers(Bold + inner + Bold);
        }

        private static List<string> Cells(string row)
        {
            var t = row.Trim();
            if (t.StartsWith('|')) t = t[1..];
            if (t.EndsWith('|') && !t.EndsWith("\\|")) t = t[..^1];
            return Regex.Split(t, @"(?<!\\)\|").Select(c => Plain(c.Replace("\\|", "|").Trim())).ToList();
        }

        // WhatsApp has no tables: the columns are lined up in a block of code (which it draws with a fixed-width font).
        private static void Table(List<string> output, List<List<string>> rows)
        {
            var columns = rows.Max(r => r.Count);
            foreach (var r in rows) while (r.Count < columns) r.Add("");
            var widths = Enumerable.Range(0, columns).Select(c => Math.Max(1, rows.Max(r => r[c].Length))).ToArray();
            var lines = new List<string>();
            string Row(List<string> r) => string.Join(" | ", r.Select((c, n) => c.PadRight(widths[n]))).TrimEnd();
            lines.Add(Row(rows[0]));
            lines.Add(string.Join("-+-", widths.Select(w => new string('-', w))));
            foreach (var r in rows.Skip(1)) lines.Add(Row(r));
            CodeBlock(output, lines);
        }

        // Text with no markers at all (table cells sit in a block of code, where WhatsApp does not format).
        private static string Plain(string text) => InlineMarked(text).Replace(Bold.ToString(), "").Replace(Italic.ToString(), "").Replace(Strike.ToString(), "").Replace("`", "");

        private static string Markers(string text) => text.Replace(Bold, '*').Replace(Italic, '_').Replace(Strike, '~');

        // Bold, italic, strikethrough, code, links and images inside one line.
        private static string Inline(string text) => Markers(InlineMarked(text));

        // The same with the bold, italic and strikethrough markers still the private ones, so a caller can take them out.
        private static string InlineMarked(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // What is final from here on (code, addresses, maths, escaped characters) sits in a slot, out of reach of the rules.
            var slots = new List<string>();
            string Hold(string final)
            {
                slots.Add(final);
                return Slot + (slots.Count - 1).ToString() + SlotEnd;
            }

            text = Regex.Replace(text, @"(`+)(?!`)(.+?[^`])\1(?!`)", m => Hold("`" + m.Groups[2].Value.Trim() + "`"));
            text = Regex.Replace(text, @"<!--.*?-->", "");
            text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\\([\\`*_{}\[\]()#+\-.!~|>$<])", m => Hold(m.Groups[1].Value));
            text = Regex.Replace(text, @"(?<!\$)\$(?![\s$])([^$\n]+?)(?<![\s$])\$(?![\d$])", m => Hold(m.Groups[1].Value));
            // Images, then links: the address stays out of reach of the italic rule (an address holds underscores)
            text = Regex.Replace(text, @"!\[([^\]]*)\]\(\s*<?([^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)", m =>
            {
                var alt = m.Groups[1].Value.Trim();
                var url = m.Groups[2].Value;
                if (!IsWeb(url)) return alt;
                return alt.Length > 0 ? alt + " " + Hold("(" + url + ")") : Hold(url);
            });
            text = Regex.Replace(text, @"\[([^\]]+)\]\(\s*<?([^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)", m =>
            {
                var label = m.Groups[1].Value;
                var url = m.Groups[2].Value;
                if (!IsWeb(url)) return label;
                var shown = url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? url[7..] : url;
                var same = string.Equals(label.Trim(), shown, StringComparison.OrdinalIgnoreCase) || string.Equals(label.Trim(), url, StringComparison.OrdinalIgnoreCase);
                return same ? Hold(shown) : label + " " + Hold("(" + shown + ")");
            });
            text = Regex.Replace(text, @"<((?:https?://|mailto:)[^>\s]+)>", m => Hold(m.Groups[1].Value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? m.Groups[1].Value[7..] : m.Groups[1].Value));
            text = Regex.Replace(text, @"https?://[^\s<>)\]]+", m => Hold(m.Value));
            text = Regex.Replace(text, @"\[\^([^\]]+)\]", m => "[" + m.Groups[1].Value + "]");
            text = Regex.Replace(text, @"</?[A-Za-z][^>]*>", "");

            text = Regex.Replace(text, @"~~(?!\s)(.+?)(?<!\s)~~", m => Strike + m.Groups[1].Value + Strike);
            text = Regex.Replace(text, @"==(?!\s)(.+?)(?<!\s)==", "$1");
            text = Regex.Replace(text, @"\*\*\*(?![\s*])(.+?)(?<![\s*])\*\*\*", m => Bold.ToString() + Italic + m.Groups[1].Value + Italic + Bold);
            text = Regex.Replace(text, @"(?<![\w_])___(?![\s_])(.+?)(?<![\s_])___(?![\w_])", m => Bold.ToString() + Italic + m.Groups[1].Value + Italic + Bold);
            text = Regex.Replace(text, @"\*\*(?![\s*])(.+?)(?<![\s*])\*\*", m => Bold + m.Groups[1].Value + Bold);
            text = Regex.Replace(text, @"(?<![\w_])__(?![\s_])(.+?)(?<![\s_])__(?![\w_])", m => Bold + m.Groups[1].Value + Bold);
            text = Regex.Replace(text, @"(?<![\w*])\*(?![\s*])(.+?)(?<![\s*])\*(?![\w*])", m => Italic + m.Groups[1].Value + Italic);
            text = Regex.Replace(text, @"(?<![\w_])_(?![\s_])(.+?)(?<![\s_])_(?![\w_])", m => Italic + m.Groups[1].Value + Italic);

            text = Regex.Replace(text, Slot + @"(\d+)" + SlotEnd, m => slots[int.Parse(m.Groups[1].Value)]);
            return text;
        }

        private static bool IsWeb(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }
}