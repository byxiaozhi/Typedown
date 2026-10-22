using System;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    internal sealed partial class WordBuilder
    {
        // The look the text has at this point: it travels down into emphasis and links, and a tag such as <u> changes it for what follows.
        private sealed class Fmt
        {
            public bool Bold, Italic, Strike, Underline, Code, Sub, Sup, Link, Highlight, Muted;
            public Fmt Clone() => (Fmt)MemberwiseClone();
        }

        private MainDocumentPart main;

        private void AppendInlines(OpenXmlCompositeElement parent, ContainerInline container, Fmt fmt)
        {
            for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling) AppendInline(parent, inline, fmt);
        }

        private void AppendInline(OpenXmlCompositeElement parent, Inline inline, Fmt fmt)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    AddText(parent, literal.Content.ToString(), fmt);
                    break;
                case EmphasisInline emphasis:
                    var inner = fmt.Clone();
                    if (emphasis.DelimiterChar is '*' or '_')
                    {
                        if (emphasis.DelimiterCount >= 2) inner.Bold = true;
                        if (emphasis.DelimiterCount != 2) inner.Italic = true;
                    }
                    else if (emphasis.DelimiterChar == '~') { if (emphasis.DelimiterCount >= 2) inner.Strike = true; else inner.Sub = true; }
                    else if (emphasis.DelimiterChar == '^') inner.Sup = true;
                    AppendInlines(parent, emphasis, inner);
                    break;
                case CodeInline code:
                    var codeFmt = fmt.Clone();
                    codeFmt.Code = true;
                    AddText(parent, code.Content, codeFmt);
                    break;
                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard || options.SoftBreaksAsLineBreaks) parent.Append(new W.Run(new W.Break()));
                    else AddText(parent, " ", fmt);
                    break;
                case LinkInline { IsImage: true } image:
                    var picture = PictureRun(image.Url, PlainText(image), null, null, null);
                    parent.Append(picture ?? PictureFallback(image.Url, PlainText(image), fmt));
                    break;
                case LinkInline link:
                    AppendLink(parent, link, fmt);
                    break;
                case MathInline math:
                    parent.Append(new M.OfficeMath(LatexToOmml.Convert(WordMarks.Strip(math.Content.ToString()))));
                    break;
                case FootnoteLink note:
                    AppendFootnote(parent, note);
                    break;
                case AutolinkInline auto:
                    var address = auto.IsEmail ? "mailto:" + auto.Url : auto.Url;
                    if (Uri.TryCreate(address, UriKind.RelativeOrAbsolute, out var autoUri))
                    {
                        var autoLink = new W.Hyperlink { Id = currentPart.AddHyperlinkRelationship(autoUri, true).Id, History = true };
                        var autoFmt = fmt.Clone();
                        autoFmt.Link = true;
                        AddText(autoLink, auto.Url, autoFmt);
                        parent.Append(autoLink);
                    }
                    else AddText(parent, auto.Url, fmt);
                    break;
                case HtmlEntityInline entity:
                    AddText(parent, entity.Transcoded.ToString(), fmt);
                    break;
                case TaskList task:
                    AddText(parent, task.Checked ? "☑" : "☐", fmt); // the space after it is the text
                    break;
                case HtmlInline html:
                    AppendHtmlTag(parent, html.Tag, fmt);
                    break;
                case ContainerInline container:
                    AppendInlines(parent, container, fmt);
                    break;
            }
        }

        // The words of an inline tree (the alt text of a picture): the text and the code, without the marks around them.
        private static string PlainText(ContainerInline container) => WordMarks.Strip(PlainTextOf(container));

        private static string PlainTextOf(ContainerInline container)
        {
            var text = new System.Text.StringBuilder();
            for (var inline = container.FirstChild; inline != null; inline = inline.NextSibling)
            {
                if (inline is LiteralInline literal) text.Append(literal.Content.ToString());
                else if (inline is CodeInline code) text.Append(code.Content);
                else if (inline is ContainerInline inner) text.Append(PlainTextOf(inner));
            }
            return text.ToString();
        }

        private void AppendLink(OpenXmlCompositeElement parent, LinkInline link, Fmt fmt)
        {
            var url = link.Url;
            // A link to a heading of the document goes to its bookmark; one that matches no heading stays text, not a dead link.
            var anchor = !string.IsNullOrWhiteSpace(url) && url.StartsWith('#') ? AnchorFor(url) : null;
            Uri uri = null;
            if (string.IsNullOrWhiteSpace(url) || (anchor == null && (url.StartsWith('#') || !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))))
            {
                AppendInlines(parent, link, fmt);
                return;
            }
            var hyperlink = anchor != null
                ? new W.Hyperlink { Anchor = anchor, History = true }
                : new W.Hyperlink { Id = currentPart.AddHyperlinkRelationship(uri, true).Id, History = true };
            var linked = fmt.Clone();
            linked.Link = true;
            if (link.FirstChild == null) AddText(hyperlink, url, linked);
            else AppendInlines(hyperlink, link, linked);
            parent.Append(hyperlink);
        }

        // The tags that mean something in the text. Anything else is dropped and its text stays.
        private void AppendHtmlTag(OpenXmlCompositeElement parent, string tag, Fmt fmt)
        {
            var name = Regex.Match(tag ?? "", @"^<\s*(/?)\s*([a-zA-Z0-9]+)");
            if (!name.Success) return;
            var closing = name.Groups[1].Value == "/";
            switch (name.Groups[2].Value.ToLowerInvariant())
            {
                case "br": parent.Append(new W.Run(new W.Break())); break;
                case "u": fmt.Underline = !closing; break;
                case "sub": fmt.Sub = !closing; break;
                case "sup": fmt.Sup = !closing; break;
                case "b" or "strong": fmt.Bold = !closing; break;
                case "i" or "em": fmt.Italic = !closing; break;
                case "s" or "del" or "strike": fmt.Strike = !closing; break;
                case "code" or "kbd": fmt.Code = !closing; break;
                case "img":
                    var picture = HtmlPicture(tag);
                    if (picture != null) parent.Append(picture);
                    break;
            }
        }

        private static W.Run TextRun(string text, Fmt f)
        {
            var props = new W.RunProperties();
            if (f.Link) props.Append(new W.RunStyle { Val = WordStyles.Hyperlink });
            else if (f.Code) props.Append(new W.RunStyle { Val = WordStyles.CodeChar });
            if (f.Bold) props.Append(new W.Bold());
            if (f.Italic) props.Append(new W.Italic());
            if (f.Strike) props.Append(new W.Strike());
            if (f.Muted)
            {
                props.Append(new W.Color { Val = "7F7F7F" });
                props.Append(new W.FontSize { Val = "18" });
                props.Append(new W.FontSizeComplexScript { Val = "18" });
            }
            if (f.Highlight) props.Append(new W.Highlight { Val = W.HighlightColorValues.Yellow });
            if (f.Underline && !f.Link) props.Append(new W.Underline { Val = W.UnderlineValues.Single });
            if (f.Sup) props.Append(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript });
            else if (f.Sub) props.Append(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Subscript });
            var run = new W.Run();
            if (props.HasChildren) run.Append(props);
            run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }
    }
}
