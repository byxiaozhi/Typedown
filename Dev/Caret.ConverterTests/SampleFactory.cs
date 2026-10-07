using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OpenMcdf;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using D = DocumentFormat.OpenXml.Drawing.Diagrams;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using P = DocumentFormat.OpenXml.Presentation;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Caret.ConverterTests
{
    // Writes the documents in samples/. They are committed, and the tests read the committed files, so
    // this only runs on purpose (CARET_GENERATE_SAMPLES=1, see README.md) to create them or to add a
    // feature to one. Everything in them is made up.
    internal static class SampleFactory
    {
        // A 2x2 red PNG, so image extraction has something real to write.
        public static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEklEQVR4nGP8z8Dwn4EIwESMolGFxCgEAB3eAgFhLdlrAAAAAElFTkSuQmCC");

        public static IReadOnlyDictionary<string, byte[]> All() => new Dictionary<string, byte[]>
        {
            ["word-report.docx"] = WordReport(),
            ["word-french.docx"] = WordFrench(),
            ["excel-budget.xlsx"] = ExcelBudget(),
            ["word-notes.docx"] = WordNotes(),
            ["word-plain.docx"] = WordPlain(),
            ["excel-report.xlsx"] = ExcelReport(),
            ["powerpoint-review.pptx"] = PowerPointReview(),
            ["pdf-article.pdf"] = PdfArticle(),
            ["pdf-layout.pdf"] = PdfLayoutSample.Build(),
            ["csv-semicolon.csv"] = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
                "Produit;Prix;Quantité;Remarque\r\nCafé;2,50;10;\"Torréfié; bio\"\r\nThé vert;3,10;4;\"Dit \"\"sencha\"\"\"\r\nChocolat;4,00;;\r\n")).ToArray(),
            ["csv-comma.csv"] = Encoding.UTF8.GetBytes(
                "id,name,notes\n1,Alpha,\"two\nlines\"\n2,Beta,pipe | in text\n,,\n3,Gamma,\n"),
            ["email-thread.eml"] = EmailThreadEml(),
            ["email-attachment.eml"] = EmailWithAttachmentEml(),
            ["email-hungarian.eml"] = EmailHungarianEml(),
            ["email-shiftjis.eml"] = EmailShiftJisEml(),
            ["email-japanese.msg"] = OutlookMsgJapanese(),
            ["email-outlook.msg"] = OutlookMsg(),
        };

        // Small documents for the tests that need something the samples do not have.
        public static byte[] EmptyWorkbook()
        {
            using var stream = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = doc.AddWorkbookPart();
                var part = workbook.AddNewPart<WorksheetPart>();
                part.Worksheet = new S.Worksheet(new S.SheetData());
                workbook.Workbook = new S.Workbook(new S.Sheets(new S.Sheet { Id = workbook.GetIdOfPart(part), SheetId = 1, Name = "Empty" }));
                workbook.Workbook.Save();
            }
            return stream.ToArray();
        }

        // A workbook from the Mac: dates count from 1904, so serial 0 is 1904-01-01 and 1 is 1904-01-02.
        public static byte[] Workbook1904()
        {
            using var stream = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = doc.AddWorkbookPart();
                workbook.Workbook = new S.Workbook(new S.WorkbookProperties { Date1904 = true });
                var styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new S.Stylesheet(
                    new S.Fonts(new S.Font()) { Count = 1 },
                    new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }), new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2 },
                    new S.Borders(new S.Border()) { Count = 1 },
                    new S.CellStyleFormats(new S.CellFormat()) { Count = 1 },
                    new S.CellFormats(new S.CellFormat(), new S.CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }) { Count = 2 });
                var part = workbook.AddNewPart<WorksheetPart>();
                part.Worksheet = new S.Worksheet(new S.SheetData(
                    new S.Row(
                        new S.Cell { CellReference = "A1", StyleIndex = 1, CellValue = new S.CellValue("0") },
                        new S.Cell { CellReference = "B1", StyleIndex = 1, CellValue = new S.CellValue("1") }) { RowIndex = 1 }));
                workbook.Workbook.Append(new S.Sheets(new S.Sheet { Id = workbook.GetIdOfPart(part), SheetId = 1, Name = "Dates" }));
                workbook.Workbook.Save();
            }
            return stream.ToArray();
        }

        // A page with no text at all: what a scanned PDF looks like to a text reader.
        public static byte[] BlankPdf() => BuildPdf(new[] { "" });

        // --- Word ---

        private static byte[] WordReport()
        {
            using var stream = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body());
                var body = main.Document.Body;
                AddWordStyles(main, "Title", "Heading1", "Heading2", "Heading3", "Quote", "Code");
                AddWordNumbering(main);
                var link = main.AddHyperlinkRelationship(new Uri("https://example.com/handbook"), true);
                var image = main.AddImagePart(ImagePartType.Png);
                image.FeedData(new MemoryStream(Png));

                var footnotes = main.AddNewPart<FootnotesPart>();
                footnotes.Footnotes = new W.Footnotes(
                    new W.Footnote(new W.Paragraph(new W.Run(new W.Text("Figures are rounded to the nearest thousand.")))) { Id = 1 });

                body.Append(
                    Para("Title", Run("Quarterly report")),
                    Para("Heading1", Run("Summary")),
                    Para(null, Run("Sales grew by "), Run("twelve percent", bold: true), Run(" while costs stayed "), Run("flat", italic: true),
                        Run(", and the old forecast was "), Run("dropped", strike: true), Run(". See the "),
                        new W.Hyperlink(Run("handbook")) { Id = link.Id }, Run(" for the method."),
                        new W.Run(new W.FootnoteReference { Id = 1 })),
                    Para("Heading2", Run("Priorities")),
                    Bullet(0, "Renew the three largest contracts"),
                    Bullet(1, "Start with the one that expires in March"),
                    Bullet(0, "Hire a second analyst"),
                    Numbered("Collect the figures"),
                    Numbered("Check them against the ledger"),
                    Numbered("Publish the report"),
                    Para("Heading3", Run("Regions")),
                    RegionsTable(),
                    Para("Quote", Run("Measure twice, cut once.")),
                    Para(null, Run("Run the export with:")),
                    Para(null, Run("report --format md", font: "Consolas")),
                    Para(null, Run("report --images ./img", font: "Consolas")),
                    Para(null, Run("Special characters: 1 * 2 = 2, snake_case_name, # not a heading, [not a link], <tag>.")),
                    Para(null, new W.Run(new W.Drawing(PictureInline(main.GetIdOfPart(image), "A small red square")))),
                    Para(null, Run("End of the report.")));
                main.Document.Save();
            }
            return stream.ToArray();
        }

        // Drop cap, superscript and subscript, an endnote and a comment, a list that carries on, a table with spacing columns, a text box.
        private static byte[] WordNotes()
        {
            using var stream = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body());
                AddWordStyles(main, "Heading1", "Heading2", "ListNumber2");
                AddWordNumbering(main);

                var endnotes = main.AddNewPart<EndnotesPart>();
                endnotes.Endnotes = new W.Endnotes(
                    new W.Endnote(new W.Paragraph(new W.Run(new W.Text("The endnote text sits at the end.")))) { Id = 1 });
                var comments = main.AddNewPart<WordprocessingCommentsPart>();
                comments.Comments = new W.Comments(
                    new W.Comment(new W.Paragraph(new W.Run(new W.Text("Please check this figure.")))) { Id = "0", Author = "Reviewer" });

                W.Run Script(string text, W.VerticalPositionValues position) =>
                    new(new W.RunProperties(new W.VerticalTextAlignment { Val = position }), new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
                W.TableCell Cell(string text) => new(new W.Paragraph(new W.Run(new W.Text(text))));

                var dropCap = new W.Paragraph(
                    new W.ParagraphProperties(new W.FrameProperties { DropCap = W.DropCapLocationValues.Drop, Lines = 3 }),
                    new W.Run(new W.Text("D")));

                main.Document.Body.Append(
                    Para("Heading1", Run("Notes sample")),
                    dropCap,
                    Para(null, Run("rop caps start a paragraph with a large first letter.")),
                    Para(null, Run("Water is H"), Script("2", W.VerticalPositionValues.Subscript), Run("O and the area is 5 m"),
                        Script("2", W.VerticalPositionValues.Superscript), Run(", the rate is 10"),
                        new W.Run(new W.RunProperties(new W.Bold(), new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }), new W.Text("-9 per day ") { Space = SpaceProcessingModeValues.Preserve }),
                        Run(". A note"), new W.Run(new W.EndnoteReference { Id = 1 }), Run(" and a comment"),
                        new W.Run(new W.CommentReference { Id = "0" }), Run(" here.")),
                    Numbered("One"),
                    Numbered("Two"),
                    new W.Paragraph(
                        new W.ParagraphProperties(
                            new W.ParagraphStyleId { Val = "ListNumber2" },
                            new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = 2 })),
                        Run("A sub-item through the List Number 2 style")),
                    Para(null, Run("A paragraph in between.")),
                    Numbered("Three"),
                    new W.Table(
                        new W.TableProperties(new W.TableStyle { Val = "TableGrid" }),
                        new W.TableGrid(new W.GridColumn { Width = "1000" }, new W.GridColumn { Width = "200" }, new W.GridColumn { Width = "1000" }, new W.GridColumn { Width = "200" }, new W.GridColumn { Width = "1000" }),
                        new W.TableRow(Cell("Mon"), Cell(""), Cell("Tue"), Cell(""), Cell("Wed")),
                        new W.TableRow(Cell("1"), Cell(""), Cell("2"), Cell(""), Cell("3"))),
                    Para(null, new W.Run(new W.Picture(new DocumentFormat.OpenXml.Vml.Shape(new DocumentFormat.OpenXml.Vml.TextBox(
                        new W.TextBoxContent(new W.Paragraph(new W.Run(new W.Text("Text in a box."))))))))),
                    new W.Paragraph(new W.ParagraphProperties(new W.FrameProperties { DropCap = W.DropCapLocationValues.Drop, Lines = 3 }), new W.Run(new W.Text("L"))),
                    Para("Heading2", Run("ast section")),
                    Para(null, Run("End.")),
                    new W.Paragraph(new W.ParagraphProperties(new W.FrameProperties { DropCap = W.DropCapLocationValues.Drop, Lines = 3 }), new W.Run(new W.Text("Q"))));
                main.Document.Save();
            }
            return stream.ToArray();
        }

        // A title above a table, number formats (currency, thousands, percent, brackets for negatives, a unit), and a note under it.
        private static byte[] ExcelReport()
        {
            using var stream = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = doc.AddWorkbookPart();
                workbook.Workbook = new S.Workbook();
                var strings = new List<string>();
                int Shared(string text)
                {
                    var i = strings.IndexOf(text);
                    if (i < 0) { strings.Add(text); i = strings.Count - 1; }
                    return i;
                }
                S.Cell Text(string reference, string text) =>
                    new() { CellReference = reference, DataType = S.CellValues.SharedString, CellValue = new S.CellValue(Shared(text).ToString()) };
                S.Cell Number(string reference, string value, uint style = 0) =>
                    new() { CellReference = reference, StyleIndex = style, CellValue = new S.CellValue(value) };
                S.Row Row(uint index, params S.Cell[] cells) => new(cells) { RowIndex = index };

                // Style indexes: 0 general, 1 currency, 2 thousands, 3 percent with a decimal, 4 negatives in brackets, 5 a unit, 6 accounting.
                var styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new S.Stylesheet(
                    new S.NumberingFormats(
                        new S.NumberingFormat { NumberFormatId = 166, FormatCode = "\"$\"#,##0.00" },
                        new S.NumberingFormat { NumberFormatId = 167, FormatCode = "0.0%" },
                        new S.NumberingFormat { NumberFormatId = 168, FormatCode = "#,##0.00;(#,##0.00)" },
                        new S.NumberingFormat { NumberFormatId = 169, FormatCode = "0.00\" kg\"" },
                        new S.NumberingFormat { NumberFormatId = 170, FormatCode = "_(\"€\"* #,##0.00_);_(\"€\"* (#,##0.00);_(\"€\"* \"-\"??_);_(@_)" }),
                    new S.Fonts(new S.Font()) { Count = 1 },
                    new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }), new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2 },
                    new S.Borders(new S.Border()) { Count = 1 },
                    new S.CellStyleFormats(new S.CellFormat()) { Count = 1 },
                    new S.CellFormats(
                        new S.CellFormat(),
                        new S.CellFormat { NumberFormatId = 166, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 3, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 167, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 168, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 169, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 170, ApplyNumberFormat = true }) { Count = 7 });

                var part = workbook.AddNewPart<WorksheetPart>();
                part.Worksheet = new S.Worksheet(
                    new S.Columns(new S.Column { Min = 9, Max = 9, Width = 10, CustomWidth = true, Hidden = true }),
                    new S.SheetData(
                    Row(1, Text("A1", "Annual summary")),
                    Row(3, Text("A3", "Item"), Text("B3", "Amount"), Text("C3", "Count"), Text("D3", "Change"), Text("E3", "Net"), Text("F3", "Weight"), Text("G3", "Euros"), Text("H3", "Total")),
                    Row(4, Text("A4", "Licences"), Number("B4", "12000.5", 1), Number("C4", "1234567", 2), Number("D4", "0.0456", 3), Number("E4", "-1500.25", 4), Number("F4", "2.5", 5), Number("G4", "1234.5", 6),
                        new S.Cell { CellReference = "H4", CellFormula = new S.CellFormula("B4+B5") }, Text("I4", "Helper column")),
                    Row(5, Text("A5", "Hosting"), Number("B5", "800", 1), Number("C5", "45", 2), Number("D5", "-0.1", 3), Number("E5", "320", 4), Number("F5", "0.125", 5), Number("G5", "-20", 6)),
                    new S.Row(Text("A6", "Filtered out")) { RowIndex = 6, Hidden = true },
                    Row(7, Text("A7", "Source: made-up figures."))));
                workbook.Workbook.Append(new S.Sheets(new S.Sheet { Id = workbook.GetIdOfPart(part), SheetId = 1, Name = "Report" }));
                var table = workbook.AddNewPart<SharedStringTablePart>();
                table.SharedStringTable = new S.SharedStringTable(strings.Select(s => new S.SharedStringItem(new S.Text(s))));
                workbook.Workbook.Save();
            }
            return stream.ToArray();
        }

        // A document typed in a plain template: no heading styles, a title set larger and section titles in bold.
        private static byte[] WordPlain()
        {
            using var stream = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body());
                W.Run Sized(string text, string halfPoints, bool bold) =>
                    new(new W.RunProperties(bold ? new W.Bold() : null, new W.FontSize { Val = halfPoints }), new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
                main.Document.Body.Append(
                    Para(null, Sized("Plain document", "40", true)),
                    Para(null, Sized("1. Background", "22", true)),
                    Para(null, Run("The text of the background is set in the ordinary size and runs over a line or two so that it is clearly a paragraph, not a title.")),
                    Para(null, Sized("Findings", "22", true)),
                    Para(null, Run("What was found goes here, with a "), new W.Run(new W.RunProperties(new W.Vanish()), new W.Text("hidden word ") { Space = SpaceProcessingModeValues.Preserve }), Run("visible ending.")),
                    Para(null, Sized("This whole sentence is bold but ends with a full stop.", "22", true)));
                main.Document.Save();
            }
            return stream.ToArray();
        }

        private static byte[] WordFrench()
        {
            using var stream = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document(new W.Body());
                AddWordStyles(main, "Heading1", "Heading2");
                AddWordNumbering(main);
                main.Document.Body.Append(
                    Para("Heading1", Run("Étude de faisabilité")),
                    Para(null, Run("Ce document résume l’étude menée à Lyon : coûts, délais et risques.")),
                    Para("Heading2", Run("Échéancier")),
                    Bullet(0, "Phase 1 – cadrage (janvier)"),
                    Bullet(0, "Phase 2 – prototype (mars)"),
                    Para(null, Run("Le budget s’élève à "), Run("120 000 €", bold: true), Run(" hors taxes.")));
                main.Document.Save();
            }
            return stream.ToArray();
        }

        private static W.Paragraph Para(string style, params OpenXmlElement[] content)
        {
            var p = new W.Paragraph();
            if (style != null) p.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = style }));
            p.Append(content);
            return p;
        }

        private static W.Run Run(string text, bool bold = false, bool italic = false, bool strike = false, string font = null)
        {
            var properties = new W.RunProperties();
            if (font != null) properties.Append(new W.RunFonts { Ascii = font, HighAnsi = font });
            if (bold) properties.Append(new W.Bold());
            if (italic) properties.Append(new W.Italic());
            if (strike) properties.Append(new W.Strike());
            var run = new W.Run();
            if (properties.HasChildren) run.Append(properties);
            run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        private static W.Paragraph Bullet(int level, string text) => ListItem(1, level, text);

        private static W.Paragraph Numbered(string text) => ListItem(2, 0, text);

        private static W.Paragraph ListItem(int numberId, int level, string text) =>
            new(new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = "ListParagraph" },
                    new W.NumberingProperties(new W.NumberingLevelReference { Val = level }, new W.NumberingId { Val = numberId })),
                Run(text));

        private static W.Table RegionsTable()
        {
            static W.TableCell Cell(string text, int span = 1)
            {
                var properties = new W.TableCellProperties();
                if (span > 1) properties.Append(new W.GridSpan { Val = span });
                return new W.TableCell(properties, new W.Paragraph(Run(text)));
            }
            static W.TableRow Row(params W.TableCell[] cells) => new(cells);
            return new W.Table(
                new W.TableProperties(new W.TableStyle { Val = "TableGrid" }),
                new W.TableGrid(new W.GridColumn { Width = "2400" }, new W.GridColumn { Width = "2400" }, new W.GridColumn { Width = "2400" }),
                Row(Cell("Region"), Cell("Sales"), Cell("Change")),
                Row(Cell("North"), Cell("1,200"), Cell("+12%")),
                Row(Cell("South | East"), Cell("900"), Cell("-3%")),
                Row(Cell("Total for all regions", 2), Cell("+5%")));
        }

        private static void AddWordStyles(MainDocumentPart main, params string[] ids)
        {
            var names = new Dictionary<string, string>
            {
                ["Title"] = "Title", ["Heading1"] = "heading 1", ["Heading2"] = "heading 2", ["Heading3"] = "heading 3", ["Quote"] = "Quote", ["Code"] = "Code", ["ListNumber2"] = "List Number 2",
            };
            var styles = new W.Styles();
            foreach (var id in ids)
                styles.Append(new W.Style(new W.StyleName { Val = names[id] }) { Type = W.StyleValues.Paragraph, StyleId = id });
            styles.Append(new W.Style(new W.StyleName { Val = "List Paragraph" }) { Type = W.StyleValues.Paragraph, StyleId = "ListParagraph" });
            main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
        }

        private static void AddWordNumbering(MainDocumentPart main)
        {
            static W.Level Level(int index, W.NumberFormatValues format, string text) =>
                new(new W.StartNumberingValue { Val = 1 }, new W.NumberingFormat { Val = format }, new W.LevelText { Val = text }) { LevelIndex = index };
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                new W.AbstractNum(Level(0, W.NumberFormatValues.Bullet, "•"), Level(1, W.NumberFormatValues.Bullet, "o")) { AbstractNumberId = 0 },
                new W.AbstractNum(Level(0, W.NumberFormatValues.Decimal, "%1."), Level(1, W.NumberFormatValues.Decimal, "%2.")) { AbstractNumberId = 1 },
                new W.NumberingInstance(new W.AbstractNumId { Val = 0 }) { NumberID = 1 },
                new W.NumberingInstance(new W.AbstractNumId { Val = 1 }) { NumberID = 2 });
        }

        private static DW.Inline PictureInline(string imageId, string description) =>
            new(
                new DW.Extent { Cx = 476250, Cy = 476250 },
                new DW.DocProperties { Id = 1, Name = "Picture 1", Description = description },
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties { Id = 0, Name = "red.png" }, new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = imageId }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = 476250, Cy = 476250 }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }));

        // --- Excel ---

        private static byte[] ExcelBudget()
        {
            using var stream = new MemoryStream();
            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = doc.AddWorkbookPart();
                workbook.Workbook = new S.Workbook();
                var strings = new List<string>();
                int Shared(string text)
                {
                    var i = strings.IndexOf(text);
                    if (i < 0) { strings.Add(text); i = strings.Count - 1; }
                    return i;
                }
                S.Cell Text(string reference, string text) =>
                    new() { CellReference = reference, DataType = S.CellValues.SharedString, CellValue = new S.CellValue(Shared(text).ToString()) };
                S.Cell Number(string reference, string value, uint style = 0) =>
                    new() { CellReference = reference, StyleIndex = style, CellValue = new S.CellValue(value) };
                S.Row Row(uint index, params S.Cell[] cells) => new(cells) { RowIndex = index };

                // Style indexes: 0 general, 1 date (14), 2 percent (9), 3 custom "0.0%", 4 custom date-time.
                var styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new S.Stylesheet(
                    new S.NumberingFormats(
                        new S.NumberingFormat { NumberFormatId = 164, FormatCode = "0.0%" },
                        new S.NumberingFormat { NumberFormatId = 165, FormatCode = "dd/mm/yyyy hh:mm" }),
                    new S.Fonts(new S.Font()) { Count = 1 },
                    new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }), new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 })) { Count = 2 },
                    new S.Borders(new S.Border()) { Count = 1 },
                    new S.CellStyleFormats(new S.CellFormat()) { Count = 1 },
                    new S.CellFormats(
                        new S.CellFormat(),
                        new S.CellFormat { NumberFormatId = 14, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 9, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
                        new S.CellFormat { NumberFormatId = 165, ApplyNumberFormat = true }) { Count = 5 });

                var sheets = new S.Sheets();
                uint sheetId = 1;
                void AddSheet(string name, S.SheetData data, S.SheetStateValues? state = null)
                {
                    var part = workbook.AddNewPart<WorksheetPart>();
                    part.Worksheet = new S.Worksheet(data);
                    var sheet = new S.Sheet { Id = workbook.GetIdOfPart(part), SheetId = sheetId++, Name = name };
                    if (state != null) sheet.State = state;
                    sheets.Append(sheet);
                }

                AddSheet("Budget", new S.SheetData(
                    Row(1, Text("A1", "Item"), Text("B1", "Amount"), Text("D1", "Due"), Text("E1", "Share")),
                    Row(2, Text("A2", "Licences"), Number("B2", "1200"), Number("D2", "45000", 1), Number("E2", "0.4", 2)),
                    Row(3, Text("A3", "Travel | hotels"), Number("B3", "845.5"), Number("D3", "45031.75", 4), Number("E3", "0.2833", 3)),
                    Row(4, Text("A4", "Total"), new S.Cell { CellReference = "B4", CellFormula = new S.CellFormula("SUM(B2:B3)"), CellValue = new S.CellValue("2045.5") }),
                    Row(6, Text("A6", "Approved"), new S.Cell { CellReference = "B6", DataType = S.CellValues.Boolean, CellValue = new S.CellValue("1") }),
                    Row(7, new S.Cell { CellReference = "A7", DataType = S.CellValues.InlineString, InlineString = new S.InlineString(new S.Text("Inline text")) },
                        new S.Cell { CellReference = "B7", DataType = S.CellValues.Error, CellValue = new S.CellValue("#DIV/0!") }),
                    Row(8, Text("A8", "Two\nlines"))));
                AddSheet("Été", new S.SheetData(
                    Row(1, Text("A1", "Mois"), Text("B1", "Ventes")),
                    Row(2, Text("A2", "Juillet"), Number("B2", "310")),
                    Row(3, Text("A3", "Août"), Number("B3", "275"))));
                AddSheet("Scratch", new S.SheetData(Row(1, Text("A1", "not for readers"))), S.SheetStateValues.Hidden);
                AddSheet("Blank", new S.SheetData());

                workbook.Workbook.Append(sheets);
                var table = workbook.AddNewPart<SharedStringTablePart>();
                table.SharedStringTable = new S.SharedStringTable(strings.Select(s => new S.SharedStringItem(new S.Text(s))));
                workbook.Workbook.Save();
            }
            return stream.ToArray();
        }

        // --- PowerPoint ---

        private static byte[] PowerPointReview()
        {
            using var stream = new MemoryStream();
            using (var doc = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
            {
                var presentation = doc.AddPresentationPart();
                presentation.Presentation = new P.Presentation();

                var theme = presentation.AddNewPart<ThemePart>();
                theme.Theme = BuildTheme();
                var master = presentation.AddNewPart<SlideMasterPart>();
                var layout = master.AddNewPart<SlideLayoutPart>();
                master.AddPart(theme);
                layout.AddPart(master);
                layout.SlideLayout = new P.SlideLayout(new P.CommonSlideData(new P.ShapeTree(GroupHeader()))) { Type = P.SlideLayoutValues.Title };
                master.SlideMaster = new P.SlideMaster(
                    new P.CommonSlideData(new P.ShapeTree(GroupHeader())),
                    new P.ColorMap { Background1 = A.ColorSchemeIndexValues.Light1, Text1 = A.ColorSchemeIndexValues.Dark1, Background2 = A.ColorSchemeIndexValues.Light2, Text2 = A.ColorSchemeIndexValues.Dark2, Accent1 = A.ColorSchemeIndexValues.Accent1, Accent2 = A.ColorSchemeIndexValues.Accent2, Accent3 = A.ColorSchemeIndexValues.Accent3, Accent4 = A.ColorSchemeIndexValues.Accent4, Accent5 = A.ColorSchemeIndexValues.Accent5, Accent6 = A.ColorSchemeIndexValues.Accent6, Hyperlink = A.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink },
                    new P.SlideLayoutIdList(new P.SlideLayoutId { Id = 2147483649U, RelationshipId = master.GetIdOfPart(layout) }));

                var notesMaster = presentation.AddNewPart<NotesMasterPart>();
                var notesTheme = notesMaster.AddNewPart<ThemePart>();
                notesTheme.Theme = BuildTheme();
                notesMaster.NotesMaster = new P.NotesMaster(
                    new P.CommonSlideData(new P.ShapeTree(GroupHeader())),
                    new P.ColorMap { Background1 = A.ColorSchemeIndexValues.Light1, Text1 = A.ColorSchemeIndexValues.Dark1, Background2 = A.ColorSchemeIndexValues.Light2, Text2 = A.ColorSchemeIndexValues.Dark2, Accent1 = A.ColorSchemeIndexValues.Accent1, Accent2 = A.ColorSchemeIndexValues.Accent2, Accent3 = A.ColorSchemeIndexValues.Accent3, Accent4 = A.ColorSchemeIndexValues.Accent4, Accent5 = A.ColorSchemeIndexValues.Accent5, Accent6 = A.ColorSchemeIndexValues.Accent6, Hyperlink = A.ColorSchemeIndexValues.Hyperlink, FollowedHyperlink = A.ColorSchemeIndexValues.FollowedHyperlink });

                var slideIds = new P.SlideIdList();
                uint slideId = 256;
                // A shape is an element, or a function that builds it once the slide part exists (a picture).
                SlidePart AddSlide(bool hidden, params object[] shapes)
                {
                    var part = presentation.AddNewPart<SlidePart>();
                    part.AddPart(layout);
                    var elements = shapes.Select(shape => shape is Func<SlidePart, OpenXmlElement> build ? build(part) : (OpenXmlElement)shape);
                    part.Slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree(GroupHeader().Concat(elements))));
                    if (hidden) part.Slide.Show = false;
                    slideIds.Append(new P.SlideId { Id = slideId++, RelationshipId = presentation.GetIdOfPart(part) });
                    return part;
                }

                // 1. Title slide
                AddSlide(false,
                    TextShape(2, "Title", P.PlaceholderValues.CenteredTitle, 0, 0, Paragraphs("Caret quarterly review")),
                    TextShape(3, "Subtitle", P.PlaceholderValues.SubTitle, 0, 1000000, Paragraphs("Prepared for the operations team")));

                // 2. Bullets with two levels, a table and a footer that must be dropped
                var second = AddSlide(false,
                    TextShape(2, "Title", P.PlaceholderValues.Title, 0, 0, Paragraphs("Highlights")),
                    TextShape(3, "Content", P.PlaceholderValues.Body, 0, 1000000, Paragraphs("Revenue up 12%", ("Driven by renewals", 1), "Two new regions opened")),
                    TableFrame(4, 0, 3000000),
                    TextShape(5, "Footer", P.PlaceholderValues.Footer, 0, 6000000, Paragraphs("Confidential")),
                    TextShape(6, "Slide number", P.PlaceholderValues.SlideNumber, 5000000, 6000000, Paragraphs("2")));
                AddNotes(presentation, notesMaster, second, "Mention the two new regions.\nAsk for questions.");

                // 3. No title: a text box, a picture
                var third = AddSlide(false,
                    TextBox(2, "Note", 4000000, 1000000, Paragraphs("Right column, read second")),
                    TextBox(3, "Note left", 0, 1000000, Paragraphs("Left column, read first")),
                    PictureShape(4, 0, 2500000));
                _ = third;

                // 4. Hidden slide
                AddSlide(true, TextShape(2, "Title", P.PlaceholderValues.Title, 0, 0, Paragraphs("Backup slide, hidden")));

                // 5. French, with accents
                AddSlide(false,
                    TextShape(2, "Title", P.PlaceholderValues.Title, 0, 0, Paragraphs("Résultats du trimestre")),
                    TextShape(3, "Content", P.PlaceholderValues.Body, 0, 1000000, Paragraphs("Chiffre d’affaires : +12 %", "Prochaine étape : l’été")));

                // 6. A title broken over two lines, a link, an indented list with nothing above it, a chart, a diagram and a comment
                var sixth = AddSlide(false,
                    TextShape(2, "Title", P.PlaceholderValues.Title, 0, 0, TitleWithBreak("Chart and", "diagram")),
                    LinkBox(3, "Link", 0, 1000000, "Read the plan", "https://example.com/plan"),
                    TextBox(4, "Indented", 4000000, 1000000, Paragraphs(("Child one", 1), ("Child two", 1), ("Grandchild", 2))),
                    ChartFrame(5, 0, 2000000),
                    ChartFrame(7, 0, 3000000, titled: false),
                    DiagramFrame(6, 0, 4000000));
                var authors = presentation.AddNewPart<CommentAuthorsPart>();
                authors.CommentAuthorList = new P.CommentAuthorList(new P.CommentAuthor { Id = 0, Name = "Reviewer", Initials = "R", LastIndex = 1, ColorIndex = 0 });
                var comments = sixth.AddNewPart<SlideCommentsPart>();
                comments.CommentList = new P.CommentList(new P.Comment(new P.Position { X = 10, Y = 10 }, new P.Text("Check the Q3 figure."))
                { AuthorId = 0, DateTime = new DateTime(2024, 5, 6, 9, 0, 0), Index = 1 });

                presentation.Presentation.Append(
                    new P.SlideMasterIdList(new P.SlideMasterId { Id = 2147483648U, RelationshipId = presentation.GetIdOfPart(master) }),
                    new P.NotesMasterIdList(new P.NotesMasterId { Id = presentation.GetIdOfPart(notesMaster) }),
                    slideIds,
                    new P.SlideSize { Cx = 9144000, Cy = 6858000, Type = P.SlideSizeValues.Screen4x3 },
                    new P.NotesSize { Cx = 6858000, Cy = 9144000 });
                presentation.Presentation.Save();
            }
            return stream.ToArray();
        }

        private static IEnumerable<OpenXmlElement> GroupHeader()
        {
            yield return new P.NonVisualGroupShapeProperties(new P.NonVisualDrawingProperties { Id = 1, Name = "" }, new P.NonVisualGroupShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties());
            yield return new P.GroupShapeProperties(new A.TransformGroup());
        }

        private static P.Shape TextShape(uint id, string name, P.PlaceholderValues placeholder, long x, long y, A.TextBody body) =>
            new(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                    new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = placeholder })),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 8000000, Cy = 800000 })),
                new P.TextBody(body.ChildElements.Select(c => c.CloneNode(true))) { });

        private static P.Shape TextBox(uint id, string name, long x, long y, A.TextBody body) =>
            new(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }) { TextBox = true },
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 3500000, Cy = 600000 })),
                new P.TextBody(body.ChildElements.Select(c => c.CloneNode(true))));

        // Paragraphs: a string is a paragraph at level 0, a (text, level) tuple one at that level.
        private static A.TextBody Paragraphs(params object[] items)
        {
            var body = new A.TextBody(new A.BodyProperties(), new A.ListStyle());
            foreach (var item in items)
            {
                var (text, level) = item is ValueTuple<string, int> t ? t : ((string)item, 0);
                var paragraph = new A.Paragraph();
                if (level > 0) paragraph.Append(new A.ParagraphProperties { Level = level });
                paragraph.Append(new A.Run(new A.RunProperties { Language = "en-US" }, new A.Text(text)));
                body.Append(paragraph);
            }
            return body;
        }

        private static A.TextBody TitleWithBreak(string first, string second) =>
            new(new A.BodyProperties(), new A.ListStyle(),
                new A.Paragraph(new A.Run(new A.RunProperties { Language = "en-US" }, new A.Text(first + " ")), new A.Break(), new A.Run(new A.RunProperties { Language = "en-US" }, new A.Text(second))));

        private static Func<SlidePart, OpenXmlElement> LinkBox(uint id, string name, long x, long y, string text, string url) => slide =>
        {
            var link = slide.AddHyperlinkRelationship(new Uri(url), true);
            return new P.Shape(
                new P.NonVisualShapeProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = name },
                    new P.NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }) { TextBox = true },
                    new P.ApplicationNonVisualDrawingProperties()),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 3500000, Cy = 600000 })),
                new P.TextBody(new A.BodyProperties(), new A.ListStyle(),
                    new A.Paragraph(new A.Run(new A.RunProperties(new A.HyperlinkOnClick { Id = link.Id }) { Language = "en-US" }, new A.Text(text)))));
        };

        // A bar chart with two series over three categories; the data sits in the chart part's cache.
        private static Func<SlidePart, OpenXmlElement> ChartFrame(uint id, long x, long y, bool titled = true) => slide =>
        {
            var chartPart = slide.AddNewPart<ChartPart>();
            static C.StringReference Strings(string formula, params string[] values)
            {
                var cache = new C.StringCache(new C.PointCount { Val = (uint)values.Length });
                for (var i = 0; i < values.Length; i++) cache.Append(new C.StringPoint(new C.NumericValue(values[i])) { Index = (uint)i });
                return new C.StringReference(new C.Formula(formula), cache);
            }
            static C.NumberReference Numbers(string formula, params string[] values)
            {
                var cache = new C.NumberingCache(new C.FormatCode("General"), new C.PointCount { Val = (uint)values.Length });
                for (var i = 0; i < values.Length; i++) cache.Append(new C.NumericPoint(new C.NumericValue(values[i])) { Index = (uint)i });
                return new C.NumberReference(new C.Formula(formula), cache);
            }
            C.BarChartSeries Series(uint index, string name, string column, params string[] values) =>
                new(new C.Index { Val = index }, new C.Order { Val = index },
                    new C.SeriesText(Strings("Sheet1!$" + column + "$1", name)),
                    new C.CategoryAxisData(Strings("Sheet1!$A$2:$A$4", "North", "South", "East")),
                    new C.Values(Numbers("Sheet1!$" + column + "$2:$" + column + "$4", values)));
            C.Title Title(string text) => new(new C.ChartText(new C.RichText(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text))))));
            var chart = new C.Chart();
            if (titled) chart.Append(Title("Sales by region"));
            else chart.Append(new C.AutoTitleDeleted { Val = true });
            chart.Append(
                new C.PlotArea(
                    new C.Layout(),
                    new C.BarChart(
                        new C.BarDirection { Val = C.BarDirectionValues.Column },
                        new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
                        Series(0, "2023", "B", "1200", "900.5", "430"),
                        Series(1, "2024", "C", "1350", "880", "510.25"),
                        new C.AxisId { Val = 111U }, new C.AxisId { Val = 222U }),
                    new C.CategoryAxis(new C.AxisId { Val = 111U }, new C.Scaling(), new C.Delete { Val = false }, new C.AxisPosition { Val = C.AxisPositionValues.Bottom }, new C.CrossingAxis { Val = 222U }),
                    new C.ValueAxis(new C.AxisId { Val = 222U }, new C.Scaling(), new C.Delete { Val = false }, new C.AxisPosition { Val = C.AxisPositionValues.Left }, Title("Revenue (USD)"), new C.CrossingAxis { Val = 111U })));
            chartPart.ChartSpace = new C.ChartSpace(chart);
            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Chart" }, new P.NonVisualGraphicFrameDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 6000000, Cy = 1800000 }),
                new A.Graphic(new A.GraphicData(new C.ChartReference { Id = slide.GetIdOfPart(chartPart) }) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" }));
        };

        // A SmartArt list with two boxes, the second with a sub-point.
        private static Func<SlidePart, OpenXmlElement> DiagramFrame(uint id, long x, long y) => slide =>
        {
            var dataPart = slide.AddNewPart<DiagramDataPart>();
            var layoutPart = slide.AddNewPart<DiagramLayoutDefinitionPart>();
            var stylePart = slide.AddNewPart<DiagramStylePart>();
            var colorsPart = slide.AddNewPart<DiagramColorsPart>();
            static D.Point Node(string modelId, string text) =>
                new(new D.PropertySet(), new D.ShapeProperties(), new D.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text))))) { ModelId = modelId };
            dataPart.DataModelRoot = new D.DataModelRoot(
                new D.PointList(
                    new D.Point(new D.PropertySet(), new D.ShapeProperties(), new D.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.EndParagraphRunProperties { Language = "en-US" }))) { ModelId = "{00000000-0000-0000-0000-000000000001}", Type = D.PointValues.Document },
                    Node("{00000000-0000-0000-0000-000000000002}", "Plan"),
                    Node("{00000000-0000-0000-0000-000000000003}", "Build"),
                    Node("{00000000-0000-0000-0000-000000000004}", "Test it first")),
                new D.ConnectionList(
                    new D.Connection { ModelId = "{00000000-0000-0000-0000-000000000011}", SourceId = "{00000000-0000-0000-0000-000000000001}", DestinationId = "{00000000-0000-0000-0000-000000000002}", SourcePosition = 0U, DestinationPosition = 0U },
                    new D.Connection { ModelId = "{00000000-0000-0000-0000-000000000012}", SourceId = "{00000000-0000-0000-0000-000000000001}", DestinationId = "{00000000-0000-0000-0000-000000000003}", SourcePosition = 1U, DestinationPosition = 0U },
                    new D.Connection { ModelId = "{00000000-0000-0000-0000-000000000013}", SourceId = "{00000000-0000-0000-0000-000000000003}", DestinationId = "{00000000-0000-0000-0000-000000000004}", SourcePosition = 0U, DestinationPosition = 0U }));
            layoutPart.LayoutDefinition = new D.LayoutDefinition(new D.Title { Val = "" }, new D.Description { Val = "" }, new D.CategoryList(), new D.SampleData(), new D.StyleData(), new D.ColorData(), new D.LayoutNode()) { UniqueId = "urn:test/layout" };
            stylePart.StyleDefinition = new D.StyleDefinition(new D.Title { Val = "" }, new D.Description { Val = "" }, new D.CategoryList(), new D.Scene3D(new A.Camera { Preset = A.PresetCameraValues.OrthographicFront }, new A.LightRig { Rig = A.LightRigValues.ThreePoints, Direction = A.LightRigDirectionValues.Top }), new D.StyleLabel { Name = "node0" }) { UniqueId = "urn:test/style" };
            colorsPart.ColorsDefinition = new D.ColorsDefinition(new D.Title { Val = "" }, new D.Description { Val = "" }, new D.CategoryList(), new D.ColorTransformStyleLabel { Name = "node0" }) { UniqueId = "urn:test/colors" };
            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Diagram" }, new P.NonVisualGraphicFrameDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 6000000, Cy = 1800000 }),
                new A.Graphic(new A.GraphicData(new D.RelationshipIds
                {
                    DataPart = slide.GetIdOfPart(dataPart),
                    LayoutPart = slide.GetIdOfPart(layoutPart),
                    StylePart = slide.GetIdOfPart(stylePart),
                    ColorPart = slide.GetIdOfPart(colorsPart),
                }) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/diagram" }));
        };

        private static P.GraphicFrame TableFrame(uint id, long x, long y)
        {
            static A.TableCell Cell(string text) => new(new A.TextBody(new A.BodyProperties(), new A.ListStyle(), new A.Paragraph(new A.Run(new A.Text(text)))), new A.TableCellProperties());
            static A.TableRow Row(params string[] cells) => new(cells.Select(Cell)) { Height = 370840 };
            return new P.GraphicFrame(
                new P.NonVisualGraphicFrameProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Table" }, new P.NonVisualGraphicFrameDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.Transform(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 6000000, Cy = 741680 }),
                new A.Graphic(new A.GraphicData(new A.Table(
                    new A.TableProperties(),
                    new A.TableGrid(new A.GridColumn { Width = 3000000 }, new A.GridColumn { Width = 3000000 }),
                    Row("Region", "Growth"),
                    Row("North", "+12%")))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
        }

        private static Func<SlidePart, OpenXmlElement> PictureShape(uint id, long x, long y) => slide =>
        {
            var image = slide.AddImagePart(ImagePartType.Png);
            image.FeedData(new MemoryStream(Png));
            return new P.Picture(
                new P.NonVisualPictureProperties(new P.NonVisualDrawingProperties { Id = id, Name = "Picture", Description = "A small red square" }, new P.NonVisualPictureDrawingProperties(), new P.ApplicationNonVisualDrawingProperties()),
                new P.BlipFill(new A.Blip { Embed = slide.GetIdOfPart(image) }, new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(new A.Transform2D(new A.Offset { X = x, Y = y }, new A.Extents { Cx = 476250, Cy = 476250 }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        };

        private static void AddNotes(PresentationPart presentation, NotesMasterPart notesMaster, SlidePart slide, string text)
        {
            var notes = slide.AddNewPart<NotesSlidePart>();
            notes.AddPart(notesMaster);
            notes.AddPart(slide);
            notes.NotesSlide = new P.NotesSlide(new P.CommonSlideData(new P.ShapeTree(GroupHeader().Concat(new OpenXmlElement[]
            {
                TextShape(2, "Notes Placeholder", P.PlaceholderValues.Body, 0, 0, Paragraphs(text.Split('\n').Cast<object>().ToArray())),
            }))));
        }

        private static A.Theme BuildTheme()
        {
            static A.FontScheme Fonts(string name) => new(
                new A.MajorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" }),
                new A.MinorFont(new A.LatinFont { Typeface = "Calibri" }, new A.EastAsianFont { Typeface = "" }, new A.ComplexScriptFont { Typeface = "" })) { Name = name };
            static A.SolidFill Solid() => new(new A.SchemeColor { Val = A.SchemeColorValues.PhColor });
            static A.Outline Line() => new(Solid()) { Width = 9525 };
            return new A.Theme(new A.ThemeElements(
                new A.ColorScheme(
                    new A.Dark1Color(new A.SystemColor { Val = A.SystemColorValues.WindowText, LastColor = "000000" }),
                    new A.Light1Color(new A.SystemColor { Val = A.SystemColorValues.Window, LastColor = "FFFFFF" }),
                    new A.Dark2Color(new A.RgbColorModelHex { Val = "1F497D" }),
                    new A.Light2Color(new A.RgbColorModelHex { Val = "EEECE1" }),
                    new A.Accent1Color(new A.RgbColorModelHex { Val = "4F81BD" }),
                    new A.Accent2Color(new A.RgbColorModelHex { Val = "C0504D" }),
                    new A.Accent3Color(new A.RgbColorModelHex { Val = "9BBB59" }),
                    new A.Accent4Color(new A.RgbColorModelHex { Val = "8064A2" }),
                    new A.Accent5Color(new A.RgbColorModelHex { Val = "4BACC6" }),
                    new A.Accent6Color(new A.RgbColorModelHex { Val = "F79646" }),
                    new A.Hyperlink(new A.RgbColorModelHex { Val = "0000FF" }),
                    new A.FollowedHyperlinkColor(new A.RgbColorModelHex { Val = "800080" })) { Name = "Office" },
                Fonts("Office"),
                new A.FormatScheme(
                    new A.FillStyleList(Solid(), Solid(), Solid()),
                    new A.LineStyleList(Line(), Line(), Line()),
                    new A.EffectStyleList(new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList()), new A.EffectStyle(new A.EffectList())),
                    new A.BackgroundFillStyleList(Solid(), Solid(), Solid())) { Name = "Office" })) { Name = "Office Theme" };
        }

        // --- PDF ---

        // A hand-written PDF: two pages of Helvetica text laid out the way a report would be (running
        // header and page number, a title, headings, a paragraph that runs over the page break, a hyphenated
        // word, a list and a table).
        private static byte[] PdfArticle()
        {
            var pages = new List<string>();

            var first = new StringBuilder();
            Line(first, 2, 72, 805, 9, "ACME Research Notes");
            Line(first, 2, 72, 720, 22, "Coffee Supply Report");
            Line(first, 1, 72, 690, 11, "This report looks at how the supply of coffee changed during the last year and what");
            Line(first, 1, 72, 675, 11, "buyers can do about it. The main finding is that prices are set by a few large exam-");
            Line(first, 1, 72, 660, 11, "ple markets, while the smaller producers follow them with a delay of several weeks.");
            Line(first, 2, 72, 625, 15, "Main findings");
            Line(first, 1, 72, 600, 11, "\\225");
            Line(first, 1, 90, 600, 11, "Prices rose in every region except the north");
            Line(first, 1, 72, 585, 11, "\\225");
            Line(first, 1, 90, 585, 11, "Shipping times are back to normal");
            Line(first, 1, 72, 570, 11, "\\225");
            Line(first, 1, 90, 570, 11, "Stocks are still lower than a year ago");
            Line(first, 2, 72, 530, 15, "Prices by region");
            foreach (var (y, a, b, c) in new[] { (505, "Region", "Price", "Change"), (490, "North", "3.10", "-2%"), (475, "South", "3.80", "+9%"), (460, "East", "3.55", "+4%") })
            {
                Line(first, 1, 72, y, 11, a);
                Line(first, 1, 250, y, 11, b);
                Line(first, 1, 400, y, 11, c);
            }
            Line(first, 1, 72, 420, 11, "The tables above show the average price paid by buyers in each region, and the paragraph");
            Line(first, 1, 72, 405, 11, "that follows carries on to the next page without any closing punctuation so that it");
            Line(first, 1, 300, 40, 9, "1");
            pages.Add(first.ToString());

            var second = new StringBuilder();
            Line(second, 2, 72, 805, 9, "ACME Research Notes");
            Line(second, 1, 72, 720, 11, "continues here, in lower case, and ends with a full stop.");
            Line(second, 2, 72, 685, 15, "Conclusion");
            Line(second, 1, 72, 660, 11, "Buyers should sign longer contracts with small producers. Smaller producers");
            Line(second, 1, 72, 645, 11, "react slowly, so a fixed price protects both sides.");
            Line(second, 1, 300, 40, 9, "2");
            pages.Add(second.ToString());

            // Page 3: a title across the page, then two columns that are read one after the other.
            var third = new StringBuilder();
            Line(third, 2, 72, 805, 9, "ACME Research Notes");
            Line(third, 2, 72, 750, 15, "Appendix: the harvest");
            var left = new[]
            {
                "The first column tells how the harvest", "began in the spring and how the early", "rains helped the plants grow well before",
                "the summer heat arrived in the valley.", "Workers picked the beans by hand and", "sorted them on long tables, one by one,",
                "so that only the ripest fruit went into", "the bags that were sent to the port.",
            };
            var right = new[]
            {
                "The second column explains what came", "next: the bags travelled by truck and", "then by ship, and the whole trip took",
                "about five weeks when the weather was", "good. Buyers in the north received the", "coffee first, while the southern markets",
                "waited a little longer for the same", "shipment because of the port queues.",
            };
            for (var i = 0; i < left.Length; i++)
            {
                Line(third, 1, 72, 715 - i * 15, 11, left[i]);
                Line(third, 1, 330, 715 - i * 15, 11, right[i]);
            }
            Line(third, 1, 300, 40, 9, "3");
            pages.Add(third.ToString());

            return BuildPdf(pages);
        }

        private static void Line(StringBuilder page, int font, int x, int y, int size, string text) =>
            page.Append($"BT /F{font} {size} Tf {x} {y} Td ({text.Replace("(", "\\(").Replace(")", "\\)")}) Tj ET\n");

        private static byte[] BuildPdf(IReadOnlyList<string> pages)
        {
            var objects = new List<string>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            var kids = string.Join(" ", pages.Select((_, i) => $"{6 + i * 2} 0 R"));
            objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
            objects.Add("<< /Producer (Caret converter tests) >>");
            for (var i = 0; i < pages.Count; i++)
            {
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {7 + i * 2} 0 R >>");
                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(pages[i])} >>\nstream\n{pages[i]}endstream");
            }
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
            pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            return Encoding.ASCII.GetBytes(pdf.ToString());
        }

        // --- Emails ---

        // Legacy code pages are what mails from before UTF-8 are written in (ISO-8859-2 for Hungarian, Shift_JIS for Japanese); .NET
        // only knows them through this provider.
        private static Encoding Legacy(string name)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(name);
        }

        // ISO-8859-2, quoted-printable, with the subject as an encoded word.
        private static byte[] EmailHungarianEml() => Encoding.ASCII.GetBytes(string.Join("\r\n", new[]
        {
            "From: Kov\u00e1cs B\u00e9la <bela.kovacs@acme.example>".Replace("\u00e1", "a").Replace("\u00e9", "e"),
            "To: Anna Nowak <anna.nowak@client.example>",
            "Subject: =?iso-8859-2?Q?=C1rv=EDzt=FBr=F5_t=FCk=F6rf=FAr=F3g=E9p?=",
            "Date: Tue, 04 Mar 2025 09:30:00 +0000",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=iso-8859-2",
            "Content-Transfer-Encoding: quoted-printable",
            "",
            "=C1rv=EDzt=FBr=F5 t=FCk=F6rf=FAr=F3g=E9p: a megbesz=E9l=E9s cs=FCt=F6rt=F6k=F6n lesz.",
            "",
        }));

        // Shift_JIS, 8 bit, with the subject as an encoded word.
        private static byte[] EmailShiftJisEml()
        {
            var shiftJis = Legacy("shift_jis");
            var head = Encoding.ASCII.GetBytes(string.Join("\r\n", new[]
            {
                "From: Tanaka <tanaka@acme.example>",
                "To: Anna Nowak <anna.nowak@client.example>",
                "Subject: =?shift_jis?B?" + Convert.ToBase64String(shiftJis.GetBytes("\u30c6\u30b9\u30c8")) + "?=",
                "Date: Tue, 04 Mar 2025 09:30:00 +0000",
                "MIME-Version: 1.0",
                "Content-Type: text/plain; charset=shift_jis",
                "Content-Transfer-Encoding: 8bit",
                "",
                "",
            }));
            return head.Concat(shiftJis.GetBytes("\u3053\u3093\u306b\u3061\u306f\u3001\u4e16\u754c\u3002\r\n")).ToArray();
        }

        // An Outlook message whose text is 8-bit, in the Japanese ANSI page (the file names no message code page, only its locale 1041
        // and the page it travelled in, ISO-2022-JP).
        private static byte[] OutlookMsgJapanese()
        {
            var shiftJis = Legacy("shift_jis");
            using var stream = new MemoryStream();
            using (var root = RootStorage.Create(stream, OpenMcdf.Version.V3, StorageModeFlags.LeaveOpen))
            {
                void Text(string tag, string value)
                {
                    using var s = root.CreateStream($"__substg1.0_{tag}001E");
                    s.Write(shiftJis.GetBytes(value));
                }
                Text("0037", "\u65e5\u672c\u8a9e\u306e\u4ef6\u540d");
                Text("0C1A", "\u7530\u4e2d");
                Text("1000", "\u65e5\u672c\u8a9e\u306e\u672c\u6587\u3067\u3059\u3002");
                using var props = root.CreateStream("__properties_version1.0");
                props.Write(Properties(32, (0x3FDE, 50220), (0x3FF1, 1041)));
            }
            return stream.ToArray();
        }

        private static byte[] EmailThreadEml() => Encoding.UTF8.GetBytes(string.Join("\r\n", new[]
        {
            "From: Jan Kowalski <jan.kowalski@acme.example>",
            "To: Anna Nowak <anna.nowak@client.example>",
            "Cc: Marie Dupont <marie.dupont@client.example>",
            "Subject: RE: Budget 2026",
            "Date: Tue, 04 Mar 2025 09:30:00 +0000",
            "Message-ID: <thread-2@acme.example>",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            "",
            "Hi Anna,",
            "",
            "Thanks, the figures look fine. Please transfer the deposit to",
            "GB82 WEST 1234 5698 7654 32 before Friday, or call me on +48 601 234 567.",
            "",
            "Best regards,",
            "Jan Kowalski",
            "Senior Analyst | ACME Ltd",
            "Tel: +48 22 123 45 67",
            "www.acme.example",
            "",
            "From: Anna Nowak <anna.nowak@client.example>",
            "Sent: Monday, March 3, 2025 4:10 PM",
            "To: Jan Kowalski <jan.kowalski@acme.example>",
            "Subject: Budget 2026",
            "",
            "Hello Jan,",
            "",
            "Please find the draft budget below. Could you confirm the amounts?",
            "",
            "Kind regards,",
            "Anna Nowak",
            "Client Services",
            "",
            "-----Original Message-----",
            "From: Jan Kowalski <jan.kowalski@acme.example>",
            "Sent: Friday, February 28, 2025 11:00 AM",
            "To: Anna Nowak <anna.nowak@client.example>",
            "Subject: Budget 2026",
            "",
            "Dear Anna,",
            "",
            "Here is the first version. Marie Dupont has seen it too.",
            "",
            "Regards,",
            "Jan",
            "",
        }));

        private static byte[] EmailWithAttachmentEml()
        {
            var boundary = "caret-boundary-1";
            var csv = Convert.ToBase64String(Encoding.UTF8.GetBytes("item,amount\r\nCoffee,12\r\nTea,7\r\n"), Base64FormattingOptions.InsertLineBreaks);
            var png = Convert.ToBase64String(Png, Base64FormattingOptions.InsertLineBreaks);
            return Encoding.UTF8.GetBytes(string.Join("\r\n", new[]
            {
                "From: Anna Nowak <anna.nowak@client.example>",
                "To: Jan Kowalski <jan.kowalski@acme.example>",
                "Subject: Order list",
                "Date: Wed, 05 Mar 2025 08:00:00 +0000",
                "MIME-Version: 1.0",
                $"Content-Type: multipart/mixed; boundary=\"{boundary}\"",
                "",
                $"--{boundary}",
                "Content-Type: text/plain; charset=utf-8",
                "",
                "Hello Jan,",
                "",
                "The order list is attached, with the logo.",
                "",
                "Thanks,",
                "Anna",
                $"--{boundary}",
                "Content-Type: text/csv; name=\"order.csv\"",
                "Content-Transfer-Encoding: base64",
                "Content-Disposition: attachment; filename=\"order.csv\"",
                "",
                csv,
                $"--{boundary}",
                "Content-Type: image/png; name=\"logo.png\"",
                "Content-Transfer-Encoding: base64",
                "Content-Disposition: attachment; filename=\"logo.png\"",
                "",
                png,
                $"--{boundary}--",
                "",
            }));
        }

        // An Outlook item: an OLE compound file of MAPI properties.
        private static byte[] OutlookMsg()
        {
            using var stream = new MemoryStream();
            using (var root = RootStorage.Create(stream, OpenMcdf.Version.V3, StorageModeFlags.LeaveOpen))
            {
                void Text(Storage storage, string tag, string value)
                {
                    using var s = storage.CreateStream($"__substg1.0_{tag}001F");
                    s.Write(Encoding.Unicode.GetBytes(value));
                }
                Text(root, "0037", "FW: Site visit on Thursday");
                Text(root, "0C1A", "Jan Kowalski");
                Text(root, "5D01", "jan.kowalski@acme.example");
                Text(root, "0E04", "Anna Nowak");
                Text(root, "007D", "Received: from mail.example\r\nDate: Thu, 06 Mar 2025 10:15:00 +0000\r\nSubject: FW: Site visit on Thursday\r\n\r\n");
                Text(root, "1000", "Hi Anna,\r\n\r\nThe site visit is on Thursday at 10:00. Please bring the safety forms.\r\n\r\nBest regards,\r\nJan Kowalski\r\nSenior Analyst\r\nTel: +48 22 123 45 67\r\n");

                var recipient = root.CreateStorage("__recip_version1.0_#00000000");
                Text(recipient, "3001", "Anna Nowak");
                Text(recipient, "39FE", "anna.nowak@client.example");
                using (var props = recipient.CreateStream("__properties_version1.0"))
                    props.Write(Properties(8, (0x0C15, 1)));
                using (var props = root.CreateStream("__properties_version1.0"))
                    props.Write(Properties(32, (0x3FFD, 65001)));
            }
            return stream.ToArray();
        }

        // A property stream: a header, then one 16-byte entry per fixed-width property (type PT_LONG).
        private static byte[] Properties(int headerSize, params (int Tag, int Value)[] longs)
        {
            using var buffer = new MemoryStream();
            buffer.Write(new byte[headerSize]);
            using var writer = new BinaryWriter(buffer);
            foreach (var (tag, value) in longs)
            {
                writer.Write((uint)((tag << 16) | 0x0003));
                writer.Write(6u);
                writer.Write(value);
                writer.Write(0);
            }
            return buffer.ToArray();
        }
    }
}
