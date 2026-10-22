using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // The template of the user: a .docx (or .dotx) of their own whose styles, page setup, headers and footers the export uses. The text of
    // the template is dropped; what is kept is what makes it theirs. The styles of the export that the template does not have are added
    // to it, so that a template with no Code style still shows code as code. Plain .NET (no WinUI).
    public sealed class WordTemplateException : Exception
    {
        public WordTemplateException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    internal sealed partial class WordTemplate : IDisposable
    {
        public MemoryStream Stream { get; } = new();

        public WordprocessingDocument Package { get; private set; }

        // The section of the template (page size, margins, headers and footers), or null when it has none.
        public W.SectionProperties Section { get; private set; }

        public int TextWidth { get; private set; } = 9026;

        public WordTemplate(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                Stream.Write(bytes, 0, bytes.Length);
                Stream.Position = 0;
                Package = WordprocessingDocument.Open(Stream, true);
                if (Package.DocumentType != WordprocessingDocumentType.Document) Package.ChangeDocumentType(WordprocessingDocumentType.Document);
                if (Package.MainDocumentPart?.Document?.Body == null) throw new WordTemplateException("It has no text part.");
            }
            catch (WordTemplateException)
            {
                Dispose();
                throw;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new WordTemplateException(ex is IOException or UnauthorizedAccessException ? ex.Message : "It is not a Word document (" + ex.Message + ").", ex);
            }
        }

        // The body, emptied: the template keeps its section, and its footnotes, endnotes and comments go with its text.
        public W.Body EmptyBody()
        {
            var main = Package.MainDocumentPart;
            var body = main.Document.Body;
            Section = body.Elements<W.SectionProperties>().LastOrDefault()?.CloneNode(true) as W.SectionProperties;
            var size = Section?.GetFirstChild<W.PageSize>();
            var margin = Section?.GetFirstChild<W.PageMargin>();
            if (size?.Width != null && margin?.Left != null && margin.Right != null)
                TextWidth = Math.Max(2000, (int)size.Width.Value - (int)margin.Left.Value - (int)margin.Right.Value);
            body.RemoveAllChildren();
            if (main.FootnotesPart != null) main.DeletePart(main.FootnotesPart);
            if (main.EndnotesPart != null) main.DeletePart(main.EndnotesPart);
            if (main.WordprocessingCommentsPart != null) main.DeletePart(main.WordprocessingCommentsPart);
            return body;
        }

        public void Dispose()
        {
            try { Package?.Dispose(); } catch (Exception) { }
            Package = null;
        }
    }
}
