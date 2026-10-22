using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    internal sealed partial class WordTemplate
    {
        // The styles of the export are added to those of the template (the template wins where both have one: by id, then by name). The
        // result says which style of the template stands for which of ours, for the ones whose id differs.
        public Dictionary<string, string> MergeStyles(W.Styles ours)
        {
            var main = Package.MainDocumentPart;
            var part = main.StyleDefinitionsPart ?? main.AddNewPart<StyleDefinitionsPart>();
            part.Styles ??= new W.Styles();
            var theirs = part.Styles;
            var ids = new HashSet<string>(theirs.Elements<W.Style>().Select(s => s.StyleId?.Value).Where(i => i != null));
            var names = new Dictionary<(W.StyleValues?, string), string>();
            foreach (var style in theirs.Elements<W.Style>())
            {
                var key = (style.Type?.Value, (style.StyleName?.Val?.Value ?? "").ToLowerInvariant());
                if (style.StyleId?.Value != null) names.TryAdd(key, style.StyleId.Value);
            }
            var map = new Dictionary<string, string>();
            foreach (var style in ours.Elements<W.Style>().ToList())
            {
                var id = style.StyleId.Value;
                if (ids.Contains(id)) continue;
                var key = (style.Type?.Value, (style.StyleName?.Val?.Value ?? "").ToLowerInvariant());
                if (names.TryGetValue(key, out var existing)) { map[id] = existing; continue; }
                theirs.Append(style.CloneNode(true));
                ids.Add(id);
            }
            if (theirs.DocDefaults == null && ours.DocDefaults != null) theirs.InsertAt(ours.DocDefaults.CloneNode(true), 0);
            return map;
        }

        // The ids of our styles in what was written (paragraphs, runs, tables, and the styles that were added) as the template names them.
        public static void Remap(OpenXmlElement root, Dictionary<string, string> map)
        {
            if (map.Count == 0 || root == null) return;
            foreach (var e in root.Descendants<W.ParagraphStyleId>()) if (e.Val?.Value != null && map.TryGetValue(e.Val.Value, out var m)) e.Val = m;
            foreach (var e in root.Descendants<W.RunStyle>()) if (e.Val?.Value != null && map.TryGetValue(e.Val.Value, out var m)) e.Val = m;
            foreach (var e in root.Descendants<W.TableStyle>()) if (e.Val?.Value != null && map.TryGetValue(e.Val.Value, out var m)) e.Val = m;
            foreach (var e in root.Descendants<W.BasedOn>()) if (e.Val?.Value != null && map.TryGetValue(e.Val.Value, out var m)) e.Val = m;
            foreach (var e in root.Descendants<W.NextParagraphStyle>()) if (e.Val?.Value != null && map.TryGetValue(e.Val.Value, out var m)) e.Val = m;
        }
    }
}
