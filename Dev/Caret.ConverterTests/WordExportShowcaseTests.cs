using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Export;
using Xunit;

namespace Caret.ConverterTests
{
    // export-samples/showcase.md holds every construct of the Word export. It must export without a skipped picture and pass the
    // validator. CARET_WORD_EXPORT_OUT=<folder> keeps the .docx there, to be opened in the real Word and looked at.
    public class WordExportShowcaseTests
    {
        [Fact]
        public void The_showcase_exports_to_a_valid_file()
        {
            var repository = Path.GetFullPath(Path.Combine(TestPaths.ProjectFolder, "..", ".."));
            var markdown = File.ReadAllText(Path.Combine(TestPaths.ProjectFolder, "export-samples", "showcase.md"));
            var kept = Environment.GetEnvironmentVariable("CARET_WORD_EXPORT_OUT");
            var folder = string.IsNullOrEmpty(kept) ? TestPaths.NewTempFolder() : kept;
            Directory.CreateDirectory(folder);
            try
            {
                var path = Path.Combine(folder, "showcase.docx");
                var result = WordExporter.ExportToFile(markdown, path, new WordExportOptions { BaseFolder = repository, Language = "en-US", PageNumbers = true });
                Assert.Equal(2, result.Pictures);
                Assert.Empty(result.SkippedPictures);
                using var doc = WordprocessingDocument.Open(path, false);
                var errors = new OpenXmlValidator().Validate(doc).Select(e => e.Description).ToList();
                Assert.True(errors.Count == 0, string.Join("\n", errors));
            }
            finally
            {
                if (string.IsNullOrEmpty(kept)) Directory.Delete(folder, true);
            }
        }

        // With CARET_WORD_EXPORT_OUT set, the showcase is also written in every look, with a header, a footer, page numbers and a table of
        // contents, to be looked at in the real Word; without it, only that each one is a valid file.
        [Fact]
        public void The_showcase_in_every_look_is_a_valid_file()
        {
            var repository = Path.GetFullPath(Path.Combine(TestPaths.ProjectFolder, "..", ".."));
            var markdown = File.ReadAllText(Path.Combine(TestPaths.ProjectFolder, "export-samples", "showcase.md"));
            var kept = Environment.GetEnvironmentVariable("CARET_WORD_EXPORT_OUT");
            var folder = string.IsNullOrEmpty(kept) ? TestPaths.NewTempFolder() : kept;
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var look in Enum.GetValues<WordLook>())
                {
                    var path = Path.Combine(folder, "showcase-" + look + ".docx");
                    WordExporter.ExportToFile(markdown, path, new WordExportOptions
                    {
                        BaseFolder = repository, Look = look, PageNumbers = true, TableOfContents = false,
                        HeaderText = "Showcase - {title}", FooterText = "{date}", Margins = WordMargins.Normal,
                    });
                    using var doc = WordprocessingDocument.Open(path, false);
                    var errors = new OpenXmlValidator().Validate(doc).Select(e => e.Description).ToList();
                    if (look == WordLook.Report)
                    {
                        // a file the exporter made, with footnotes, comments and fields of its own, as the template of the next one
                        var onTemplate = Path.Combine(folder, "showcase-on-template.docx");
                        WordExporter.ExportToFile(markdown, onTemplate, new WordExportOptions { BaseFolder = repository, TemplatePath = path, TableOfContents = true });
                        using var second = WordprocessingDocument.Open(onTemplate, false);
                        var more = new OpenXmlValidator().Validate(second).Select(e => e.Description).ToList();
                        Assert.True(more.Count == 0, "on a template: " + string.Join("\n", more));
                    }
                    Assert.True(errors.Count == 0, look + ": " + string.Join("\n", errors));
                }
            }
            finally
            {
                if (string.IsNullOrEmpty(kept)) Directory.Delete(folder, true);
            }
        }
    }
}
