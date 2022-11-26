using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Typedown.Universal.Models;
using System.Text.RegularExpressions;

namespace Typedown.Universal.Utilities
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
            return TranslateBlocks2Markdown(blocks);
        }

        public string TranslateBlocks2Markdown(IEnumerable<MarkdownBlock> blocks, string indent = "", string listIndent = "")
        {
            var result = new StringBuilder();
            var lastListBullet = string.Empty;
            foreach (var block in blocks)
            {
                if (block.Type != "ul" && block.Type != "ol")
                    lastListBullet = string.Empty;
                switch (block.Type)
                {
                    case "p":
                    case "hr":
                        InsertLineBreak(result, indent);
                        result.Append(TranslateBlocks2Markdown(block.Children, indent));
                        break;
                    case "span":
                        result.Append(NormalizeParagraphText(block, indent));
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        InsertLineBreak(result, indent);
                        result.Append(NormalizeHeaderText(block, indent));
                        break;
                    case "figure":
                        InsertLineBreak(result, indent);
                        switch (block.FunctionType)
                        {
                            case "table":
                                var table = block.Children[0];
                                result.Append(NormalizeTable(table, indent));
                                break;
                            case "html":
                                result.Append(NormalizeHTML(block, indent));
                                break;
                            case "footnote":
                                result.Append(NormalizeFootnote(block, indent));
                                break;
                            case "multiplemath":
                                result.Append(NormalizeMultipleMath(block, indent));
                                break;
                            case "mermaid":
                            case "flowchart":
                            case "sequence":
                            case "plantuml":
                            case "vega-lite":
                                result.Append(NormalizeContainer(block, indent));
                                break;
                        }
                        break;
                    case "li":
                        var insertNewLine1 = block.IsLooseListItem;
                        isLooseParentList = insertNewLine1;
                        if (insertNewLine1)
                            this.InsertLineBreak(result, indent);
                        result.Append(NormalizeListItem(block, indent + listIndent));
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
                            InsertLineBreak(result, indent);
                        }
                        listType.Push(new() { Type = "ul" });
                        result.Append(NormalizeList(block, indent, listIndent));
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
                            InsertLineBreak(result, indent);
                        var listCount = block.Start ?? 1;
                        listType.Push(new() { Type = "ol", ListCount = listCount });
                        result.Append(NormalizeList(block, indent, listIndent));
                        listType.Pop();
                        break;
                    case "pre":
                        InsertLineBreak(result, indent);
                        if (block.FunctionType == "frontmatter")
                            result.Append(NormalizeFrontMatter(block, indent));
                        else
                            result.Append(NormalizeCodeBlock(block, indent));
                        break;
                    case "blockquote":
                        InsertLineBreak(result, indent);
                        result.Append(NormalizeBlockquote(block, indent));
                        break;
                }
            }
            return result.ToString();
        }

        public void InsertLineBreak(StringBuilder result, string indent)
        {
            if (result.Length > 0) result.Append($"{indent}\n");
        }

        public string NormalizeParagraphText(MarkdownBlock block, string indent)
        {
            return string.Join('\n', block.Text.Split('\n').Select(line => $"{indent}{line}")) + '\n';
        }

        public string NormalizeHeaderText(MarkdownBlock block, string indent)
        {
            var headingStyle = block.HeadingStyle;
            var marker = block.Marker ?? string.Empty;
            var text = block.Children[0].Text;
            if (headingStyle == "atx")
            {
                var match = Regex.Match(text, @"(#{1,6})(.*)");
                var atxHeadingText = $"{match.Groups[1]} {match.Groups[2].ToString().Trim()}";
                return $"{indent}{atxHeadingText}\n";
            }
            else if (headingStyle == "setext")
            {
                var lines = text.Trim().Split('\n');
                return string.Join('\n', lines.Select(line => $"{indent}{line}")) + $"\n{indent}{marker.Trim()}\n";
            }
            return string.Empty;
        }

        public string NormalizeBlockquote(MarkdownBlock block, string indent)
        {
            return TranslateBlocks2Markdown(block.Children, $"{indent}> ");
        }

        public string NormalizeFrontMatter(MarkdownBlock block, string indent)
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
            var result = new StringBuilder();
            result.Append(startToken);
            foreach (var line in block.Children[0].Children)
                result.Append($"{line.Text}\n");
            result.Append(endToken);
            return result.ToString();
        }

        public string NormalizeMultipleMath(MarkdownBlock block, string indent)
        {
            string startToken = "$$";
            string endToken = "$$";
            if (isGitlabCompatibilityEnabled && block.MathStyle == "gitlab")
            {
                startToken = "```math";
                endToken = "```";
            }
            var result = new StringBuilder();
            result.Append($"{indent}{startToken}\n");
            foreach (var line in block.Children[0].Children[0].Children)
                result.Append($"{indent}{line.Text}\n");
            result.Append($"{indent}{endToken}\n");
            return result.ToString();
        }

        public string NormalizeContainer(MarkdownBlock block, string indent)
        {
            var result = new StringBuilder();
            var diagramType = block.Children[0].FunctionType;
            result.Append($"```{diagramType}\n");
            foreach (var line in block.Children[0].Children[0].Children)
                result.Append($"{line.Text}\n");
            result.Append("```\n");
            return result.ToString();
        }

        public string NormalizeCodeBlock(MarkdownBlock block, string indent)
        {
            var result = new StringBuilder();
            var codeContent = block.Children[1].Children[0];
            var textList = codeContent.Text.Split('\n');
            var functionType = block.FunctionType;
            if (functionType == "fencecode")
            {
                result.Append($"{indent}```{block.Lang ?? ""}\n");
                foreach (var text in textList)
                    result.Append($"{indent}{text}\n");
                result.Append($"{indent}```\n");
            }
            else
            {
                foreach (var text in textList)
                    result.Append($"{indent}    {text}\n");
            }
            return result.ToString();
        }

        public string NormalizeHTML(MarkdownBlock block, string indent)
        {
            var result = new StringBuilder();
            var codeContentText = block.Children[0].Children[0].Children[0].Text;
            var lines = codeContentText.Split('\n');
            foreach (var text in lines)
                result.Append($"{indent}{text}\n");
            return result.ToString();
        }

        public string NormalizeTable(MarkdownBlock table, string indent)
        {
            var result = new StringBuilder();
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
                var rs = indent + "|" + string.Join('|', r.Select((cell, j) =>
                {
                    var raw = $" {cell + string.Join("", Enumerable.Repeat(" ", columnWidth[j].width))}";
                    return raw.Substring(0, columnWidth[j].width);
                })) + "|\n";
                result.Append(rs);
                if (i == 0)
                {
                    var cutOff = indent + "|" + string.Join('|', columnWidth.Select(x =>
                    {
                        var raw = string.Join("", Enumerable.Repeat("-", x.width - 2));
                        switch (x.align)
                        {
                            case "left":
                                raw = $":{raw} ";
                                break;
                            case "center":
                                raw = $":{raw}:";
                                break;
                            case "right":
                                raw = $" {raw}:";
                                break;
                            default:
                                raw = $" {raw} ";
                                break;
                        }
                        return raw;
                    })) + "|\n";
                    result.Append(cutOff);
                }
            }
            result.Append("\n");
            return result.ToString();
        }

        public string NormalizeList(MarkdownBlock block, string indent, string listIndent)
        {
            return TranslateBlocks2Markdown(block.Children, indent, listIndent);
        }

        public string NormalizeListItem(MarkdownBlock block, string indent)
        {
            var result = new StringBuilder();
            var listInfo = listType.Peek();
            var isUnorderedList = listInfo.Type == "ul";
            var children = block.Children as IEnumerable<MarkdownBlock>;
            var bulletMarkerOrDelimiter = block.BulletMarkerOrDelimiter;
            string itemMarker;

            if (isUnorderedList)
            {
                itemMarker = !string.IsNullOrEmpty(bulletMarkerOrDelimiter) ? $"{bulletMarkerOrDelimiter} " : "- ";
            }
            else
            {
                // NOTE: GitHub and Bitbucket limit the list count to 99 but this is nowhere defined.
                //  We limit the number to 99 for Daring Fireball Markdown to prevent indentation issues.
                var n = listInfo.ListCount;
                if ((listIndentationIsDfm && n > 99) || n > 999999999)
                {
                    n = 1;
                }
                listInfo.ListCount++;
                var delimiter = string.IsNullOrEmpty(bulletMarkerOrDelimiter) ? "." : bulletMarkerOrDelimiter;
                itemMarker = $"{n}{delimiter} ";
            }

            // Subsequent paragraph indentation
            var newIndent = indent + string.Join("", Enumerable.Repeat(" ", itemMarker.Length));

            // New list indentation. We already added one space to the indentation
            var listIndent = string.Empty;
            if (listIndentationIsDfm)
            {
                listIndent = string.Join("", Enumerable.Repeat(" ", 4 - itemMarker.Length));
            }
            else
            {
                listIndent = string.Join("", Enumerable.Repeat(" ", listIndentation - 1));
            }

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

            result.Append($"{indent}{itemMarker}");
            result.Append(TranslateBlocks2Markdown(children, newIndent, listIndent).Substring(newIndent.Length));
            return result.ToString();
        }

        public string NormalizeFootnote(MarkdownBlock block, string indent)
        {
            var result = new StringBuilder();
            var identifier = block.Children[0].Text;
            result.Append($"{indent}[^{identifier}]:");
            var hasMultipleBlocks = block.Children.Count > 2 || block.Children[1].Type != "p";
            if (hasMultipleBlocks)
            {
                result.Append('\n');
                var newIndent = indent + "    ";
                result.Append(TranslateBlocks2Markdown(block.Children.Skip(1), newIndent));
            }
            else
            {
                result.Append(' ');
                var paragraphContent = block.Children[1].Children[0];
                result.Append(NormalizeParagraphText(paragraphContent, indent));
            }
            return result.ToString();
        }
    }
}
