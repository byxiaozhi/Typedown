using System;
using System.IO;
using System.Linq;
using Typedown.WinUI.Services.Conversion;
using Xunit;

namespace Caret.ConverterTests
{
    // The conversion tray (MainWindow.Tray.cs): what its drop overlay accepts, the source side of Compare, and the masked values
    // that Compare marks, all without the window.
    public class ConversionTrayTests
    {
        private static string Sample(string name) => Path.Combine(TestPaths.Samples, name);

        [Fact]
        public void A_drag_is_a_conversion_for_documents_and_folders_but_not_for_notes_and_pictures()
        {
            Assert.True(ConvertDrop.Accepts(new[] { (@"C:\in\report.docx", false) }));
            Assert.True(ConvertDrop.Accepts(new[] { (@"C:\in\mail.MSG", false), (@"C:\in\a.png", false) }));
            Assert.True(ConvertDrop.Accepts(new[] { (@"C:\in\Old", true) }));
            Assert.True(ConvertDrop.Accepts(new[] { (@"C:\in\old.doc", false) }), "old Word files get a row that says why they cannot be converted");
            Assert.False(ConvertDrop.Accepts(new[] { (@"C:\in\note.md", false), (@"C:\in\a.png", false), (@"C:\in\a.zip", false) }));
            Assert.False(ConvertDrop.Accepts(Array.Empty<(string, bool)>()));
        }

        [Theory]
        [InlineData("word-plain.docx")]
        [InlineData("excel-budget.xlsx")]
        [InlineData("powerpoint-review.pptx")]
        [InlineData("pdf-article.pdf")]
        [InlineData("csv-comma.csv")]
        [InlineData("email-outlook.msg")]
        [InlineData("email-thread.eml")]
        public void Every_format_has_a_source_text(string name)
        {
            var preview = SourceText.Read(Sample(name));
            Assert.False(string.IsNullOrWhiteSpace(preview.Text));
            Assert.False(preview.Truncated);
            Assert.DoesNotContain("\r", preview.Text);
        }

        [Fact]
        public void Word_is_paragraph_by_paragraph_without_Markdown()
        {
            var text = SourceText.Read(Sample("word-report.docx")).Text;
            Assert.Contains("Quarterly report", text);
            Assert.Contains("Sales grew by twelve percent while costs stayed flat", text);
            Assert.DoesNotContain("**", text);
            Assert.Equal("Quarterly report", text.Split('\n')[0]); // the Title style is not made a "# " heading
        }

        [Fact]
        public void Excel_is_rows_with_tabs_per_visible_sheet()
        {
            var text = SourceText.Read(Sample("excel-budget.xlsx")).Text;
            Assert.StartsWith("--- Budget ---\nItem\tAmount\t\tDue\tShare\n", text);
            Assert.Contains("--- Été ---\nMois\tVentes\nJuillet\t310", text);
            Assert.DoesNotContain("|", text.Split('\n')[1]);
        }

        [Fact]
        public void PowerPoint_is_slide_by_slide_hidden_slides_too()
        {
            var text = SourceText.Read(Sample("powerpoint-review.pptx")).Text;
            Assert.StartsWith("--- 1 ---\nCaret quarterly review\nPrepared for the operations team", text);
            Assert.Contains("--- 4 ---\nBackup slide, hidden", text);
        }

        [Fact]
        public void Pdf_is_page_by_page_in_lines()
        {
            var text = SourceText.Read(Sample("pdf-article.pdf")).Text;
            Assert.StartsWith("--- 1 ---\nACME Research Notes\nCoffee Supply Report\n", text);
            Assert.Contains("--- 2 ---", text);
        }

        [Fact]
        public void An_email_shows_its_headers_and_text_as_they_are_before_anything_is_masked()
        {
            var text = SourceText.Read(Sample("email-thread.eml")).Text;
            Assert.StartsWith("From: Jan Kowalski <jan.kowalski@acme.example>\nTo: Anna Nowak <anna.nowak@client.example>\nCc: Marie Dupont", text);
            Assert.Contains("GB82 WEST 1234 5698 7654 32", text);
            Assert.Contains("+48 601 234 567", text);
            Assert.Contains("Kind regards,\nAnna Nowak", text); // the quoted reply and the signature are still there
            var attachments = SourceText.Read(Sample("email-attachment.eml")).Text;
            Assert.EndsWith("Attachments: order.csv; logo.png", attachments);
        }

        [Fact]
        public void A_long_source_is_cut_at_a_line_end_and_says_so()
        {
            var text = string.Join("\n", Enumerable.Range(1, 500).Select(i => "line " + i));
            var cut = SourceText.Cut(text, 1000);
            Assert.True(cut.Truncated);
            Assert.True(cut.Text.Length <= 1000);
            Assert.EndsWith(cut.Text.Split('\n').Last(), cut.Text);
            Assert.StartsWith("line ", cut.Text.Split('\n').Last());
            Assert.False(SourceText.Cut("short", 1000).Truncated);
            // no line end in reach: cut at the limit
            Assert.Equal(1000, SourceText.Cut(new string('x', 5000), 1000).Text.Length);
        }

        [Fact]
        public void A_big_csv_is_read_only_as_far_as_the_preview_needs_and_keeps_its_encoding()
        {
            var work = TestPaths.NewTempFolder();
            try
            {
                var path = Path.Combine(work, "big.csv");
                // 20,000 lines of UTF-8 with Polish letters: cut in the middle of the bytes, a letter must not be split
                const string Words = "żółć gęślą jaźń";
                File.WriteAllText(path, "id;nazwa\n" + string.Concat(Enumerable.Range(1, 20000).Select(i => $"{i};{Words}\n")), new System.Text.UTF8Encoding(false));
                var preview = SourceText.Read(path, 1000);
                Assert.True(preview.Truncated);
                Assert.True(preview.Text.Length <= 1000);
                Assert.StartsWith("id;nazwa\n1;" + Words + "\n", preview.Text);
                Assert.DoesNotContain("�", preview.Text);
                Assert.DoesNotContain("Å", preview.Text); // no legacy code page taken for UTF-8
                Assert.EndsWith(Words, preview.Text);
                // a small file is shown whole
                Assert.False(SourceText.Read(Sample("csv-comma.csv"), 1000).Truncated);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_csv_row_with_no_line_end_is_not_cut_in_the_middle_of_a_letter()
        {
            var work = TestPaths.NewTempFolder();
            try
            {
                var path = Path.Combine(work, "one-row.csv");
                // 1 + 3,000 two-byte letters and no line end: the cut after limit * 4 bytes falls between the two bytes of a letter
                File.WriteAllText(path, "a" + new string('ż', 3000), new System.Text.UTF8Encoding(false));
                var preview = SourceText.Read(path, 1000);
                Assert.True(preview.Truncated);
                Assert.StartsWith("ażżż", preview.Text);
                Assert.DoesNotContain("Å", preview.Text);
                Assert.DoesNotContain("�", preview.Text);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_big_workbook_is_read_only_as_far_as_the_preview_needs()
        {
            var work = TestPaths.NewTempFolder();
            try
            {
                var path = Path.Combine(work, "big.xlsx");
                using (var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                {
                    var workbook = doc.AddWorkbookPart();
                    var part = workbook.AddNewPart<DocumentFormat.OpenXml.Packaging.WorksheetPart>();
                    var data = new DocumentFormat.OpenXml.Spreadsheet.SheetData();
                    for (var r = 1; r <= 20000; r++)
                        data.Append(new DocumentFormat.OpenXml.Spreadsheet.Row(
                            new DocumentFormat.OpenXml.Spreadsheet.Cell { CellReference = "A" + r, CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue(r.ToString()) },
                            new DocumentFormat.OpenXml.Spreadsheet.Cell { CellReference = "B" + r, CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue((r * 2).ToString()) }) { RowIndex = (uint)r });
                    part.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(data);
                    workbook.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook(new DocumentFormat.OpenXml.Spreadsheet.Sheets(
                        new DocumentFormat.OpenXml.Spreadsheet.Sheet { Id = workbook.GetIdOfPart(part), SheetId = 1, Name = "Big" }));
                    workbook.Workbook.Save();
                }
                var preview = SourceText.Read(path, 1000);
                Assert.True(preview.Truncated);
                Assert.True(preview.Text.Length <= 1000);
                Assert.StartsWith("--- Big ---\n1\t2\n2\t4\n", preview.Text);
                Assert.False(SourceText.Read(Sample("excel-budget.xlsx"), 1000).Truncated);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_many_page_pdf_is_read_only_as_far_as_the_preview_needs()
        {
            var work = TestPaths.NewTempFolder();
            try
            {
                var path = Path.Combine(work, "long.pdf");
                File.WriteAllBytes(path, SampleFactory.BuildPdf(Enumerable.Range(1, 300).Select(i => $"Page text number {i} with a few words on it").ToList()));
                var preview = SourceText.Read(path, 1000);
                Assert.True(preview.Truncated);
                Assert.True(preview.Text.Length <= 1000);
                Assert.StartsWith("--- 1 ---\n", preview.Text);
                Assert.Contains("--- 2 ---", preview.Text);
                Assert.DoesNotContain("--- 300 ---", preview.Text);
                Assert.False(SourceText.Read(Sample("pdf-article.pdf"), 100_000).Truncated);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void A_file_that_is_open_elsewhere_can_be_read()
        {
            var work = TestPaths.NewTempFolder();
            try
            {
                var copy = Path.Combine(work, "open.csv");
                File.Copy(Sample("csv-comma.csv"), copy);
                using var held = new FileStream(copy, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                Assert.StartsWith("id,name,notes", SourceText.Read(copy).Text);
            }
            finally { Directory.Delete(work, true); }
        }

        [Fact]
        public void An_unknown_type_has_no_source_text()
        {
            Assert.Throws<NotSupportedException>(() => SourceText.Read(Sample("csv-comma.csv.expected.md")));
        }

        [Fact]
        public void Masked_values_are_found_and_counted_by_kind()
        {
            var markdown = "From [PERSON-1] <[EMAIL-1]> to [PERSON-2]; [PERSON-1] again. Call [PHONE-1] or [PHONE-2], IBAN [IBAN-1], ID [ID-1], card [CARD-1]. [OTHER-1] and [link](x) stay.";
            var found = RedactionMarks.Find(markdown);
            Assert.Equal(9, found.Count);
            Assert.Equal("[PERSON-1]", markdown.Substring(found[0].Start, found[0].Length));
            Assert.Equal(new[] { ("PERSON", 2), ("EMAIL", 1), ("PHONE", 2), ("ID", 1), ("IBAN", 1), ("CARD", 1) }, RedactionMarks.Count(markdown).ToArray());
            Assert.Empty(RedactionMarks.Count("Nothing masked [x-1] here"));
            Assert.Empty(RedactionMarks.Find(null));
        }

        [Fact]
        public void The_converter_masks_what_Compare_marks()
        {
            var result = DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions { EmailRedact = true });
            var counts = RedactionMarks.Count(result.Markdown).ToDictionary(k => k.Kind, k => k.Count);
            Assert.True(counts["PERSON"] >= 3);
            Assert.True(counts["EMAIL"] >= 3);
            Assert.True(counts["IBAN"] >= 1);
            Assert.True(counts["PHONE"] >= 1);
            Assert.Empty(RedactionMarks.Count(DocumentConverter.Convert(Sample("email-thread.eml"), new ConversionOptions { EmailRedact = false }).Markdown));
        }
    }
}
