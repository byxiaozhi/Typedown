using System;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // Phase 4: the page (size, orientation, margins), its header and footer.
    internal sealed partial class WordBuilder
    {
        // The page in twips (a twentieth of a point): width, height, and the margin all around.
        private (int Width, int Height, int Margin) Geometry()
        {
            var (width, height) = options.PageSize switch
            {
                WordPageSize.Letter => (12240, 15840),
                WordPageSize.Legal => (12240, 20160),
                _ => (11906, 16838),
            };
            if (options.Landscape) (width, height) = (height, width);
            var margin = options.Margins switch { WordMargins.Narrow => 720, WordMargins.Wide => 1800, _ => 1440 };
            return (width, height, margin);
        }

        // {title} and {date} in the text of a header or a footer: the title of the document and today, in the language of the file.
        private string Tokens(string text, string title)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            CultureInfo culture;
            try { culture = new CultureInfo(options.Language ?? "en-US"); }
            catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
            return text.Replace("{title}", title ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", DateTime.Today.ToString("D", culture), StringComparison.OrdinalIgnoreCase).Trim();
        }

        private static W.Run Small(string text) =>
            new(new W.RunProperties(new W.Color { Val = "595959" }, new W.FontSize { Val = "20" }), new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });

        // The text of the header, centered, small and gray; null when there is none.
        private string AddHeader(string title)
        {
            var text = Tokens(options.HeaderText, title);
            if (text.Length == 0) return null;
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(
                new W.ParagraphProperties(new W.SpacingBetweenLines { After = "0" }, new W.Justification { Val = W.JustificationValues.Center }),
                Small(text)));
            return main.GetIdOfPart(header);
        }

        // The footer: the text at the left and the page number at the right, or either alone, centered; null when there is neither.
        private string AddFooter(string title, int textWidth)
        {
            var text = Tokens(options.FooterText, title);
            var number = options.PageNumbers;
            if (text.Length == 0 && !number) return null;
            var props = new W.ParagraphProperties();
            if (text.Length > 0 && number) props.Append(new W.Tabs(new W.TabStop { Val = W.TabStopValues.Right, Position = textWidth }));
            props.Append(new W.SpacingBetweenLines { After = "0" });
            if (text.Length == 0 || !number) props.Append(new W.Justification { Val = W.JustificationValues.Center });
            var paragraph = new W.Paragraph(props);
            if (text.Length > 0) paragraph.Append(Small(text));
            if (text.Length > 0 && number) paragraph.Append(new W.Run(new W.TabChar()));
            if (number)
                paragraph.Append(new W.SimpleField(new W.Run(new W.RunProperties(new W.NoProof(), new W.Color { Val = "595959" }, new W.FontSize { Val = "20" }), new W.Text("1"))) { Instruction = " PAGE " });
            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new W.Footer(paragraph);
            return main.GetIdOfPart(footer);
        }
    }
}
