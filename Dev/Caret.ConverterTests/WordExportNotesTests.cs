using System;
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
    // Phase 2 of the Word export: footnotes, the properties from the front matter, links to headings, the table of contents and the
    // page numbers.
    public class WordExportNotesTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private string Export(string markdown, Action<WordExportOptions> configure = null)
        {
            var options = new WordExportOptions { BaseFolder = work };
            configure?.Invoke(options);
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            WordExporter.ExportToFile(markdown, path, options);
            using var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return path;
        }

        [Fact]
        public void A_footnote_is_a_real_footnote_with_its_text()
        {
            var path = Export("Claim.[^a] Another claim.[^b]\n\n[^a]: First *note*.\n[^b]: Second note with [a link](https://example.com/n).");
            using var doc = WordprocessingDocument.Open(path, false);
            var references = doc.MainDocumentPart.Document.Body.Descendants<W.FootnoteReference>().Select(r => r.Id.Value).ToList();
            Assert.Equal(new long[] { 1, 2 }, references);
            var notes = doc.MainDocumentPart.FootnotesPart.Footnotes.Elements<W.Footnote>().ToList();
            Assert.Equal(new long[] { -1, 0, 1, 2 }, notes.Select(n => n.Id.Value).ToArray());
            Assert.Equal(" First note.", notes[2].InnerText);
            Assert.Equal(" Second note with a link.", notes[3].InnerText);
            Assert.NotEmpty(notes[2].Descendants<W.FootnoteReferenceMark>());
            Assert.Equal("FootnoteText", notes[2].Descendants<W.Paragraph>().Single().ParagraphProperties.ParagraphStyleId.Val.Value);
            // the link in a footnote belongs to the footnotes part, not to the document
            Assert.Single(doc.MainDocumentPart.FootnotesPart.HyperlinkRelationships);
            Assert.Empty(doc.MainDocumentPart.HyperlinkRelationships);
        }

        [Fact]
        public void Footnotes_round_trip_through_the_word_converter()
        {
            var path = Export("Claim.[^1]\n\n[^1]: The note.");
            var text = DocumentConverter.Convert(path, new ConversionOptions()).Markdown.Replace("\r\n", "\n").Trim();
            Assert.Equal("Claim.[^1]\n\n[^1]: The note.", text);
        }

        [Fact]
        public void A_footnote_with_two_paragraphs_and_an_undefined_one()
        {
            var path = Export("Text[^x] and [^missing].\n\n[^x]: First paragraph.\n\n    Second paragraph.");
            using var doc = WordprocessingDocument.Open(path, false);
            var note = doc.MainDocumentPart.FootnotesPart.Footnotes.Elements<W.Footnote>().Last();
            Assert.Equal(2, note.Elements<W.Paragraph>().Count());
            Assert.Contains("[^missing]", doc.MainDocumentPart.Document.Body.InnerText);
        }

        [Fact]
        public void The_front_matter_becomes_the_properties_and_is_not_printed()
        {
            var path = Export("---\ntitle: \"Annual report\"\nauthor: Ana Pérez\nsubject: Results\ndescription: The year in numbers\ntags:\n  - finance\n  - 2026\nlang: fr-FR\nunknown: kept out\n---\n\n# First heading\n\nText.");
            using var doc = WordprocessingDocument.Open(path, false);
            var properties = doc.PackageProperties;
            Assert.Equal("Annual report", properties.Title);
            Assert.Equal("Ana Pérez", properties.Creator);
            Assert.Equal("Results", properties.Subject);
            Assert.Equal("The year in numbers", properties.Description);
            Assert.Equal("finance, 2026", properties.Keywords);
            var language = doc.MainDocumentPart.StyleDefinitionsPart.Styles.DocDefaults.RunPropertiesDefault.RunPropertiesBaseStyle.GetFirstChild<W.Languages>();
            Assert.Equal("fr-FR", language.Val.Value);
            Assert.DoesNotContain("Ana", doc.MainDocumentPart.Document.Body.InnerText);
        }

        [Fact]
        public void Keywords_in_brackets_and_a_title_from_the_heading()
        {
            var path = Export("---\nkeywords: [alpha, \"beta\"]\n---\n\n# From the heading");
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.Equal("alpha, beta", doc.PackageProperties.Keywords);
            Assert.Equal("From the heading", doc.PackageProperties.Title);
        }

        [Fact]
        public void A_link_to_a_heading_goes_to_its_bookmark()
        {
            var path = Export("# Intro\n\nSee [the second part](#second-part), [again](#Second-Part-1) and [nowhere](#missing).\n\n## Second part\n\ntext\n\n## Second part\n\ntext");
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart.Document.Body;
            var bookmarks = body.Descendants<W.BookmarkStart>().ToList();
            Assert.Equal(3, bookmarks.Count);
            Assert.Equal(3, bookmarks.Select(b => b.Name.Value).Distinct().Count());
            Assert.Equal(3, body.Descendants<W.BookmarkEnd>().Count());
            var links = body.Descendants<W.Hyperlink>().ToList();
            Assert.Equal(2, links.Count);
            Assert.Equal(bookmarks[1].Name.Value, links[0].Anchor.Value);
            Assert.Equal(bookmarks[2].Name.Value, links[1].Anchor.Value);
            Assert.Contains("nowhere", body.InnerText);
        }

        [Fact]
        public void A_toc_line_becomes_a_table_of_contents_field_with_its_entries()
        {
            var path = Export("[TOC]\n\n# One\n\n## Two\n\n### Three\n\n#### Four\n\ntext");
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart.Document.Body;
            var entries = body.Elements<W.Paragraph>().Where(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value?.StartsWith("TOC") == true).ToList();
            Assert.Equal(new[] { "TOC1", "TOC2", "TOC3" }, entries.Select(p => p.ParagraphProperties.ParagraphStyleId.Val.Value).ToArray());
            var codes = body.Descendants<W.FieldCode>().Select(c => c.Text.Trim()).ToList();
            Assert.Contains(codes, c => c.StartsWith("TOC "));
            Assert.Equal(3, codes.Count(c => c.StartsWith("PAGEREF ")));
            var begins = body.Descendants<W.FieldChar>().Count(f => f.FieldCharType.Value == W.FieldCharValues.Begin);
            Assert.Equal(begins, body.Descendants<W.FieldChar>().Count(f => f.FieldCharType.Value == W.FieldCharValues.End));
            Assert.DoesNotContain("[TOC]", body.InnerText);
        }

        [Fact]
        public void A_toc_line_without_headings_writes_nothing()
        {
            var path = Export("[TOC]\n\nonly text");
            using var doc = WordprocessingDocument.Open(path, false);
            Assert.Equal("only text", doc.MainDocumentPart.Document.Body.InnerText);
        }

        [Fact]
        public void Page_numbers_are_a_centered_field_in_the_footer()
        {
            var path = Export("text", o => o.PageNumbers = true);
            using (var doc = WordprocessingDocument.Open(path, false))
            {
                var footer = Assert.Single(doc.MainDocumentPart.FooterParts).Footer;
                Assert.Equal(" PAGE ", footer.Descendants<W.SimpleField>().Single().Instruction.Value);
                Assert.Equal(W.JustificationValues.Center, footer.Descendants<W.Justification>().Single().Val.Value);
                Assert.NotNull(doc.MainDocumentPart.Document.Body.GetFirstChild<W.SectionProperties>().GetFirstChild<W.FooterReference>());
            }
            using var without = WordprocessingDocument.Open(Export("text"), false);
            Assert.Empty(without.MainDocumentPart.FooterParts);
        }

        [Fact]
        public void A_heading_called_foo_1_does_not_take_the_anchor_of_the_second_foo()
        {
            var path = Export("# Foo\n\n# Foo\n\n# Foo 1\n\n[a](#foo-1) [b](#foo-1-1) [c](#foo)");
            using var doc = WordprocessingDocument.Open(path, false);
            var body = doc.MainDocumentPart.Document.Body;
            var bookmarks = body.Descendants<W.BookmarkStart>().Select(b => b.Name.Value).ToList();
            var links = body.Descendants<W.Hyperlink>().Select(h => h.Anchor.Value).ToList();
            Assert.Equal(new[] { bookmarks[1], bookmarks[2], bookmarks[0] }, links);
        }
    }
}
