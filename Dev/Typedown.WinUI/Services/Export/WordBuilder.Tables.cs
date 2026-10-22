using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    internal sealed partial class WordBuilder
    {
        // A real Word table: the header row repeats on every page, a column keeps the alignment the Markdown asked for.
        private void RenderTable(Table table, Ctx ctx)
        {
            pendingMarker = null;
            var rows = table.OfType<TableRow>().ToList();
            var count = Math.Max(table.ColumnDefinitions.Count, rows.Select(r => r.OfType<TableCell>().Sum(c => Math.Max(c.ColumnSpan, 1))).DefaultIfEmpty(1).Max());
            if (target.LastChild is W.Table) target.Append(new W.Paragraph()); // two tables in a row would become one in Word
            var columnWidth = textWidth / count;

            var grid = new W.TableGrid();
            for (var i = 0; i < count; i++) grid.Append(new W.GridColumn { Width = columnWidth.ToString() });
            var word = new W.Table(
                new W.TableProperties(
                    new W.TableStyle { Val = WordStyles.TableGrid },
                    new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct },
                    new W.TableLook { Val = "04A0" }),
                grid);

            foreach (var row in rows)
            {
                var wordRow = new W.TableRow();
                if (row.IsHeader) wordRow.Append(new W.TableRowProperties(new W.CantSplit(), new W.TableHeader()));
                var column = 0;
                foreach (var cell in row.OfType<TableCell>())
                {
                    var span = Math.Max(cell.ColumnSpan, 1);
                    var props = new W.TableCellProperties(new W.TableCellWidth { Width = (columnWidth * span).ToString(), Type = W.TableWidthUnitValues.Dxa });
                    if (span > 1) props.Append(new W.GridSpan { Val = span });
                    if (row.IsHeader) props.Append(new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = WordLooks.Of(options.Look).TableHeaderFill });
                    var wordCell = new W.TableCell(props);
                    var definition = column < table.ColumnDefinitions.Count ? table.ColumnDefinitions[column] : null;
                    var align = definition?.Alignment switch
                    {
                        TableColumnAlign.Center => W.JustificationValues.Center,
                        TableColumnAlign.Right => W.JustificationValues.Right,
                        _ => (W.JustificationValues?)null,
                    };
                    var saved = target;
                    target = wordCell;
                    foreach (var block in cell) RenderBlock(block, Ctx.Root with { InTable = true, Align = align, Bold = row.IsHeader });
                    target = saved;
                    if (!wordCell.Elements<W.Paragraph>().Any()) wordCell.Append(new W.Paragraph());
                    if (wordCell.LastChild is W.Table) wordCell.Append(new W.Paragraph()); // a cell ends with a paragraph
                    wordRow.Append(wordCell);
                    column += span;
                }
                word.Append(wordRow);
            }
            target.Append(word);
            gapBefore = true;
        }

        private static readonly Regex ImgTag = new(@"<img\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex AnyTag = new("<[^>]+>");

        // HTML blocks in Markdown: the text of it, without the tags. A comment is nothing; a line break stays a line break; a picture is a picture.
        private void RenderHtml(HtmlBlock html, Ctx ctx)
        {
            if (html.Type == HtmlBlockType.Comment) return;
            var raw = html.Lines.ToString().Replace("\r\n", "\n");
            var at = 0;
            foreach (Match m in ImgTag.Matches(raw))
            {
                AddHtmlText(raw.Substring(at, m.Index - at), ctx);
                var picture = HtmlPicture(m.Value);
                if (picture != null) AddRunParagraph(picture, ctx);
                at = m.Index + m.Length;
            }
            AddHtmlText(raw.Substring(at), ctx);
        }

        private void AddRunParagraph(OpenXmlElement run, Ctx ctx)
        {
            var paragraph = new W.Paragraph(Properties(ctx, null));
            paragraph.Append(run);
            target.Append(paragraph);
        }

        private void AddHtmlText(string html, Ctx ctx)
        {
            html = Regex.Replace(html, "<!--.*?-->", "", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<br\s*/?>", "\u0001", RegexOptions.IgnoreCase);
            html = WordMarks.Strip(WebUtility.HtmlDecode(AnyTag.Replace(html, "")));
            foreach (var group in Regex.Split(html, @"\n[ \t]*\n"))
            {
                var text = string.Join(" ", group.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                if (text.Trim('\u0001', ' ').Length == 0) continue;
                var paragraph = new W.Paragraph(Properties(ctx, null));
                var pieces = text.Split('\u0001');
                for (var i = 0; i < pieces.Length; i++)
                {
                    if (i > 0) paragraph.Append(new W.Run(new W.Break()));
                    if (pieces[i].Length > 0) paragraph.Append(TextRun(pieces[i], new Fmt { Bold = ctx.Bold }));
                }
                target.Append(paragraph);
            }
        }
    }
}
