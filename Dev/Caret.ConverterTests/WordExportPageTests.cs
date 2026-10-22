using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Export;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Caret.ConverterTests
{
    // Phase 4 of the Word export: the look, the page, the header and the footer.
    public class WordExportPageTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private const string Text = "# Report title\n\nBody text with a [link](https://example.com).\n\n## Section\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";

        private WordprocessingDocument Open(string markdown, Action<WordExportOptions> configure)
        {
            var options = new WordExportOptions { BaseFolder = work };
            configure?.Invoke(options);
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            WordExporter.ExportToFile(markdown, path, options);
            var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return doc;
        }

        private static W.Style StyleOf(WordprocessingDocument doc, string id) =>
            doc.MainDocumentPart.StyleDefinitionsPart.Styles.Elements<W.Style>().Single(s => s.StyleId.Value == id);

        private static string BodyFont(WordprocessingDocument doc) =>
            doc.MainDocumentPart.StyleDefinitionsPart.Styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle.GetFirstChild<W.RunFonts>().Ascii.Value;

        [Theory]
        [InlineData(WordLook.Plain, "Calibri", "Calibri Light", false)]
        [InlineData(WordLook.Report, "Cambria", "Calibri", true)]
        [InlineData(WordLook.Business, "Times New Roman", "Times New Roman", false)]
        [InlineData(WordLook.Modern, "Arial", "Arial", false)]
        public void A_look_sets_the_fonts_of_the_text_and_of_the_headings(WordLook look, string body, string heading, bool rule)
        {
            using var doc = Open(Text, o => o.Look = look);
            Assert.Equal(body, BodyFont(doc));
            var h1 = StyleOf(doc, "Heading1");
            Assert.Equal(heading, h1.StyleRunProperties.GetFirstChild<W.RunFonts>().Ascii.Value);
            Assert.Equal(rule, h1.StyleParagraphProperties.ParagraphBorders != null);
            Assert.Equal(heading != "Calibri Light", h1.StyleRunProperties.Bold != null);
        }

        [Fact]
        public void The_structure_is_the_same_in_every_look()
        {
            var counts = Enum.GetValues<WordLook>().Select(look =>
            {
                using var doc = Open(Text, o => o.Look = look);
                var body = doc.MainDocumentPart.Document.Body;
                return (body.Descendants<W.Paragraph>().Count(), body.Descendants<W.Table>().Count(), body.Descendants<W.Hyperlink>().Count());
            }).Distinct().ToList();
            Assert.Single(counts);
        }

        [Fact]
        public void A_look_colors_the_table_header_and_the_links()
        {
            using var doc = Open(Text, o => o.Look = WordLook.Modern);
            var fill = doc.MainDocumentPart.Document.Body.Descendants<W.TableCell>().First().TableCellProperties.Shading.Fill.Value;
            Assert.Equal("DCEBEE", fill);
            Assert.Equal("0B7285", StyleOf(doc, "Hyperlink").StyleRunProperties.Color.Val.Value);
        }

        [Theory]
        [InlineData(WordPageSize.A4, false, 11906, 16838)]
        [InlineData(WordPageSize.Letter, false, 12240, 15840)]
        [InlineData(WordPageSize.Legal, false, 12240, 20160)]
        [InlineData(WordPageSize.A4, true, 16838, 11906)]
        public void The_page_has_the_size_and_the_orientation_asked_for(WordPageSize size, bool landscape, uint width, uint height)
        {
            using var doc = Open(Text, o => { o.PageSize = size; o.Landscape = landscape; });
            var page = doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>().GetFirstChild<W.PageSize>();
            Assert.Equal(width, page.Width.Value);
            Assert.Equal(height, page.Height.Value);
            Assert.Equal(landscape, page.Orient?.Value == W.PageOrientationValues.Landscape);
        }

        [Theory]
        [InlineData(WordMargins.Normal, 1440)]
        [InlineData(WordMargins.Narrow, 720)]
        [InlineData(WordMargins.Wide, 1800)]
        public void The_margins_are_the_ones_asked_for_and_the_text_follows_them(WordMargins margins, int margin)
        {
            using var doc = Open(Text, o => o.Margins = margins);
            var section = doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>();
            Assert.Equal((uint)margin, section.GetFirstChild<W.PageMargin>().Left.Value);
            var grid = doc.MainDocumentPart.Document.Body.Descendants<W.GridColumn>().Select(c => int.Parse(c.Width.Value)).ToList();
            Assert.Equal((11906 - 2 * margin) / 2, grid[0]);
        }

        [Fact]
        public void The_header_is_centered_text_with_the_title_and_the_date()
        {
            using var doc = Open(Text, o => { o.HeaderText = "Confidential - {title} - {date}"; o.Language = "en-US"; });
            var header = Assert.Single(doc.MainDocumentPart.HeaderParts).Header;
            Assert.StartsWith("Confidential - Report title - ", header.InnerText);
            Assert.Contains(DateTime.Today.Year.ToString(), header.InnerText);
            Assert.Equal(W.JustificationValues.Center, header.Descendants<W.Justification>().Single().Val.Value);
            var section = doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>();
            Assert.Equal(0, section.ChildElements.ToList().IndexOf(section.GetFirstChild<W.HeaderReference>()));
        }

        [Fact]
        public void The_footer_has_its_text_at_the_left_and_the_number_at_the_right()
        {
            using var doc = Open(Text, o => { o.FooterText = "Acme - {title}"; o.PageNumbers = true; });
            var footer = Assert.Single(doc.MainDocumentPart.FooterParts).Footer;
            Assert.Equal("Acme - Report title1", footer.InnerText);
            var tab = footer.Descendants<W.TabStop>().Single();
            Assert.Equal(W.TabStopValues.Right, tab.Val.Value);
            Assert.Equal(11906 - 2 * 1440, tab.Position.Value);
            Assert.NotEmpty(footer.Descendants<W.TabChar>());
            Assert.Single(footer.Descendants<W.SimpleField>());
            Assert.Empty(footer.Descendants<W.Justification>());
        }

        [Fact]
        public void A_footer_with_only_one_of_them_is_centered_and_none_is_no_footer()
        {
            using (var doc = Open(Text, o => o.FooterText = "Only text"))
            {
                var footer = Assert.Single(doc.MainDocumentPart.FooterParts).Footer;
                Assert.Equal("Only text", footer.InnerText);
                Assert.Empty(footer.Descendants<W.SimpleField>());
                Assert.Equal(W.JustificationValues.Center, footer.Descendants<W.Justification>().Single().Val.Value);
            }
            using (var doc = Open(Text, null))
            {
                Assert.Empty(doc.MainDocumentPart.FooterParts);
                Assert.Empty(doc.MainDocumentPart.HeaderParts);
            }
        }

        private static int Tocs(WordprocessingDocument doc) =>
            doc.MainDocumentPart.Document.Body.Descendants<W.FieldCode>().Count(c => c.Text.Trim().StartsWith("TOC "));

        [Fact]
        public void A_table_of_contents_at_the_start_is_added_once_and_only_when_there_are_headings()
        {
            using (var doc = Open(Text, o => o.TableOfContents = true))
            {
                var body = doc.MainDocumentPart.Document.Body;
                Assert.Equal("TOC1", body.Elements<W.Paragraph>().First().ParagraphProperties.ParagraphStyleId.Val.Value);
                Assert.Equal(1, Tocs(doc));
                Assert.NotEmpty(body.Descendants<W.Break>().Where(b => b.Type?.Value == W.BreakValues.Page));
            }
            using (var doc = Open("[TOC]\n\n# One\n\ntext", o => o.TableOfContents = true)) Assert.Equal(1, Tocs(doc));
            using (var doc = Open("only text", o => o.TableOfContents = true)) Assert.Equal(0, Tocs(doc));
        }

        [Fact]
        public void Every_combination_is_a_valid_file()
        {
            foreach (var look in Enum.GetValues<WordLook>())
                foreach (var landscape in new[] { false, true })
                {
                    using var doc = Open(Text + "\n[^1]\n\n[^1]: note\n", o =>
                    {
                        o.Look = look; o.Landscape = landscape; o.Margins = WordMargins.Wide; o.PageSize = WordPageSize.Legal;
                        o.HeaderText = "h"; o.FooterText = "f"; o.PageNumbers = true; o.TableOfContents = true;
                    });
                    Assert.NotNull(doc.MainDocumentPart.Document);
                }
        }
    }
}
