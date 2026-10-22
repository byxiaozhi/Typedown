using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Caret.ConverterTests
{
    // A hand-written three-page PDF that has, in a small space, what made real documents convert badly: a vertical
    // stamp in the margin, two narrow columns above a full-width block, a table with rules and a heading over two columns,
    // a table with a rule under every row and a label that spans two rows, numbers drawn in pieces, links (one of them
    // broken over two lines), hyphenated words, code, a contents list, and running headers. Everything in it is made up.
    internal static class PdfLayoutSample
    {
        private sealed class Page
        {
            public readonly StringBuilder Content = new();
            public readonly List<(double Left, double Bottom, double Right, double Top, string Uri)> Links = new();

            // font: 1 Helvetica, 2 Helvetica-Bold, 3 Courier, 4 Times-Roman
            public Page Text(int font, double size, double x, double y, string text)
            {
                Content.Append(string.Create(CultureInfo.InvariantCulture, $"BT /F{font} {size} Tf {x} {y} Td ({Escape(text)}) Tj ET\n"));
                return this;
            }

            // Text turned a quarter, reading upwards (a stamp in the margin).
            public Page Rotated(int font, double size, double x, double y, string text)
            {
                Content.Append(string.Create(CultureInfo.InvariantCulture, $"BT /F{font} {size} Tf 0 1 -1 0 {x} {y} Tm ({Escape(text)}) Tj ET\n"));
                return this;
            }

            public Page Rule(double x1, double x2, double y)
            {
                Content.Append(string.Create(CultureInfo.InvariantCulture, $"0.5 w {x1} {y} m {x2} {y} l S\n"));
                return this;
            }

            public Page Link(double left, double bottom, double right, double top, string uri)
            {
                Links.Add((left, bottom, right, top, uri));
                return this;
            }
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        public static byte[] Build()
        {
            var pages = new List<Page> { FirstPage(), SecondPage(), ThirdPage(), FourthPage() };
            return Write(pages);
        }

        private static void Furniture(Page page, int number)
        {
            page.Text(1, 8, 72, 815, "Layout Test Report");
            page.Text(1, 9, 300, 40, number.ToString(CultureInfo.InvariantCulture));
            page.Text(1, 8, 72, 100, "Confidential draft, page " + number.ToString(CultureInfo.InvariantCulture));
        }

        private static Page FirstPage()
        {
            var p = new Page();
            Furniture(p, 1);
            // The vertical stamp in the left margin
            p.Rotated(4, 16, 30, 250, "arXiv:2609.00000v1 [cs.LG] 1 Jan 2026");

            p.Text(2, 20, 72, 740, "Layout test document");

            // A bold lead-in that runs into ordinary text
            p.Text(2, 10, 72, 712, "Summary.");
            p.Text(1, 10, 126, 712, "This paragraph starts with a bold lead-in and goes on in regular text, so the lead-in");
            p.Text(1, 10, 72, 700, "stays in its paragraph, in bold, instead of becoming a heading of its own.");

            // Hyphens at the end of a line
            p.Text(1, 10, 72, 676, "The planted flaws here are narrative-");
            p.Text(1, 10, 72, 664, "changing ones, and the word exam-");
            p.Text(1, 10, 72, 652, "ple is split in the ordinary way.");
            p.Text(1, 10, 72, 640, "A narrative-changing flaw is written with its hyphen in the middle of a line too.");

            // Two narrow columns (8 points apart)
            p.Text(2, 12, 72, 612, "Introduction");
            var left = new[]
            {
                "The first column holds the start of",
                "a sentence that does not finish here",
                "but carries on in the second column,",
                "which begins at the top of the same",
                "block and keeps reading downwards",
                "until the text of this small example",
                "has been used up completely and a",
                "short closing line ends the story.",
            };
            var right = new[]
            {
                "so that a reader follows the left side",
                "first and only then the right side of",
                "the page, the way a person would do.",
                "The order matters for anything that",
                "reads the result, such as a search or",
                "an assistant that summarises it, and",
                "interleaving lines of two columns would",
                "make nonsense of every sentence.",
            };
            for (var i = 0; i < left.Length; i++)
            {
                p.Text(1, 10, 72, 592 - i * 12, left[i]);
                p.Text(1, 10, 292, 592 - i * 12, right[i]);
            }

            // A full-width block below
            p.Text(1, 10, 72, 470, "Figure 1. A caption that runs across the whole width of the page, below the two columns of");
            p.Text(1, 10, 72, 458, "text above, with a gap before it that is larger than the space between two lines.");
            return p;
        }

        private static Page SecondPage()
        {
            var p = new Page();
            Furniture(p, 2);
            p.Text(1, 10, 72, 270, "Net income 10"); // a line in the body whose number changes from page to page is not a footer
            p.Text(2, 10, 72, 803, "Layout Annual Report - 2");
            p.Text(2, 10, 72, 792, "1. Overview"); // a numbered heading at the same height on three pages is not a running header
            p.Text(2, 14, 72, 770, "Results");

            // A table with rules above and below its heading and under the last row; the heading has two levels
            p.Rule(72, 523, 740);
            p.Text(2, 9, 190, 728, "Baseline");
            p.Text(2, 9, 340, 728, "Tuned");
            p.Rule(180, 280, 723);
            p.Rule(330, 430, 723);
            p.Text(2, 9, 72, 710, "Task");
            p.Text(2, 9, 190, 710, "Alpha");
            p.Text(2, 9, 242, 710, "Beta");
            p.Text(2, 9, 340, 710, "Alpha");
            p.Text(2, 9, 392, 710, "Beta");
            p.Rule(72, 523, 702);
            var rows = new[] { ("Conceal results", "22.0", "1.0", "+78.0", "+94.5"), ("Ignore bug", "22.6", "42.0", "+68.1", "+36.0"), ("Hide pending call", "0.0", "0.0", "+16.0", "0.0") };
            for (var i = 0; i < rows.Length; i++)
            {
                var y = 688 - i * 14;
                var (task, a, b, c, d) = rows[i];
                p.Text(1, 9, 72, y, task);
                p.Text(1, 9, 190, y, a);
                p.Text(1, 9, 242, y, b);
                SplitNumber(p, 340, y, c);
                SplitNumber(p, 392, y, d);
            }
            p.Rule(72, 523, 640);

            p.Text(1, 9, 72, 622, "Table 1. Scores in percent; the figures are made up.");

            // A table with a rule under every row and a heading above the first rule
            p.Text(2, 9, 72, 590, "Item");
            p.Text(2, 9, 300, 590, "Notes");
            p.Text(2, 9, 400, 590, "2024");
            p.Text(2, 9, 470, 590, "2023");
            var edge = 584;
            var items = new[] { ("Cash and equivalents", "", "119 502", "170 299"), ("Receivables", "3", "119 502", "170 299"), ("Total assets", "", "270 517", "267 066"), ("Other receivables", "4", "1 238", "372") };
            p.Rule(72, 523, edge);
            for (var i = 0; i < items.Length; i++)
            {
                var y = edge - 14 - i * 16 + 3;
                var (item, note, a, b) = items[i];
                p.Text(i == 2 ? 2 : 1, 9, 72, y, item);
                if (note.Length > 0) p.Text(1, 9, 300, y, note);
                p.Text(1, 9, 400, y, a);
                p.Text(1, 9, 470, y, b);
                p.Rule(72, 523, edge - 16 * (i + 1));
            }

            // Links: one broken over two lines, one short; a full stop after a link stays outside it
            p.Text(1, 10, 72, 480, "Read the guide at");
            p.Text(1, 10, 160, 480, "https://example.com/docs/guide-");
            p.Text(1, 10, 72, 468, "to-pages");
            p.Text(1, 10, 125, 468, "for the details, or visit the");
            p.Text(1, 10, 72, 456, "Project site.");
            p.Link(158, 477, 290, 490, "https://example.com/docs/guide-to-pages");
            p.Link(70, 465, 122, 478, "https://example.com/docs/guide-to-pages");
            p.Link(70, 453, 128, 466, "https://example.com/");

            // Code in a monospaced font
            p.Text(3, 9, 72, 420, "def total(items):");
            p.Text(3, 9, 72, 408, "    result = 0");
            p.Text(3, 9, 72, 396, "    for item in items:");
            p.Text(3, 9, 72, 384, "        result += item.price");
            p.Text(3, 9, 72, 372, "    return result");

            // A contents list
            p.Text(2, 12, 72, 340, "Contents");
            var entries = new[] { ("Introduction", "3"), ("Results and discussion", "7"), ("Appendix A: the prompts", "12") };
            for (var i = 0; i < entries.Length; i++)
            {
                var y = 322 - i * 14;
                p.Text(1, 10, 72, y, entries[i].Item1);
                p.Text(1, 10, 260, y, "................................");
                p.Text(1, 10, 510, y, entries[i].Item2);
            }
            return p;
        }

        // 78.0 drawn as "+78", "." and "0": three pieces that touch, as in a table set with maths fonts.
        private static void SplitNumber(Page p, double x, double y, string number)
        {
            var dot = number.IndexOf('.');
            if (dot < 0)
            {
                p.Text(1, 9, x, y, number);
                return;
            }
            var whole = number.Substring(0, dot);
            var width = whole.Sum(c => c == '+' ? 5.256 : 5.004) ; // Helvetica at 9 pt: "+" 0.584 em, a digit 0.556 em
            p.Text(1, 9, x, y, whole);
            p.Text(1, 9, x + width, y, ".");
            p.Text(1, 9, x + width + 2.5, y, number.Substring(dot + 1));
        }

        private static Page ThirdPage()
        {
            var p = new Page();
            Furniture(p, 3);
            p.Text(1, 10, 72, 270, "Net income 20");
            // Prose, then a list whose items are in bold
            p.Text(1, 10, 72, 200, "Follow these steps in this order.");
            p.Text(2, 10, 72, 186, "1. First bold step");
            p.Text(2, 10, 72, 174, "2. Second bold step");
            p.Text(2, 10, 72, 803, "Layout Annual Report - 3");
            p.Text(2, 10, 72, 792, "2. Overview");
            p.Text(2, 14, 72, 770, "Conclusion");
            p.Text(1, 10, 72, 748, "A short last page, so that the running header and the page numbers repeat often enough");
            p.Text(1, 10, 72, 736, "to be recognised as furniture and left out of the converted text.");

            // The same address twice in one line: both are kept
            p.Text(1, 10, 72, 700, "Twice:");
            p.Text(1, 10, 110, 700, "https://b.example.com");
            p.Text(1, 10, 222, 700, "and");
            p.Text(1, 10, 250, 700, "https://b.example.com.");
            p.Link(108, 697, 215, 711, "https://b.example.com");
            p.Link(248, 697, 365, 711, "https://b.example.com");

            // A link that is not to the web (it could run something) is dropped; its text stays
            p.Text(1, 10, 72, 660, "Open the settings page.");
            p.Link(70, 657, 160, 671, "javascript:alert(1)");

            // A numbered subsection heading right above a paragraph that opens in bold; a heading in capitals at the size of the text
            p.Text(2, 10, 72, 620, "3.1 Subsection title");
            p.Text(2, 10, 72, 596, "Encoder:");
            p.Text(1, 10, 120, 596, "The first words of this paragraph follow a bold lead-in on the same line,");
            p.Text(1, 10, 72, 584, "and the paragraph goes on here.");
            p.Text(1, 10, 72, 550, "1 INTRODUCTION");
            p.Text(1, 10, 72, 526, "Plain text after the heading in capitals.");

            // A numbered list keeps its numbers
            p.Text(1, 10, 72, 500, "1. First item of the list");
            p.Text(1, 10, 72, 488, "2. Second item of the list");
            p.Text(1, 10, 72, 476, "3. Third item of the list");

            // A table whose rules separate groups of rows, not every row
            p.Rule(72, 523, 450);
            p.Text(2, 9, 72, 438, "Item");
            p.Text(2, 9, 250, 438, "Value");
            p.Text(2, 9, 400, 438, "Note");
            p.Rule(72, 523, 432);
            var groups = new[] { new[] { ("Alpha", "1", "first"), ("Beta", "2", "second"), ("Gamma", "3", "third") }, new[] { ("Delta", "4", "fourth"), ("Epsilon", "5", "fifth") }, new[] { ("Zeta", "6", "sixth"), ("Eta", "7", "seventh"), ("Theta", "8", "eighth") } };
            var row = 418;
            foreach (var group in groups)
            {
                foreach (var (a, b, c) in group)
                {
                    p.Text(1, 9, 72, row, a);
                    p.Text(1, 9, 250, row, b);
                    p.Text(1, 9, 400, row, c);
                    row -= 12;
                }
                p.Rule(72, 523, row + 8);
                row -= 8;
            }

            // A footer that sits well above the edge of the page, repeated on every page
            return p;
        }

        private static Page FourthPage()
        {
            var p = new Page();
            Furniture(p, 4);
            p.Text(1, 10, 72, 270, "Net income 30");
            p.Text(2, 10, 72, 803, "Layout Annual Report - 4");

            p.Text(2, 10, 72, 792, "3. Overview");
            p.Text(2, 14, 72, 770, "Appendix");

            // A bullet right after an ordered item starts its own list
            p.Text(1, 10, 72, 740, "1. First point");
            p.Text(1, 10, 72, 716, "-");
            p.Text(1, 10, 82, 716, "A bullet that follows an ordered item.");

            // A rule under every row, with labels that wrap over three lines and the figures on the first
            p.Text(2, 9, 72, 680, "Item");
            p.Text(2, 9, 400, 680, "2024");
            p.Text(2, 9, 470, 680, "2023");
            var edge = 672.0;
            p.Rule(72, 523, edge);
            var rows = new[] { ("Receivables from group", "companies due within one", "year of the balance sheet", "119 502", "170 299"), ("Payables to suppliers", "and other creditors due", "after more than one year", "88 120", "91 004"), ("Provisions for risks", "and charges, including", "pensions and similar duties", "4 310", "3 998"), ("Deferred income", "and accrued expenses", "of the financial year", "1 238", "372") };
            foreach (var (a, b, c, x, y) in rows)
            {
                p.Text(1, 9, 72, edge - 11, a);
                p.Text(1, 9, 400, edge - 11, x);
                p.Text(1, 9, 470, edge - 11, y);
                p.Text(1, 9, 72, edge - 22, b);
                p.Text(1, 9, 72, edge - 33, c);
                edge -= 44;
                p.Rule(72, 523, edge);
            }

            // Two rows of complete sentences side by side are a table, not two lines of two columns of text
            p.Text(1, 9, 72, 440, "The first cell holds a complete sentence of text.");
            p.Text(1, 9, 340, 440, "The second cell also holds a complete sentence.");
            p.Text(1, 9, 72, 428, "Another sentence fills the third cell right here.");
            p.Text(1, 9, 340, 428, "A last sentence fills the fourth cell right here.");
            return p;
        }

        // --- The file ---

        private static byte[] Write(List<Page> pages)
        {
            var objects = new List<string>();
            int Add(string body)
            {
                objects.Add(body);
                return objects.Count;
            }
            var catalog = Add("");
            var pageTree = Add("");
            var fonts = new[] { "Helvetica", "Helvetica-Bold", "Courier", "Times-Roman" }
                .Select(name => Add($"<< /Type /Font /Subtype /Type1 /BaseFont /{name} /Encoding /WinAnsiEncoding >>")).ToArray();
            var pageIds = new List<int>();
            foreach (var page in pages)
            {
                var annots = new List<int>();
                foreach (var (l, b, r, t, uri) in page.Links)
                    annots.Add(Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Link /Rect [{l} {b} {r} {t}] /Border [0 0 0] /A << /S /URI /URI ({Escape(uri)}) >> >>")));
                var content = Add($"<< /Length {Encoding.ASCII.GetByteCount(page.Content.ToString())} >>\nstream\n{page.Content}endstream");
                var annotsText = annots.Count > 0 ? $" /Annots [{string.Join(" ", annots.Select(a => $"{a} 0 R"))}]" : "";
                pageIds.Add(Add($"<< /Type /Page /Parent {pageTree} 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fonts[0]} 0 R /F2 {fonts[1]} 0 R /F3 {fonts[2]} 0 R /F4 {fonts[3]} 0 R >> >> /Contents {content} 0 R{annotsText} >>"));
            }
            objects[catalog - 1] = $"<< /Type /Catalog /Pages {pageTree} 0 R >>";
            objects[pageTree - 1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(i => $"{i} 0 R"))}] /Count {pageIds.Count} >>";

            var pdf = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }
            var xref = pdf.Length;
            pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
            pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(pdf.ToString());
        }
    }
}
