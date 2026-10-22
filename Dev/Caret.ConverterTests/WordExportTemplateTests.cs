using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Export;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Caret.ConverterTests
{
    // Phase 4 of the Word export: the template of the user. The one made here stands for a company template: its own fonts and heading
    // style, a landscape Letter page with narrow margins, a header and a footer, and a list definition with numbers of its own.
    public class WordExportTemplateTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private string MakeTemplate(string name, string headingId = "Heading1", WordprocessingDocumentType type = WordprocessingDocumentType.Document)
        {
            var path = Path.Combine(work, name);
            using var doc = WordprocessingDocument.Create(path, type);
            var main = doc.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("ACME HEADER"))));
            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("ACME FOOTER"))));
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("TEMPLATE TEXT TO DROP"))),
                new W.SectionProperties(
                    new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) },
                    new W.PageSize { Width = 15840u, Height = 12240u, Orient = W.PageOrientationValues.Landscape },
                    new W.PageMargin { Top = 1000, Right = 1000u, Bottom = 1000, Left = 1000u, Header = 500u, Footer = 500u, Gutter = 0u })));
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                new W.DocDefaults(new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(new W.RunFonts { Ascii = "Georgia", HighAnsi = "Georgia" }, new W.FontSize { Val = "24" }))),
                new W.Style(new W.StyleName { Val = "Normal" }, new W.PrimaryStyle()) { Type = W.StyleValues.Paragraph, StyleId = "Normal", Default = true },
                new W.Style(new W.StyleName { Val = "heading 1" }, new W.BasedOn { Val = "Normal" }, new W.PrimaryStyle(),
                    new W.StyleRunProperties(new W.RunFonts { Ascii = "Verdana", HighAnsi = "Verdana" }, new W.Color { Val = "FF0000" }))
                    { Type = W.StyleValues.Paragraph, StyleId = headingId });
            var level = new W.Level(new W.StartNumberingValue { Val = 1 }, new W.NumberingFormat { Val = W.NumberFormatValues.Bullet }, new W.LevelText { Val = "-" }) { LevelIndex = 0 };
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                new W.AbstractNum(level) { AbstractNumberId = 5 }, new W.NumberingInstance(new W.AbstractNumId { Val = 5 }) { NumberID = 7 });
            // a real template: its settings name the separators of the footnotes and endnotes it has
            var notes = main.AddNewPart<FootnotesPart>();
            notes.Footnotes = new W.Footnotes(new W.Footnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                new W.Footnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });
            var endnotes = main.AddNewPart<EndnotesPart>();
            endnotes.Endnotes = new W.Endnotes(new W.Endnote(new W.Paragraph(new W.Run(new W.SeparatorMark()))) { Type = W.FootnoteEndnoteValues.Separator, Id = -1 },
                new W.Endnote(new W.Paragraph(new W.Run(new W.ContinuationSeparatorMark()))) { Type = W.FootnoteEndnoteValues.ContinuationSeparator, Id = 0 });
            main.AddNewPart<DocumentSettingsPart>().Settings = new W.Settings(new W.DefaultTabStop { Val = 720 },
                new W.FootnoteDocumentWideProperties(new W.FootnoteSpecialReference { Id = -1 }, new W.FootnoteSpecialReference { Id = 0 }),
                new W.EndnoteDocumentWideProperties(new W.EndnoteSpecialReference { Id = -1 }, new W.EndnoteSpecialReference { Id = 0 }),
                new W.Compatibility());
            return path;
        }

        private WordprocessingDocument Open(string markdown, string template, Action<WordExportOptions> configure = null)
        {
            var options = new WordExportOptions { BaseFolder = work, TemplatePath = template };
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

        private const string Text = "# Title\n\nBody with a [link](https://example.com).\n\n## Section\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n```\ncode\n```\n";

        [Fact]
        public void The_styles_of_the_template_win_and_ours_fill_what_it_lacks()
        {
            using var doc = Open(Text, MakeTemplate("company.docx"));
            var heading = StyleOf(doc, "Heading1");
            Assert.Equal("Verdana", heading.StyleRunProperties.GetFirstChild<W.RunFonts>().Ascii.Value);
            Assert.Equal("FF0000", heading.StyleRunProperties.Color.Val.Value);
            var defaults = doc.MainDocumentPart.StyleDefinitionsPart.Styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle;
            Assert.Equal("Georgia", defaults.GetFirstChild<W.RunFonts>().Ascii.Value);
            // what the template does not have is added
            Assert.NotNull(StyleOf(doc, "Heading2"));
            Assert.NotNull(StyleOf(doc, "Code"));
            Assert.NotNull(StyleOf(doc, "Hyperlink"));
            Assert.NotNull(StyleOf(doc, "TableGrid"));
        }

        [Fact]
        public void The_page_the_header_and_the_footer_are_the_ones_of_the_template_and_its_text_is_gone()
        {
            using var doc = Open(Text, MakeTemplate("company.docx"), o => { o.HeaderText = "ours"; o.FooterText = "ours"; o.PageNumbers = true; o.Landscape = false; o.Margins = WordMargins.Wide; });
            var section = doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>();
            var page = section.GetFirstChild<W.PageSize>();
            Assert.Equal(15840u, page.Width.Value);
            Assert.Equal(W.PageOrientationValues.Landscape, page.Orient.Value);
            Assert.Equal(1000u, section.GetFirstChild<W.PageMargin>().Left.Value);
            Assert.Equal("ACME HEADER", Assert.Single(doc.MainDocumentPart.HeaderParts).Header.InnerText);
            Assert.Equal("ACME FOOTER", Assert.Single(doc.MainDocumentPart.FooterParts).Footer.InnerText);
            Assert.DoesNotContain("TEMPLATE TEXT TO DROP", doc.MainDocumentPart.Document.Body.InnerText);
            // the width of the text is that of the template: two columns across 15840 - 2 * 1000
            Assert.Equal((15840 - 2000) / 2, int.Parse(doc.MainDocumentPart.Document.Body.Descendants<W.GridColumn>().First().Width.Value));
        }

        [Fact]
        public void Our_lists_are_numbered_past_the_ones_of_the_template()
        {
            using var doc = Open("- a\n- b\n\n1. c\n2. d\n\n- [x] done", MakeTemplate("company.docx"));
            var numbering = doc.MainDocumentPart.NumberingDefinitionsPart.Numbering;
            var abstracts = numbering.Elements<W.AbstractNum>().Select(a => a.AbstractNumberId.Value).ToList();
            Assert.Equal(abstracts.Count, abstracts.Distinct().Count());
            Assert.Contains(5, abstracts);
            var instances = numbering.Elements<W.NumberingInstance>().ToDictionary(i => i.NumberID.Value, i => i.AbstractNumId.Val.Value);
            Assert.True(instances.ContainsKey(7));
            var used = doc.MainDocumentPart.Document.Body.Descendants<W.NumberingId>().Select(n => n.Val.Value).Distinct().ToList();
            Assert.NotEmpty(used);
            Assert.All(used, id => { Assert.True(id > 7); Assert.True(instances.ContainsKey(id)); Assert.Contains(instances[id], abstracts); });
        }

        [Fact]
        public void A_style_the_template_calls_something_else_is_found_by_its_name()
        {
            using var doc = Open(Text, MakeTemplate("french.docx", headingId: "Titre1"));
            var headings = doc.MainDocumentPart.Document.Body.Elements<W.Paragraph>().Where(p => p.InnerText == "Title").ToList();
            Assert.Equal("Titre1", headings.Single().ParagraphProperties.ParagraphStyleId.Val.Value);
            var ids = doc.MainDocumentPart.StyleDefinitionsPart.Styles.Elements<W.Style>().Select(s => s.StyleId.Value).ToList();
            Assert.DoesNotContain("Heading1", ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());
            // our styles that are based on the heading style follow it
            Assert.All(doc.MainDocumentPart.StyleDefinitionsPart.Styles.Descendants<W.BasedOn>(), b => Assert.Contains(b.Val.Value, ids));
        }

        [Fact]
        public void A_dotx_template_gives_a_docx()
        {
            using var doc = Open(Text, MakeTemplate("company.dotx", type: WordprocessingDocumentType.Template));
            Assert.Equal(WordprocessingDocumentType.Document, doc.DocumentType);
            Assert.Equal("FF0000", StyleOf(doc, "Heading1").StyleRunProperties.Color.Val.Value);
        }

        [Fact]
        public void Footnotes_comments_and_a_table_of_contents_work_on_a_template()
        {
            const string md = "[TOC]\n\n# One\n\nText[^1] and {==this==}{>>@Ana 2026-10-10: why<<} and {++new++}.\n\n[^1]: A note.";
            using var doc = Open(md, MakeTemplate("company.docx"));
            Assert.NotNull(doc.MainDocumentPart.FootnotesPart);
            Assert.Single(doc.MainDocumentPart.WordprocessingCommentsPart.Comments.Elements<W.Comment>());
            var settings = doc.MainDocumentPart.DocumentSettingsPart.Settings;
            Assert.NotNull(settings.GetFirstChild<W.FootnoteDocumentWideProperties>());
            Assert.True(settings.ChildElements.ToList().IndexOf(settings.GetFirstChild<W.FootnoteDocumentWideProperties>()) < settings.ChildElements.ToList().IndexOf(settings.GetFirstChild<W.Compatibility>()));
        }

        [Fact]
        public void A_file_that_is_not_a_template_is_an_error_that_says_so_and_leaves_nothing()
        {
            var fake = Path.Combine(work, "fake.docx");
            File.WriteAllText(fake, "this is not a Word document");
            var target = Path.Combine(work, "out.docx");
            Assert.Throws<WordTemplateException>(() => WordExporter.ExportToFile(Text, target, new WordExportOptions { TemplatePath = fake }));
            Assert.Throws<WordTemplateException>(() => WordExporter.ExportToFile(Text, target, new WordExportOptions { TemplatePath = Path.Combine(work, "missing.docx") }));
            Assert.False(File.Exists(target));
            Assert.Empty(Directory.GetFiles(work, "*.tmp"));
        }

        [Fact]
        public void The_settings_of_a_template_name_no_note_that_is_gone()
        {
            // the template has footnotes and endnotes of its own and its settings name their separators: with no footnote in the text,
            // nothing is named (a name with nothing behind it makes Word say the file is corrupted)
            using (var doc = Open("plain text", MakeTemplate("company.docx")))
            {
                var settings = doc.MainDocumentPart.DocumentSettingsPart.Settings;
                Assert.Null(settings.GetFirstChild<W.FootnoteDocumentWideProperties>());
                Assert.Null(settings.GetFirstChild<W.EndnoteDocumentWideProperties>());
                Assert.Null(doc.MainDocumentPart.FootnotesPart);
                Assert.Null(doc.MainDocumentPart.EndnotesPart);
            }
            // with a footnote, the separators are those of the new footnotes part
            using (var doc = Open("Text[^1]\n\n[^1]: Note", MakeTemplate("company2.docx")))
            {
                var settings = doc.MainDocumentPart.DocumentSettingsPart.Settings;
                Assert.NotNull(settings.GetFirstChild<W.FootnoteDocumentWideProperties>());
                Assert.Null(settings.GetFirstChild<W.EndnoteDocumentWideProperties>());
                var ids = doc.MainDocumentPart.FootnotesPart.Footnotes.Elements<W.Footnote>().Select(n => n.Id.Value).ToList();
                Assert.Contains(-1, ids);
                Assert.Contains(0, ids);
            }
        }
    }
}
