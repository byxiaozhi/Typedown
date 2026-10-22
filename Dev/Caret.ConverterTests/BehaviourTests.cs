using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // What the converters promise besides the Markdown itself: options, warnings, images, bad input.
    public class BehaviourTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private static string Sample(string name) => Path.Combine(TestPaths.Samples, name);

        private string Write(string name, byte[] data)
        {
            var path = Path.Combine(work, name);
            File.WriteAllBytes(path, data);
            return path;
        }

        [Fact]
        public void Every_sample_extension_is_supported_and_others_are_refused()
        {
            foreach (var extension in new[] { ".docx", ".docm", ".dotx", ".xlsx", ".xlsm", ".pptx", ".pptm", ".pdf", ".csv", ".msg", ".eml" })
                Assert.Contains(extension, DocumentConverter.SupportedExtensions);
            Assert.False(DocumentConverter.IsSupported("notes.txt"));
            Assert.False(DocumentConverter.IsSupported(null));
            Assert.Throws<NotSupportedException>(() => DocumentConverter.Convert("notes.txt", new ConversionOptions()));
        }

        [Fact]
        public void Word_images_are_written_and_counted()
        {
            var options = new ConversionOptions { ImageDirectory = Path.Combine(work, "pics"), ImageLinkPrefix = "report_images" };
            var result = DocumentConverter.Convert(Sample("word-report.docx"), options);
            Assert.Equal(1, result.ImageCount);
            Assert.Contains("![A small red square](report_images/image1.png)", result.Markdown);
            var image = File.ReadAllBytes(Path.Combine(work, "pics", "image1.png"));
            Assert.Equal(SampleFactory.Png, image);
        }

        [Fact]
        public void Without_an_image_folder_no_images_are_written()
        {
            var result = DocumentConverter.Convert(Sample("word-report.docx"), new ConversionOptions());
            Assert.Equal(0, result.ImageCount);
            Assert.DoesNotContain("![", result.Markdown);
        }

        [Fact]
        public void Powerpoint_headings_and_notes_label_follow_the_options()
        {
            var options = new ConversionOptions { SlideHeadingFormat = "Diapositive {0} : {1}", SlideHeadingUntitledFormat = "Diapositive {0}", NotesLabel = "Notes du présentateur" };
            var markdown = DocumentConverter.Convert(Sample("powerpoint-review.pptx"), options).Markdown.Replace("\r\n", "\n");
            Assert.Contains("## Diapositive 1 : Caret quarterly review", markdown);
            Assert.Contains("## Diapositive 3\n", markdown);
            Assert.Contains("> **Notes du présentateur:** Mention the two new regions.", markdown);
            Assert.DoesNotContain("Backup slide", markdown); // hidden
            Assert.DoesNotContain("Confidential", markdown); // footer
        }

        [Fact]
        public void An_empty_workbook_is_reported_as_no_data()
        {
            var result = DocumentConverter.Convert(Write("empty.xlsx", SampleFactory.EmptyWorkbook()), new ConversionOptions());
            Assert.Equal("", result.Markdown);
            Assert.Contains(ConversionWarning.NoData, result.Warnings);
        }

        [Fact]
        public void Dates_in_a_workbook_from_the_mac_count_from_1904()
        {
            var markdown = DocumentConverter.Convert(Write("mac.xlsx", SampleFactory.Workbook1904()), new ConversionOptions()).Markdown;
            Assert.Contains("1904-01-01", markdown);
            Assert.Contains("1904-01-02", markdown);
        }

        [Fact]
        public void An_empty_csv_is_reported_as_no_data()
        {
            var result = DocumentConverter.Convert(Write("empty.csv", Encoding.UTF8.GetBytes("\n\n,,\n")), new ConversionOptions());
            Assert.Equal("", result.Markdown);
            Assert.Contains(ConversionWarning.NoData, result.Warnings);
        }

        [Fact]
        public void A_pdf_without_a_text_layer_is_reported()
        {
            var result = DocumentConverter.Convert(Write("scan.pdf", SampleFactory.BlankPdf()), new ConversionOptions());
            Assert.Equal("", result.Markdown);
            Assert.Contains(ConversionWarning.PdfHasNoText, result.Warnings);
        }

        [Fact]
        public void Email_masks_addresses_names_phones_and_ibans_by_default()
        {
            var markdown = DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions()).Markdown;
            foreach (var secret in new[] { "Kowalski", "Anna", "Nowak", "Marie", "Dupont", "acme.example", "client.example", "GB82", "601 234 567", "22 123 45 67" })
                Assert.DoesNotContain(secret, markdown);
            Assert.Contains("[EMAIL-1]", markdown);
            Assert.Contains("[IBAN-1]", markdown);
            Assert.Contains("[PHONE-1]", markdown);
        }

        [Fact]
        public void Email_redaction_can_be_turned_off()
        {
            var markdown = DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions { EmailRedact = false }).Markdown;
            Assert.Contains("jan.kowalski@acme.example", markdown);
            Assert.Contains("GB82 WEST 1234 5698 7654 32", markdown);
            Assert.Contains("redacted: false", markdown);
        }

        [Fact]
        public void Email_signatures_are_cut_unless_kept()
        {
            var cut = DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions()).Markdown;
            Assert.DoesNotContain("Senior Analyst", cut);
            var kept = DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions { EmailKeepSignatures = true }).Markdown;
            Assert.Contains("Senior Analyst", kept);
        }

        [Fact]
        public void Extra_names_are_masked_wherever_they_appear()
        {
            var options = new ConversionOptions { EmailKeepSignatures = true, EmailNames = new[] { "ACME" } };
            var markdown = DocumentConverter.Convert(Sample("email-thread.eml"), options).Markdown;
            Assert.DoesNotContain("ACME", markdown);
        }

        [Fact]
        public void Email_attachments_are_converted_or_left_out()
        {
            var with = DocumentConverter.Convert(Sample("email-attachment.eml"), new ConversionOptions()).Markdown;
            Assert.Contains("| Coffee | 12 |", with);
            var without = DocumentConverter.Convert(Sample("email-attachment.eml"), new ConversionOptions { EmailAttachments = false }).Markdown;
            Assert.DoesNotContain("| Coffee | 12 |", without);
        }

        [Fact]
        public void Conversion_is_repeatable()
        {
            foreach (var path in Directory.GetFiles(TestPaths.Samples).Where(DocumentConverter.IsSupported))
            {
                var first = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
                var second = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
                Assert.Equal(first, second);
            }
        }

        [Fact]
        public async Task ConvertAsync_gives_the_same_result()
        {
            var path = Sample("excel-budget.xlsx");
            var sync = DocumentConverter.Convert(path, new ConversionOptions());
            var async = await DocumentConverter.ConvertAsync(path, null);
            Assert.Equal(sync.Markdown, async.Markdown);
        }

        [Fact]
        public void A_document_open_in_another_program_can_still_be_read()
        {
            var path = Write("open.docx", File.ReadAllBytes(Sample("word-french.docx")));
            using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            var markdown = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
            Assert.Contains("Étude de faisabilité", markdown);
        }

        [Fact]
        public void A_file_name_with_spaces_and_accents_converts_and_links_its_images()
        {
            var path = Write("Rapport été (v2).docx", File.ReadAllBytes(Sample("word-report.docx")));
            var options = new ConversionOptions { ImageDirectory = Path.Combine(work, "Rapport été (v2)_images"), ImageLinkPrefix = "Rapport été (v2)_images" };
            var markdown = DocumentConverter.Convert(path, options).Markdown;
            Assert.Contains("(Rapport%20été%20%28v2%29_images/image1.png)", markdown);
        }

        [Theory]
        [InlineData("word-report.docx")]
        [InlineData("excel-budget.xlsx")]
        [InlineData("powerpoint-review.pptx")]
        public void A_damaged_office_file_fails_with_an_error_and_never_hangs(string name)
        {
            var bytes = File.ReadAllBytes(Sample(name));
            var truncated = Write("cut-" + name, bytes.Take(bytes.Length / 2).ToArray());
            var garbage = Write("garbage-" + name, Encoding.UTF8.GetBytes("this is not an Office file"));
            foreach (var path in new[] { truncated, garbage })
                Assert.ThrowsAny<Exception>(() => DocumentConverter.Convert(path, new ConversionOptions()));
        }

        [Fact]
        public void A_damaged_pdf_fails_with_an_error()
        {
            var path = Write("garbage.pdf", Encoding.UTF8.GetBytes("%PDF-1.4 this is not a PDF"));
            Assert.ThrowsAny<Exception>(() => DocumentConverter.Convert(path, new ConversionOptions()));
        }

        [Fact]
        public void Token_estimate_is_about_a_quarter_of_the_characters()
        {
            Assert.Equal(0, DocumentConverter.EstimateTokens(""));
            Assert.Equal(1, DocumentConverter.EstimateTokens("abcd"));
            Assert.Equal(3, DocumentConverter.EstimateTokens("abcdefghi"));
        }
    }
}
