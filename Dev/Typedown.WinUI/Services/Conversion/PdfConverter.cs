using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Typedown.WinUI.Services.Conversion
{
    // .pdf → Markdown from the text layer. A PDF has no paragraphs, headings, lists or tables — only
    // positioned glyphs — so structure is rebuilt from layout:
    //   - letters → words by the gap between them (PdfPig's own splitter merges "As large" in justified text),
    //     keeping only the text that runs the way the page does: a margin stamp such as arXiv's vertical
    //     "arXiv:2609.36139v1 [cs.CL]" is not part of the document;
    //   - glyphs that touch ("+78", ".", "0" in three fonts) are one word again, and glyphs a font reports with no
    //     height (maths italics, a decimal point) get one from their size so they stay on their line;
    //   - words → lines by vertical overlap (a bullet glyph or superscript stays on its own line);
    //   - tables drawn with rules (the usual LaTeX and Word tables) are found between the rules and split into
    //     columns by the vertical gaps every row shares; spanning headings label the columns under them;
    //   - two-column pages are read column by column when there is a clear gutter;
    //   - a new paragraph starts at a font-size change, a larger gap, or a list marker;
    //   - headings come from font size relative to the body text (lines that are bold from end to end are minor
    //     headings; a bold phrase that runs into ordinary text stays in its paragraph, in bold);
    //   - rows of cells separated by wide gaps and aligned across lines become a table, rules or not;
    //   - links to web addresses are kept (a link broken over two lines is one link);
    //   - lines in a monospaced font are code: a fenced block with the indentation of the page (and `code` for a monospaced word
    //     inside a sentence); a contents page ("Title ........ 12") becomes a list;
    //   - running headers/footers and page numbers are dropped; "exam-\nple" is rejoined, "narrative-\nchanging"
    //     keeps its hyphen when the document writes the word that way elsewhere.
    // Scanned PDFs have no text layer (there is no OCR) and are reported as such.
    internal static class PdfConverter
    {
        private sealed class WordBox
        {
            public string Text;
            public double Left, Right, Top, Bottom, Size;
            public bool Bold;
            public bool Mono;
            // The advance of one letter, which in monospaced type is the width of a character.
            public double Advance;
            public string Uri;
            public double Center => (Top + Bottom) / 2;
            public double Height => Math.Max(Top - Bottom, 0.1);
        }

        private sealed class Line
        {
            public int Page;
            public List<WordBox> Words = new();
            public double Top => Words.Max(w => w.Top);
            public double Bottom => Words.Min(w => w.Bottom);
            public double Left => Words.Min(w => w.Left);
            public double Size => Median(Words.SelectMany(w => Enumerable.Repeat(w.Size, Math.Max(1, w.Text.Length))));
            public bool Bold => BoldFraction > 0.5;
            // Share of the letters that are bold: a heading is bold from end to end.
            public double BoldFraction
            {
                get
                {
                    var total = Words.Sum(w => w.Text.Length);
                    return total == 0 ? 0 : Words.Sum(w => w.Bold ? w.Text.Length : 0) / (double)total;
                }
            }
            // Share of the letters set in a monospaced font.
            public double MonoFraction
            {
                get
                {
                    var total = Words.Sum(w => w.Text.Length);
                    return total == 0 ? 0 : Words.Sum(w => w.Mono ? w.Text.Length : 0) / (double)total;
                }
            }
            public bool IsEdge;
            public double RelY = -1; // the baseline as a fraction of the page height: a line that repeats at the same place on page after page is a running header or footer
            public string Text => string.Join(" ", Words.Select(w => w.Text));
            // A table found between rules: the Markdown is ready, Words holds one empty box where it sits.
            public string TableMarkdown;
        }

        private sealed class LinkBox
        {
            public double Left, Right, Top, Bottom;
            public string Uri;
        }

        private sealed class Rule
        {
            public double Left, Right, Y;
        }

        private static readonly Regex NumberedMarker = new(@"^\(?\d{1,3}[.)]$", RegexOptions.Compiled);
        private static readonly Regex ContentsEntry = new(@"^(.*?\S)\s*(?:\.\s*){3,}\s*(\d{1,4}|[ivxlcdm]{1,6})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Words in the document, for the hyphen at the end of a line: written with a hyphen elsewhere ("narrative-changing")
        // or as one word ("example")?
        private sealed class Vocabulary
        {
            public readonly Dictionary<string, int> Words = new();
            public readonly HashSet<string> Hyphenated = new();

            public void Add(string text)
            {
                foreach (var token in Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{M}'’-]+"))
                {
                    var t = token.Trim('-', '\'', '’');
                    if (t.Length < 2) continue;
                    if (t.Contains('-')) Hyphenated.Add(t);
                    else Words[t] = Words.GetValueOrDefault(t) + 1;
                }
            }
        }

        public static string Convert(Stream stream, ConversionContext context)
        {
            using var pdf = PdfDocument.Open(stream);
            var lines = new List<Line>();
            var vocabulary = new Vocabulary();
            foreach (var page in pdf.GetPages())
            {
                var pageLines = ReadPage(page);
                foreach (var line in pageLines)
                    foreach (var word in line.Words) vocabulary.Add(word.Text);
                lines.AddRange(pageLines);
            }
            if (lines.Count == 0 || lines.All(l => l.TableMarkdown == null && l.Words.All(w => w.Text.Length == 0)))
            {
                context.Warnings.Add(ConversionWarning.PdfHasNoText);
                return "";
            }

            var bodySize = Mode(lines.Where(l => !l.IsEdge).SelectMany(l => l.Words).SelectMany(w => Enumerable.Repeat(Math.Round(w.Size, 1), w.Text.Length)));
            // A document typed entirely in a monospaced font is text, not code.
            var allLetters = lines.Where(l => l.TableMarkdown == null).Sum(l => l.Words.Sum(w => w.Text.Length));
            var monospacedDocument = allLetters > 0 && lines.Where(l => l.TableMarkdown == null).Sum(l => l.Words.Sum(w => w.Mono ? w.Text.Length : 0)) > allLetters * 0.6;
            var pageCount = lines.Max(l => l.Page);
            var repeated = RepeatedEdgeText(lines, pageCount, bodySize);
            lines = lines.Where(l => !(IsRepeated(l, repeated, bodySize) || (l.IsEdge && Regex.IsMatch(l.Text, @"^(page\s*)?\d+(\s*(/|of|sur|de)\s*\d+)?$", RegexOptions.IgnoreCase)))).ToList();

            var lineStep = Median(lines.Zip(lines.Skip(1), (a, b) => (a, b))
                .Where(p => p.a.TableMarkdown == null && p.b.TableMarkdown == null && p.a.Page == p.b.Page && Math.Abs(p.a.Size - bodySize) < 0.6 && Math.Abs(p.b.Size - bodySize) < 0.6 && p.a.Bottom > p.b.Bottom)
                .Select(p => p.a.Bottom - p.b.Bottom).Where(d => d > 0 && d < bodySize * 3).DefaultIfEmpty(bodySize * 1.2));

            // Heading levels by rank of font size: the largest heading size is "#", the next "##"...
            // (so a document title and its section headings don't both become "#").
            var headingSizes = lines.Where(l => l.TableMarkdown == null && l.Size / bodySize >= 1.12).Select(l => Math.Round(l.Size * 2) / 2)
                .Distinct().OrderByDescending(s => s).ToList();
            int HeadingRank(double size) => Math.Clamp(headingSizes.IndexOf(Math.Round(size * 2) / 2) + 1, 1, 3);

            var blocks = new List<string>();
            var paragraph = new StringBuilder();
            string paragraphKind = null; // "p", "h" (by size), "h3" (bold line), "li"
            double paragraphSize = 0;
            Line previous = null;
            string openLink = null; // the web address whose text carried on from the line before
            double? listBase = null; // the left edge of the bullet list being read (nesting is measured from it, not from the page)

            void Flush()
            {
                if (paragraph.Length == 0) { paragraphKind = null; return; }
                // List items keep their leading indentation (nesting).
                var text = paragraphKind == "li" ? paragraph.ToString().TrimEnd() : paragraph.ToString().Trim();
                // A long run of sentences set a little larger or in bold (a notice at the top of a paper) is text, not a heading.
                if ((paragraphKind == "h" || paragraphKind == "h3") && text.Length > 120 && text.EndsWith('.')) paragraphKind = "p";
                blocks.Add(paragraphKind switch
                {
                    "h" => new string('#', HeadingRank(paragraphSize)) + " " + Plain(text),
                    "h3" => new string('#', NumberedLevel(text)) + " " + Plain(text),
                    "hc" => new string('#', NumberedLevel(text)) + " " + Plain(text),
                    "li" => text, // already formatted
                    _ => MarkdownText.EscapeBlockStart(text),
                });
                paragraph.Clear();
                paragraphKind = null;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // A table found between rules.
                if (line.TableMarkdown != null)
                {
                    Flush();
                    blocks.Add(line.TableMarkdown);
                    previous = null;
                    openLink = null;
                    continue;
                }

                // A contents line ("Title ........ 12").
                var contents = ContentsEntry.Match(line.Text);
                if (contents.Success && line.Words.Count > 2)
                {
                    Flush();
                    openLink = null;
                    blocks.Add("- " + Plain(contents.Groups[1].Value) + " … " + contents.Groups[2].Value);
                    previous = line;
                    continue;
                }

                // Code: lines in a monospaced font, with the indentation they have on the page.
                if (!monospacedDocument && !line.IsEdge && line.MonoFraction >= 0.8)
                {
                    var end = i;
                    // The code goes on through the next line if that is monospaced too, even across a page or column break.
                    while (end + 1 < lines.Count && lines[end + 1].TableMarkdown == null && lines[end + 1].MonoFraction >= 0.4
                        && (lines[end + 1].Page != lines[end].Page || lines[end + 1].Bottom >= lines[end].Bottom || lines[end].Bottom - lines[end + 1].Top < lineStep * 3)) end++;
                    Flush();
                    openLink = null;
                    blocks.Add(MarkdownText.CodeBlock(CodeText(lines.GetRange(i, end - i + 1))));
                    i = end;
                    previous = null;
                    continue;
                }

                // A table without rules: two or more consecutive lines split into the same number of aligned cells.
                var tableRows = TryTable(lines, i, out var consumed);
                if (tableRows != null)
                {
                    Flush();
                    if (AreTextColumns(tableRows, lines.GetRange(i, consumed)))
                    {
                        // Not a table: two columns of running text (a reference list) that were not cut apart. Each is read down.
                        for (var column = 0; column < tableRows[0].Count; column++)
                        {
                            var text = new StringBuilder();
                            foreach (var row in tableRows)
                            {
                                if (text.Length == 0) text.Append(Plain(row[column].Text));
                                else JoinLine(text, Plain(row[column].Text), vocabulary);
                            }
                            blocks.Add(MarkdownText.EscapeBlockStart(text.ToString().Trim()));
                        }
                    }
                    else
                        blocks.Add(MarkdownText.Table(tableRows.Select(r => (IReadOnlyList<string>)r.Select(c => MarkdownText.EscapeInline(c.Text)).ToList()).ToList()));
                    i += consumed - 1;
                    previous = null;
                    openLink = null;
                    continue;
                }

                var ratio = line.Size / bodySize;
                var kind = ratio >= 1.12 ? "h" : "p";
                // A bold line is a heading unless it is the start of a longer bold phrase that goes on below (it ends in a hyphen,
                // or the next line is at the normal spacing and starts in bold too).
                var next = i + 1 < lines.Count ? lines[i + 1] : null;
                // (A numbered line with a bold paragraph opening below it is a heading, unless that next line is the next list item.)
                var nextIsMarker = next != null && (NumberedMarker.IsMatch(next.Words[0].Text) || IsBulletMarker(next.Words[0]));
                var goesOn = line.Text.EndsWith('-') || (next != null && next.TableMarkdown == null && next.Page == line.Page && !(HeadingNumber.IsMatch(line.Text) && !nextIsMarker)
                    && next.Words[0].Bold && line.Bottom - next.Top < lineStep * 1.35 && next.Bottom < line.Bottom);
                if (kind == "p" && line.BoldFraction >= 0.9 && line.Text.Length < 90 && !line.Text.EndsWith('.') && !IsBulletMarker(line.Words[0]) && !goesOn
                    && (previous == null || !previous.Bold || previous.Page != line.Page || SubsectionNumber.IsMatch(line.Text)))
                    kind = "h3";

                // A heading set in capitals or small capitals at the size of the text ("1 INTRODUCTION"): short, alone on its lines.
                if (kind == "p" && IsCapitalsHeading(line, previous, lineStep, next)) kind = "hc";

                var first = line.Words[0];
                var isBullet = IsBulletMarker(first) && line.Words.Count > 1;
                var isNumbered = NumberedMarker.IsMatch(first.Text) && line.Words.Count > 1 && kind == "p";
                // A flow break: a new page, or a jump back up to the top of the next column.
                var flowBreak = previous != null && (previous.Page != line.Page || line.Bottom > previous.Bottom);
                var gap = previous != null && !flowBreak ? previous.Bottom - line.Bottom : double.MaxValue;
                // A body paragraph that runs over a page or column break: the last line had no closing
                // punctuation and this one starts in lower case.
                var continuesOverPage = flowBreak && kind == "p" && (paragraphKind == "p" || paragraphKind == "li")
                    && !Regex.IsMatch(paragraph.ToString().TrimEnd(), @"[.!?:;»""”)]$") && char.IsLower(line.Text[0]);
                var newParagraph = previous == null || paragraphKind == null
                    || isBullet || isNumbered || (kind == "p" && SubsectionNumber.IsMatch(line.Text))
                    || kind != (paragraphKind == "li" ? "p" : paragraphKind)
                    || Math.Abs(line.Size - previous.Size) > bodySize * 0.12
                    || (gap > lineStep * Math.Max(1, line.Size / bodySize) * 1.45 && !continuesOverPage);

                if (newParagraph)
                {
                    var continuesList = paragraphKind == "li";
                    Flush();
                    openLink = null;
                    if (isBullet || isNumbered)
                    {
                        if (isBullet && (!continuesList || listBase == null || first.Left < listBase - 4)) listBase = first.Left;
                        if (!isBullet) listBase = null; // the first bullet after an ordered item starts its own list
                        var level = isBullet ? (int)Math.Clamp(Math.Round((first.Left - listBase.Value) / 18.0), 0, 3) : 0;
                        var marker = isBullet ? "-" : Regex.Match(first.Text, @"\d+").Value + (first.Text.EndsWith(')') ? ")" : ".");
                        paragraph.Append(new string(' ', 2 * level)).Append(marker).Append(' ').Append(Markup(line.Words.Skip(1).ToList(), ref openLink, runIn: false));
                        paragraphKind = "li";
                    }
                    else if (kind == "p")
                    {
                        paragraph.Append(Markup(line.Words, ref openLink, runIn: true));
                        paragraphKind = kind;
                        paragraphSize = line.Size;
                    }
                    else
                    {
                        paragraph.Append(line.Text); // headings are escaped as a whole when flushed
                        paragraphKind = kind;
                        paragraphSize = line.Size;
                    }
                }
                else
                {
                    var text = paragraphKind == "li" || paragraphKind == "p" ? Markup(line.Words, ref openLink, runIn: false) : line.Text;
                    JoinLine(paragraph, text, vocabulary);
                }
                previous = line;
            }
            Flush();

            var sb = new StringBuilder();
            for (var i = 0; i < blocks.Count; i++)
            {
                if (i > 0) sb.Append(IsListItem(blocks[i - 1]) && IsListItem(blocks[i]) ? "\n" : "\n\n");
                sb.Append(blocks[i]);
            }
            return sb.ToString();
        }

        // The text of lines of code: each word where the page has it, in the width of one character (monospaced type), so
        // the indentation and the alignment inside a line survive. The indentation is measured from the left edge of the
        // code on its page or column.
        private static string CodeText(List<Line> codeLines)
        {
            var advances = codeLines.SelectMany(l => l.Words).Where(w => w.Mono && w.Advance > 0.5).Select(w => w.Advance).ToList();
            var charWidth = advances.Count > 0 ? Median(advances) : codeLines[0].Size * 0.6;
            // Where the code starts again at the top of the next page or column, a new left edge.
            var margins = new double[codeLines.Count];
            for (var start = 0; start < codeLines.Count;)
            {
                var end = start;
                while (end + 1 < codeLines.Count && codeLines[end + 1].Page == codeLines[end].Page && codeLines[end + 1].Bottom < codeLines[end].Bottom) end++;
                var margin = codeLines.Skip(start).Take(end - start + 1).Min(l => l.Left);
                for (var k = start; k <= end; k++) margins[k] = margin;
                start = end + 1;
            }
            var sb = new StringBuilder();
            for (var n = 0; n < codeLines.Count; n++)
            {
                var line = codeLines[n];
                // A blank line where the page has a gap of more than a line.
                if (n > 0 && codeLines[n - 1].Page == line.Page && codeLines[n - 1].Bottom - line.Top > codeLines[n - 1].Size * 1.2 && codeLines[n - 1].Bottom > line.Bottom) sb.Append('\n');
                var text = new StringBuilder();
                var column = 0;
                foreach (var word in line.Words)
                {
                    var target = (int)Math.Round((word.Left - margins[n]) / charWidth);
                    if (text.Length > 0) target = Math.Max(target, column + 1);
                    text.Append(' ', Math.Max(0, target - column)).Append(word.Text);
                    column = target + word.Text.Length;
                }
                sb.Append(text.ToString().TrimEnd());
                if (n + 1 < codeLines.Count) sb.Append('\n');
            }
            return sb.ToString();
        }

        // A bold or capitals heading's level from its number: "1 Introduction" is ##, "3.1 Encoder" ###, "3.2.1 Setup" ####;
        // one without a number ("Abstract", "References") is ##.
        private static int NumberedLevel(string text)
        {
            var number = Regex.Match(text, @"^(\d+(?:\.\d+)*)\.?\s+\p{L}");
            if (!number.Success) return 2;
            return Math.Clamp(number.Groups[1].Value.Split('.').Length + 1, 2, 4);
        }

        // "1 Introduction", "3.1 Encoder": a numbered heading ends with its line even when a paragraph opens in bold right below.
        private static readonly Regex HeadingNumber = new(@"^\d+(\.\d+)*\.?\s+\p{Lu}", RegexOptions.Compiled);

        private static bool IsCapitalsHeading(Line line, Line previous, double lineStep, Line next)
        {
            var text = line.Text;
            var letters = text.Count(char.IsLetter);
            if (letters < 4 || text.Length > 80 || text.EndsWith('.') || text.EndsWith(':') || text.EndsWith(',') || IsBulletMarker(line.Words[0])) return false;
            if (text.Count(char.IsUpper) < letters * 0.9) return false;
            if (line.Words.Count > 10 || line.Words.Any(w => w.Mono)) return false;
            var spaceBefore = previous == null || previous.Page != line.Page || previous.Bottom < line.Bottom || previous.Bottom - line.Bottom > lineStep * 1.25;
            var spaceAfter = next == null || next.TableMarkdown != null || next.Page != line.Page || line.Bottom - next.Bottom > lineStep * 1.15;
            return spaceBefore && spaceAfter;
        }

        // "3.1 Encoder", "6.2.1 Setup": a subsection right under its section's heading is a heading too, not a second line of it.
        private static readonly Regex SubsectionNumber = new(@"^\d+(\.\d+)+\.?\s+\p{Lu}", RegexOptions.Compiled);

        private static bool IsListItem(string block) => Regex.IsMatch(block, @"^\s*(- |\d+[.)] )");

        // Adds the next line to the paragraph. A hyphen at the end of the paragraph so far is the end of a word cut
        // across lines: "exam-" + "ple" is "example", but "narrative-" + "changing" is one hyphenated word when the
        // document writes it that way elsewhere, and "long-" + "horizon" keeps its hyphen when both halves are words
        // of their own.
        private static void JoinLine(StringBuilder paragraph, string text, Vocabulary vocabulary)
        {
            if (text.Length > 0 && paragraph.Length > 1 && paragraph[^1] == '-' && char.IsLetter(paragraph[^2]) && char.IsLetter(text[0]))
            {
                var before = LastWord(paragraph.ToString(0, paragraph.Length - 1)).ToLowerInvariant();
                var match = Regex.Match(text, @"^[\p{L}\p{M}'’-]+");
                var after = match.Value.Trim('-', '\'', '’').ToLowerInvariant();
                if (char.IsUpper(text[0]) || KeepsHyphen(before, after, vocabulary))
                {
                    paragraph.Append(text); // "narrative-changing": the hyphen stays, no space
                    return;
                }
                paragraph.Length--; // "exam-ple"
                paragraph.Append(text);
                return;
            }
            paragraph.Append(' ').Append(text);
        }

        private static bool KeepsHyphen(string before, string after, Vocabulary vocabulary)
        {
            if (before.Length == 0 || after.Length == 0) return false;
            if (vocabulary.Hyphenated.Contains(before + "-" + after)) return true;
            if (vocabulary.Words.ContainsKey(before + after)) return false;
            // Neither form appears whole elsewhere: two words of their own make a compound.
            return before.Length >= 3 && after.Length >= 3 && vocabulary.Words.GetValueOrDefault(before) >= 2 && vocabulary.Words.GetValueOrDefault(after) >= 2;
        }

        private static string LastWord(string text)
        {
            var match = Regex.Match(text, @"[\p{L}\p{M}'’-]+$");
            return match.Success ? match.Value.Trim('-', '\'', '’') : "";
        }

        // The words of one line as Markdown: links kept, and a bold phrase at the start of a paragraph that runs into
        // ordinary text kept as bold (a "run-in heading").
        private static string Markup(IReadOnlyList<WordBox> words, ref string openLink, bool runIn)
        {
            var boldPrefix = 0;
            if (runIn)
            {
                while (boldPrefix < words.Count && words[boldPrefix].Bold) boldPrefix++;
                if (boldPrefix == words.Count || boldPrefix == 0 || !char.IsLetterOrDigit(words[0].Text[0])) boldPrefix = 0;
            }
            var sb = new StringBuilder();
            for (var i = 0; i < words.Count;)
            {
                var uri = words[i].Uri;
                if (uri == null && words[i].Mono && !(boldPrefix > 0 && i == 0))
                {
                    openLink = null;
                    var code = new List<string>();
                    while (i < words.Count && words[i].Mono && words[i].Uri == null) code.Add(words[i++].Text);
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                    sb.Append(MarkdownText.CodeSpan(string.Join(" ", code)));
                    continue;
                }
                if (uri == null)
                {
                    openLink = null;
                    if (boldPrefix > 0 && i == 0)
                    {
                        sb.Append("**").Append(Plain(string.Join(" ", words.Take(boldPrefix).Select(w => w.Text)))).Append("** ");
                        i = boldPrefix;
                        continue;
                    }
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                    sb.Append(Plain(words[i].Text));
                    i++;
                    continue;
                }
                var run = new List<WordBox>();
                if (i > 0 && openLink == uri) openLink = null; // only a run at the start of a line carries on from the line before
                while (i < words.Count && words[i].Uri == uri) run.Add(words[i++]);
                if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                sb.Append(LinkMarkup(run, uri, ref openLink));
            }
            return sb.ToString().TrimEnd();
        }

        // A web address in the text is written as the address (a link that wrapped onto the next line carries on
        // from the line before and is written once); any other link text is a Markdown link.
        private static string LinkMarkup(List<WordBox> run, string uri, ref string openLink)
        {
            var text = string.Join("", run.Select(w => w.Text));
            var looksLikeAddress = Regex.IsMatch(text, @"^(https?://|www\.|ftp://|mailto:)", RegexOptions.IgnoreCase) || (openLink == uri);
            if (!looksLikeAddress)
            {
                openLink = null;
                // The full stop, comma or bracket the link rectangle happens to cover belongs to the sentence.
                var label = string.Join(" ", run.Select(w => w.Text));
                var before = Regex.Match(label, @"^[(\[“""‘']+").Value;
                var after = Regex.Match(label.Substring(before.Length), @"[.,;:!?)\]”""’']+$").Value;
                var core = label.Substring(before.Length, label.Length - before.Length - after.Length);
                if (core.Length == 0) return Plain(label);
                return Plain(before) + "[" + Plain(core) + "](" + EscapeUri(uri) + ")" + Plain(after);
            }
            // Punctuation after the address ("…system-card,") belongs to the sentence, not to the link.
            var suffix = Regex.Match(text, @"[.,;:!?)\]]+$").Value;
            if (uri.EndsWith(suffix, StringComparison.Ordinal) && suffix.Length > 0) suffix = "";
            if (openLink == uri) return Plain(suffix);
            openLink = uri;
            return "<" + EscapeUri(uri, parentheses: false) + ">" + Plain(suffix);
        }

        // What would end or break the Markdown around an address (a space, a bracket, a control character) is written as %XX.
        private static string EscapeUri(string uri, bool parentheses = true) =>
            Regex.Replace(uri, parentheses ? @"[\s<>()\\\x00-\x1F]" : @"[\s<>\\\x00-\x1F]",
                m => string.Concat(Encoding.UTF8.GetBytes(m.Value).Select(b => "%" + b.ToString("X2"))));

        private static string Plain(string text) => MarkdownText.EscapeInline(Regex.Replace(text, @"\s+", " ").Trim());

        // --- A page ---

        private static List<Line> ReadPage(Page page)
        {
            var words = ReadWords(page);
            if (words.Count == 0) return new List<Line>();
            AttachLinks(page, words);

            // Tables drawn with rules first: their words leave the text, and a line that holds the finished table
            // takes their place.
            var tableLines = new List<Line>();
            foreach (var table in FindRuledTables(page, words))
            {
                foreach (var word in table.Words) words.Remove(word);
                tableLines.Add(table.Line);
            }
            return PageLines(words, tableLines, page.Number, page.Width, page.Height).ToList();
        }

        // Letters → words. Only the text that runs the way most of the page does (a margin stamp is rotated), and a word
        // splits wherever the gap between two letters is as wide as a space: PdfPig's own extractor merged "As large"
        // when the justified space was only slightly over its limit.
        private static List<WordBox> ReadWords(Page page)
        {
            var letters = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
            if (letters.Count == 0) return new List<WordBox>();
            var orientation = letters.GroupBy(l => l.TextOrientation).OrderByDescending(g => g.Count()).First().Key;
            var kept = page.Letters.Where(l => l.TextOrientation == orientation).ToList();
            var result = new List<WordBox>();
            foreach (var word in NearestNeighbourWordExtractor.Instance.GetWords(kept))
            {
                if (string.IsNullOrWhiteSpace(word.Text) || word.Letters.Count == 0) continue;
                foreach (var part in SplitAtSpaces(word.Letters)) result.Add(part);
            }
            return result;
        }

        private static IEnumerable<WordBox> SplitAtSpaces(IReadOnlyList<Letter> letters)
        {
            var ordered = letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)).OrderBy(l => l.StartBaseLine.X).ToList();
            if (ordered.Count == 0) yield break;
            var group = new List<Letter> { ordered[0] };
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                var size = Math.Max(Math.Max(previous.PointSize, ordered[i].PointSize), 1);
                // Measured from where one letter's advance ends to where the next begins (not between the drawn shapes,
                // which leave room around a narrow "1"); a justified space can be as small as 0.2 em.
                if (ordered[i].StartBaseLine.X - previous.EndBaseLine.X > size * 0.12)
                {
                    yield return Box(group);
                    group = new List<Letter>();
                }
                group.Add(ordered[i]);
            }
            yield return Box(group);
        }

        private static WordBox Box(List<Letter> letters)
        {
            var size = Median(letters.Select(l => l.PointSize));
            if (size <= 0) size = 10;
            var bottom = letters.Min(l => l.GlyphRectangle.Bottom);
            var top = letters.Max(l => l.GlyphRectangle.Top);
            // Some fonts (maths italics, a decimal point) report a glyph with no height: size it from the type size, on the
            // baseline, so it overlaps the line it stands in.
            if (top - bottom < size * 0.3) top = bottom + size * 0.72;
            return new WordBox
            {
                Text = Unligature(string.Concat(letters.Select(l => l.Value)).Trim()),
                Left = letters.Min(l => l.GlyphRectangle.Left),
                Right = letters.Max(l => l.GlyphRectangle.Right),
                Top = top,
                Bottom = bottom,
                Size = size,
                Bold = letters.Count(l => IsBoldFont(l.FontName)) * 2 > letters.Count,
                Mono = letters.Count(l => IsMonoFont(l.FontName)) * 2 > letters.Count,
                Advance = Median(letters.Select(l => l.EndBaseLine.X - l.StartBaseLine.X)),
            };
        }

        // Links to web addresses (and e-mail): the words they cover carry the address. Links inside the document
        // (a citation jumping to the reference list) are not kept.
        private static void AttachLinks(Page page, List<WordBox> words)
        {
            List<LinkBox> links;
            try
            {
                links = page.GetAnnotations()
                    .Where(a => a.Action is UriAction { Uri: { Length: > 0 } u } && Regex.IsMatch(u, @"^(https?|ftp)://|^mailto:", RegexOptions.IgnoreCase))
                    .Select(a => new LinkBox
                    {
                        Left = a.Rectangle.Left,
                        Right = a.Rectangle.Right,
                        Top = a.Rectangle.Top,
                        Bottom = a.Rectangle.Bottom,
                        Uri = ((UriAction)a.Action).Uri,
                    }).ToList();
            }
            catch
            {
                return; // damaged annotations: the text is still worth having
            }
            if (links.Count == 0) return;
            foreach (var word in words)
            {
                var cx = (word.Left + word.Right) / 2;
                var cy = word.Center;
                var link = links.FirstOrDefault(l => cx >= l.Left && cx <= l.Right && cy >= Math.Min(l.Bottom, l.Top) && cy <= Math.Max(l.Bottom, l.Top));
                if (link != null) word.Uri = link.Uri;
            }
        }

        // Words of one page → lines in reading order. The page is cut into blocks wherever there is empty space (the
        // classic "XY cut"): a full-width gap between a title, the columns of text, a figure and its caption; a vertical gap
        // between columns when both sides are running text. Each block is read top to bottom, blocks in order, so a page
        // with columns above a diagram, or three columns of different lengths, is read the way a person reads it.
        private static IEnumerable<Line> PageLines(List<WordBox> words, List<Line> tableLines, int pageNumber, double width, double height)
        {
            // A finished table takes part as a box the size of the table, so the blocks around it are cut correctly.
            var tables = new Dictionary<WordBox, Line>();
            foreach (var table in tableLines)
            {
                table.Page = pageNumber;
                tables[table.Words[0]] = table;
            }
            // The strips at the top and bottom of the page (running header, footer, page number) are lines of their own: cut into
            // columns they would come out in pieces that differ from page to page, and no longer be recognised as repeated.
            bool InEdge(WordBox w) => w.Center > height * 0.93 || w.Center < height * 0.07;
            var edgeWords = words.Where(InEdge).ToList();
            var bodyWords = words.Where(w => !InEdge(w)).ToList();
            var all = new List<WordBox>(bodyWords);
            all.AddRange(tables.Keys);
            var bodySize = Mode(bodyWords.SelectMany(w => Enumerable.Repeat(Math.Round(w.Size, 1), w.Text.Length)));
            if (bodySize <= 0) bodySize = 10;
            var blocks = new List<List<WordBox>>();
            Cut(all, blocks, bodySize, 0);

            var ordered = new List<Line>();
            var edgeLines = GroupLines(edgeWords, pageNumber);
            foreach (var line in edgeLines) { line.IsEdge = true; line.RelY = line.Bottom / height; }
            ordered.AddRange(edgeLines.Where(l => l.Top > height * 0.5));
            foreach (var block in blocks)
            {
                var lines = BlockLines(block.Where(w => !tables.ContainsKey(w)).ToList(), pageNumber, width, height);
                var blockTables = block.Where(w => tables.ContainsKey(w)).Select(w => tables[w]).ToList();
                if (blockTables.Count == 0) { ordered.AddRange(lines); continue; }
                // Tables sit among the block's lines where they are on the page.
                var merged = lines.Concat(blockTables).OrderByDescending(l => l.Top).ToList();
                ordered.AddRange(merged);
            }
            ordered.AddRange(edgeLines.Where(l => l.Top <= height * 0.5));
            return ordered;
        }

        // Cuts a set of words into blocks: by a vertical gap when the two sides are columns of text, else by a large
        // horizontal gap; what cannot be cut further is a block.
        private static void Cut(List<WordBox> words, List<List<WordBox>> blocks, double bodySize, int depth)
        {
            if (words.Count < 12 || depth > 10)
            {
                blocks.Add(words);
                return;
            }
            var x = FindVerticalCut(words, bodySize);
            if (x != null)
            {
                Cut(words.Where(w => (w.Left + w.Right) / 2 < x.Value).ToList(), blocks, bodySize, depth + 1);
                Cut(words.Where(w => (w.Left + w.Right) / 2 >= x.Value).ToList(), blocks, bodySize, depth + 1);
                return;
            }
            var y = FindHorizontalCut(words, bodySize);
            if (y != null)
            {
                Cut(words.Where(w => w.Center >= y.Value).ToList(), blocks, bodySize, depth + 1);
                Cut(words.Where(w => w.Center < y.Value).ToList(), blocks, bodySize, depth + 1);
                return;
            }
            blocks.Add(words);
        }

        // The widest empty vertical strip, if the text on both sides is made of lines of words (columns), not cells.
        private static double? FindVerticalCut(List<WordBox> words, double bodySize)
        {
            // Columns can be close together (a journal's are often 6 points apart); a strip that stays empty down a whole block
            // of running text is not a coincidence of word gaps.
            var minGap = Math.Max(4.0, bodySize * 0.4);
            var gaps = EmptyStrips(words.Select(w => (w.Left, w.Right)), minGap);
            foreach (var (a, b) in gaps.OrderByDescending(g => g.B - g.A))
            {
                var x = (a + b) / 2;
                var left = words.Where(w => (w.Left + w.Right) / 2 < x).ToList();
                var right = words.Where(w => (w.Left + w.Right) / 2 >= x).ToList();
                if (left.Count < 6 || right.Count < 6) continue;
                if (IsRunningText(left) && IsRunningText(right)) return x;
            }
            return null;
        }

        // A large empty horizontal strip: between a title and the text, a text and a figure, a figure and its caption.
        private static double? FindHorizontalCut(List<WordBox> words, double bodySize)
        {
            var minGap = Math.Max(8.0, bodySize * 1.1);
            var gaps = EmptyStrips(words.Where(w => w.Text.Length > 0 || w.Size == 0).Select(w => (w.Bottom, w.Top)), minGap);
            var best = gaps.OrderByDescending(g => g.B - g.A).Select(g => ((double, double)?)g).FirstOrDefault();
            if (best == null) return null;
            var y = (best.Value.Item1 + best.Value.Item2) / 2;
            return words.Count(w => w.Center >= y) >= 3 && words.Count(w => w.Center < y) >= 3 ? y : null;
        }

        // The gaps (at least minGap wide) between the intervals, which are merged where they overlap.
        private static List<(double A, double B)> EmptyStrips(IEnumerable<(double From, double To)> intervals, double minGap)
        {
            var sorted = intervals.Select(i => (From: Math.Min(i.From, i.To), To: Math.Max(i.From, i.To))).OrderBy(i => i.From).ToList();
            var gaps = new List<(double, double)>();
            if (sorted.Count == 0) return gaps;
            var end = sorted[0].To;
            foreach (var interval in sorted.Skip(1))
            {
                if (interval.From - end >= minGap) gaps.Add((end, interval.From));
                end = Math.Max(end, interval.To);
            }
            return gaps;
        }

        // Lines of several words each, as in prose; a table's cells are short.
        private static bool IsRunningText(List<WordBox> words)
        {
            var lines = GroupLines(words.Select(Clone).ToList(), 0);
            if (lines.Count < 3) return false;
            var perLine = lines.Select(l => l.Words.Count).OrderBy(c => c).ToList();
            // Words, not figures: a column of amounts ("119 502 170 299") has several "words" per line too.
            var wordLike = words.Count(w => w.Text.Count(char.IsLetter) >= Math.Max(2, w.Text.Length / 2));
            return perLine[perLine.Count / 2] >= 4 && wordLike >= words.Count * 0.7;
        }

        // The lines of one block, with the page's old handling of a title across two columns kept for blocks that hold both.
        private static List<Line> BlockLines(List<WordBox> words, int pageNumber, double width, double height)
        {
            if (words.Count == 0) return new List<Line>();
            var left = words.Min(w => w.Left);
            var gutter = FindGutter(words, left, words.Max(w => w.Right) - left);
            var all = GroupLines(words, pageNumber);
            foreach (var line in all)
            {
                line.IsEdge = line.Top > height * 0.93 || line.Bottom < height * 0.07;
                line.RelY = line.Bottom / height;
            }
            if (gutter == null)
                return all;

            var g = gutter.Value;
            var ordered = new List<Line>();
            var leftLines = new List<Line>();
            var rightLines = new List<Line>();
            void FlushColumns()
            {
                ordered.AddRange(leftLines);
                ordered.AddRange(rightLines);
                leftLines.Clear();
                rightLines.Clear();
            }
            foreach (var line in all)
            {
                if (line.Words.Any(w => w.Left < g && w.Right > g))
                {
                    FlushColumns();
                    ordered.Add(line);
                    continue;
                }
                var l = line.Words.Where(w => w.Right <= g).ToList();
                var r = line.Words.Where(w => w.Left >= g).ToList();
                if (l.Count > 0) leftLines.Add(new Line { Page = pageNumber, Words = l, IsEdge = line.IsEdge, RelY = line.RelY });
                if (r.Count > 0) rightLines.Add(new Line { Page = pageNumber, Words = r, IsEdge = line.IsEdge, RelY = line.RelY });
            }
            FlushColumns();
            return ordered;
        }

        private static List<Line> GroupLines(List<WordBox> words, int pageNumber)
        {
            // Each line is matched against its "core" word — the largest one so far — rather than the
            // first: a small superscript (a footnote number) would otherwise anchor the line and push
            // short words like "non" onto a line of their own.
            var lines = new List<(WordBox Core, Line Line)>();
            foreach (var word in words.OrderByDescending(w => w.Size).ThenByDescending(w => w.Center))
            {
                var index = lines.FindIndex(x =>
                {
                    var overlap = Math.Min(x.Core.Top, word.Top) - Math.Max(x.Core.Bottom, word.Bottom);
                    return overlap >= 0.5 * Math.Min(x.Core.Height, word.Height);
                });
                if (index >= 0) lines[index].Line.Words.Add(word);
                else lines.Add((word, new Line { Page = pageNumber, Words = { word } }));
            }
            foreach (var (_, line) in lines)
            {
                line.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
                line.Words = MergeTouching(line.Words);
            }
            return lines.Select(x => x.Line).OrderByDescending(l => l.Top).ToList();
        }

        // Pieces that touch with no space between them are one word: a number set in three fonts ("+78" "." "0"),
        // a word and the comma after it, a figure and its footnote mark.
        private static List<WordBox> MergeTouching(List<WordBox> sorted)
        {
            var merged = new List<WordBox>();
            foreach (var word in sorted)
            {
                var last = merged.Count > 0 ? merged[^1] : null;
                var gap = last == null ? double.MaxValue : word.Left - last.Right;
                // Only a short piece (a decimal point, a comma, a sign, a footnote mark) joins its neighbour: two whole words that
                // merely sit close (tight type, a narrow headline) are still two words.
                if (last != null && gap <= Math.Max(last.Size, word.Size) * 0.05 && gap >= -Math.Max(last.Size, word.Size) * 0.4 && last.Uri == word.Uri
                    && (word.Text.Length <= 2 || last.Text.Length <= 2) && !(IsBulletMarker(last) && last.Text.Length == 1))
                {
                    var lastLength = last.Text.Length;
                    last.Text += word.Text;
                    last.Right = Math.Max(last.Right, word.Right);
                    last.Top = Math.Max(last.Top, word.Top);
                    last.Bottom = Math.Min(last.Bottom, word.Bottom);
                    last.Bold = (last.Bold ? lastLength : 0) + (word.Bold ? word.Text.Length : 0) > (lastLength + word.Text.Length) / 2.0;
                    last.Size = Math.Max(last.Size, word.Size);
                    last.Mono = last.Mono && word.Mono;
                    continue;
                }
                merged.Add(word);
            }
            return merged;
        }

        // A vertical strip in the middle of a block that almost no word crosses, with plenty of text
        // on both sides (a title across the top is the exception that lines are kept in place for).
        private static double? FindGutter(List<WordBox> words, double regionLeft, double width)
        {
            if (words.Count < 40) return null;
            double? best = null;
            var bestCrossing = int.MaxValue;
            for (var x = regionLeft + width * 0.35; x <= regionLeft + width * 0.65; x += 2)
            {
                var crossing = words.Count(w => w.Left < x && w.Right > x);
                if (crossing < bestCrossing) { bestCrossing = crossing; best = x; }
            }
            if (best == null || bestCrossing > words.Count * 0.03) return null;
            var g = best.Value;
            var leftCount = words.Count(w => w.Right <= g);
            var rightCount = words.Count(w => w.Left >= g);
            if (leftCount < words.Count * 0.25 || rightCount < words.Count * 0.25) return null;
            // A real gutter is empty for a while on both sides, not just a lucky line between words.
            var nearGutter = words.Count(w => w.Right > g - 6 && w.Left < g + 6);
            return nearGutter <= words.Count * 0.03 ? g : null;
        }

        // --- Tables drawn with rules ---

        private sealed class RuledTable
        {
            public Line Line;
            public List<WordBox> Words;
        }

        // The horizontal rules of the page; the text between rules that holds columns is a table. A booktabs table has a
        // rule above and below the heading and one under the last row (and short rules under spanning headings).
        private static IEnumerable<RuledTable> FindRuledTables(Page page, List<WordBox> words)
        {
            var rules = new List<Rule>();
            try
            {
                if (page.Paths.Count > 4000) return Array.Empty<RuledTable>(); // a chart or a map, not a table
                foreach (var path in page.Paths)
                {
                    var box = path.GetBoundingRectangle();
                    if (box == null || box.Value.Height > 1.6 || box.Value.Width < 18) continue;
                    rules.Add(new Rule { Left = box.Value.Left, Right = box.Value.Right, Y = (box.Value.Top + box.Value.Bottom) / 2 });
                }
            }
            catch
            {
                return Array.Empty<RuledTable>();
            }
            rules = JoinRules(rules);
            var wide = rules.Where(r => r.Right - r.Left >= page.Width * 0.25).OrderByDescending(r => r.Y).ToList();
            if (wide.Count < 2) return Array.Empty<RuledTable>();

            var found = new List<RuledTable>();
            var used = new HashSet<Rule>();
            var i = 0;
            while (i < wide.Count - 1)
            {
                // The rules that line up with this one, one below the other, make up a candidate.
                var chain = new List<Rule> { wide[i] };
                for (var j = i + 1; j < wide.Count; j++)
                {
                    if (Math.Abs(wide[j].Left - wide[i].Left) <= 6 && Math.Abs(wide[j].Right - wide[i].Right) <= 6 && chain[^1].Y - wide[j].Y <= page.Height)
                        chain.Add(wide[j]);
                }
                // From a rule down to the farthest rule below it such that everything between holds columns: a booktabs table (top
                // rule, heading, rule, rows, rule) or a table with a rule under every row. Then on from there.
                // Running text between two rules (a paragraph above a table, a caption between two tables) is not part of a table,
                // and no table reaches across it.
                var prose = Enumerable.Range(0, chain.Count - 1).Select(m => HoldsProse(BetweenRules(words, chain[m].Y, chain[m + 1].Y, chain[m].Left, chain[m].Right))).ToArray();
                var k = 0;
                while (k + 1 < chain.Count)
                {
                    if (prose[k])
                    {
                        k++;
                        continue;
                    }
                    RuledTable table = null;
                    var end = -1;
                    var reach = k + 1;
                    while (reach < chain.Count - 1 && !prose[reach]) reach++;
                    for (var j = Math.Min(reach, k + 80); j > k && table == null; j--)
                    {
                        var between = BetweenRules(words, chain[k].Y, chain[j].Y, chain[k].Left, chain[k].Right);
                        if (!LooksTabular(between, j - k)) continue;
                        table = BuildRuledTable(words, rules, chain[k], chain[j]);
                        if (table != null) end = j;
                    }
                    if (table != null)
                    {
                        found.Add(table);
                        k = end;
                    }
                    else k++;
                }
                // Continue with the next rule that no chain has used yet (a table beside this one has rules of its own).
                foreach (var r in chain) used.Add(r);
                i = wide.FindIndex(i + 1, r => !used.Contains(r));
                if (i < 0) break;
            }
            // A word belongs to one table only.
            var taken = new HashSet<WordBox>();
            return found.Where(t => t.Words.All(w => taken.Add(w))).ToList();
        }

        // Rules drawn as one segment per column, side by side on the same line, are one rule.
        private static List<Rule> JoinRules(List<Rule> rules)
        {
            var joined = new List<Rule>();
            foreach (var rule in rules.OrderBy(r => Math.Round(r.Y / 1.5)).ThenBy(r => r.Left))
            {
                var last = joined.Count > 0 ? joined[^1] : null;
                if (last != null && Math.Abs(last.Y - rule.Y) <= 1.5 && rule.Left - last.Right <= 3)
                    last.Right = Math.Max(last.Right, rule.Right);
                else
                    joined.Add(new Rule { Left = rule.Left, Right = rule.Right, Y = rule.Y });
            }
            return joined;
        }

        private static List<WordBox> BetweenRules(List<WordBox> words, double top, double bottom, double left, double right) =>
            words.Where(w => w.Center < top && w.Center > bottom && w.Left >= left - 3 && w.Right <= right + 3).ToList();

        // Lines whose words fall into separate groups (gaps wider than a space) – columns – rather than running prose.
        private static bool LooksTabular(List<WordBox> inside, int intervals = 1)
        {
            if (inside.Count < 4) return false;
            var rows = GroupLines(inside.Select(Clone).ToList(), 0);
            if (rows.Count < 1) return false;
            var tabular = rows.Count(r => r.Words.Count >= 3 && CellCount(r) >= 3);
            // A table with a rule under every row has label-only rows between the rows that hold figures.
            return tabular >= Math.Max(1, rows.Count * (intervals >= 3 ? 0.3 : 0.6));
        }

        // A line of several words with no gap wider than a space: a sentence, not table cells.
        // A caption ("Table 2: ...") between two tables is prose too, even when its first word is left of the rules.
        private static readonly Regex CaptionStart = new(@"^((Table|Figure|Fig\.?|Tableau|Tabla|Tabelle|Abbildung)\s*\d+[:.]|\d+:)\s*\p{Lu}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool HoldsProse(List<WordBox> inside)
        {
            if (inside.Count < 4) return false;
            var lines = GroupLines(inside.Select(Clone).ToList(), 0);
            return lines.Any(l => l.Words.Count >= 4 && CaptionStart.IsMatch(l.Text))
                || (inside.Count >= 7 && lines.Any(l => l.Words.Count >= 7 && CellCount(l) <= 1));
        }

        private static WordBox Clone(WordBox w) => new() { Text = w.Text, Left = w.Left, Right = w.Right, Top = w.Top, Bottom = w.Bottom, Size = w.Size, Bold = w.Bold, Mono = w.Mono, Advance = w.Advance, Uri = w.Uri };

        private static int CellCount(Line line)
        {
            var limit = Math.Max(3.5, line.Size * 0.5);
            var cells = 1;
            for (var i = 1; i < line.Words.Count; i++)
                if (line.Words[i].Left - line.Words[i - 1].Right > limit) cells++;
            return cells;
        }

        // Rules between groups of rows (the sections of a results table, the lines of a booktabs table) are not a rule under every
        // row: that needs the bands between rules to be single rows, not several lines each (a superscript or a wrapped cell
        // does not make a second row).
        private static bool BandsAreRows(List<WordBox> words, Rule top, Rule bottom, List<Rule> fullInner)
        {
            var edges = new List<double> { top.Y };
            edges.AddRange(fullInner.Select(r => r.Y).OrderByDescending(y => y));
            edges.Add(bottom.Y);
            var bands = 0;
            var tall = 0;
            for (var i = 0; i + 1 < edges.Count; i++)
            {
                var band = BetweenRules(words, edges[i], edges[i + 1], top.Left, top.Right);
                if (band.Count == 0) continue;
                var size = Median(band.Select(w => w.Size));
                var bandLines = GroupLines(band.Where(w => w.Size >= size * 0.85).Select(Clone).ToList(), 0);
                var lines = bandLines.Count;
                // Several lines that each hold two cells or more are several rows; a cell wrapped over lines leaves most of them with one.
                var lineCells = bandLines.Count(l => CellCount(l) >= 2);
                bands++;
                if (lines >= 3 && lineCells >= lines * 0.6) tall++;
            }
            return bands > 0 && tall * 3 < bands;
        }

        private static RuledTable BuildRuledTable(List<WordBox> words, List<Rule> rules, Rule top, Rule bottom)
        {
            var inside = BetweenRules(words, top.Y + 0.5, bottom.Y - 0.5, top.Left, top.Right);
            if (inside.Count < 4) return null;
            var rows = GroupLines(inside.Select(Clone).ToList(), 0).OrderByDescending(l => l.Top).ToList();
            var size = Median(inside.Select(w => w.Size));
            // Rules inside the table (not its own top and bottom): the one under the heading, short ones under spanning headings.
            var inner = rules.Where(r => r.Y < top.Y - 0.5 && r.Y > bottom.Y + 0.5 && r.Left >= top.Left - 3 && r.Right <= top.Right + 3).OrderByDescending(r => r.Y).ToList();
            var fullInner = inner.Where(r => r.Right - r.Left >= (top.Right - top.Left) * 0.85).ToList();
            var headingEnd = fullInner.Count > 0 ? fullInner[0].Y : (double?)null;
            var shortRules = inner.Where(r => r.Right - r.Left < (top.Right - top.Left) * 0.85).ToList();
            // A rule under every row: each row is the band between two rules.
            if (fullInner.Count >= 3 && BandsAreRows(words, top, bottom, fullInner)) return BuildSeparatedTable(words, top, bottom, inner);

            // The columns come from the rows of data, whose gaps are wide: a heading is often wider than its numbers
            // (and a footnote mark such as "†" can sit in the narrow gap between two headings).
            var dataRows = headingEnd == null ? rows : rows.Where(r => r.Top < headingEnd.Value).ToList();
            if (dataRows.Count < 2) dataRows = rows;
            // A row that is one phrase ("Published", "Top Leaderboard Systems") labels a group of rows and says nothing about columns.
            List<Line> WithCells(List<Line> r)
            {
                var many = r.Where(x => CellCount(x) >= 2).ToList();
                return many.Count >= 3 ? many : r;
            }
            var columns = FindColumns(WithCells(dataRows), top.Left, top.Right, size);
            if (columns.Count < 1) return null; // boundaries between columns: one fewer than columns
            // A label centred on its group of rows ("Conceal Negative Results") sits between the rows and blurs them, so once the
            // first column is known, the others are found again from the rows of everything to its right.
            if (columns.Count >= 2)
            {
                var edge = columns[0];
                var rightOfLabels = (headingEnd == null ? inside : inside.Where(w => w.Center < headingEnd.Value).ToList())
                    .Where(w => (w.Left + w.Right) / 2 > edge).Select(Clone).ToList();
                var valueRowsOnly = GroupLines(rightOfLabels, 0).OrderByDescending(l => l.Top).ToList();
                if (valueRowsOnly.Count >= 3)
                {
                    var refined = FindColumns(WithCells(valueRowsOnly), edge, top.Right, size);
                    if (refined.Count >= 1)
                    {
                        columns = new List<double> { edge };
                        columns.AddRange(refined);
                    }
                }
            }

            int ColumnOf(double x)
            {
                var c = 0;
                while (c < columns.Count && x > columns[c]) c++;
                return c;
            }
            var count = columns.Count + 1;
            // Where each column's data sits: a heading is centred over its numbers and can be wider than them, so a heading
            // word goes to the column whose data is nearest, not to whichever side of a boundary it falls on.
            var dataLeft = Enumerable.Repeat(double.MaxValue, count).ToArray();
            var dataRight = Enumerable.Repeat(double.MinValue, count).ToArray();
            foreach (var row in WithCells(dataRows))
                foreach (var word in row.Words)
                {
                    var c = ColumnOf((word.Left + word.Right) / 2);
                    dataLeft[c] = Math.Min(dataLeft[c], word.Left);
                    dataRight[c] = Math.Max(dataRight[c], word.Right);
                }
            int NearestColumn(WordBox word)
            {
                var centre = (word.Left + word.Right) / 2;
                var best = ColumnOf(centre);
                var bestDistance = double.MaxValue;
                for (var c = 0; c < count; c++)
                {
                    if (dataLeft[c] > dataRight[c]) continue;
                    var distance = Math.Abs(centre - (dataLeft[c] + dataRight[c]) / 2);
                    if (centre >= dataLeft[c] && centre <= dataRight[c]) distance = 0;
                    if (distance < bestDistance) { bestDistance = distance; best = c; }
                }
                return best;
            }

            var header = new List<string[]>();
            var spans = new List<(int From, int To, string Label)>();
            var body = new List<string[]>();

            // The heading: the words above the rule under it.
            var headingWords = headingEnd == null ? new List<WordBox>() : inside.Where(w => w.Center >= headingEnd.Value).ToList();
            var bodyWords = headingEnd == null ? inside : inside.Where(w => w.Center < headingEnd.Value).ToList();
            foreach (var row in GroupLines(headingWords.Select(Clone).ToList(), 0).OrderByDescending(l => l.Top))
            {
                var cells = new string[count];
                var parts = Enumerable.Range(0, count).Select(_ => new List<string>()).ToList();
                foreach (var phrase in Phrases(row.Words, row.Size)) parts[NearestColumn(phrase)].Add(phrase.Text);
                for (var c = 0; c < count; c++) cells[c] = string.Join(" ", parts[c]);
                // A heading over several columns has a short rule right under it that says which ones.
                var under = shortRules.Where(r => r.Y < row.Bottom + 0.5 && r.Y > row.Bottom - row.Size * 2.5).OrderBy(r => r.Left).ToList();
                if (under.Count > 0)
                {
                    var rest = new List<WordBox>(row.Words);
                    foreach (var rule in under)
                    {
                        var labelWords = row.Words.Where(w => (w.Left + w.Right) / 2 >= rule.Left - 2 && (w.Left + w.Right) / 2 <= rule.Right + 2).ToList();
                        if (labelWords.Count == 0) continue;
                        foreach (var w in labelWords) rest.Remove(w);
                        var covered = Enumerable.Range(0, count).Where(c => dataLeft[c] <= dataRight[c] && (dataLeft[c] + dataRight[c]) / 2 >= rule.Left - 2 && (dataLeft[c] + dataRight[c]) / 2 <= rule.Right + 2).ToList();
                        if (covered.Count == 0) covered.Add(ColumnOf((rule.Left + rule.Right) / 2));
                        spans.Add((covered.Min(), covered.Max(), string.Join(" ", labelWords.Select(w => w.Text))));
                    }
                    if (rest.Count == 0) continue;
                    parts = Enumerable.Range(0, count).Select(_ => new List<string>()).ToList();
                    foreach (var phrase in Phrases(rest, row.Size)) parts[NearestColumn(phrase)].Add(phrase.Text);
                    for (var c = 0; c < count; c++) cells[c] = string.Join(" ", parts[c]);
                }
                header.Add(cells);
            }

            // The rows. A label in the first column that is centred on its group of rows ("Conceal Negative Results" beside
            // four models) sits between the rows, not on them, so the rows are made from the other columns and the label goes
            // to the first row of its group.
            var labelColumnWords = bodyWords.Where(w => ColumnOf((w.Left + w.Right) / 2) == 0).ToList();
            var valueRows = GroupLines(bodyWords.Where(w => ColumnOf((w.Left + w.Right) / 2) != 0).Select(Clone).ToList(), 0).OrderByDescending(l => l.Top).ToList();
            var fragments = GroupLines(labelColumnWords.Select(Clone).ToList(), 0).OrderByDescending(l => l.Top).ToList();
            // Rows are compared by their baselines (the middle of a row moves with its tallest letters).
            double Centre(Line l) => Median(l.Words.Select(w => w.Bottom));
            var pitch = valueRows.Count > 1 ? Median(valueRows.Zip(valueRows.Skip(1), (x, y) => Centre(x) - Centre(y)).Where(d => d > 0)) : 0;
            var centred = fragments.Count > 0 && valueRows.Count >= 3 && pitch > 0
                && fragments.Count(f => valueRows.Min(r => Math.Abs(Centre(r) - Centre(f))) <= pitch * 0.3) < fragments.Count * 0.7;
            if (centred)
            {
                // Groups of rows: where the step from one row to the next is larger than usual.
                var firstOfGroup = new List<int> { 0 };
                for (var r = 1; r < valueRows.Count; r++)
                    if (Centre(valueRows[r - 1]) - Centre(valueRows[r]) > pitch * 1.12) firstOfGroup.Add(r);
                // Labels: neighbouring fragments are one label.
                var labels = new List<(double Centre, string Text)>();
                var run = new List<Line>();
                void EndLabel()
                {
                    if (run.Count == 0) return;
                    labels.Add((run.Average(Centre), string.Join(" ", run.Select(l => l.Text))));
                    run.Clear();
                }
                foreach (var fragment in fragments)
                {
                    if (run.Count > 0 && Centre(run[^1]) - Centre(fragment) > pitch * 1.5) EndLabel();
                    run.Add(fragment);
                }
                EndLabel();
                var labelOfRow = new string[valueRows.Count];
                foreach (var (centre, text) in labels)
                {
                    var nearest = Enumerable.Range(0, valueRows.Count).OrderBy(r => Math.Abs(Centre(valueRows[r]) - centre)).First();
                    var group = firstOfGroup.Where(g => g <= nearest).Max();
                    labelOfRow[group] = (labelOfRow[group] + " " + text).Trim();
                }
                // Every row of a group carries its label, so a row read on its own still says what it belongs to.
                string groupLabel = "";
                for (var r = 0; r < valueRows.Count; r++)
                {
                    if (labelOfRow[r] != null) groupLabel = labelOfRow[r];
                    var cells = new string[count];
                    var parts = Enumerable.Range(0, count).Select(_ => new List<string>()).ToList();
                    foreach (var word in valueRows[r].Words) parts[ColumnOf((word.Left + word.Right) / 2)].Add(word.Text);
                    for (var c = 0; c < count; c++) cells[c] = string.Join(" ", parts[c]);
                    cells[0] = groupLabel;
                    body.Add(cells);
                }
            }
            else
            {
                var bodyLines = new List<Line>();
                foreach (var row in GroupLines(bodyWords.Select(Clone).ToList(), 0).OrderByDescending(l => l.Top))
                {
                    var cells = new string[count];
                    var parts = Enumerable.Range(0, count).Select(_ => new List<string>()).ToList();
                    foreach (var word in row.Words) parts[ColumnOf((word.Left + word.Right) / 2)].Add(word.Text);
                    for (var c = 0; c < count; c++) cells[c] = string.Join(" ", parts[c]);
                    // A phrase set across several columns labels the rows below it: one cell, not pieces.
                    if (row.Words.Count >= 3 && CellCount(row) == 1 && ColumnOf((row.Words[0].Left + row.Words[0].Right) / 2) != ColumnOf((row.Words[^1].Left + row.Words[^1].Right) / 2))
                    {
                        for (var c = 0; c < count; c++) cells[c] = "";
                        cells[0] = string.Join(" ", row.Words.Select(w => w.Text));
                    }
                    if (body.Count > 0 && cells[0].Length == 0 && cells.Any(c => c.Length > 0) && bodyLines[^1].Bottom - row.Top < row.Size * 0.6)
                    {
                        // A cell that wraps onto a second line.
                        var previous = body[^1];
                        for (var c = 0; c < count; c++)
                            if (cells[c].Length > 0) previous[c] = (previous[c] + " " + cells[c]).Trim();
                        bodyLines[^1] = row;
                    }
                    else
                    {
                        body.Add(cells);
                        bodyLines.Add(row);
                    }
                }
            }
            if (body.Count == 0) return null;

            // One heading row: the lines of the heading joined column by column, each under its spanning label.
            var headingCells = new string[count];
            for (var c = 0; c < count; c++)
            {
                var label = string.Join(" ", spans.Where(s => c >= s.From && c <= s.To).Select(s => s.Label));
                var own = string.Join(" ", header.Select(h => h[c]).Where(t => t.Length > 0));
                headingCells[c] = (label + " " + own).Trim();
            }
            if (header.Count == 0 && spans.Count == 0)
            {
                headingCells = body[0];
                body.RemoveAt(0);
            }
            var rowsOut = new List<IReadOnlyList<string>> { headingCells.Select(MarkdownText.EscapeInline).ToList() };
            rowsOut.AddRange(body.Select(r => (IReadOnlyList<string>)r.Select(MarkdownText.EscapeInline).ToList()));
            var markdown = MarkdownText.Table(rowsOut);

            var line = new Line
            {
                TableMarkdown = markdown,
                Words = { new WordBox { Text = "", Left = top.Left, Right = top.Right, Top = top.Y, Bottom = bottom.Y, Size = 0 } },
            };
            return new RuledTable { Line = line, Words = words.Where(w => inside.Any(i => i.Text == w.Text && Math.Abs(i.Left - w.Left) < 0.01 && Math.Abs(i.Bottom - w.Bottom) < 0.01)).ToList() };
        }

        // A table with a rule under every row: each row is the words between two rules (so a figure set a little higher than
        // its label, or a cell that wraps, still lands in its row), and the line above the first rule is the heading.
        private static RuledTable BuildSeparatedTable(List<WordBox> words, Rule top, Rule bottom, List<Rule> inner)
        {
            // Every rule inside the table separates rows, including the short ones under a heading that spans columns.
            var edges = new List<double> { top.Y };
            // (A short rule is a piece of underlined text, such as a best score, not a border.)
            var tableWidth = top.Right - top.Left;
            foreach (var y in inner.Where(r => r.Right - r.Left >= Math.Max(40, tableWidth * 0.2)).Select(r => r.Y).OrderByDescending(y => y))
                if (edges[^1] - y > 1.5) edges.Add(y);
            if (edges[^1] - bottom.Y > 1.5) edges.Add(bottom.Y); else edges[^1] = bottom.Y;
            var pitch = Median(edges.Zip(edges.Skip(1), (a, b) => a - b));
            var bands = new List<List<WordBox>>();
            var rulesUnder = new List<double>(); // the y of the rule under each band
            var heading = BetweenRules(words, top.Y + pitch, top.Y + 0.5, top.Left, top.Right);
            // What stands above the first rule is the table's heading only if it is made of cells, not a sentence of the text
            // before the table.
            if (heading.Count > 0 && !GroupLines(heading.Select(Clone).ToList(), 0).Any(l => CellCount(l) >= 2)) heading = new List<WordBox>();
            if (heading.Count > 0)
            {
                bands.Add(heading);
                rulesUnder.Add(top.Y);
            }
            for (var i = 0; i + 1 < edges.Count; i++)
            {
                var band = BetweenRules(words, edges[i] - 0.3, edges[i + 1] + 0.3, top.Left, top.Right);
                if (band.Count == 0) continue;
                bands.Add(band);
                rulesUnder.Add(edges[i + 1]);
            }
            if (bands.Count < 3) return null;
            var rows = bands.Select(b => new Line { Words = b.Select(Clone).OrderBy(w => w.Left).ToList() }).ToList();
            var size = Median(bands.SelectMany(b => b).Select(w => w.Size));
            var columns = FindColumns(rows, top.Left, top.Right, size);
            if (columns.Count < 1) return null;
            int ColumnOf(double x)
            {
                var c = 0;
                while (c < columns.Count && x > columns[c]) c++;
                return c;
            }
            var count = columns.Count + 1;
            // Per row: each phrase (a heading over two columns stays in one piece) with the column its middle falls in.
            var bandPhrases = rows.Select(row => GroupLines(row.Words, 0).OrderByDescending(l => l.Top)
                .SelectMany(visual => Phrases(visual.Words, visual.Size).Select(phrase => (Phrase: phrase, Words: visual.Words.Where(w => w.Left >= phrase.Left - 0.01 && w.Right <= phrase.Right + 0.01).ToList())).ToList())
                .ToList()).ToList();
            string[] CellsOf(int band, IEnumerable<(WordBox Phrase, List<WordBox> Words)> only = null)
            {
                var cellWords = Enumerable.Range(0, count).Select(_ => new List<WordBox>()).ToList();
                foreach (var (phrase, wordsOf) in only ?? bandPhrases[band]) cellWords[ColumnOf((phrase.Left + phrase.Right) / 2)].AddRange(wordsOf);
                // A cell that wraps reads line by line.
                return cellWords.Select(c => string.Join(" ", GroupLines(c, 0).OrderByDescending(l => l.Top).Select(l => l.Text))).ToArray();
            }
            var table = Enumerable.Range(0, bands.Count).Select(band => CellsOf(band)).ToList();

            // A heading in two stages ("Attack success rate" over "Attempts | Scenarios"): the label goes over every column it spans,
            // and joins the second line of the heading.
            if (bands.Count >= 3 && table[0].Count(c => c.Length > 0) < table[1].Count(c => c.Length > 0) && !table[1].Any(c => c.Any(char.IsDigit)))
            {
                var dataLeft = Enumerable.Repeat(double.MaxValue, count).ToArray();
                var dataRight = Enumerable.Repeat(double.MinValue, count).ToArray();
                for (var band = 2; band < bandPhrases.Count; band++)
                    foreach (var (phrase, _) in bandPhrases[band])
                    {
                        var c = ColumnOf((phrase.Left + phrase.Right) / 2);
                        dataLeft[c] = Math.Min(dataLeft[c], phrase.Left);
                        dataRight[c] = Math.Max(dataRight[c], phrase.Right);
                    }
                var labels = new string[count];
                foreach (var (phrase, wordsOf) in bandPhrases[0])
                {
                    var covered = Enumerable.Range(0, count).Where(c => dataLeft[c] <= dataRight[c] && (dataLeft[c] + dataRight[c]) / 2 >= phrase.Left - 2 && (dataLeft[c] + dataRight[c]) / 2 <= phrase.Right + 2).ToList();
                    if (covered.Count < 2) covered = new List<int> { ColumnOf((phrase.Left + phrase.Right) / 2) };
                    foreach (var c in covered) labels[c] = ((labels[c] ?? "") + " " + phrase.Text).Trim();
                }
                var heading2 = new string[count];
                for (var c = 0; c < count; c++) heading2[c] = ((labels[c] ?? "") + " " + table[1][c]).Trim();
                table[1] = heading2;
                table.RemoveAt(0);
                rulesUnder.RemoveAt(0);
            }

            // A cell that spans two rows (a rule that stops short of its column, as a model's name beside two settings): its text is
            // whole, and each of the rows it spans says it.
            for (var b = 1; b + 1 < table.Count; b++)
            {
                for (var c = 0; c < count; c++)
                {
                    var middle = c == 0 ? (top.Left + columns[0]) / 2 : c == count - 1 ? (columns[^1] + top.Right) / 2 : (columns[c - 1] + columns[c]) / 2;
                    var covered = inner.Concat(new[] { top, bottom }).Any(r => Math.Abs(r.Y - rulesUnder[b]) <= 1.5 && r.Right - r.Left >= 20 && middle >= r.Left - 2 && middle <= r.Right + 2);
                    if (covered || (table[b][c].Length == 0 && table[b + 1][c].Length == 0)) continue;
                    var text = (table[b][c] + " " + table[b + 1][c]).Trim();
                    table[b][c] = text;
                    table[b + 1][c] = text;
                }
            }
            var rowsOut = table.Select(r => (IReadOnlyList<string>)r.Select(MarkdownText.EscapeInline).ToList()).ToList();
            var line = new Line
            {
                TableMarkdown = MarkdownText.Table(rowsOut),
                Words = { new WordBox { Text = "", Left = top.Left, Right = top.Right, Top = heading.Count > 0 ? top.Y + pitch : top.Y, Bottom = bottom.Y, Size = 0 } },
            };
            return new RuledTable { Line = line, Words = bands.SelectMany(b => b).Distinct().ToList() };
        }

        // The words of a heading that belong together (their gaps are those of a space, not of a column), as one box: a heading
        // such as "Gemini 3.1 Pro" goes to one column as a whole.
        private static List<WordBox> Phrases(IEnumerable<WordBox> words, double size)
        {
            var phrases = new List<WordBox>();
            foreach (var word in words.OrderBy(w => w.Left))
            {
                var last = phrases.Count > 0 ? phrases[^1] : null;
                if (last != null && word.Left - last.Right <= Math.Max(size * 0.4, 2.0))
                {
                    last.Text += " " + word.Text;
                    last.Right = Math.Max(last.Right, word.Right);
                }
                else phrases.Add(Clone(word));
            }
            return phrases;
        }

        // The vertical gaps that (nearly) every row leaves empty: the boundaries between columns, at the middle of each gap.
        private static List<double> FindColumns(List<Line> rows, double left, double right, double size)
        {
            var minGap = Math.Max(3.0, size * 0.4);
            var enough = Math.Max(1, (int)Math.Ceiling(rows.Count * 0.8));
            var votes = new List<int>();
            var start = left;
            var steps = (int)((right - left) / 0.5) + 1;
            for (var s = 0; s < steps; s++) votes.Add(0);
            foreach (var row in rows)
            {
                var gaps = new List<(double A, double B)>();
                var edge = left;
                foreach (var word in row.Words.OrderBy(w => w.Left))
                {
                    if (word.Left - edge >= minGap) gaps.Add((edge, word.Left));
                    edge = Math.Max(edge, word.Right);
                }
                if (right - edge >= minGap) gaps.Add((edge, right));
                foreach (var (a, b) in gaps)
                    for (var s = (int)Math.Max(0, Math.Ceiling((a - start) / 0.5)); s < steps && start + s * 0.5 <= b; s++) votes[s]++;
            }
            // Only gaps with text on both sides count: not the margins of the table.
            var firstText = rows.Min(r => r.Words.Min(w => w.Left));
            var lastText = rows.Max(r => r.Words.Max(w => w.Right));
            var boundaries = new List<double>();
            var runStart = -1;
            for (var s = 0; s <= steps; s++)
            {
                var ok = s < steps && votes[s] >= enough;
                if (ok && runStart < 0) runStart = s;
                if (!ok && runStart >= 0)
                {
                    // Within a stretch that nearly every row leaves free, the real gap is where the most rows do (a heading
                    // wider than its numbers reaches into the stretch, but only that one row does).
                    var most = Enumerable.Range(runStart, s - runStart).Max(k => votes[k]);
                    var bestStart = -1;
                    var bestLength = 0;
                    var currentStart = -1;
                    for (var k = runStart; k <= s; k++)
                    {
                        var top = k < s && votes[k] == most;
                        if (top && currentStart < 0) currentStart = k;
                        if (!top && currentStart >= 0)
                        {
                            if (k - currentStart > bestLength) { bestStart = currentStart; bestLength = k - currentStart; }
                            currentStart = -1;
                        }
                    }
                    var a = start + bestStart * 0.5;
                    var b = start + (bestStart + bestLength - 1) * 0.5;
                    if (bestStart >= 0 && b - a >= minGap * 0.8 && a > firstText + 1 && b < lastText - 1) boundaries.Add((a + b) / 2);
                    runStart = -1;
                }
            }
            return boundaries;
        }

        // --- Tables without rules ---

        // Rows of cells of aligned lines, or null when there are fewer than two rows.
        private static List<List<(double Left, string Text)>> TryTable(List<Line> lines, int start, out int consumed)
        {
            consumed = 0;
            var rows = new List<List<(double Left, string Text)>>();
            for (var i = start; i < lines.Count; i++)
            {
                if (lines[i].TableMarkdown != null) break;
                if (rows.Count > 0 && lines[i].Page != lines[i - 1].Page) break;
                var cells = Cells(lines[i]);
                if (cells.Count < 2) break;
                if (rows.Count > 0 && (cells.Count != rows[0].Count || !Aligned(cells, rows[0]))) break;
                rows.Add(cells);
            }
            if (rows.Count < 2) return null;
            consumed = rows.Count;
            return rows;
        }

        // Several lines whose cells are all sentences' worth of words, in every column: columns of text, not a table (a table's
        // cells are short, or at least one of its columns is a label).
        private static bool AreTextColumns(List<List<(double Left, string Text)>> rows, List<Line> sourceLines = null)
        {
            if (rows.Count < 2) return false;
            for (var column = 0; column < rows[0].Count; column++)
                if (rows.Average(r => r[column].Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length) < 5) return false;
            // Two lines are text columns only when the text of each column (but the last) fills its width on at least one of them,
            // up to the gutter: lines of a column run to its edge, while the cells of a table, even long ones, usually stop short of
            // the next cell (two sentences with room to spare beside them are a table of two rows).
            if (rows.Count == 2 && sourceLines != null && sourceLines.Count == 2)
                for (var column = 0; column + 1 < rows[0].Count; column++)
                {
                    var fills = false;
                    for (var r = 0; r < 2 && !fills; r++)
                    {
                        var left = rows[r][column].Left;
                        var next = rows[r][column + 1].Left;
                        var right = sourceLines[r].Words.Where(w => w.Left >= left - 0.5 && w.Left < next - 0.5).Select(w => w.Right).DefaultIfEmpty(left).Max();
                        fills = (right - left) >= 0.8 * (next - left);
                    }
                    if (!fills) return false;
                }
            return true;
        }

        // A line split into cells where the space between words is much wider than a normal space.
        private static List<(double Left, string Text)> Cells(Line line)
        {
            var cells = new List<(double Left, string Text)>();
            var gapLimit = Math.Max(10, line.Size * 1.8);
            var current = new List<WordBox> { line.Words[0] };
            for (var i = 1; i < line.Words.Count; i++)
            {
                if (line.Words[i].Left - line.Words[i - 1].Right > gapLimit)
                {
                    cells.Add((current[0].Left, string.Join(" ", current.Select(w => w.Text))));
                    current = new List<WordBox>();
                }
                current.Add(line.Words[i]);
            }
            cells.Add((current[0].Left, string.Join(" ", current.Select(w => w.Text))));
            return cells;
        }

        private static bool Aligned(List<(double Left, string Text)> a, List<(double Left, string Text)> b) =>
            a.Zip(b, (x, y) => Math.Abs(x.Left - y.Left) < 25).All(ok => ok);

        private static bool IsBulletMarker(WordBox word)
        {
            if (word.Text.Length != 1) return false;
            var c = word.Text[0];
            return "•●○◦▪▫■□‣⁃–-·*".Contains(c) || (c >= '' && c <= '') || (c == 'o' && word.Size <= 14 && word.Right - word.Left < word.Size);
        }

        // "ﬁ", "ﬂ"... (one glyph for two letters) as the letters they are: searchable, and the same as the word typed out.
        private static string Unligature(string text)
        {
            if (text.All(c => c < 'ﬀ' || c > 'ﬆ')) return text;
            return text.Replace("ﬀ", "ff").Replace("ﬁ", "fi").Replace("ﬂ", "fl").Replace("ﬃ", "ffi").Replace("ﬄ", "ffl").Replace("ﬅ", "st").Replace("ﬆ", "st");
        }

        private static bool IsMonoFont(string name) =>
            name != null && Regex.IsMatch(name, @"mono|courier|consolas|menlo|monaco|typewriter|inconsolata|cmtt|lucidaconsole|sourcecode|cousine|\bcode\b", RegexOptions.IgnoreCase);

        // "Bold", "Black", "Heavy", "Demi" and the Computer Modern / Nimbus names of a bold face (CMBX10, NimbusRomNo9L-Medi, Foo-Bd).
        private static bool IsBoldFont(string name) =>
            name != null && Regex.IsMatch(name, @"bold|black|heavy|demi|cmbx|cmssbx|[-,]medi$|[-,]bd(it)?$", RegexOptions.IgnoreCase);

        private static string Normalize(string text) => Regex.Replace(text, @"\d+", "#").Trim().ToLowerInvariant();

        // Text that stands at the same place on half the pages or more (a running header or footer, wherever the margin puts
        // it) as (text, place in 0.5 % of the page height). A footer's page number changes from page to page, so numbers are
        // blanked, except in a bold or larger line: "1. Overview", "2. Overview" at the same height on three pages are three
        // headings, not one running header.
        private static string RepeatKey(Line line, double bodySize)
        {
            var text = line.Text.Trim();
            // Only text in the outer part of the page can be a header or footer whose number changes from page to page: in the middle
            // of a page "Net income 10", "Net income 20", "Net income 30" at the same height are three different lines.
            var outer = line.IsEdge || line.RelY < 0.15 || line.RelY > 0.85;
            // A header that ends in its page number ("Annual Report - 12", "Draft | page 3") is the same header on every page;
            // other numbers in it are left alone.
            if (outer) text = TrailingPageNumber.Replace(text, "");
            return !outer || line.BoldFraction >= 0.5 || line.Size > bodySize * 1.1 ? text.ToLowerInvariant() : Normalize(text);
        }

        private static readonly Regex TrailingPageNumber = new(@"\s+(?:[—–|-]|page|p\.|pág\.?|seite)\s*\d+(?:\s*(?:/|of|de|sur)\s*\d+)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static HashSet<(string, int)> RepeatedEdgeText(List<Line> lines, int pageCount, double bodySize)
        {
            var repeated = new HashSet<(string, int)>();
            if (pageCount < 3) return repeated;
            var needed = Math.Max(3, (pageCount + 1) / 2);
            var pages = new Dictionary<(string, int), HashSet<int>>();
            foreach (var l in lines.Where(l => l.TableMarkdown == null && l.RelY >= 0 && l.Text.Length >= 3))
            {
                var key = (RepeatKey(l, bodySize), (int)Math.Round(l.RelY * 200));
                if (!pages.TryGetValue(key, out var set)) pages[key] = set = new HashSet<int>();
                set.Add(l.Page);
            }
            // The same text a little higher or lower on some pages is still the same footer: count the pages of the places next to each.
            foreach (var (key, _) in pages)
            {
                var near = new HashSet<int>();
                for (var d = -1; d <= 1; d++)
                    if (pages.TryGetValue((key.Item1, key.Item2 + d), out var set)) near.UnionWith(set);
                if (near.Count >= needed) repeated.Add(key);
            }
            return repeated;
        }

        private static bool IsRepeated(Line line, HashSet<(string, int)> repeated, double bodySize)
        {
            if (repeated.Count == 0 || line.TableMarkdown != null || line.RelY < 0) return false;
            var text = RepeatKey(line, bodySize);
            var place = (int)Math.Round(line.RelY * 200);
            return repeated.Contains((text, place)) || repeated.Contains((text, place - 1)) || repeated.Contains((text, place + 1));
        }

        private static double Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(v => v).ToList();
            return list.Count == 0 ? 0 : list[list.Count / 2];
        }

        private static double Mode(IEnumerable<double> values)
        {
            var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count()).ToList();
            return groups.Count == 0 ? 10 : groups[0].Key;
        }
    }
}
