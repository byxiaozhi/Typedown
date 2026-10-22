using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // Phase 2: the front matter (the properties of the file), the footnotes, the anchors inside the document, the table of contents
    // and the page numbers.
    internal sealed partial class WordBuilder
    {
        private const char DoubleQuote = (char)34, SingleQuote = (char)39;

        // The part whose relationships the text being written uses: a link or a picture in a footnote belongs to the footnotes part.
        private OpenXmlPartContainer currentPart;

        private sealed class FrontMatter
        {
            public string Title, Author, Subject, Description, Keywords, Language;
        }

        // The few keys that mean something to Word. Anything else in the front matter is left alone, and none of it is printed.
        private static FrontMatter ReadFrontMatter(MarkdownDocument tree)
        {
            var meta = new FrontMatter();
            var block = tree.OfType<YamlFrontMatterBlock>().FirstOrDefault();
            if (block == null) return meta;
            string listKey = null;
            var items = new List<string>();
            void Flush()
            {
                if (listKey != null) SetFrontMatter(meta, listKey, string.Join(", ", items));
                listKey = null;
                items.Clear();
            }
            foreach (var raw in block.Lines.ToString().Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (listKey != null && line.TrimStart().StartsWith("- ", StringComparison.Ordinal))
                {
                    items.Add(Unquote(line.TrimStart().Substring(2)));
                    continue;
                }
                Flush();
                var colon = line.IndexOf(':');
                if (colon <= 0 || char.IsWhiteSpace(line[0])) continue;
                var key = line.Substring(0, colon).Trim().ToLowerInvariant();
                var value = line.Substring(colon + 1).Trim();
                if (value.Length == 0) { listKey = key; continue; }
                if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
                    value = string.Join(", ", value.Substring(1, value.Length - 2).Split(',').Select(v => Unquote(v)).Where(v => v.Length > 0));
                else value = Unquote(value);
                SetFrontMatter(meta, key, value);
            }
            Flush();
            return meta;
        }

        private static string Unquote(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && ((value[0] == DoubleQuote && value[^1] == DoubleQuote) || (value[0] == SingleQuote && value[^1] == SingleQuote)))
                value = value.Substring(1, value.Length - 2);
            return value;
        }

        private static void SetFrontMatter(FrontMatter meta, string key, string value)
        {
            if (value.Length == 0) return;
            switch (key)
            {
                case "title": meta.Title = value; break;
                case "author" or "authors" or "creator": meta.Author = value; break;
                case "subject": meta.Subject = value; break;
                case "description" or "summary": meta.Description = value; break;
                case "keywords" or "tags": meta.Keywords = value; break;
                case "lang" or "language":
                    if (Regex.IsMatch(value, "^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$")) meta.Language = value;
                    break;
            }
        }

        // --- footnotes: a real Word footnote at every [^label], its text the paragraphs of the definition
        private FootnotesPart footnotesPart;
        private int nextFootnoteId = 1;
        private bool noteMarkPending;

        private W.Footnotes EnsureFootnotes()
        {
            if (footnotesPart != null) return footnotesPart.Footnotes;
            footnotesPart = main.AddNewPart<FootnotesPart>();
            W.Footnote Separator(int id, W.FootnoteEndnoteValues type, OpenXmlElement mark) => new(
                new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }), new W.Run(mark)))
                { Type = type, Id = id };
            footnotesPart.Footnotes = new W.Footnotes(
                Separator(-1, W.FootnoteEndnoteValues.Separator, new W.SeparatorMark()),
                Separator(0, W.FootnoteEndnoteValues.ContinuationSeparator, new W.ContinuationSeparatorMark()));
            return footnotesPart.Footnotes;
        }

        private void AppendFootnote(OpenXmlCompositeElement parent, FootnoteLink link)
        {
            if (link.IsBackLink || link.Footnote == null) return;
            var footnotes = EnsureFootnotes();
            var id = nextFootnoteId++;
            var note = new W.Footnote { Id = id };
            var (savedTarget, savedPart, savedMarker, savedGap) = (target, currentPart, pendingMarker, gapBefore);
            var savedState = state;
            state = new MarkState();
            (target, currentPart, pendingMarker, gapBefore, noteMarkPending) = (note, footnotesPart, null, false, true);
            RenderBlocks(link.Footnote, Ctx.Root with { InNote = true });
            if (!note.Elements<W.Paragraph>().Any()) AddParagraph(null, Ctx.Root with { InNote = true }, null);
            (target, currentPart, pendingMarker, gapBefore, noteMarkPending) = (savedTarget, savedPart, savedMarker, savedGap, false);
            state = savedState;
            footnotes.Append(note);
            parent.Append(new W.Run(new W.RunProperties(new W.RunStyle { Val = WordStyles.FootnoteReference }), new W.FootnoteReference { Id = id }));
        }

        // The mark that opens the text of a footnote, in front of its first paragraph.
        private void AddNoteMark(W.Paragraph paragraph)
        {
            if (!noteMarkPending) return;
            noteMarkPending = false;
            paragraph.Append(new W.Run(new W.RunProperties(new W.RunStyle { Val = WordStyles.FootnoteReference }), new W.FootnoteReferenceMark()));
            paragraph.Append(new W.Run(new W.Text(" ") { Space = SpaceProcessingModeValues.Preserve }));
        }

        // --- anchors: every heading is a bookmark, so a link to #heading-text goes there and a table of contents can point at it
        private readonly Dictionary<string, string> anchors = new();
        private readonly Dictionary<HeadingBlock, string> headingBookmarks = new();
        private readonly List<(int Level, string Text, string Bookmark)> headings = new();
        private int bookmarkId;

        private void CollectHeadings(MarkdownDocument tree)
        {
            foreach (var heading in tree.Descendants<HeadingBlock>())
            {
                var text = heading.Inline == null ? "" : PlainText(heading.Inline).Trim();
                var slug = Slug(text);
                // a second heading with the same words is -1, -2...; the number is tried until the anchor is free (a heading "Foo 1" has taken foo-1)
                var wanted = slug;
                for (var n = 1; anchors.ContainsKey(slug); n++) slug = wanted + "-" + n;
                var name = "_Caret" + (headings.Count + 1);
                anchors[slug] = name;
                headingBookmarks[heading] = name;
                headings.Add((heading.Level, text, name));
            }
        }

        // The anchor GitHub makes of a heading: lower case, no punctuation, spaces as hyphens.
        private static string Slug(string text) =>
            Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}\p{M} _-]", "").Replace(' ', '-');

        private string AnchorFor(string url)
        {
            var slug = Uri.UnescapeDataString(url.TrimStart('#')).Trim().ToLowerInvariant();
            return anchors.TryGetValue(slug, out var name) ? name : null;
        }

        // --- the table of contents: a [TOC] line, as the editor writes it. Word fills in the page numbers when it updates the fields
        // (the entries and the links to the headings are already there).
        private static bool IsTocMarker(ParagraphBlock paragraph) =>
            paragraph.Inline != null && PlainText(paragraph.Inline).Trim().Equals("[TOC]", StringComparison.OrdinalIgnoreCase);

        private static W.Run Field(W.FieldCharValues type, bool dirty = false) =>
            new(new W.FieldChar { FieldCharType = type, Dirty = dirty ? true : null });

        private static W.Run Instruction(string text) => new(new W.FieldCode(text) { Space = SpaceProcessingModeValues.Preserve });

        private void RenderToc()
        {
            pendingMarker = null;
            var entries = headings.Where(h => h.Level <= 3).ToList();
            if (entries.Count == 0) return;
            for (var i = 0; i < entries.Count; i++)
            {
                var (level, text, bookmark) = entries[i];
                var paragraph = new W.Paragraph(new W.ParagraphProperties(new W.ParagraphStyleId { Val = WordStyles.Toc(level) }));
                if (i == 0)
                {
                    paragraph.Append(Field(W.FieldCharValues.Begin, true));
                    paragraph.Append(Instruction(@" TOC \o ""1-3"" \h \z \u "));
                    paragraph.Append(Field(W.FieldCharValues.Separate));
                }
                var link = new W.Hyperlink { Anchor = bookmark, History = true };
                link.Append(TextRun(text, new Fmt { Link = true }));
                link.Append(new W.Run(new W.TabChar()));
                link.Append(Field(W.FieldCharValues.Begin, true));
                link.Append(Instruction(" PAGEREF " + bookmark + @" \h "));
                link.Append(Field(W.FieldCharValues.Separate));
                link.Append(new W.Run(new W.Text("")));
                link.Append(Field(W.FieldCharValues.End));
                paragraph.Append(link);
                if (i == entries.Count - 1) paragraph.Append(Field(W.FieldCharValues.End));
                target.Append(paragraph);
            }
            gapBefore = true;
        }

        // Word needs to know which footnotes are the separators; the settings of the file say so. A template has its settings already: the
        // footnote part of them goes where the schema has it, before the elements that come after it.
        private void AddSettings()
        {
            // the settings of a template name its footnotes and endnotes (the separators), which went with its text: a name with nothing
            // behind it makes Word say the file is corrupted
            if (main.DocumentSettingsPart?.Settings is { } existing)
            {
                existing.RemoveAllChildren<W.EndnoteDocumentWideProperties>();
                if (footnotesPart == null) existing.RemoveAllChildren<W.FootnoteDocumentWideProperties>();
            }
            if (footnotesPart == null) return;
            var part = main.DocumentSettingsPart ?? main.AddNewPart<DocumentSettingsPart>();
            part.Settings ??= new W.Settings();
            var settings = part.Settings;
            settings.RemoveAllChildren<W.FootnoteDocumentWideProperties>();
            var notes = new W.FootnoteDocumentWideProperties(new W.FootnoteSpecialReference { Id = -1 }, new W.FootnoteSpecialReference { Id = 0 });
            OpenXmlElement later = settings.GetFirstChild<W.EndnoteDocumentWideProperties>() ?? settings.GetFirstChild<W.Compatibility>()
                ?? settings.GetFirstChild<W.DocumentVariables>() ?? settings.GetFirstChild<W.Rsids>() ?? settings.GetFirstChild<W.ThemeFontLanguages>()
                ?? settings.GetFirstChild<W.ColorSchemeMapping>() ?? settings.GetFirstChild<W.DecimalSymbol>() ?? (OpenXmlElement)settings.GetFirstChild<W.ListSeparator>();
            if (later != null) settings.InsertBefore(notes, later);
            else settings.Append(notes);
        }
    }
}
