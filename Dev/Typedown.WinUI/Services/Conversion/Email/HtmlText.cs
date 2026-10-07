using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using ReverseMarkdown;

namespace Typedown.WinUI.Services.Conversion
{
    // HTML mail bodies (and .html attachments) as Markdown. Most mail from recent Outlook versions has no
    // plain-text body at all, so this is the text the thread splitter usually works on.
    internal static class HtmlText
    {
        private static readonly Regex EscapedSeparator = new(@"^[ \t]*(?:\\?[_\-=]){10,}[ \t]*$", RegexOptions.Multiline);

        private static readonly ReverseMarkdown.Config Settings = new()
        {
            GithubFlavored = true,
            Tags = { Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass },
            Links = { SmartHref = true },
            Formatting = { RemoveComments = true, ListBulletChar = '*' },
        };

        public static string ToMarkdown(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            var document = new HtmlParser().ParseDocument(html);

            // Outlook's <head> carries a large <style> block; none of this is text
            foreach (var element in document.QuerySelectorAll("head, style, script, title, xml").ToList())
                element.Remove();
            // Images embedded in the mail (signature logos, pasted screenshots) point at parts that aren't
            // in the Markdown; a "![](cid:image001.png@01DB...)" line is only noise
            foreach (var image in document.QuerySelectorAll("img").Where(i => IsEmbedded(i.GetAttribute("src"))).ToList())
                image.Remove();
            // Mail layout (signatures above all) is built with tables; only real data tables stay tables.
            // Innermost first, so an outer table sees what is left inside it.
            foreach (var table in document.QuerySelectorAll("table").OfType<IHtmlTableElement>().Reverse().ToList())
                if (IsLayout(table)) Unwrap(document, table);

            var markdown = new Converter(Settings).Convert(document.Body?.InnerHtml ?? "").Replace("\r\n", "\n");
            // "\_\_\_\_..." is the separator line Outlook puts above a quoted header block
            markdown = EscapedSeparator.Replace(markdown, m => m.Value.Replace("\\", ""));
            // ReverseMarkdown keeps "<" and ">" as entities; the thread splitter needs the text as the
            // reader sees it ("Anna Nowak <anna@x.fr>"), like the plugin's converter gives it
            return WebUtility.HtmlDecode(markdown);
        }

        private static bool IsEmbedded(string source) =>
            source != null && (source.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) || source.StartsWith("data:", StringComparison.OrdinalIgnoreCase));

        private static bool IsLayout(IHtmlTableElement table)
        {
            var rows = table.Rows.ToList();
            if (rows.Count < 2 || rows.Max(r => r.Cells.Length) < 2) return true;
            if (table.QuerySelector("table, img") != null) return true;
            // A data cell holds one line of text; a layout cell holds blocks (name, title, phone...)
            return rows.SelectMany(r => r.Cells).Any(c =>
                c.QuerySelector("br") != null || c.Children.Count(e => e.LocalName is "p" or "div" && e.TextContent.Trim().Length > 0) > 1);
        }

        private static void Unwrap(IHtmlDocument document, IHtmlTableElement table)
        {
            var replacement = document.CreateElement("div");
            foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                {
                    var block = document.CreateElement("div");
                    while (cell.FirstChild != null) block.AppendChild(cell.FirstChild);
                    replacement.AppendChild(block);
                }
            table.Replace(replacement);
        }
    }
}
