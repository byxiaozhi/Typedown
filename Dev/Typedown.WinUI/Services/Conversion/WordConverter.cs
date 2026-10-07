using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Conversion
{
    // .docx → Markdown. Headings come from paragraph styles (Word stores built-in style names in
    // English — "heading 1" — whatever the UI language, so French and Spanish documents work the same)
    // or outline levels; lists from the numbering definitions; tables become pipe tables.
    internal static class WordConverter
    {
        private static readonly HashSet<string> MonospaceFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Consolas", "Courier New", "Courier", "Cascadia Code", "Cascadia Mono", "Lucida Console", "Menlo", "Monaco", "Source Code Pro", "Fira Code",
        };

        public static string Convert(Stream stream, ConversionContext context)
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var main = doc.MainDocumentPart;
            var body = main?.Document?.Body;
            if (body == null) return "";
            var state = new State(main, context);
            var blocks = new List<Block>();
            foreach (var element in BodyBlocks(body)) state.AddBlock(element, blocks);
            state.FlushCode(blocks);
            state.FlushDropCap(blocks);
            var sb = new StringBuilder();
            Block previous = null;
            foreach (var block in blocks)
            {
                if (previous != null) sb.Append(previous.IsListItem && block.IsListItem ? "\n" : "\n\n");
                sb.Append(block.Markdown);
                previous = block;
            }
            if (state.Footnotes.Count > 0)
            {
                sb.Append("\n\n");
                foreach (var (id, text) in state.Footnotes) sb.Append($"[^{id}]: {text}\n");
            }
            return sb.ToString();
        }

        // Body children in order, looking inside content controls (a whole document can sit in one).
        private static IEnumerable<OpenXmlElement> BodyBlocks(OpenXmlElement container)
        {
            foreach (var child in container.ChildElements)
            {
                if (child is W.SdtBlock sdt)
                {
                    var content = sdt.SdtContentBlock;
                    if (content != null) foreach (var inner in BodyBlocks(content)) yield return inner;
                }
                else if (child is W.CustomXmlBlock custom)
                {
                    foreach (var inner in BodyBlocks(custom)) yield return inner;
                }
                else yield return child;
            }
        }

        private sealed record Block(string Markdown, bool IsListItem, bool IsCode = false);

        private sealed class State
        {
            private readonly MainDocumentPart main;
            private readonly ConversionContext context;
            private readonly Dictionary<string, W.Style> styles;
            private readonly Dictionary<OpenXmlPart, Dictionary<string, string>> hyperlinks = new();
            // The part whose relationships the current text uses: the main document, or the footnotes
            // part while a footnote is read (its link and image IDs are its own, not the document's).
            private OpenXmlPart owner;
            private readonly List<string> codeLines = new();
            private string pendingDropCap;
            // The number each ordered list has reached, per numbering instance (numId) and level.
            private readonly Dictionary<int, int[]> counters = new();

            public List<(string Id, string Text)> Footnotes { get; } = new();

            public State(MainDocumentPart main, ConversionContext context)
            {
                this.main = main;
                this.context = context;
                styles = main.StyleDefinitionsPart?.Styles?.Elements<W.Style>()
                    .Where(s => s.StyleId?.Value != null)
                    .GroupBy(s => s.StyleId.Value).ToDictionary(g => g.Key, g => g.First())
                    ?? new Dictionary<string, W.Style>();
                owner = main;
            }

            public void AddBlock(OpenXmlElement element, List<Block> blocks)
            {
                if (element is W.Paragraph p)
                {
                    if (IsCodeParagraph(p))
                    {
                        FlushDropCap(blocks);
                        codeLines.Add(string.Concat(p.Descendants<W.Text>().Select(t => t.Text)));
                        return;
                    }
                    FlushCode(blocks);
                    // A drop cap is a paragraph of its own holding one letter; it belongs in front of the paragraph after it.
                    if (p.ParagraphProperties?.FrameProperties?.DropCap?.Value is W.DropCapLocationValues cap && cap != W.DropCapLocationValues.None)
                    {
                        var letter = string.Concat(p.Descendants<W.Text>().Select(t => t.Text)).Trim();
                        if (letter.Length is > 0 and <= 3) { FlushDropCap(blocks); pendingDropCap = letter; return; }
                    }
                    var dropCap = pendingDropCap;
                    pendingDropCap = null;
                    var block = Paragraph(p, dropCap);
                    // A drop cap with nothing after it to join (an empty paragraph, a contents entry) stands alone rather than moving on.
                    if (block == null && dropCap != null) block = new Block(MarkdownText.EscapeBlockStart(dropCap), false);
                    if (block != null) blocks.Add(block);
                }
                else if (element is W.Table table)
                {
                    FlushCode(blocks);
                    FlushDropCap(blocks);
                    var md = Table(table);
                    if (md.Length > 0) blocks.Add(new Block(md, false));
                }
            }

            public void FlushDropCap(List<Block> blocks)
            {
                if (pendingDropCap == null) return;
                blocks.Add(new Block(MarkdownText.EscapeBlockStart(pendingDropCap), false));
                pendingDropCap = null;
            }

            public void FlushCode(List<Block> blocks)
            {
                if (codeLines.Count == 0) return;
                blocks.Add(new Block(MarkdownText.CodeBlock(string.Join("\n", codeLines)), false, true));
                codeLines.Clear();
            }

            private Block Paragraph(W.Paragraph p, string dropCap = null)
            {
                var styleId = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                var styleName = StyleName(styleId);
                // Tables of contents are page-number lists Word generates; they only add noise.
                if (styleName.StartsWith("toc ", StringComparison.OrdinalIgnoreCase) || styleName.Equals("toc heading", StringComparison.OrdinalIgnoreCase))
                    return null;

                var heading = HeadingLevel(p, styleId);
                var inline = new InlineBuilder();
                AddInlines(p, inline, "  \n");
                if (inline.IsEmpty) return null;
                if (heading == 0 && Numbering(p, styleId) == null) heading = VisualHeadingLevel(p, styleId, inline.PlainText);

                if (heading > 0)
                {
                    var text = dropCap + inline.Build(plainHeading: true).Replace("  \n", " ").Trim();
                    return new Block(new string('#', heading) + " " + text, false);
                }

                var numbering = Numbering(p, styleId);
                if (numbering is (int numId, int numberingLevel))
                {
                    // "List Bullet 2", "List Number 3"… put an item one level deeper through the style
                    // itself, while their numbering still says level 0.
                    var styleLevel = System.Text.RegularExpressions.Regex.Match(styleName, @"^list (bullet|number|continue) (\d)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var level = Math.Max(numberingLevel, styleLevel.Success ? int.Parse(styleLevel.Groups[2].Value) - 1 : 0);
                    var marker = IsOrdered(numId, numberingLevel) ? NextNumber(numId, level) + "." : "-";
                    var indent = new string(' ', 4 * Math.Min(level, 8));
                    var text = dropCap + inline.Build().Trim().Replace("  \n", "  \n" + indent + "  ");
                    return new Block(indent + marker + " " + text, true);
                }

                var content = MarkdownText.EscapeBlockStart(dropCap + inline.Build().Trim());
                if (styleName.Equals("quote", StringComparison.OrdinalIgnoreCase) || styleName.Equals("intense quote", StringComparison.OrdinalIgnoreCase))
                    content = "> " + content.Replace("\n", "\n> ");
                return new Block(content, false);
            }

            private void AddInlines(OpenXmlElement container, InlineBuilder inline, string lineBreak, string link = null)
            {
                foreach (var child in container.ChildElements)
                {
                    switch (child)
                    {
                        case W.Run run:
                            AddRun(run, inline, lineBreak, link);
                            break;
                        case W.Hyperlink h:
                            var target = h.Id?.Value != null && Hyperlinks(owner).TryGetValue(h.Id.Value, out var uri) ? uri : null;
                            AddInlines(h, inline, lineBreak, target ?? link);
                            break;
                        case W.DeletedRun:
                        case W.ParagraphProperties:
                        case W.MoveFromRun:
                            break;
                        case W.SdtRun sdt when sdt.SdtContentRun != null:
                            AddInlines(sdt.SdtContentRun, inline, lineBreak, link);
                            break;
                        default:
                            if (child.HasChildren) AddInlines(child, inline, lineBreak, link);
                            break;
                    }
                }
            }

            private void AddRun(W.Run run, InlineBuilder inline, string lineBreak, string link)
            {
                var rp = run.RunProperties;
                if (IsOn(rp?.Vanish)) return; // hidden text
                var runStyle = rp?.RunStyle?.Val?.Value ?? "";
                var bold = IsOn(rp?.Bold) || runStyle.Contains("Strong", StringComparison.OrdinalIgnoreCase);
                var italic = IsOn(rp?.Italic) || runStyle.Contains("Emphasis", StringComparison.OrdinalIgnoreCase);
                var strike = IsOn(rp?.Strike) || IsOn(rp?.DoubleStrike);
                var code = MonospaceFonts.Contains(rp?.RunFonts?.Ascii?.Value ?? "") || runStyle.Contains("Code", StringComparison.OrdinalIgnoreCase);
                foreach (var part in run.ChildElements)
                {
                    switch (part)
                    {
                        case W.Text t:
                            if (rp?.VerticalTextAlignment?.Val?.Value is W.VerticalPositionValues position && position != W.VerticalPositionValues.Baseline && !code)
                                AddScript(inline, t.Text, position == W.VerticalPositionValues.Superscript, bold, italic, strike, link);
                            else
                                inline.Add(t.Text, bold, italic, strike, code, link);
                            break;
                        case W.TabChar:
                            inline.Add(" ", bold, italic, strike, code, link);
                            break;
                        case W.NoBreakHyphen:
                            inline.Add("-", bold, italic, strike, code, link);
                            break;
                        case W.Break br when br.Type?.Value != W.BreakValues.Page && br.Type?.Value != W.BreakValues.Column:
                            inline.AddRaw(lineBreak);
                            break;
                        case W.CarriageReturn:
                            inline.AddRaw(lineBreak);
                            break;
                        case W.Drawing drawing:
                            var picture = Image(drawing);
                            inline.AddRaw(picture.Length > 0 ? picture : TextBox(drawing));
                            break;
                        case W.FootnoteReference fn when fn.Id?.Value != null:
                            inline.AddRaw(Footnote(fn.Id.Value.ToString()));
                            break;
                        case W.EndnoteReference en when en.Id?.Value != null:
                            inline.AddRaw(Endnote(en.Id.Value.ToString()));
                            break;
                        case W.CommentReference cr when cr.Id?.Value != null:
                            inline.AddRaw(CommentNote(cr.Id.Value));
                            break;
                        case W.Picture:
                        case AlternateContent:
                            inline.AddRaw(TextBox(part));
                            break;
                    }
                }
            }

            private const string SuperscriptFrom = "0123456789+-=()ni";
            private const string SuperscriptTo = "\u2070\u00b9\u00b2\u00b3\u2074\u2075\u2076\u2077\u2078\u2079\u207a\u207b\u207c\u207d\u207e\u207f\u2071";
            private const string SubscriptFrom = "0123456789+-=()aehklmnopstx";
            private const string SubscriptTo = "\u2080\u2081\u2082\u2083\u2084\u2085\u2086\u2087\u2088\u2089\u208a\u208b\u208c\u208d\u208e\u2090\u2091\u2095\u2096\u2097\u2098\u2099\u2092\u209a\u209b\u209c\u2093";

            // "m2" set as a superscript is "m\u00b2" and "H2O" with a subscript "H\u2082O" where the characters exist; otherwise <sup>/<sub>.
            private static void AddScript(InlineBuilder inline, string text, bool superscript, bool bold, bool italic, bool strike, string link)
            {
                var from = superscript ? SuperscriptFrom : SubscriptFrom;
                var to = superscript ? SuperscriptTo : SubscriptTo;
                if (text.Length > 0 && text.All(c => from.Contains(c)))
                {
                    inline.Add(new string(text.Select(c => to[from.IndexOf(c)]).ToArray()), bold, italic, strike, false, link);
                    return;
                }
                var core = text.Trim();
                if (core.Length == 0)
                {
                    inline.Add(text, bold, italic, strike, false, link);
                    return;
                }
                var tag = superscript ? "sup" : "sub";
                // The spaces around the words stay outside the tags; the emphasis goes inside them, where "**" can open ("10**<sup>"
                // would not), and the link stays around it.
                inline.Add(text.Substring(0, text.Length - text.TrimStart().Length));
                var inner = MarkdownText.EscapeInline(core);
                if (strike) inner = "~~" + inner + "~~";
                if (italic) inner = "*" + inner + "*";
                if (bold) inner = "**" + inner + "**";
                inline.AddRaw("<" + tag + ">" + inner + "</" + tag + ">", link);
                inline.Add(text.Substring(text.TrimEnd().Length));
            }

            private Dictionary<string, string> Hyperlinks(OpenXmlPart part)
            {
                if (!hyperlinks.TryGetValue(part, out var links))
                    hyperlinks[part] = links = part.HyperlinkRelationships.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Uri.ToString());
                return links;
            }

            private static bool IsOn(W.OnOffType value) => value != null && (value.Val == null || value.Val.Value);

            private string Image(W.Drawing drawing)
            {
                var embed = drawing.Descendants<A.Blip>().FirstOrDefault()?.Embed?.Value;
                if (embed == null) return "";
                if (owner.Parts.Where(p => p.RelationshipId == embed).Select(p => p.OpenXmlPart).FirstOrDefault() is not ImagePart image) return "";
                var props = drawing.Descendants<DW.DocProperties>().FirstOrDefault();
                var alt = props?.Description?.Value ?? props?.Title?.Value ?? "";
                using var data = image.GetStream();
                return context.SaveImage(data, image.ContentType, alt);
            }

            // The words in a text box (one copy: a text box is stored twice, for new and old versions of Word).
            private string TextBox(OpenXmlElement element)
            {
                var box = element.Descendants<W.TextBoxContent>().FirstOrDefault();
                if (box == null) return "";
                var previous = owner;
                var inline = new InlineBuilder();
                foreach (var p in box.Descendants<W.Paragraph>()) { AddInlines(p, inline, " "); inline.AddRaw(" "); }
                owner = previous;
                var text = inline.Build().Trim();
                return text.Length == 0 ? "" : " " + text + " ";
            }

            private string Endnote(string id)
            {
                var note = main.EndnotesPart?.Endnotes?.Elements<W.Endnote>().FirstOrDefault(f => f.Id?.Value.ToString() == id);
                if (note == null) return "";
                var inline = new InlineBuilder();
                var previous = owner;
                owner = main.EndnotesPart;
                try
                {
                    foreach (var p in note.Elements<W.Paragraph>()) { AddInlines(p, inline, " "); inline.AddRaw(" "); }
                }
                finally { owner = previous; }
                var text = inline.Build().Trim();
                if (text.Length == 0) return "";
                var label = "e" + id;
                if (!Footnotes.Any(f => f.Id == label)) Footnotes.Add((label, text));
                return $"[^{label}]";
            }

            // A comment in the margin is kept as a note: "[^c0]: Comment (Author): text".
            private string CommentNote(string id)
            {
                var comment = main.WordprocessingCommentsPart?.Comments?.Elements<W.Comment>().FirstOrDefault(c => c.Id?.Value == id);
                if (comment == null) return "";
                var inline = new InlineBuilder();
                foreach (var p in comment.Elements<W.Paragraph>()) { AddInlines(p, inline, " "); inline.AddRaw(" "); }
                var text = inline.Build().Trim();
                if (text.Length == 0) return "";
                var author = comment.Author?.Value;
                var label = "c" + id;
                if (!Footnotes.Any(f => f.Id == label)) Footnotes.Add((label, "Comment" + (string.IsNullOrWhiteSpace(author) ? "" : " (" + author.Trim() + ")") + ": " + text));
                return $"[^{label}]";
            }

            private string Footnote(string id)
            {
                var note = main.FootnotesPart?.Footnotes?.Elements<W.Footnote>().FirstOrDefault(f => f.Id?.Value.ToString() == id);
                if (note == null) return "";
                var inline = new InlineBuilder();
                var previous = owner;
                owner = main.FootnotesPart;
                try
                {
                    foreach (var p in note.Elements<W.Paragraph>()) { AddInlines(p, inline, " "); inline.AddRaw(" "); }
                }
                finally { owner = previous; }
                var text = inline.Build().Trim();
                if (text.Length == 0) return "";
                if (!Footnotes.Any(f => f.Id == id)) Footnotes.Add((id, text));
                return $"[^{id}]";
            }

            private string Table(W.Table table)
            {
                var rows = new List<IReadOnlyList<string>>();
                foreach (var row in table.Elements<W.TableRow>())
                {
                    var cells = new List<string>();
                    // The header row is bold in Markdown already; "**Region**" there only costs tokens.
                    var isHeader = rows.Count == 0;
                    foreach (var cell in row.Elements<W.TableCell>())
                    {
                        var props = cell.TableCellProperties;
                        var continued = props?.VerticalMerge != null && (props.VerticalMerge.Val == null || props.VerticalMerge.Val.Value == W.MergedCellValues.Continue);
                        cells.Add(continued ? "" : CellText(cell, isHeader));
                        var span = props?.GridSpan?.Val?.Value ?? 1;
                        for (var i = 1; i < span; i++) cells.Add("");
                    }
                    rows.Add(cells);
                }
                // Drop rows that are entirely empty (spacer rows).
                rows = rows.Where(r => r.Any(c => c.Length > 0)).ToList();
                // Drop columns that are empty in every row (the spacing columns of a calendar).
                var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
                var keep = Enumerable.Range(0, width).Where(c => rows.Any(r => c < r.Count && r[c].Length > 0)).ToList();
                if (keep.Count > 0 && keep.Count < width)
                    rows = rows.Select(r => (IReadOnlyList<string>)keep.Select(c => c < r.Count ? r[c] : "").ToList()).ToList();
                return MarkdownText.Table(rows);
            }

            private string CellText(W.TableCell cell, bool isHeader)
            {
                var parts = new List<string>();
                foreach (var p in cell.Descendants<W.Paragraph>())
                {
                    var inline = new InlineBuilder();
                    AddInlines(p, inline, "<br>");
                    var text = inline.Build(plainHeading: isHeader).Trim();
                    if (text.Length == 0) continue;
                    var numbering = Numbering(p, p.ParagraphProperties?.ParagraphStyleId?.Val?.Value);
                    parts.Add(numbering != null ? "• " + text : text);
                }
                return string.Join("<br>", parts);
            }

            private bool IsCodeParagraph(W.Paragraph p)
            {
                var name = StyleName(p.ParagraphProperties?.ParagraphStyleId?.Val?.Value);
                if (name.Contains("code", StringComparison.OrdinalIgnoreCase) || name.Contains("preformatted", StringComparison.OrdinalIgnoreCase)) return true;
                var runs = p.Descendants<W.Run>().Where(r => r.Descendants<W.Text>().Any(t => t.Text.Trim().Length > 0)).ToList();
                return runs.Count > 0 && runs.All(r => MonospaceFonts.Contains(r.RunProperties?.RunFonts?.Ascii?.Value ?? ""));
            }

            private string StyleName(string styleId)
            {
                if (styleId == null || !styles.TryGetValue(styleId, out var style)) return "";
                return style.StyleName?.Val?.Value ?? styleId;
            }

            // When the document has a Title, it is the one "#" and Heading 1 becomes "##".
            private int? headingOffset;

            private int HeadingOffset(W.Paragraph anyParagraph)
            {
                headingOffset ??= anyParagraph.Ancestors<W.Body>().FirstOrDefault()?.Descendants<W.ParagraphStyleId>()
                    .Any(s => StyleName(s.Val?.Value).Equals("title", StringComparison.OrdinalIgnoreCase)) == true ? 1 : 0;
                return headingOffset.Value;
            }

            // --- Headings made by looks: a document with no heading styles at all (typed in a plain template) still has titles, set
            // larger or in bold. Only used when the document has no heading styles anywhere.
            private bool? hasHeadingStyles;
            private double bodyHalfPoints;
            private List<double> headingSizes;

            private double DefaultHalfPoints()
            {
                var fromDefaults = main.StyleDefinitionsPart?.Styles?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle?.FontSize?.Val?.Value;
                return double.TryParse(fromDefaults, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 22;
            }

            private double ParagraphHalfPoints(W.Paragraph p, string styleId)
            {
                var sized = p.Descendants<W.Run>().Where(r => r.Descendants<W.Text>().Any(t => t.Text.Trim().Length > 0)).ToList();
                if (sized.Count == 0) return 0;
                double fromStyle = 0;
                for (var id = styleId; id != null && styles.TryGetValue(id, out var style); id = style.BasedOn?.Val?.Value)
                {
                    if (double.TryParse(style.StyleRunProperties?.FontSize?.Val?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) { fromStyle = v; break; }
                    if (id == style.BasedOn?.Val?.Value) break;
                }
                return sized.Max(r => double.TryParse(r.RunProperties?.FontSize?.Val?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fromStyle > 0 ? fromStyle : DefaultHalfPoints());
            }

            private bool AllBold(W.Paragraph p, string styleId)
            {
                var styleBold = false;
                for (var id = styleId; id != null && styles.TryGetValue(id, out var style); id = style.BasedOn?.Val?.Value)
                {
                    if (style.StyleRunProperties?.Bold != null) { styleBold = IsOn(style.StyleRunProperties.Bold); break; }
                    if (id == style.BasedOn?.Val?.Value) break;
                }
                var runs = p.Descendants<W.Run>().Where(r => r.Descendants<W.Text>().Any(t => t.Text.Trim().Length > 0)).ToList();
                return runs.Count > 0 && runs.All(r => r.RunProperties?.Bold != null ? IsOn(r.RunProperties.Bold) : styleBold);
            }

            private int VisualHeadingLevel(W.Paragraph p, string styleId, string plain)
            {
                var body = p.Ancestors<W.Body>().FirstOrDefault();
                if (body == null) return 0;
                hasHeadingStyles ??= body.Descendants<W.Paragraph>().Any(x => HeadingLevel(x, x.ParagraphProperties?.ParagraphStyleId?.Val?.Value) > 0);
                if (hasHeadingStyles == true) return 0;
                if (bodyHalfPoints == 0)
                {
                    // The size most of the text is set in, and the larger sizes that short paragraphs use.
                    var weights = new Dictionary<double, int>();
                    var shortSizes = new HashSet<double>();
                    foreach (var x in body.Descendants<W.Paragraph>())
                    {
                        var text = string.Concat(x.Descendants<W.Text>().Select(t => t.Text)).Trim();
                        if (text.Length == 0) continue;
                        var size = ParagraphHalfPoints(x, x.ParagraphProperties?.ParagraphStyleId?.Val?.Value);
                        weights[size] = weights.GetValueOrDefault(size) + text.Length;
                    }
                    bodyHalfPoints = weights.Count == 0 ? DefaultHalfPoints() : weights.OrderByDescending(w => w.Value).First().Key;
                    headingSizes = weights.Keys.Where(k => k >= bodyHalfPoints * 1.15).OrderByDescending(k => k).Take(3).ToList();
                }
                var trimmed = plain.Trim();
                if (trimmed.Length == 0 || trimmed.Length > 100 || ".:,;".Contains(trimmed[^1]) || trimmed.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))) return 0;
                var half = ParagraphHalfPoints(p, styleId);
                if (half >= bodyHalfPoints * 1.15)
                {
                    var rank = headingSizes.IndexOf(headingSizes.OrderBy(k => Math.Abs(k - half)).First());
                    return Math.Min(rank + 1, 3);
                }
                return AllBold(p, styleId) ? Math.Min(Math.Max(headingSizes.Count + 1, 2), 4) : 0;
            }

            private int HeadingLevel(W.Paragraph p, string styleId)
            {
                var outline = p.ParagraphProperties?.OutlineLevel?.Val?.Value;
                if (outline is int o && o < 6) return Math.Min(o + 1 + HeadingOffset(p), 6);
                for (var id = styleId; id != null && styles.TryGetValue(id, out var style); id = style.BasedOn?.Val?.Value)
                {
                    var name = (style.StyleName?.Val?.Value ?? "").ToLowerInvariant();
                    if (name == "title") return 1;
                    if (name == "subtitle") return 2;
                    if (name.StartsWith("heading ") && int.TryParse(name.Substring(8), out var n) && n >= 1)
                        return Math.Min(n + HeadingOffset(p), 6);
                    var styleOutline = style.StyleParagraphProperties?.OutlineLevel?.Val?.Value;
                    if (styleOutline is int so && so < 6) return Math.Min(so + 1 + HeadingOffset(p), 6);
                    if (id == style.BasedOn?.Val?.Value) break;
                }
                return 0;
            }

            private (int NumId, int Level)? Numbering(W.Paragraph p, string styleId)
            {
                var numPr = p.ParagraphProperties?.NumberingProperties;
                var level = numPr?.NumberingLevelReference?.Val?.Value;
                var numId = numPr?.NumberingId?.Val?.Value;
                for (var id = styleId; numId == null && id != null && styles.TryGetValue(id, out var style); id = style.BasedOn?.Val?.Value)
                {
                    var styleNum = style.StyleParagraphProperties?.NumberingProperties;
                    numId = styleNum?.NumberingId?.Val?.Value;
                    level ??= styleNum?.NumberingLevelReference?.Val?.Value;
                    if (id == style.BasedOn?.Val?.Value) break;
                }
                if (numId is not int n || n == 0) return null;
                return (n, level ?? 0);
            }

            // The number of the next item of an ordered list. Items of the same numbering instance carry on counting after a paragraph in
            // between ("continued lists"), a deeper level starts again at its own start value, and an instance can override its start.
            private int NextNumber(int numId, int level)
            {
                var numbering = main.NumberingDefinitionsPart?.Numbering;
                var instance = numbering?.Elements<W.NumberingInstance>().FirstOrDefault(i => i.NumberID?.Value == numId);
                var abstractId = instance?.AbstractNumId?.Val?.Value ?? numId;
                var abstractNum = numbering?.Elements<W.AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
                int StartOf(int lvlIndex)
                {
                    var lvl = abstractNum?.Elements<W.Level>().FirstOrDefault(l => l.LevelIndex?.Value == lvlIndex);
                    return lvl?.StartNumberingValue?.Val?.Value ?? 1;
                }
                if (!counters.TryGetValue(numId, out var counts)) counters[numId] = counts = new int[9];
                var slot = Math.Min(level, 8); // the effective level: a "List Number 2" style puts an item one deeper than its numbering says
                var overrides = instance?.Elements<W.LevelOverride>().FirstOrDefault(o => o.LevelIndex?.Value == slot)?.StartOverrideNumberingValue?.Val?.Value;
                counts[slot] = counts[slot] == 0 ? (overrides ?? StartOf(slot)) : counts[slot] + 1;
                for (var deeper = slot + 1; deeper < counts.Length; deeper++) counts[deeper] = 0;
                return counts[slot];
            }

            private bool IsOrdered(int numId, int level)
            {
                var numbering = main.NumberingDefinitionsPart?.Numbering;
                var instance = numbering?.Elements<W.NumberingInstance>().FirstOrDefault(i => i.NumberID?.Value == numId);
                var abstractId = instance?.AbstractNumId?.Val?.Value;
                var abstractNum = numbering?.Elements<W.AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
                var lvl = abstractNum?.Elements<W.Level>().FirstOrDefault(l => l.LevelIndex?.Value == level);
                var format = lvl?.NumberingFormat?.Val?.Value;
                return format != null && format != W.NumberFormatValues.Bullet && format != W.NumberFormatValues.None;
            }
        }
    }
}
