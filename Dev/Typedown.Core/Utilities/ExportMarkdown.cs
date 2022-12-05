using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Typedown.Core.Models;
using System.Text.RegularExpressions;

namespace Typedown.Core.Utilities
{
    public class ExportMarkdown
    {
        private class ListType
        {
            public string Type { get; set; }
            public int ListCount { get; set; } = 1;
        }

        private readonly Stack<ListType> listType = new();

        private bool isLooseParentList = true;

        private readonly int listIndentation;

        private readonly bool listIndentationIsDfm;

        private readonly bool isGitlabCompatibilityEnabled;

        public ExportMarkdown(string listIndentation = "1", bool isGitlabCompatibilityEnabled = false)
        {
            this.isGitlabCompatibilityEnabled = isGitlabCompatibilityEnabled;
            if (!int.TryParse(listIndentation, out this.listIndentation))
            {
                listIndentationIsDfm = listIndentation == "dfm";
                this.listIndentation = 1;
            }
        }

        public string Generate(IEnumerable<MarkdownBlock> blocks)
        {
            var builder = new StringBuilder(2000000);
            TranslateBlocks2Markdown(builder, blocks);
            return builder.ToString();
        }

        public void TranslateBlocks2Markdown(StringBuilder builder, IEnumerable<MarkdownBlock> blocks, string indent = "", string listIndent = "")
        {
            var lastListBullet = string.Empty;
            var startLength = builder.Length;
            foreach (var block in blocks)
            {
                if (block.Type != "ul" && block.Type != "ol")
                    lastListBullet = string.Empty;
                switch (block.Type)
                {
                    case "p":
                    case "hr":
                        InsertLineBreak(startLength, builder, indent);
                        TranslateBlocks2Markdown(builder, block.Children, indent);
                        break;
                    case "span":
                        NormalizeParagraphText(builder, block, indent);
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        InsertLineBreak(startLength, builder, indent);
                        NormalizeHeaderText(builder, block, indent);
                        break;
                    case "figure":
                        InsertLineBreak(startLength, builder, indent);
                        switch (block.FunctionType)
                        {
                            case "table":
                                var table = block.Children[0];
                                NormalizeTable(builder, table, indent);
                                break;
                            case "html":
                                NormalizeHTML(builder, block, indent);
                                break;
                            case "footnote":
                                NormalizeFootnote(builder, block, indent);
                                break;
                            case "multiplemath":
                                NormalizeMultipleMath(builder, block, indent);
                                break;
                            case "mermaid":
                            case "flowchart":
                            case "sequence":
                            case "plantuml":
                            case "vega-lite":
                                NormalizeContainer(builder, block, indent);
                                break;
                        }
                        break;
                    case "li":
                        var insertNewLine1 = block.IsLooseListItem;
                        isLooseParentList = insertNewLine1;
                        if (insertNewLine1)
                            InsertLineBreak(startLength, builder, indent);
                        NormalizeListItem(builder, block, indent + listIndent);
                        isLooseParentList = true;
                        break;
                    case "ul":
                        var insertNewLine2 = isLooseParentList;
                        isLooseParentList = true;
                        // Start a new list without separation due changing the bullet or ordered list delimiter starts a new list.
                        var bulletMarkerOrDelimiter1 = block.Children[0].BulletMarkerOrDelimiter;
                        if (!string.IsNullOrEmpty(lastListBullet) && lastListBullet != bulletMarkerOrDelimiter1)
                            insertNewLine2 = false;
                        lastListBullet = bulletMarkerOrDelimiter1;
                        if (insertNewLine2)
                        {
                            InsertLineBreak(startLength, builder, indent);
                        }
                        listType.Push(new() { Type = "ul" });
                        NormalizeList(builder, block, indent, listIndent);
                        listType.Pop();
                        break;
                    case "ol":
                        var insertNewLine3 = isLooseParentList;
                        isLooseParentList = true;
                        var bulletMarkerOrDelimiter2 = block.Children[0].BulletMarkerOrDelimiter;
                        if (!string.IsNullOrEmpty(lastListBullet) && lastListBullet != bulletMarkerOrDelimiter2)
                            insertNewLine3 = false;
                        lastListBullet = bulletMarkerOrDelimiter2;
                        if (insertNewLine3)
                            InsertLineBreak(startLength, builder, indent);
                        var listCount = block.Start ?? 1;
                        listType.Push(new() { Type = "ol", ListCount = listCount });
                        NormalizeList(builder, block, indent, listIndent);
                        listType.Pop();
                        break;
                    case "pre":
                        InsertLineBreak(startLength, builder, indent);
                        if (block.FunctionType == "frontmatter")
                            NormalizeFrontMatter(builder, block, indent);
                        else
                            NormalizeCodeBlock(builder, block, indent);
                        break;
                    case "blockquote":
                        InsertLineBreak(startLength, builder, indent);
                        NormalizeBlockquote(builder, block, indent);
                        break;
                }
            }
        }

        public void InsertLineBreak(int startLength, StringBuilder builder, string indent)
        {
            if (builder.Length - startLength > 0)
                builder.Append(indent).Append('\n');
        }

        public void NormalizeParagraphText(StringBuilder builder, MarkdownBlock block, string indent)
        {
            foreach (var line in block.Text.Split('\n'))
                builder.Append(indent).Append(line).Append('\n');
        }

        public void NormalizeHeaderText(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var headingStyle = block.HeadingStyle;
            var text = block.Children[0].Text;
            if (headingStyle == "atx")
            {
                var match = Regex.Match(text, @"(#{1,6})(.*)");
                builder.Append(indent).Append(match.Groups[1]).Append(' ').Append(match.Groups[2].ToString().Trim()).Append('\n');
            }
            else if (headingStyle == "setext")
            {
                var marker = block.Marker ?? string.Empty;
                foreach (var line in text.Split('\n'))
                    builder.Append(indent).Append(line).Append('\n');
                builder.Append(indent).Append(marker.Trim()).Append('\n');
            }
        }

        public void NormalizeBlockquote(StringBuilder builder, MarkdownBlock block, string indent)
        {
            TranslateBlocks2Markdown(builder, block.Children, indent + "> ");
        }

        public void NormalizeFrontMatter(StringBuilder builder, MarkdownBlock block, string indent)
        {
            string startToken = string.Empty;
            string endToken = string.Empty;
            switch (block.Lang)
            {
                case "yaml":
                    startToken = "---\n";
                    endToken = "---\n";
                    break;
                case "toml":
                    startToken = "+++\n";
                    endToken = "+++\n'";
                    break;
                case "json":
                    if (block.Style == ";")
                    {
                        startToken = ";;;\n";
                        endToken = ";;;\n";
                    }
                    else
                    {
                        startToken = "{\n";
                        endToken = "}\n";
                    }
                    break;
            }
            builder.Append(startToken);
            foreach (var line in block.Children[0].Children)
                builder.Append(line.Text).Append('\n');
            builder.Append(endToken);
        }

        public void NormalizeMultipleMath(StringBuilder builder, MarkdownBlock block, string indent)
        {
            string startToken = "$$";
            string endToken = "$$";
            if (isGitlabCompatibilityEnabled && block.MathStyle == "gitlab")
            {
                startToken = "```math";
                endToken = "```";
            }
            builder.Append(indent).Append(startToken).Append('\n');
            foreach (var line in block.Children[0].Children[0].Children)
                builder.Append(indent).Append(line.Text).Append('\n');
            builder.Append(indent).Append(endToken).Append('\n');
        }

        public void NormalizeContainer(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var diagramType = block.Children[0].FunctionType;
            builder.Append("```").Append(diagramType).Append('\n');
            foreach (var line in block.Children[0].Children[0].Children)
                builder.Append(line.Text).Append('\n');
            builder.Append("```\n");
        }

        public void NormalizeCodeBlock(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var codeContent = block.Children[1].Children[0];
            var textList = codeContent.Text.Split('\n');
            var functionType = block.FunctionType;
            if (functionType == "fencecode")
            {
                builder.Append(indent).Append("```").Append(block.Lang ?? "").Append('\n');
                foreach (var text in textList)
                    builder.Append(indent).Append(text).Append('\n');
                builder.Append(indent).Append("```\n");
            }
            else
            {
                foreach (var text in textList)
                    builder.Append(indent).Append(new string(' ', 4)).Append(text).Append('\n');
            }
        }

        public void NormalizeHTML(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var codeContentText = block.Children[0].Children[0].Children[0].Text;
            var lines = codeContentText.Split('\n');
            foreach (var text in lines)
                builder.Append(indent).Append(text).Append('\n');
        }

        public void NormalizeTable(StringBuilder builder, MarkdownBlock table, string indent)
        {
            var row = table.Row;
            var column = table.Column;
            var tableData = new List<List<string>>();
            var tHeader = table.Children[0];
            var escapeText = (string str) => Regex.Replace(str, @"([^\\])\|", @"$1\\|");
            tableData.Add(tHeader.Children[0].Children.Select(th => escapeText(th.Children[0].Text).Trim()).ToList());
            if (table.Children.Count > 1)
            {
                var tBody = table.Children[1];
                tBody.Children.ForEach(bodyRow =>
                {
                    tableData.Add(bodyRow.Children.Select(td => escapeText(td.Children[0].Text).Trim()).ToList());
                });
            }
            var columnWidth = tHeader.Children[0].Children.Select(th => (width: 5, align: th.Align)).ToList();
            for (int i = 0; i <= row; i++)
            {
                for (int j = 0; j <= column; j++)
                {
                    columnWidth[j] = (Math.Max(columnWidth[j].width, tableData[i][j].Length + 2), columnWidth[j].align);// add 2, because have two space around text
                }
            }
            for (int i = 0; i <= row; i++)
            {
                var r = tableData[i];
                builder.Append(indent).Append('|');
                builder.AppendJoin('|', r.Select((cell, j) =>
                {
                    var raw = " " + cell + new string(' ', columnWidth[j].width);
                    return raw.Substring(0, columnWidth[j].width);
                }));
                builder.Append("|\n");
                if (i == 0)
                {
                    builder.Append(indent).Append('|');
                    builder.AppendJoin('|', columnWidth.Select(x =>
                    {
                        var raw = new string('-', x.width - 2);
                        switch (x.align)
                        {
                            case "left":
                                raw = ':' + raw + ' ';
                                break;
                            case "center":
                                raw = ':' + raw + ':';
                                break;
                            case "right":
                                raw = ' ' + raw + ':';
                                break;
                            default:
                                raw = ' ' + raw + ' ';
                                break;
                        }
                        return raw;
                    }));
                    builder.Append("|\n");
                }
            }
        }

        public void NormalizeList(StringBuilder builder, MarkdownBlock block, string indent, string listIndent)
        {
            TranslateBlocks2Markdown(builder, block.Children, indent, listIndent);
        }

        public void NormalizeListItem(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var listInfo = listType.Peek();
            var isUnorderedList = listInfo.Type == "ul";
            var children = block.Children as IEnumerable<MarkdownBlock>;
            var bulletMarkerOrDelimiter = block.BulletMarkerOrDelimiter;
            string itemMarker;

            if (isUnorderedList)
            {
                itemMarker = (string.IsNullOrEmpty(bulletMarkerOrDelimiter) ? "-" : bulletMarkerOrDelimiter) + ' ';
            }
            else
            {
                // NOTE: GitHub and Bitbucket limit the list count to 99 but this is nowhere defined.
                //  We limit the number to 99 for Daring Fireball Markdown to prevent indentation issues.
                var n = listInfo.ListCount;
                if ((listIndentationIsDfm && n > 99) || n > 999999999)
                    n = 1;
                listInfo.ListCount++;
                var delimiter = string.IsNullOrEmpty(bulletMarkerOrDelimiter) ? "." : bulletMarkerOrDelimiter;
                itemMarker = n.ToString() + delimiter + ' ';
            }

            // Subsequent paragraph indentation
            var newIndent = indent + new string(' ', itemMarker.Length);

            // New list indentation. We already added one space to the indentation
            string listIndent;
            if (listIndentationIsDfm)
                listIndent = new string(' ', 4 - itemMarker.Length);
            else
                listIndent = new string(' ', listIndentation - 1);

            // TODO: Indent subsequent paragraphs by one tab. - not important
            //  Problem: "translateBlocks2Markdown" use "indent" in spaces to indent elements. How should
            //  we integrate tabs in blockquotes and subsequent paragraphs and how to combine with spaces?
            //  I don't know how to combine tabs and spaces and it seems not specified, so work for another day.

            if (isUnorderedList && block.ListItemType == "task")
            {
                var firstChild = children.First();
                itemMarker += firstChild.Checked ? "[x] " : "[ ] ";
                children = children.Skip(1);
            }

            builder.Append(indent).Append(itemMarker);
            var startIndex = builder.Length;
            TranslateBlocks2Markdown(builder, children, newIndent, listIndent);
            builder.Remove(startIndex, newIndent.Length);
        }

        public void NormalizeFootnote(StringBuilder builder, MarkdownBlock block, string indent)
        {
            var identifier = block.Children[0].Text;
            builder.Append(indent).Append("[^").Append(identifier).Append("]:");
            var hasMultipleBlocks = block.Children.Count > 2 || block.Children[1].Type != "p";
            if (hasMultipleBlocks)
            {
                builder.Append('\n');
                TranslateBlocks2Markdown(builder, block.Children.Skip(1), indent + new string(' ', 4));
            }
            else
            {
                builder.Append(' ');
                var paragraphContent = block.Children[1].Children[0];
                NormalizeParagraphText(builder, paragraphContent, indent);
            }
        }
    }
}
