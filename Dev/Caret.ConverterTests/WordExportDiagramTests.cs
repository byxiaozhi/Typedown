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
    // Phase 2 of the Word export: diagrams. The editor draws them (here a tiny PNG stands in for its picture).
    public class WordExportDiagramTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private static readonly WordDiagram Drawn = new(Png);

        private const string Document = "# D\n\n```mermaid\ngraph TD\n  A-->B\n```\n\ntext\n\n- item\n\n  ```sequence\n  Alice->Bob: Hi\n  ```\n\n```plantuml\n@startuml\n@enduml\n```\n\n```js\nvar x;\n```\n\n```vega-lite\n{\"mark\": \"bar\"}\n```\n";

        private (WordprocessingDocument Doc, WordExportResult Result) Export(string markdown, WordDiagram[] images)
        {
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            var result = WordExporter.ExportToFile(markdown, path, new WordExportOptions { DiagramImages = images });
            var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => e.Description).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return (doc, result);
        }

        [Fact]
        public void The_diagrams_are_found_in_document_order_without_plantuml_and_code()
        {
            var found = WordExporter.FindDiagrams(Document);
            Assert.Equal(new[] { "mermaid", "sequence", "vega-lite" }, found.Select(d => d.Type).ToArray());
            Assert.Contains("A-->B", found[0].Code);
            Assert.Contains("Alice->Bob", found[1].Code);
        }

        [Fact]
        public void A_diagram_in_a_footnote_is_not_asked_for()
        {
            var found = WordExporter.FindDiagrams("Text[^1]\n\n[^1]: Note\n\n    ```mermaid\n    graph TD\n    ```\n\n```mermaid\ngraph LR\n```\n");
            Assert.Single(found);
            Assert.Contains("graph LR", found[0].Code);
        }

        [Fact]
        public void A_drawn_diagram_is_a_picture_with_its_source_as_the_alt_text()
        {
            var (doc, result) = Export(Document, new[] { Drawn, Drawn, Drawn });
            using (doc)
            {
                Assert.Equal(3, result.Pictures);
                Assert.Empty(result.SkippedPictures);
                var descriptions = doc.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Select(p => p.Description.Value).ToList();
                Assert.StartsWith("mermaid: graph TD A-->B", descriptions[0]);
                Assert.StartsWith("sequence: Alice->Bob: Hi", descriptions[1]);
                // the ordinary code and the diagram language Word cannot get from the editor stay code
                var texts = doc.MainDocumentPart.Document.Body.Descendants<W.Paragraph>().Select(p => p.InnerText).ToList();
                Assert.Contains("var x;", texts);
                Assert.Contains("@startuml", texts);
                Assert.DoesNotContain("graph TD", texts);
            }
        }

        [Fact]
        public void A_diagram_that_could_not_be_drawn_stays_code_and_is_reported()
        {
            var (doc, result) = Export(Document, new[] { Drawn, null, Drawn });
            using (doc)
            {
                Assert.Equal(2, result.Pictures);
                Assert.Equal(new[] { "sequence 2" }, result.SkippedPictures);
                var texts = doc.MainDocumentPart.Document.Body.Descendants<W.Paragraph>().Select(p => p.InnerText).ToList();
                Assert.Contains("Alice->Bob: Hi", texts);
            }
        }

        [Fact]
        public void Without_pictures_from_the_editor_diagrams_are_plain_code()
        {
            var (doc, result) = Export(Document, null);
            using (doc)
            {
                Assert.Equal(0, result.Pictures);
                Assert.Empty(result.SkippedPictures);
                var texts = doc.MainDocumentPart.Document.Body.Descendants<W.Paragraph>().Select(p => p.InnerText).ToList();
                Assert.Contains("graph TD", texts);
            }
        }

        [Fact]
        public void A_diagram_drawn_at_twice_the_size_is_put_in_at_its_own_size()
        {
            var png = (byte[])Png.Clone();
            png[18] = 0x01; png[19] = 0x90; // 400 pixels wide
            png[22] = 0x00; png[23] = 0xC8; // 200 pixels high
            var (doc, _) = Export("```mermaid\ngraph TD\n```", new[] { new WordDiagram(png, 2) });
            using (doc)
            {
                var extent = doc.MainDocumentPart.Document.Body.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
                Assert.Equal(200L * 9525, extent.Cx.Value);
                Assert.Equal(100L * 9525, extent.Cy.Value);
            }
        }
    }
}
