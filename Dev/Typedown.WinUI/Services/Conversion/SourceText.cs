using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Conversion
{
    // New since the fork: the left side of Compare in the conversion tray. A file as plain text, the words in it and nothing of
    // how they are formatted, and read apart from the converter (own reading of the file, not its output), so that it can be
    // put next to the Markdown to see what the conversion kept, dropped or masked. Paragraph by paragraph for Word, slide by
    // slide for PowerPoint, row by row (tab-separated) for Excel and CSV, page by page for PDF, and for an email the headers
    // and the text of the mail as they are, before anything is cleaned up or masked.
    public sealed record SourcePreview(string Text, bool Truncated);

    public static class SourceText
    {
        public const int DefaultLimit = 200_000;

        public static SourcePreview Read(string path, int limit = DefaultLimit)
        {
            // FileShare.ReadWrite: a document that is open in Word or Excel can be read too (as the conversion does).
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var prefixCut = false;
            var text = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".docx" or ".docm" or ".dotx" => Word(stream),
                ".xlsx" or ".xlsm" => ExcelConverter.PlainRows(stream, limit, out prefixCut),
                ".pptx" or ".pptm" => PowerPoint(stream),
                ".pdf" => Pdf(stream, limit, out prefixCut),
                ".csv" => Csv(stream, limit, out prefixCut),
                ".eml" => Email(EmlReader.Read(stream, HtmlText.ToMarkdown)),
                ".msg" => Email(MsgFileReader.Read(stream, HtmlText.ToMarkdown)),
                _ => throw new NotSupportedException(Path.GetExtension(path)),
            };
            var preview = Cut(text.Replace("\r\n", "\n").Trim('\n'), limit);
            return prefixCut && !preview.Truncated ? preview with { Truncated = true } : preview;
        }

        // At a line end when there is one near the limit, so a line is not shown half.
        internal static SourcePreview Cut(string text, int limit)
        {
            if (text.Length <= limit) return new SourcePreview(text, false);
            var end = text.LastIndexOf('\n', limit - 1);
            if (end < limit / 2) end = limit;
            return new SourcePreview(text[..end].TrimEnd('\n'), true);
        }

        private static string Word(Stream stream)
        {
            using var doc = WordprocessingDocument.Open(stream, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return "";
            var lines = new List<string>();
            Blocks(body, lines);
            return string.Join("\n", lines);
        }

        private static void Blocks(OpenXmlElement container, List<string> lines)
        {
            foreach (var child in container.ChildElements)
            {
                switch (child)
                {
                    case W.Paragraph paragraph:
                        lines.Add(ParagraphText(paragraph));
                        break;
                    case W.Table table:
                        foreach (var row in table.Elements<W.TableRow>())
                            lines.Add(string.Join("\t", row.Elements<W.TableCell>().Select(c => string.Join(" ", c.Descendants<W.Paragraph>().Select(ParagraphText).Where(t => t.Length > 0)))));
                        break;
                    case W.SdtBlock { SdtContentBlock: { } content }:
                        Blocks(content, lines);
                        break;
                }
            }
        }

        private static string ParagraphText(W.Paragraph paragraph)
        {
            var sb = new StringBuilder();
            foreach (var element in paragraph.Descendants())
            {
                switch (element)
                {
                    case W.Text text: sb.Append(text.Text); break;
                    case W.TabChar: sb.Append('\t'); break;
                    case W.Break or W.CarriageReturn: sb.Append('\n'); break;
                }
            }
            return sb.ToString();
        }

        private static string PowerPoint(Stream stream)
        {
            using var doc = PresentationDocument.Open(stream, false);
            var presentation = doc.PresentationPart;
            var slideIds = presentation?.Presentation?.SlideIdList?.Elements<P.SlideId>().ToList();
            if (slideIds == null) return "";
            var sb = new StringBuilder();
            var number = 0;
            foreach (var slideId in slideIds)
            {
                number++;
                if (slideId.RelationshipId?.Value == null || presentation.GetPartById(slideId.RelationshipId.Value) is not SlidePart slide) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("--- ").Append(number).Append(" ---");
                foreach (var paragraph in slide.Slide?.Descendants<A.Paragraph>() ?? Enumerable.Empty<A.Paragraph>())
                {
                    var line = string.Concat(paragraph.Descendants().Select(e => e switch { A.Text t => t.Text, A.Break => "\n", _ => "" }));
                    if (line.Length > 0) sb.Append('\n').Append(line);
                }
            }
            return sb.ToString();
        }

        // Read from the file itself (no copy of it in memory), page by page, and no further once the preview has its text.
        private static string Pdf(Stream stream, int limit, out bool cut)
        {
            cut = false;
            using var pdf = PdfDocument.Open(stream);
            var sb = new StringBuilder();
            foreach (var page in pdf.GetPages())
            {
                if (sb.Length > limit)
                {
                    cut = true;
                    break;
                }
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("--- ").Append(page.Number).Append(" ---\n").Append(ContentOrderTextExtractor.GetText(page).Trim());
            }
            return sb.ToString();
        }

        // Only the start of a big file is read: four bytes for each character that is shown is more than enough in any encoding, and the
        // cut is made at a line end (or, in UTF-16, at a character), so that a character is not split and the encoding is still found.
        private static string Csv(Stream stream, int limit, out bool cut)
        {
            var max = (long)limit * 4;
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while (buffer.Length < max && (read = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, max - buffer.Length))) > 0)
                buffer.Write(chunk, 0, read);
            cut = stream.ReadByte() >= 0;
            var bytes = buffer.ToArray();
            if (cut)
            {
                var utf16 = bytes.Length >= 2 && (bytes[0] == 0xFF && bytes[1] == 0xFE || bytes[0] == 0xFE && bytes[1] == 0xFF);
                if (utf16) bytes = bytes[..(bytes.Length & ~1)];
                else if (Array.LastIndexOf(bytes, (byte)'\n') is var end and > 0) bytes = bytes[..end];
                else bytes = WithoutIncompleteUtf8(bytes); // a row with no line end in all that: not in the middle of a letter
            }
            return Utilities.TextFileEncoding.Decode(bytes).Text;
        }

        // A UTF-8 letter cut in two would make the whole text look like a legacy code page. In another encoding this takes at most three
        // bytes off the end, which a cut text can do without.
        private static byte[] WithoutIncompleteUtf8(byte[] bytes)
        {
            var i = bytes.Length - 1;
            var continuation = 0;
            while (i >= 0 && continuation < 3 && (bytes[i] & 0xC0) == 0x80)
            {
                i--;
                continuation++;
            }
            if (i < 0) return bytes;
            var expected = bytes[i] >= 0xF0 ? 3 : bytes[i] >= 0xE0 ? 2 : bytes[i] >= 0xC0 ? 1 : 0;
            return expected > continuation ? bytes[..i] : bytes;
        }

        private static string Email(EmailDocument email)
        {
            var sb = new StringBuilder();
            void Header(string name, string value)
            {
                if (!string.IsNullOrWhiteSpace(value)) sb.Append(name).Append(": ").Append(value).Append('\n');
            }
            Header("From", email.Sender?.ToString());
            Header("To", string.Join("; ", email.To));
            Header("Cc", string.Join("; ", email.Cc));
            Header("Bcc", string.Join("; ", email.Bcc));
            Header("Date", email.Date);
            Header("Subject", email.Subject);
            sb.Append('\n').Append(email.Body);
            var names = email.Attachments.Where(a => !a.Inline).Select(a => a.FileName).ToList();
            if (names.Count > 0) sb.Append("\n\n").Append("Attachments: ").Append(string.Join("; ", names));
            return sb.ToString();
        }
    }
}
