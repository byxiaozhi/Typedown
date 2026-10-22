using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // One export: the Markdown tree in, the parts of a .docx out. The blocks are here, the inlines in WordBuilder.Inlines.cs and the
    // pictures in WordBuilder.Images.cs.
    internal sealed partial class WordBuilder
    {
        // The reading of the Markdown matches the editor: tables, task lists, ~~strike~~, ~sub~ and ^super^, bare links. The
        // extension that reads ==x== as a highlight is left out on purpose: it would eat the {==x==} of a review.
        internal static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables().UseTaskLists().UseAutoLinks().UseYamlFrontMatter().UseFootnotes().UseMathematics()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough | EmphasisExtraOptions.Subscript | EmphasisExtraOptions.Superscript)
            .Build();

        // Where a paragraph is: how deep in quotes and lists, in a table, and what a table column asks of its text.
        private sealed record Ctx(int Quote, int ListLevel, bool InTable, W.JustificationValues? Align, bool Bold, bool InNote = false)
        {
            public static readonly Ctx Root = new(0, -1, false, null, false);
        }

        private readonly WordExportOptions options;
        private readonly List<string> skipped = new();
        private OpenXmlCompositeElement target;
        private W.Numbering numbering;
        private int nextNumId = WordStyles.FirstNumberedNum;
        private int textWidth;
        private (int NumId, int Level)? pendingMarker;
        // the next paragraph needs room above it: it comes after a table or a list
        private bool gapBefore;

        public WordBuilder(WordExportOptions options) => this.options = options;

        // '\r'LF and lone '\r' are line feeds, and a byte order mark is not text.
        internal static string Normalize(string markdown) =>
            (markdown ?? "").Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('﻿');

        public WordExportResult Build(string markdown, Stream stream)
        {
            var text = WordMarks.Prepare(Normalize(markdown), options, marks);
            var tree = Markdown.Parse(text, Pipeline);
            var meta = ReadFrontMatter(tree);
            // the template of the user, when there is one, is the file that is written on: its styles, page setup, headers and footers stay
            using var template = string.IsNullOrEmpty(options.TemplatePath) ? null : new WordTemplate(options.TemplatePath);
            var package = template?.Package ?? WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true);
            try
            {
                Write(package, template, meta, tree);
            }
            finally
            {
                package.Dispose();
            }
            if (template != null)
            {
                template.Stream.Position = 0;
                template.Stream.CopyTo(stream);
            }
            return new WordExportResult { Pictures = pictures, SkippedPictures = skipped };
        }

        private void Write(WordprocessingDocument package, WordTemplate template, FrontMatter meta, MarkdownDocument tree)
        {
            main = package.MainDocumentPart ?? package.AddMainDocumentPart();
            W.Body body;
            if (template != null) body = template.EmptyBody();
            else
            {
                body = new W.Body();
                main.Document = new W.Document(body);
            }
            target = body;
            currentPart = main;
            var (pageWidth, pageHeight, margin) = Geometry();
            textWidth = template != null ? template.TextWidth : pageWidth - 2 * margin;
            var styles = WordStyles.Create(meta.Language ?? options.Language ?? "en-US", textWidth, options.Look);
            var styleMap = template?.MergeStyles(styles);
            if (template == null) main.AddNewPart<StyleDefinitionsPart>().Styles = styles;
            numbering = WordStyles.CreateNumbering();
            var mergeNumbering = template?.Package.MainDocumentPart.NumberingDefinitionsPart?.Numbering != null;
            if (!mergeNumbering) main.AddNewPart<NumberingDefinitionsPart>().Numbering = numbering;

            CollectHeadings(tree);
            if (options.TableOfContents && !tree.Descendants<ParagraphBlock>().Any(IsTocMarker) && headings.Count > 0)
            {
                RenderToc();
                target.Append(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
            }
            RenderBlocks(tree, Ctx.Root);
            if (body.LastChild is W.Table) body.Append(new W.Paragraph());
            var title = meta.Title ?? FirstHeading(tree) ?? options.Title;
            body.Append(Section(template, title, pageWidth, pageHeight, margin));
            AddComments();
            AddSettings();

            if (template != null)
            {
                if (mergeNumbering) template.MergeNumbering(numbering, body, footnotesPart?.Footnotes);
                WordTemplate.Remap(body, styleMap);
                WordTemplate.Remap(footnotesPart?.Footnotes, styleMap);
                WordTemplate.Remap(main.WordprocessingCommentsPart?.Comments, styleMap);
                WordTemplate.Remap(main.StyleDefinitionsPart.Styles, styleMap);
            }
            var properties = package.PackageProperties;
            properties.Title = title;
            // a template keeps its own author and subject unless the text says otherwise
            if (template == null || meta.Author != null) properties.Creator = meta.Author;
            if (template == null || meta.Subject != null) properties.Subject = meta.Subject;
            if (template == null || meta.Description != null) properties.Description = meta.Description;
            if (template == null || meta.Keywords != null) properties.Keywords = meta.Keywords;
            properties.Created = properties.Modified = DateTime.UtcNow;
        }

        // The last section: the page, and the header and the footer. A template keeps its own; ours go in only where it has none.
        private W.SectionProperties Section(WordTemplate template, string title, int pageWidth, int pageHeight, int margin)
        {
            var section = template?.Section ?? new W.SectionProperties();
            if (!section.Elements<W.HeaderReference>().Any() && AddHeader(title) is { } headerId)
                section.InsertAt(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = headerId }, 0);
            if (!section.Elements<W.FooterReference>().Any() && AddFooter(title, textWidth) is { } footerId)
                section.InsertAt(new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = footerId }, section.Elements<W.HeaderReference>().Count());
            if (section.GetFirstChild<W.PageSize>() == null)
            {
                var pageSize = new W.PageSize { Width = (uint)pageWidth, Height = (uint)pageHeight };
                if (options.Landscape) pageSize.Orient = W.PageOrientationValues.Landscape;
                section.Append(pageSize);
            }
            if (section.GetFirstChild<W.PageMargin>() == null)
                section.Append(new W.PageMargin { Top = margin, Right = (uint)margin, Bottom = margin, Left = (uint)margin, Header = 708u, Footer = 708u, Gutter = 0u });
            return section;
        }

        private static string FirstHeading(MarkdownDocument tree)
        {
            var heading = tree.Descendants<HeadingBlock>().FirstOrDefault();
            var text = heading?.Inline == null ? null : PlainText(heading.Inline).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void RenderBlocks(ContainerBlock container, Ctx ctx)
        {
            foreach (var block in container) RenderBlock(block, ctx);
        }

        private void RenderBlock(Block block, Ctx ctx)
        {
            switch (block)
            {
                case YamlFrontMatterBlock: break; // its few known keys are the properties of the file; none of it is printed
                case FootnoteGroup or Footnote: break; // written where they are referred to
                case HeadingBlock heading: AddParagraph(heading.Inline, ctx, WordStyles.Heading(Math.Clamp(heading.Level, 1, 6)), headingBookmarks.GetValueOrDefault(heading)); break;
                case ParagraphBlock paragraph when IsTocMarker(paragraph): RenderToc(); break;
                case ParagraphBlock paragraph: AddParagraph(paragraph.Inline, ctx, null); break;
                case QuoteBlock quote: RenderBlocks(quote, ctx with { Quote = ctx.Quote + 1 }); break;
                case ListBlock list: RenderList(list, ctx); break;
                case MathBlock math: RenderMathBlock(math, ctx); break;
                case CodeBlock code: RenderCode(code, ctx); break;
                case ThematicBreakBlock: RenderRule(); break;
                case Table table: RenderTable(table, ctx); break;
                case HtmlBlock html: RenderHtml(html, ctx); break;
                case ContainerBlock container: RenderBlocks(container, ctx); break;
                case LeafBlock leaf when leaf.Inline != null: AddParagraph(leaf.Inline, ctx, null); break;
            }
        }
    }
}
