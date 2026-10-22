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
    // Phase 3 of the Word export: the marks of a review are tracked changes and comments, the marks of a speech are gray notes.
    public class WordExportReviewTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private static readonly DateTime Moment = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        private string Export(string markdown, Action<WordExportOptions> configure = null)
        {
            var options = new WordExportOptions { BaseFolder = work, ReviewAuthor = "Reviewer", ReviewDate = Moment };
            configure?.Invoke(options);
            var path = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8) + ".docx");
            WordExporter.ExportToFile(markdown, path, options);
            using var doc = WordprocessingDocument.Open(path, false);
            var errors = new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
            return path;
        }

        private static W.Body Body(WordprocessingDocument doc) => doc.MainDocumentPart.Document.Body;

        private static bool Stray(char c) => c >= (char)0x2E02 && c <= (char)0x2E24;

        [Fact]
        public void An_addition_is_an_inserted_run_by_the_reviewer()
        {
            using var doc = WordprocessingDocument.Open(Export("Keep {++added++} text"), false);
            var paragraph = Body(doc).Elements<W.Paragraph>().Single();
            var inserted = Assert.Single(paragraph.Elements<W.InsertedRun>());
            Assert.Equal("added", inserted.InnerText);
            Assert.Equal("Reviewer", inserted.Author.Value);
            Assert.Equal(Moment, inserted.Date.Value.ToUniversalTime());
            Assert.Equal("Keep added text", paragraph.InnerText);
        }

        [Fact]
        public void A_stamp_after_a_change_says_who_and_when_and_is_not_a_comment()
        {
            using var doc = WordprocessingDocument.Open(Export("A {++new++}{>>@Ana Pérez 2026-10-09<<} word"), false);
            var inserted = Body(doc).Descendants<W.InsertedRun>().Single();
            Assert.Equal("Ana Pérez", inserted.Author.Value);
            Assert.Equal(new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc), inserted.Date.Value.ToUniversalTime());
            Assert.Null(doc.MainDocumentPart.WordprocessingCommentsPart);
            Assert.Equal("A new word", Body(doc).InnerText);
        }

        [Fact]
        public void A_deletion_is_a_deleted_run_with_deleted_text()
        {
            using var doc = WordprocessingDocument.Open(Export("Drop {--old--} words"), false);
            var deleted = Body(doc).Descendants<W.DeletedRun>().Single();
            Assert.Equal("old", deleted.Descendants<W.DeletedText>().Single().Text);
            Assert.Empty(deleted.Descendants<W.Text>());
            Assert.Equal("Reviewer", deleted.Author.Value);
        }

        [Fact]
        public void A_replacement_is_a_deletion_and_an_insertion_by_the_same_author()
        {
            using var doc = WordprocessingDocument.Open(Export("Say {~~old~>new~~}{>>@Ana 2026-10-09<<} now"), false);
            var paragraph = Body(doc).Elements<W.Paragraph>().Single();
            var deleted = paragraph.Elements<W.DeletedRun>().Single();
            var inserted = paragraph.Elements<W.InsertedRun>().Single();
            Assert.Equal("old", deleted.InnerText);
            Assert.Equal("new", inserted.InnerText);
            Assert.Equal("Ana", deleted.Author.Value);
            Assert.Equal("Ana", inserted.Author.Value);
            Assert.NotEqual(deleted.Id.Value, inserted.Id.Value);
        }

        [Fact]
        public void A_highlight_alone_is_a_yellow_highlight()
        {
            using var doc = WordprocessingDocument.Open(Export("See {==this part==} here"), false);
            var run = Body(doc).Descendants<W.Run>().Single(r => r.InnerText == "this part");
            Assert.Equal(W.HighlightColorValues.Yellow, run.RunProperties.Highlight.Val.Value);
            Assert.Null(doc.MainDocumentPart.WordprocessingCommentsPart);
        }

        [Fact]
        public void A_comment_on_a_highlight_is_a_comment_on_its_range()
        {
            using var doc = WordprocessingDocument.Open(Export("Check {==this part==}{>>@Ana Pérez 2026-10-10: why this?<<} again"), false);
            var paragraph = Body(doc).Elements<W.Paragraph>().Single();
            var start = paragraph.Elements<W.CommentRangeStart>().Single();
            var end = paragraph.Elements<W.CommentRangeEnd>().Single();
            var reference = paragraph.Descendants<W.CommentReference>().Single();
            Assert.Equal(start.Id.Value, end.Id.Value);
            Assert.Equal(start.Id.Value, reference.Id.Value);
            var children = paragraph.ChildElements.ToList();
            var covered = children.OfType<W.Run>().Single(r => r.InnerText == "this part");
            Assert.True(children.IndexOf(start) < children.IndexOf(covered));
            Assert.True(children.IndexOf(end) > children.IndexOf(covered));
            var comment = doc.MainDocumentPart.WordprocessingCommentsPart.Comments.Elements<W.Comment>().Single();
            Assert.Equal(start.Id.Value, comment.Id.Value);
            Assert.Equal("Ana Pérez", comment.Author.Value);
            Assert.Equal("AP", comment.Initials.Value);
            Assert.Equal("why this?", comment.InnerText);
            Assert.Equal(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc), comment.Date.Value.ToUniversalTime());
        }

        [Fact]
        public void A_comment_with_no_highlight_sits_at_its_place_and_two_comments_have_two_ids()
        {
            using var doc = WordprocessingDocument.Open(Export("One{>>@Ana 2026-10-10: first<<} and {==two==}{>>@Bo 2026-10-11: second<<}"), false);
            var comments = doc.MainDocumentPart.WordprocessingCommentsPart.Comments.Elements<W.Comment>().ToList();
            Assert.Equal(new[] { "first", "second" }, comments.Select(c => c.InnerText).ToArray());
            Assert.Equal(2, comments.Select(c => c.Id.Value).Distinct().Count());
            Assert.Equal(2, Body(doc).Descendants<W.CommentReference>().Count());
        }

        [Fact]
        public void Formatting_inside_a_change_stays_inside_it()
        {
            using var doc = WordprocessingDocument.Open(Export("A {++**bold** and *italic* new++} end"), false);
            var inserted = Body(doc).Descendants<W.InsertedRun>().ToList();
            Assert.Equal("bold and italic new", string.Concat(inserted.Select(i => i.InnerText)));
            Assert.Contains(inserted, i => i.Descendants<W.Bold>().Any());
            Assert.Contains(inserted, i => i.Descendants<W.Italic>().Any());
            Assert.Equal("A bold and italic new end", Body(doc).InnerText);
        }

        [Fact]
        public void Code_is_never_a_mark()
        {
            const string md = "Inline `{++x++}` and\n\n```\n{--y--}\n```";
            using var doc = WordprocessingDocument.Open(Export(md), false);
            Assert.Empty(Body(doc).Descendants<W.InsertedRun>());
            Assert.Empty(Body(doc).Descendants<W.DeletedRun>());
            Assert.Contains("{++x++}", Body(doc).InnerText);
            Assert.Contains("{--y--}", Body(doc).InnerText);
        }

        [Fact]
        public void Changes_in_headings_tables_lists_and_footnotes_are_changes_there()
        {
            const string md = "[TOC]\n\n# Title {++new++}\n\n| a |\n|---|\n| {--gone--} |\n\n- item {++added++}\n\nText[^1]\n\n[^1]: Note {~~a~>b~~}";
            using var doc = WordprocessingDocument.Open(Export(md), false);
            Assert.Equal(3, Body(doc).Descendants<W.InsertedRun>().Count() + Body(doc).Descendants<W.DeletedRun>().Count());
            var notes = doc.MainDocumentPart.FootnotesPart.Footnotes;
            Assert.Equal(2, notes.Descendants<W.InsertedRun>().Count() + notes.Descendants<W.DeletedRun>().Count());
            var toc = Body(doc).Elements<W.Paragraph>().First(p => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "TOC1");
            Assert.Contains("Title new", toc.InnerText);
            Assert.DoesNotContain(Body(doc).InnerText, Stray);
        }

        [Fact]
        public void Speech_marks_are_gray_notes_or_gone()
        {
            const string md = "Hello {pause 2s} there {slow}calmly{/slow} and {name} stays";
            using (var doc = WordprocessingDocument.Open(Export(md), false))
            {
                var gray = Body(doc).Descendants<W.Run>().Where(r => r.RunProperties?.Color?.Val?.Value == "7F7F7F").Select(r => r.InnerText).ToList();
                Assert.Equal(new[] { "{pause 2s}", "{slow}", "{/slow}" }, gray);
                Assert.Contains("{name} stays", Body(doc).InnerText);
            }
            using (var doc = WordprocessingDocument.Open(Export(md, o => o.SpeechMarks = WordSpeechMarks.Remove), false))
            {
                Assert.Equal("Hello  there calmly and {name} stays", Body(doc).InnerText);
            }
        }

        [Fact]
        public void A_speech_mark_inside_code_or_a_change_is_handled_where_it_is()
        {
            using var doc = WordprocessingDocument.Open(Export("`{pause}` and {++added {beat} text++}"), false);
            Assert.Contains("{pause}", Body(doc).InnerText);
            var inserted = Body(doc).Descendants<W.InsertedRun>().ToList();
            Assert.Equal("added {beat} text", string.Concat(inserted.Select(i => i.InnerText)));
        }

        [Fact]
        public void Odd_marks_never_stop_the_export_and_leave_no_stray_character()
        {
            foreach (var md in new[] { "{++open", "{++a++}{>>", "{==x==}{>>@Ana 2026-10-10<<}", "{>><<}", "{~~a~~}", "{++a\n\nb++}", "\\{++x++}", "{++{++nested++}++}" })
            {
                using var doc = WordprocessingDocument.Open(Export(md), false);
                Assert.DoesNotContain(Body(doc).InnerText, Stray);
            }
        }

        [Fact]
        public void The_word_converter_reads_the_file_without_a_stray_character()
        {
            var path = Export("Text {++added++} and {--cut--} and {==this==}{>>@Ana 2026-10-10: why<<}.");
            var text = DocumentConverter.Convert(path, new ConversionOptions()).Markdown;
            Assert.DoesNotContain(text, Stray);
            Assert.Contains("added", text);
        }

        [Fact]
        public void A_stamp_with_no_name_is_the_reviewer()
        {
            using var doc = WordprocessingDocument.Open(Export("A {++new++}{>>@  2026-10-09<<} and {==x==}{>>@  2026-10-09: note<<}"), false);
            Assert.Equal("Reviewer", Body(doc).Descendants<W.InsertedRun>().Single().Author.Value);
            var comment = doc.MainDocumentPart.WordprocessingCommentsPart.Comments.Elements<W.Comment>().Single();
            Assert.Equal("Reviewer", comment.Author.Value);
            Assert.Equal("R", comment.Initials.Value);
        }
    }
}
