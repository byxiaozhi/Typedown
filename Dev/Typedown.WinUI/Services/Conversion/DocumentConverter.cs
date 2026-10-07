using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.WinUI.Services.Conversion
{
    // New since the fork: Caret's own document-to-Markdown conversion. Word, Excel, PowerPoint, PDF and
    // CSV are converted in-process (Open XML SDK, PdfPig) — no Python, no pip, no network — so it works
    // on locked-down machines and passes Store review. Plain .NET with no UI dependency: everything
    // user-visible it needs (slide headings, the notes label) comes in through ConversionOptions, and
    // warnings go back as codes the UI localizes.
    public sealed class ConversionOptions
    {
        // Where extracted images are written; null skips images entirely (smallest output).
        public string ImageDirectory { get; set; }

        // How the Markdown refers to that folder, e.g. "report_images" (relative to the .md file).
        public string ImageLinkPrefix { get; set; }

        public string SlideHeadingFormat { get; set; } = "Slide {0}: {1}";
        public string SlideHeadingUntitledFormat { get; set; } = "Slide {0}";
        public string NotesLabel { get; set; } = "Notes";

        // Emails (.msg, .eml): mask names, addresses, phone numbers, IBANs and IDs with placeholders.
        public bool EmailRedact { get; set; } = true;

        // Emails: convert attachments (Word, Excel, PDF...) into the same Markdown file.
        public bool EmailAttachments { get; set; } = true;

        // Emails: keep signatures instead of cutting them after the closing and name.
        public bool EmailKeepSignatures { get; set; }

        // Emails: extra names to mask (customers, projects), besides the people in the mail's headers.
        public IReadOnlyList<string> EmailNames { get; set; } = Array.Empty<string>();
    }

    public enum ConversionWarning
    {
        // The PDF has no text layer — probably a scan. There's no OCR.
        PdfHasNoText,
        // Some images were in a format a browser can't show (EMF/WMF) and were left out.
        SkippedUnsupportedImages,
        // The workbook had no visible sheet with data.
        NoData,
    }

    public sealed class ConversionResult
    {
        public string Markdown { get; init; } = "";
        public IReadOnlyList<ConversionWarning> Warnings { get; init; } = Array.Empty<ConversionWarning>();
        public int ImageCount { get; init; }
    }

    public static class DocumentConverter
    {
        private static readonly Dictionary<string, Func<Stream, ConversionContext, string>> converters = new(StringComparer.OrdinalIgnoreCase)
        {
            [".docx"] = WordConverter.Convert,
            [".docm"] = WordConverter.Convert,
            [".dotx"] = WordConverter.Convert,
            [".xlsx"] = ExcelConverter.Convert,
            [".xlsm"] = ExcelConverter.Convert,
            [".pptx"] = PowerPointConverter.Convert,
            [".pptm"] = PowerPointConverter.Convert,
            [".pdf"] = PdfConverter.Convert,
            [".csv"] = CsvConverter.Convert,
            [".msg"] = EmailConverter.ConvertMsg,
            [".eml"] = EmailConverter.ConvertEml,
        };

        public static IReadOnlyCollection<string> SupportedExtensions => converters.Keys;

        public static bool IsSupported(string path) => converters.ContainsKey(Path.GetExtension(path ?? ""));

        public static Task<ConversionResult> ConvertAsync(string path, ConversionOptions options, CancellationToken cancellationToken = default) =>
            Task.Run(() => Convert(path, options ?? new ConversionOptions()), cancellationToken);

        public static ConversionResult Convert(string path, ConversionOptions options)
        {
            if (!converters.TryGetValue(Path.GetExtension(path), out var convert))
                throw new NotSupportedException(Path.GetExtension(path));
            // FileShare.ReadWrite: a document the user still has open in Word or Excel can be read too.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var context = new ConversionContext(options) { SourceName = Path.GetFileName(path) };
            var markdown = MarkdownText.Tidy(convert(stream, context));
            return new ConversionResult { Markdown = markdown, Warnings = context.Warnings.Distinct().ToList(), ImageCount = context.ImageCount };
        }

        // Converts an attachment inside an email with the same context (so its images are numbered after
        // the ones already saved). False for formats without a converter.
        internal static bool TryConvertStream(string extension, Stream stream, ConversionContext context, out string markdown)
        {
            markdown = null;
            if (string.IsNullOrEmpty(extension) || !converters.TryGetValue(extension, out var convert) || extension is ".msg" or ".eml") return false;
            markdown = convert(stream, context);
            return true;
        }

        // A rough, tokenizer-independent estimate (about four characters per token for English prose,
        // a little fewer for French and Spanish). Shown with "≈" — it's for comparing, not billing.
        public static int EstimateTokens(string text) => string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
    }

    internal sealed class ConversionContext
    {
        private int imageNumber;

        public ConversionContext(ConversionOptions options) => Options = options;

        public ConversionOptions Options { get; }

        // The file name, recorded in an email's front matter.
        public string SourceName { get; init; }

        public List<ConversionWarning> Warnings { get; } = new();

        public int ImageCount { get; private set; }

        // Writes an image to the image folder and returns its Markdown, or "" when images are off or the
        // format can't be shown.
        public string SaveImage(Stream data, string contentType, string altText)
        {
            if (string.IsNullOrEmpty(Options.ImageDirectory)) return "";
            var extension = contentType?.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/tiff" => ".tif",
                "image/svg+xml" => ".svg",
                "image/webp" => ".webp",
                _ => null,
            };
            if (extension == null)
            {
                Warnings.Add(ConversionWarning.SkippedUnsupportedImages);
                return "";
            }
            Directory.CreateDirectory(Options.ImageDirectory);
            var name = $"image{++imageNumber}{extension}";
            using (var file = File.Create(Path.Combine(Options.ImageDirectory, name)))
                data.CopyTo(file);
            ImageCount++;
            var link = string.IsNullOrEmpty(Options.ImageLinkPrefix) ? name : $"{Options.ImageLinkPrefix.TrimEnd('/')}/{name}";
            return $"![{MarkdownText.EscapeInline(altText ?? "").Replace("\n", " ")}]({link.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29")})";
        }
    }
}
