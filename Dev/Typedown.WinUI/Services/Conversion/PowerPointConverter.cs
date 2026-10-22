using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using D = DocumentFormat.OpenXml.Drawing.Diagrams;
using P = DocumentFormat.OpenXml.Presentation;

namespace Typedown.WinUI.Services.Conversion
{
    // .pptx → one "## Slide N: Title" section per visible slide: text boxes in reading order (top to
    // bottom, then left to right), bullet levels kept, tables as pipe tables, charts as tables of their data,
    // SmartArt as a list, links kept, comments and speaker notes as quotes.
    internal static class PowerPointConverter
    {
        public static string Convert(Stream stream, ConversionContext context)
        {
            using var doc = PresentationDocument.Open(stream, false);
            var presentation = doc.PresentationPart;
            var slideIds = presentation?.Presentation?.SlideIdList?.Elements<P.SlideId>().ToList();
            if (slideIds == null) return "";
            var sb = new StringBuilder();
            var number = 0;
            foreach (var slideId in slideIds)
            {
                number++;
                if (slideId.RelationshipId?.Value == null || presentation.GetPartById(slideId.RelationshipId.Value) is not SlidePart slide) continue;
                if (slide.Slide?.Show?.Value == false) continue;
                var tree = slide.Slide?.CommonSlideData?.ShapeTree;
                if (tree == null) continue;

                string title = null;
                var body = new List<(long Y, long X, string Markdown)>();
                foreach (var element in Flatten(tree))
                {
                    switch (element)
                    {
                        case P.Shape shape:
                            var placeholder = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value;
                            if (title == null && (placeholder == P.PlaceholderValues.Title || placeholder == P.PlaceholderValues.CenteredTitle))
                            {
                                title = PlainText(shape.TextBody);
                                continue;
                            }
                            // Slide numbers, dates and footers repeat on every slide.
                            if (placeholder == P.PlaceholderValues.SlideNumber || placeholder == P.PlaceholderValues.DateAndTime || placeholder == P.PlaceholderValues.Footer)
                                continue;
                            var text = TextBody(slide, shape.TextBody, bulleted: placeholder == P.PlaceholderValues.Body || placeholder == P.PlaceholderValues.Object || placeholder == null && CountParagraphs(shape.TextBody) > 1);
                            if (text.Length > 0) body.Add(Position(shape.ShapeProperties?.Transform2D, text));
                            break;
                        case P.GraphicFrame frame:
                            var table = frame.Descendants<A.Table>().FirstOrDefault();
                            if (table != null)
                            {
                                var rows = table.Elements<A.TableRow>()
                                    .Select(r => (IReadOnlyList<string>)r.Elements<A.TableCell>().Select(c => PlainText(c.TextBody, "<br>")).ToList())
                                    .Where(r => r.Any(c => c.Length > 0)).ToList();
                                var md = MarkdownText.Table(rows);
                                if (md.Length > 0) body.Add(Position(frame.Transform, md));
                                break;
                            }
                            var chart = ChartMarkdown(slide, frame);
                            if (chart.Length > 0) { body.Add(Position(frame.Transform, chart)); break; }
                            var diagram = DiagramMarkdown(slide, frame);
                            if (diagram.Length > 0) body.Add(Position(frame.Transform, diagram));
                            break;
                        case P.Picture picture:
                            var embed = picture.BlipFill?.Blip?.Embed?.Value;
                            if (embed != null && slide.GetPartById(embed) is ImagePart image)
                            {
                                var alt = picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Description?.Value ?? "";
                                using var data = image.GetStream();
                                var md = context.SaveImage(data, image.ContentType, alt);
                                if (md.Length > 0) body.Add(Position(picture.ShapeProperties?.Transform2D, md));
                            }
                            break;
                    }
                }

                var notes = NotesText(slide);
                var comments = CommentsText(presentation, slide);
                if (title == null && body.Count == 0 && notes.Length == 0 && comments.Length == 0) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                var heading = string.IsNullOrWhiteSpace(title)
                    ? string.Format(context.Options.SlideHeadingUntitledFormat, number)
                    : string.Format(context.Options.SlideHeadingFormat, number, MarkdownText.EscapeInline(System.Text.RegularExpressions.Regex.Replace(title, @"\s+", " ").Trim()));
                sb.Append("## ").Append(heading);
                foreach (var item in body.OrderBy(b => b.Y).ThenBy(b => b.X))
                    sb.Append("\n\n").Append(item.Markdown);
                if (comments.Length > 0)
                    sb.Append("\n\n").Append(comments);
                if (notes.Length > 0)
                    sb.Append("\n\n> **").Append(context.Options.NotesLabel).Append(":** ").Append(notes.Replace("\n", "\n> "));
            }
            return sb.ToString();
        }

        private static IEnumerable<OpenXmlElement> Flatten(OpenXmlElement container)
        {
            foreach (var child in container.ChildElements)
            {
                if (child is P.GroupShape group)
                    foreach (var inner in Flatten(group)) yield return inner;
                else yield return child;
            }
        }

        private static (long, long, string) Position(OpenXmlElement transform, string markdown)
        {
            var offset = transform?.GetFirstChild<A.Offset>();
            return (offset?.Y?.Value ?? long.MaxValue / 2, offset?.X?.Value ?? 0, markdown);
        }

        private static int CountParagraphs(OpenXmlElement textBody) =>
            textBody?.Elements<A.Paragraph>().Count(p => ParagraphText(p).Trim().Length > 0) ?? 0;

        private static string TextBody(SlidePart slide, OpenXmlElement textBody, bool bulleted)
        {
            if (textBody == null) return "";
            var items = new List<(int Level, bool Bullet, string Text)>();
            foreach (var p in textBody.Elements<A.Paragraph>())
            {
                var inline = new InlineBuilder();
                AddRuns(slide, p, inline);
                var text = inline.Build().Trim();
                if (text.Length == 0) continue;
                var noBullet = p.ParagraphProperties?.GetFirstChild<A.NoBullet>() != null;
                items.Add((p.ParagraphProperties?.Level?.Value ?? 0, bulleted && !noBullet, text));
            }
            // Levels count from the shallowest one in the text box and never go more than one deeper than the item above: an indented
            // list with no item above it would otherwise be four spaces in, which Markdown reads as code.
            var floor = items.Where(i => i.Bullet).Select(i => i.Level).DefaultIfEmpty(0).Min();
            var lines = new List<string>();
            var previousLevel = -1;
            foreach (var (level, bullet, text) in items)
            {
                if (bullet)
                {
                    var relative = Math.Min(Math.Min(level - floor, 8), previousLevel + 1);
                    previousLevel = Math.Max(relative, 0);
                    lines.Add(new string(' ', 4 * Math.Max(relative, 0)) + "- " + text);
                }
                else
                {
                    previousLevel = -1;
                    lines.Add(MarkdownText.EscapeBlockStart(text));
                }
            }
            var allBullets = lines.All(l => l.TrimStart().StartsWith("- "));
            return string.Join(allBullets ? "\n" : "\n\n", lines);
        }

        private static void AddRuns(SlidePart slide, A.Paragraph p, InlineBuilder inline)
        {
            foreach (var child in p.ChildElements)
            {
                switch (child)
                {
                    case A.Run run:
                        var rp = run.RunProperties;
                        inline.Add(run.Text?.Text, rp?.Bold?.Value == true, rp?.Italic?.Value == true,
                            rp?.Strike != null && rp.Strike.Value != A.TextStrikeValues.NoStrike, false, Link(slide, rp));
                        break;
                    case A.Field field:
                        inline.Add(field.Text?.Text);
                        break;
                    case A.Break:
                        inline.AddRaw("  \n");
                        break;
                }
            }
        }

        // The address a run links to (a click on the text opens it); links to another slide have no address.
        private static string Link(SlidePart slide, A.RunProperties properties)
        {
            var id = properties?.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value;
            if (string.IsNullOrEmpty(id)) return null;
            var uri = slide.HyperlinkRelationships.FirstOrDefault(r => r.Id == id)?.Uri;
            return uri != null && uri.IsAbsoluteUri ? uri.ToString() : null;
        }

        // A line break inside a paragraph is a space in a title ("Math 260" / "Foundations of Geometry" were one word).
        private static string ParagraphText(A.Paragraph p)
        {
            var sb = new StringBuilder();
            foreach (var child in p.ChildElements)
            {
                switch (child)
                {
                    case A.Run run: sb.Append(run.Text?.Text); break;
                    case A.Field field: sb.Append(field.Text?.Text); break;
                    case A.Break: sb.Append(' '); break;
                }
            }
            return sb.ToString();
        }

        private static string PlainText(OpenXmlElement textBody, string separator = "\n") =>
            textBody == null ? "" : string.Join(separator, textBody.Elements<A.Paragraph>().Select(ParagraphText).Where(t => t.Trim().Length > 0)).Trim();

        private static string NotesText(SlidePart slide)
        {
            var notes = slide.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree;
            if (notes == null) return "";
            var bodies = notes.Descendants<P.Shape>()
                .Where(s => s.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value == P.PlaceholderValues.Body)
                .Select(s => PlainText(s.TextBody));
            return string.Join("\n", bodies.Where(t => t.Length > 0)).Trim();
        }

        // Review comments on the slide, one quote each: "> **Comment (Author):** text".
        private static string CommentsText(PresentationPart presentation, SlidePart slide)
        {
            var list = slide.SlideCommentsPart?.CommentList?.Elements<P.Comment>().ToList();
            if (list == null || list.Count == 0) return "";
            var authors = presentation.CommentAuthorsPart?.CommentAuthorList?.Elements<P.CommentAuthor>()
                .Where(a => a.Id?.Value != null).GroupBy(a => a.Id.Value).ToDictionary(g => g.Key, g => g.First().Name?.Value);
            var lines = new List<string>();
            foreach (var comment in list)
            {
                var text = comment.Text?.Text?.Trim();
                if (string.IsNullOrEmpty(text)) continue;
                var author = comment.AuthorId?.Value is uint id && authors != null && authors.TryGetValue(id, out var name) ? name : null;
                lines.Add("> **Comment" + (string.IsNullOrWhiteSpace(author) ? "" : " (" + author.Trim() + ")") + ":** " + MarkdownText.EscapeInline(text).Replace("\n", "\n> "));
            }
            return string.Join("\n\n", lines);
        }

        // --- Charts: the data behind the picture, as a table (categories down, one column per series).

        private static string ChartMarkdown(SlidePart slide, P.GraphicFrame frame)
        {
            var id = frame.Descendants<C.ChartReference>().FirstOrDefault()?.Id?.Value;
            if (id == null || slide.GetPartById(id) is not ChartPart part || part.ChartSpace == null) return "";
            var series = part.ChartSpace.Descendants().Where(e => e.LocalName == "ser").ToList();
            if (series.Count == 0) return "";

            // The chart's own title, not the first title found: the titles of its axes are "c:title" too.
            var chartTitle = part.ChartSpace.GetFirstChild<C.Chart>()?.Title;
            var title = string.Join(" ", chartTitle?.Descendants<A.Text>().Select(t => t.Text) ?? Enumerable.Empty<string>()).Trim();
            var names = new List<string>();
            var categories = new SortedDictionary<int, string>();
            var columns = new List<Dictionary<int, string>>();
            foreach (var ser in series)
            {
                names.Add(CacheValues(ser.Elements().FirstOrDefault(e => e.LocalName == "tx")).Select(v => v.Value).FirstOrDefault() ?? "");
                var cat = ser.Elements().FirstOrDefault(e => e.LocalName is "cat" or "xVal");
                foreach (var (index, value) in CacheValues(cat)) categories.TryAdd(index, value);
                var values = new Dictionary<int, string>();
                foreach (var (index, value) in CacheValues(ser.Elements().FirstOrDefault(e => e.LocalName is "val" or "yVal"), formatted: true)) values[index] = value;
                columns.Add(values);
            }
            // Series without category labels (a scatter, a pie with none) are numbered by position.
            var indexes = categories.Keys.Union(columns.SelectMany(c => c.Keys)).OrderBy(i => i).ToList();
            if (indexes.Count == 0) return "";
            var rows = new List<IReadOnlyList<string>>();
            rows.Add(new[] { "" }.Concat(names.Select((n, i) => n.Length > 0 ? n : "Series " + (i + 1))).ToList());
            foreach (var index in indexes)
                rows.Add(new[] { categories.TryGetValue(index, out var label) ? label : (index + 1).ToString(CultureInfo.InvariantCulture) }
                    .Concat(columns.Select(c => c.TryGetValue(index, out var v) ? v : "")).ToList());
            var table = MarkdownText.Table(rows);
            return (title.Length > 0 ? "**" + MarkdownText.EscapeInline(title) + "**\n\n" : "") + table;
        }

        // The cached points of a chart reference or literal: (index, text).
        private static IEnumerable<(int Index, string Value)> CacheValues(OpenXmlElement holder, bool formatted = false)
        {
            if (holder == null) yield break;
            var formatCode = holder.Descendants().FirstOrDefault(e => e.LocalName == "formatCode")?.InnerText ?? "";
            foreach (var point in holder.Descendants().Where(e => e.LocalName == "pt"))
            {
                var indexText = point.GetAttributes().FirstOrDefault(a => a.LocalName == "idx").Value;
                if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
                var text = point.Descendants().FirstOrDefault(e => e.LocalName == "v")?.InnerText ?? "";
                if (formatted && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    text = formatCode.Contains('%') ? Math.Round(number * 100, 2).ToString("0.##", CultureInfo.InvariantCulture) + "%" : number.ToString("G15", CultureInfo.InvariantCulture);
                yield return (index, text.Trim());
            }
            // A literal series name ("<c:tx><c:v>Sales</c:v>") has no points.
            if (!holder.Descendants().Any(e => e.LocalName == "pt"))
            {
                var literal = holder.Descendants().FirstOrDefault(e => e.LocalName == "v")?.InnerText;
                if (!string.IsNullOrWhiteSpace(literal)) yield return (0, literal.Trim());
            }
        }

        // --- SmartArt: the text of its boxes as a list, nested as the diagram is.

        private static string DiagramMarkdown(SlidePart slide, P.GraphicFrame frame)
        {
            var relationships = frame.Descendants<D.RelationshipIds>().FirstOrDefault();
            var dataId = relationships?.DataPart?.Value;
            if (dataId == null || slide.GetPartById(dataId) is not DiagramDataPart dataPart || dataPart.DataModelRoot == null) return "";
            var model = dataPart.DataModelRoot;
            var points = model.PointList?.Elements<D.Point>().ToList();
            if (points == null) return "";
            string TextOf(D.Point point) => string.Join(" ", point.Descendants<A.Paragraph>().Select(ParagraphText).Where(t => t.Trim().Length > 0)).Trim();
            var nodes = points.Where(p => p.Type == null || p.Type.Value == D.PointValues.Node).Where(p => p.ModelId?.Value != null).ToDictionary(p => p.ModelId.Value, p => p, StringComparer.Ordinal);
            if (nodes.Count == 0) return "";
            // Parent → children, in the order the diagram gives them.
            var children = new Dictionary<string, List<(uint Order, string Id)>>();
            var hasParent = new HashSet<string>();
            foreach (var connection in model.ConnectionList?.Elements<D.Connection>() ?? Enumerable.Empty<D.Connection>())
            {
                if (connection.Type != null && connection.Type.Value != D.ConnectionValues.ParentOf) continue;
                var parent = connection.SourceId?.Value;
                var child = connection.DestinationId?.Value;
                if (parent == null || child == null || !nodes.ContainsKey(child)) continue;
                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<(uint, string)>();
                list.Add((connection.SourcePosition?.Value ?? 0, child));
                hasParent.Add(child);
            }
            var lines = new List<string>();
            void Walk(string id, int depth, HashSet<string> seen)
            {
                if (!seen.Add(id)) return;
                if (nodes.TryGetValue(id, out var node))
                {
                    var text = TextOf(node);
                    if (text.Length > 0) lines.Add(new string(' ', 4 * Math.Min(depth, 8)) + "- " + MarkdownText.EscapeInline(text));
                    else depth--;
                }
                else depth--; // the document point itself
                if (children.TryGetValue(id, out var kids))
                    foreach (var (_, child) in kids.OrderBy(k => k.Order)) Walk(child, depth + 1, seen);
            }
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var root = points.FirstOrDefault(p => p.Type?.Value == D.PointValues.Document)?.ModelId?.Value;
            if (root != null) Walk(root, 0, seenIds);
            foreach (var id in nodes.Keys.Where(k => !hasParent.Contains(k) && !seenIds.Contains(k))) Walk(id, 0, seenIds);
            return string.Join("\n", lines);
        }
    }
}
