using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Validation;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // Every file in samples/ that Caret can convert is converted and compared with its expected Markdown
    // (<file>.expected.md next to it). Adding a document to the folder adds a test: CARET_UPDATE_SAMPLES=1
    // writes the expected file the first time, to be read and committed like any other change.
    public class SampleTests
    {
        public static IEnumerable<object[]> SampleNames() =>
            Directory.GetFiles(TestPaths.Samples)
                .Where(DocumentConverter.IsSupported)
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => new object[] { n });

        // The developer's own culture (Hungarian, comma decimals) and a French one, besides English: a
        // conversion must not depend on the PC's regional settings.
        public static IEnumerable<object[]> SamplesInCultures() =>
            from sample in SampleNames()
            from culture in new[] { "en-US", "fr-FR", "hu-HU" }
            select new[] { sample[0], culture };

        [Theory]
        [MemberData(nameof(SamplesInCultures))]
        public void Sample_converts_to_the_expected_markdown(string name, string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            var previousUi = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var work = TestPaths.NewTempFolder();
            try
            {
                var actual = Convert(name, work);
                var expectedPath = Path.Combine(TestPaths.Samples, name + ".expected.md");
                if (Environment.GetEnvironmentVariable("CARET_UPDATE_SAMPLES") == "1")
                {
                    // Updating: the English case writes the file, and no case compares while it is being written
                    // (the others could run before it, against a missing or stale file).
                    if (culture == "en-US") File.WriteAllText(expectedPath, actual, new UTF8Encoding(false));
                    return;
                }
                Assert.True(File.Exists(expectedPath), $"{name} has no expected Markdown yet: run with CARET_UPDATE_SAMPLES=1, read the result and commit it.");
                var expected = File.ReadAllText(expectedPath).Replace("\r\n", "\n");
                Assert.Equal(expected, actual);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
                CultureInfo.CurrentUICulture = previousUi;
                Directory.Delete(work, true);
            }
        }

        // Images go to a folder next to the Markdown; the link is what the .md file would hold.
        internal static string Convert(string name, string workFolder, ConversionOptions options = null)
        {
            options ??= new ConversionOptions();
            options.ImageDirectory ??= Path.Combine(workFolder, "images");
            options.ImageLinkPrefix ??= "images";
            return DocumentConverter.Convert(Path.Combine(TestPaths.Samples, name), options).Markdown.Replace("\r\n", "\n");
        }

        // Writes the sample documents from SampleFactory (CARET_GENERATE_SAMPLES=1). The Office files are
        // checked against the Open XML schema, so Word, Excel and PowerPoint can open them.
        [Fact]
        public void Generate_samples_when_asked()
        {
            var only = Environment.GetEnvironmentVariable("CARET_GENERATE_SAMPLES");
            if (string.IsNullOrEmpty(only)) return;
            foreach (var (name, data) in SampleFactory.All())
            {
                if (only != "1" && only != name) continue; // "1" writes them all, a file name just that one
                File.WriteAllBytes(Path.Combine(TestPaths.Samples, name), data);
                using var stream = new MemoryStream(data);
                DocumentFormat.OpenXml.Packaging.OpenXmlPackage package = Path.GetExtension(name) switch
                {
                    ".docx" => DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false),
                    ".xlsx" => DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(stream, false),
                    ".pptx" => DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(stream, false),
                    _ => null,
                };
                if (package == null) continue;
                using (package)
                {
                    var errors = new OpenXmlValidator().Validate(package).Select(e => $"{e.Path?.XPath}: {e.Description}").ToList();
                    Assert.True(errors.Count == 0, name + ":\n" + string.Join("\n", errors));
                }
            }
        }
    }
}
