using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Conversion;
using Typedown.WinUI.Services.Export;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Caret.ConverterTests
{
    // File > Export > Word: Markdown goes in, a .docx comes out. The main check is the round trip: what Caret writes, the Word
    // converter of Caret reads back as the same Markdown. The rest looks at the file itself (styles, lists, tables) and has it
    // checked by the Open XML validator.
    public class WordExportTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        // 1x1 PNG
        private const string TinyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

        private (string Path, WordExportResult Result) Export(string markdown, Action<WordExportOptions> configure = null)
        {
            var options = new WordExportOptions { BaseFolder = work };
            configure?.Invoke(options);
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            var result = WordExporter.ExportToFile(markdown, path, options);
            AssertValid(path); // every file a test makes goes through the Open XML validator
            return (path, result);
        }

        // Exported, then read back the way a .docx from anywhere is read.
        private string RoundTrip(string markdown)
        {
            var (path, _) = Export(markdown);
            var images = Path.Combine(work, "images" + Guid.NewGuid().ToString("N").Substring(0, 6));
            var converted = DocumentConverter.Convert(path, new ConversionOptions { ImageDirectory = images, ImageLinkPrefix = "images" });
            return converted.Markdown.Replace("\r\n", "\n").Trim();
        }

        private static void AssertValid(string path)
        {
            using var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }

        private static List<W.Paragraph> Paragraphs(string path)
        {
            using var doc = WordprocessingDocument.Open(path, false);
            return doc.MainDocumentPart.Document.Body.Descendants<W.Paragraph>().Select(p => (W.Paragraph)p.CloneNode(true)).ToList();
        }

        private static string StyleOf(W.Paragraph p) => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

        [Fact]
        public void Headings_and_paragraphs_round_trip()
        {
            const string md = "# Title\n\nFirst paragraph.\n\n## Section\n\nSecond paragraph.\n\n### Part\n\nThird.";
            Assert.Equal(md, RoundTrip(md));
        }

        [Fact]
        public void Headings_use_the_heading_styles_of_Word()
        {
            var (path, _) = Export("# A\n\n## B\n\n###### F\n\ntext");
            var styles = Paragraphs(path).Select(StyleOf).ToList();
            Assert.Equal(new[] { "Heading1", "Heading2", "Heading6", null }, styles);
        }

        [Fact]
        public void Inline_formatting_and_links_round_trip()
        {
            const string md = "A **bold**, *italic*, ~~gone~~ and `code` word with a [link](https://example.com/page).";
            Assert.Equal(md, RoundTrip(md));
        }

        [Fact]
        public void Bold_and_italic_together_and_nested()
        {
            var (path, _) = Export("***both*** and **bold with *italic* inside**");
            using var doc = WordprocessingDocument.Open(path, false);
            var runs = doc.MainDocumentPart.Document.Body.Descendants<W.Run>().ToDictionary(r => r.InnerText, r => r.RunProperties);
            Assert.NotNull(runs["both"].Bold);
            Assert.NotNull(runs["both"].Italic);
            Assert.NotNull(runs["bold with "].Bold);
            Assert.Null(runs["bold with "].Italic);
            Assert.NotNull(runs["italic"].Bold);
            Assert.NotNull(runs["italic"].Italic);
            Assert.Null(runs[" and "]);
        }

        [Fact]
        public void Subscript_superscript_and_underline_tags()
        {
            var (path, _) = Export("H~2~O and x^2^ and <u>under</u>");
            using var doc = WordprocessingDocument.Open(path, false);
            var runs = doc.MainDocumentPart.Document.Body.Descendants<W.Run>().ToList();
            Assert.Contains(runs, r => r.RunProperties?.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Subscript && r.InnerText == "2");
            Assert.Contains(runs, r => r.RunProperties?.VerticalTextAlignment?.Val?.Value == W.VerticalPositionValues.Superscript && r.InnerText == "2");
            Assert.Contains(runs, r => r.RunProperties?.Underline != null && r.InnerText == "under");
            AssertValid(path);
        }

        [Fact]
        public void Bullet_and_numbered_lists_round_trip()
        {
            const string md = "- one\n- two\n    - nested\n- three\n\n1. first\n2. second\n3. third";
            // the converter writes two lists of different kinds one under the other, with no blank line between
            Assert.Equal(md.Replace("three\n\n1.", "three\n1."), RoundTrip(md));
        }

        [Fact]
        public void Every_numbered_list_starts_again()
        {
            const string md = "1. a\n2. b\n\nA paragraph between.\n\n1. c\n2. d\n\nAnother one.\n\n7. e\n8. f";
            var text = RoundTrip(md);
            Assert.Contains("1. a\n2. b", text);
            Assert.Contains("1. c\n2. d", text);
            Assert.Contains("7. e\n8. f", text);
        }
        [Fact]
        public void Lists_are_real_lists_of_Word()
        {
            var (path, _) = Export("- a\n- b\n\n1. c\n2. d");
            var paragraphs = Paragraphs(path);
            Assert.All(paragraphs, p => Assert.Equal("ListParagraph", StyleOf(p)));
            Assert.All(paragraphs, p => Assert.NotNull(p.ParagraphProperties.NumberingProperties));
            Assert.Equal(1, paragraphs[0].ParagraphProperties.NumberingProperties.NumberingId.Val.Value);
            Assert.NotEqual(1, paragraphs[2].ParagraphProperties.NumberingProperties.NumberingId.Val.Value);
            AssertValid(path);
        }

        [Fact]
        public void A_second_paragraph_in_a_list_item_lines_up_under_the_text()
        {
            var (path, _) = Export("- first\n\n  continued\n- second");
            var paragraphs = Paragraphs(path);
            Assert.NotNull(paragraphs[0].ParagraphProperties.NumberingProperties);
            Assert.Null(paragraphs[1].ParagraphProperties.NumberingProperties);
            Assert.Equal("720", paragraphs[1].ParagraphProperties.Indentation.Left.Value);
            AssertValid(path);
        }

        [Fact]
        public void Task_list_items_start_with_a_check_box()
        {
            var (path, _) = Export("- [x] done\n- [ ] open");
            var text = Paragraphs(path).Select(p => p.InnerText).ToList();
            Assert.Equal(new[] { "☑ done", "☐ open" }, text);
            AssertValid(path);
        }

        [Fact]
        public void Quotes_round_trip_and_nested_quotes_move_in()
        {
            Assert.Equal("> A quoted line.", RoundTrip("> A quoted line."));
            var (path, _) = Export("> outer\n>\n> > inner");
            var paragraphs = Paragraphs(path);
            Assert.All(paragraphs, p => Assert.Equal("Quote", StyleOf(p)));
            Assert.Null(paragraphs[0].ParagraphProperties.Indentation);
            Assert.Equal("1440", paragraphs[1].ParagraphProperties.Indentation.Left.Value);
            AssertValid(path);
        }

        [Fact]
        public void Code_blocks_keep_every_line_and_the_blank_ones()
        {
            const string md = "```\nline one\n  indented\n\nafter a blank\n```";
            Assert.Equal(md, RoundTrip(md));
            var (path, _) = Export(md);
            Assert.All(Paragraphs(path), p => Assert.Equal("Code", StyleOf(p)));
            Assert.Equal(4, Paragraphs(path).Count);
        }

        [Fact]
        public void A_tab_in_code_is_a_tab()
        {
            var (path, _) = Export("```\n\tindented\n```");
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.NotEmpty(doc.MainDocumentPart.Document.Body.Descendants<W.TabChar>());
        }

        [Fact]
        public void Tables_are_real_tables_with_a_repeating_header()
        {
            const string md = "| Name | Qty |\n| :--- | ---: |\n| Apples | 3 |\n| Pears | 12 |";
            var (path, _) = Export(md);
            using (var doc = WordprocessingDocument.Open(path, false))
            {
                var table = doc.MainDocumentPart.Document.Body.Descendants<W.Table>().Single();
                var rows = table.Elements<W.TableRow>().ToList();
                Assert.Equal(3, rows.Count);
                Assert.NotNull(rows[0].TableRowProperties.GetFirstChild<W.TableHeader>());
                Assert.Null(rows[1].TableRowProperties);
                var qty = rows[1].Elements<W.TableCell>().Last().Descendants<W.Paragraph>().Single();
                Assert.Equal(W.JustificationValues.Right, qty.ParagraphProperties.Justification.Val.Value);
            }
            AssertValid(path);
            Assert.Equal("| Name | Qty |\n| --- | --- |\n| Apples | 3 |\n| Pears | 12 |", RoundTrip(md));
        }

        [Fact]
        public void Two_tables_in_a_row_stay_two_tables()
        {
            var (path, _) = Export("| a |\n|---|\n| 1 |\n\n<!-- apart -->\n\n| b |\n|---|\n| 2 |");
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.Equal(2, doc.MainDocumentPart.Document.Body.Descendants<W.Table>().Count());
            AssertValid(path);
        }

        [Fact]
        public void The_paragraph_after_a_list_or_a_table_has_room_above_it()
        {
            var (path, _) = Export("- a\n- b\n\nafter the list\n\n| h |\n|---|\n| c |\n\nafter the table");
            var after = Paragraphs(path).Where(p => p.InnerText.StartsWith("after")).ToList();
            Assert.Equal(2, after.Count);
            Assert.All(after, p => Assert.Equal("160", p.ParagraphProperties.SpacingBetweenLines.Before.Value));
        }

        [Fact]
        public void Addresses_in_angle_brackets_and_bare_ones_are_links()
        {
            var (path, _) = Export("Mail <ana@example.com>, web <https://example.org/a> and www.bare.example.");
            using var doc = WordprocessingDocument.Open(path, false);
            var targets = doc.MainDocumentPart.HyperlinkRelationships.Select(r => r.Uri.ToString()).ToList();
            Assert.Equal(3, targets.Count);
            Assert.Contains("mailto:ana@example.com", targets);
            Assert.Contains("https://example.org/a", targets);
            Assert.Contains(targets, t => t.Contains("www.bare.example"));
            AssertValid(path);
        }

        [Fact]
        public void A_picture_is_put_in_the_file_with_its_alt_text()
        {
            File.WriteAllBytes(Path.Combine(work, "dot.png"), Convert.FromBase64String(TinyPng));
            var (path, result) = Export("Before ![A tiny dot](dot.png) after.");
            Assert.Equal(1, result.Pictures);
            Assert.Empty(result.SkippedPictures);
            using (var doc = WordprocessingDocument.Open(path, false))
            {
                Assert.Single(doc.MainDocumentPart.ImageParts);
                var properties = doc.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Single();
                Assert.Equal("A tiny dot", properties.Description.Value);
            }
            AssertValid(path);
            Assert.Matches(@"^Before !\[A tiny dot\]\(images[0-9a-f]*/image1\.png\) after\.$", RoundTrip("Before ![A tiny dot](dot.png) after."));
        }

        // A PNG header with the size given: only the first 24 bytes matter to the exporter.
        private static byte[] PngOfSize(int width, int height)
        {
            var data = Convert.FromBase64String(TinyPng);
            data[18] = (byte)(width >> 8); data[19] = (byte)width;
            data[22] = (byte)(height >> 8); data[23] = (byte)height;
            return data;
        }

        [Fact]
        public void A_picture_is_never_wider_than_the_text()
        {
            File.WriteAllBytes(Path.Combine(work, "wide.png"), PngOfSize(3000, 1000));
            var (path, _) = Export("![wide](wide.png)");
            using var doc = WordprocessingDocument.Open(path, false);
            var extent = doc.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
            Assert.Equal(9026L * 635, extent.Cx.Value);
            Assert.InRange(extent.Cy.Value, 9026L * 635 / 3 - 2, 9026L * 635 / 3 + 2);
        }

        [Fact]
        public void A_picture_with_a_zoom_in_html_is_scaled()
        {
            File.WriteAllBytes(Path.Combine(work, "half.png"), PngOfSize(400, 200));
            var (path, result) = Export("<img src=\"half.png\" alt=\"half\" style=\"zoom:50%;\">");
            Assert.Equal(1, result.Pictures);
            using var doc = WordprocessingDocument.Open(path, false);
            var extent = doc.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
            Assert.Equal(200L * 9525, extent.Cx.Value);
            Assert.Equal(100L * 9525, extent.Cy.Value);
        }

        [Fact]
        public void A_picture_in_a_data_address_is_decoded()
        {
            var (path, result) = Export("![dot](data:image/png;base64," + TinyPng + ")");
            Assert.Equal(1, result.Pictures);
            AssertValid(path);
        }

        [Fact]
        public void A_missing_or_remote_picture_leaves_its_words_and_is_reported()
        {
            var (path, result) = Export("![gone](nothing.png) and ![far](https://example.com/a.png)");
            Assert.Equal(0, result.Pictures);
            Assert.Equal(new[] { "nothing.png", "https://example.com/a.png" }, result.SkippedPictures);
            Assert.Equal("[gone] and [far]", Paragraphs(path).Single().InnerText);
            AssertValid(path);
        }

        [Fact]
        public void A_text_file_named_png_is_not_a_picture()
        {
            File.WriteAllText(Path.Combine(work, "fake.png"), "this is not a picture, only a text that is long enough");
            var (_, result) = Export("![fake](fake.png)");
            Assert.Equal(new[] { "fake.png" }, result.SkippedPictures);
        }

        [Fact]
        public void Html_text_loses_its_tags_and_comments_are_nothing()
        {
            var (path, _) = Export("<div>Hello <b>there</b><br>second line</div>\n\n<!-- hidden -->\n\nAfter");
            var paragraphs = Paragraphs(path);
            Assert.Equal(new[] { "Hello theresecond line", "After" }, paragraphs.Select(p => p.InnerText).ToArray());
            Assert.NotEmpty(paragraphs[0].Descendants<W.Break>());
        }

        [Fact]
        public void Front_matter_is_not_printed()
        {
            var (path, _) = Export("---\ntitle: Hidden\nauthor: Nobody\n---\n\n# Visible");
            Assert.Equal(new[] { "Visible" }, Paragraphs(path).Select(p => p.InnerText).ToArray());
        }

        [Fact]
        public void A_hard_break_is_a_break_and_a_soft_one_a_space()
        {
            var (path, _) = Export("one  \ntwo\nthree");
            var paragraph = Paragraphs(path).Single();
            Assert.Single(paragraph.Descendants<W.Break>());
            Assert.Equal("onetwo three", paragraph.InnerText);
            var (lines, _) = Export("a\nb", o => o.SoftBreaksAsLineBreaks = true);
            Assert.Single(Paragraphs(lines).Single().Descendants<W.Break>());
        }

        [Fact]
        public void The_rule_is_a_border_not_a_row_of_dashes()
        {
            var (path, _) = Export("above\n\n---\n\nbelow");
            var rule = Paragraphs(path)[1];
            Assert.Equal("", rule.InnerText);
            Assert.NotNull(rule.ParagraphProperties.ParagraphBorders.BottomBorder);
        }

        [Fact]
        public void Language_page_size_and_title_are_set()
        {
            var (path, _) = Export("# Rapport annuel\n\ntexte", o => { o.Language = "fr-FR"; o.PageSize = WordPageSize.Letter; });
            using var doc = WordprocessingDocument.Open(path, false);
            var defaults = doc.MainDocumentPart.StyleDefinitionsPart.Styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle;
            Assert.Equal("fr-FR", defaults.GetFirstChild<W.Languages>().Val.Value);
            var page = doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>().GetFirstChild<W.PageSize>();
            Assert.Equal(12240u, page.Width.Value);
            Assert.Equal("Rapport annuel", doc.PackageProperties.Title);
        }

        [Fact]
        public void The_title_falls_back_to_the_given_one()
        {
            var (path, _) = Export("only text", o => o.Title = "notes");
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.Equal("notes", doc.PackageProperties.Title);
        }

        [Fact]
        public void A_failed_export_leaves_no_file_and_no_leftover()
        {
            var target = Path.Combine(work, "missing folder", "x.docx");
            Assert.ThrowsAny<IOException>(() => WordExporter.ExportToFile("text", target));
            Assert.Empty(Directory.GetFiles(work, "*.tmp"));
        }

        [Fact]
        public void An_existing_file_is_replaced()
        {
            var target = Path.Combine(work, "again.docx");
            File.WriteAllText(target, "old");
            WordExporter.ExportToFile("new text", target);
            AssertValid(target);
            Assert.Empty(Directory.GetFiles(work, "*.tmp"));
        }

        [Fact]
        public void Empty_and_odd_input_gives_a_valid_file()
        {
            foreach (var md in new[] { "", "   ", "\n\n", "﻿# With a mark", "- \n- x", "|a|\n|-|", "<br>", "[]()", "![]()", "```\n```" })
            {
                var (path, _) = Export(md);
                AssertValid(path);
            }
        }

        [Fact]
        public void A_header_cut_short_is_a_skipped_picture_not_a_failed_export()
        {
            // 24 and 25 bytes starting with BM: too short to hold the height of a bitmap
            foreach (var length in new[] { 24, 25 })
            {
                var data = new byte[length];
                data[0] = (byte)'B'; data[1] = (byte)'M';
                File.WriteAllBytes(Path.Combine(work, "short.bmp"), data);
                var (_, result) = Export("![short](short.bmp)");
                Assert.Equal(new[] { "short.bmp" }, result.SkippedPictures);
            }
        }

        [Fact]
        public void A_bitmap_with_an_impossible_height_is_skipped()
        {
            var data = new byte[40];
            data[0] = (byte)'B'; data[1] = (byte)'M';
            BitConverter.GetBytes(10).CopyTo(data, 18);
            BitConverter.GetBytes(int.MinValue).CopyTo(data, 22);
            File.WriteAllBytes(Path.Combine(work, "odd.bmp"), data);
            var (_, result) = Export("![odd](odd.bmp)");
            Assert.Equal(new[] { "odd.bmp" }, result.SkippedPictures);
        }
    }
}
