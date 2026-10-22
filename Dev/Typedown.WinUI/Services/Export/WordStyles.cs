using System.Collections.Generic;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Typedown.WinUI.Services.Export
{
    // The styles and the list definitions of an exported document. The built-in style ids and internal names of Word (heading 1,
    // Quote, List Paragraph, Hyperlink, Table Grid) are used on purpose: the navigation pane of Word, a screen reader and the
    // Word converter of Caret all know them. Plain .NET (no WinUI).
    internal static class WordStyles
    {
        public const string Normal = "Normal";
        public const string ListParagraph = "ListParagraph";
        public const string Quote = "Quote";
        public const string Code = "Code";
        public const string CodeChar = "CodeChar";
        public const string Hyperlink = "Hyperlink";
        public const string TableGrid = "TableGrid";
        public const string FootnoteText = "FootnoteText";
        public const string FootnoteReference = "FootnoteReference";
        public const string CommentReference = "CommentReference";
        public const string CommentText = "CommentText";
        public static string Toc(int level) => "TOC" + level;
        public static string Heading(int level) => "Heading" + level;

        // Numbering definitions: the bullet list, the numbered list and the task list (a check box character is the marker, so none here).
        public const int BulletAbstract = 0, DecimalAbstract = 1, TaskAbstract = 2;
        public const int BulletNum = 1, TaskNum = 2, FirstNumberedNum = 3;

        public const int ListIndent = 720, ListHanging = 360;

        private static W.RunFonts Font(string name) => new W.RunFonts { Ascii = name, HighAnsi = name, EastAsia = name, ComplexScript = name };

        public static W.Styles Create(string language, int textWidth, WordLook look = WordLook.Plain)
        {
            var spec = WordLooks.Of(look);
            var styles = new W.Styles();
            styles.Append(new W.DocDefaults(
                new W.RunPropertiesDefault(new W.RunPropertiesBaseStyle(
                    Font(spec.BodyFont),
                    new W.FontSize { Val = spec.BodySize.ToString() },
                    new W.FontSizeComplexScript { Val = spec.BodySize.ToString() },
                    new W.Languages { Val = language, EastAsia = language, Bidi = "ar-SA" })),
                new W.ParagraphPropertiesDefault(new W.ParagraphPropertiesBaseStyle(
                    new W.SpacingBetweenLines { After = spec.After.ToString(), Line = spec.Line.ToString(), LineRule = W.LineSpacingRuleValues.Auto }))));

            styles.Append(new W.Style(new W.StyleName { Val = "Normal" }, new W.PrimaryStyle()) { Type = W.StyleValues.Paragraph, StyleId = Normal, Default = true });
            styles.Append(new W.Style(new W.StyleName { Val = "Default Paragraph Font" }, new W.UIPriority { Val = 1 }, new W.SemiHidden(), new W.UnhideWhenUsed())
                { Type = W.StyleValues.Character, StyleId = "DefaultParagraphFont", Default = true });
            styles.Append(new W.Style(
                new W.StyleName { Val = "Normal Table" }, new W.UIPriority { Val = 99 }, new W.SemiHidden(), new W.UnhideWhenUsed(),
                new W.StyleTableProperties(new W.TableIndentation { Width = 0, Type = W.TableWidthUnitValues.Dxa },
                    new W.TableCellMarginDefault(
                        new W.TopMargin { Width = "0", Type = W.TableWidthUnitValues.Dxa }, new W.TableCellLeftMargin { Width = 108, Type = W.TableWidthValues.Dxa },
                        new W.BottomMargin { Width = "0", Type = W.TableWidthUnitValues.Dxa }, new W.TableCellRightMargin { Width = 108, Type = W.TableWidthValues.Dxa })))
                { Type = W.StyleValues.Table, StyleId = "TableNormal", Default = true });
            AddHeadings(styles, spec);
            AddBlocks(styles, spec);
            AddNotes(styles, textWidth);
            return styles;
        }

        // heading 1 to 6. outlineLvl is what the navigation pane and a table of contents read.
        private static void AddHeadings(W.Styles styles, LookSpec spec)
        {
            for (var level = 1; level <= 6; level++)
            {
                var size = spec.HeadingSizes[level - 1].ToString();
                var rPr = new W.StyleRunProperties();
                rPr.Append(Font(spec.HeadingFont));
                // bold, except in a light heading font (Calibri Light is the weight Word itself gives its headings)
                if (spec.HeadingFont != "Calibri Light") rPr.Append(new W.Bold());
                if (spec.HeadingItalic[level - 1]) rPr.Append(new W.Italic());
                rPr.Append(new W.Color { Val = spec.HeadingColors[level - 1] });
                rPr.Append(new W.FontSize { Val = size });
                rPr.Append(new W.FontSizeComplexScript { Val = size });
                var pPr = new W.StyleParagraphProperties(new W.KeepNext(), new W.KeepLines());
                if (level == 1 && spec.TitleRule)
                    pPr.Append(new W.ParagraphBorders(new W.BottomBorder { Val = W.BorderValues.Single, Size = 8, Space = 4, Color = spec.HeadingColors[0] }));
                pPr.Append(new W.SpacingBetweenLines { Before = spec.HeadingBefore[level - 1].ToString(), After = spec.HeadingAfter[level - 1].ToString() });
                pPr.Append(new W.OutlineLevel { Val = level - 1 });
                styles.Append(new W.Style(
                    new W.StyleName { Val = "heading " + level }, new W.BasedOn { Val = Normal }, new W.NextParagraphStyle { Val = Normal },
                    new W.UIPriority { Val = 9 }, new W.PrimaryStyle(), pPr, rPr)
                    { Type = W.StyleValues.Paragraph, StyleId = Heading(level) });
            }
        }

        private static void AddBlocks(W.Styles styles, LookSpec spec)
        {
            styles.Append(new W.Style(
                new W.StyleName { Val = "List Paragraph" }, new W.BasedOn { Val = Normal }, new W.UIPriority { Val = 34 }, new W.PrimaryStyle(),
                new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "80" }, new W.Indentation { Left = ListIndent.ToString() }))
                { Type = W.StyleValues.Paragraph, StyleId = ListParagraph });

            // A block quote: a bar in the margin and gray text, not the centered italic Quote of Word.
            styles.Append(new W.Style(
                new W.StyleName { Val = "Quote" }, new W.BasedOn { Val = Normal }, new W.NextParagraphStyle { Val = Normal }, new W.UIPriority { Val = 29 }, new W.PrimaryStyle(),
                new W.StyleParagraphProperties(
                    new W.ParagraphBorders(new W.LeftBorder { Val = W.BorderValues.Single, Size = 18, Space = 8, Color = spec.QuoteBar }),
                    new W.SpacingBetweenLines { After = "120" }, new W.Indentation { Left = "720" }),
                new W.StyleRunProperties(new W.Color { Val = spec.QuoteText }))
                { Type = W.StyleValues.Paragraph, StyleId = Quote });

            // A line of code: one paragraph per line, so the lines stay lines in Word.
            styles.Append(new W.Style(
                new W.StyleName { Val = "Code" }, new W.BasedOn { Val = Normal }, new W.UIPriority { Val = 30 }, new W.PrimaryStyle(),
                new W.StyleParagraphProperties(
                    new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = spec.CodeFill },
                    new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }),
                new W.StyleRunProperties(Font("Consolas"), new W.FontSize { Val = "19" }, new W.FontSizeComplexScript { Val = "19" }))
                { Type = W.StyleValues.Paragraph, StyleId = Code });

            styles.Append(new W.Style(
                new W.StyleName { Val = "Code Char" }, new W.BasedOn { Val = "DefaultParagraphFont" }, new W.UIPriority { Val = 30 }, new W.PrimaryStyle(),
                new W.StyleRunProperties(Font("Consolas"), new W.FontSize { Val = "20" }, new W.FontSizeComplexScript { Val = "20" },
                    new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = spec.CodeFill }))
                { Type = W.StyleValues.Character, StyleId = CodeChar });

            styles.Append(new W.Style(
                new W.StyleName { Val = "Hyperlink" }, new W.BasedOn { Val = "DefaultParagraphFont" }, new W.UIPriority { Val = 99 }, new W.UnhideWhenUsed(),
                new W.StyleRunProperties(new W.Color { Val = spec.LinkColor }, new W.Underline { Val = W.UnderlineValues.Single }))
                { Type = W.StyleValues.Character, StyleId = Hyperlink });

            styles.Append(new W.Style(
                new W.StyleName { Val = "Table Grid" }, new W.BasedOn { Val = "TableNormal" }, new W.UIPriority { Val = 39 },
                new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }),
                new W.StyleTableProperties(new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder },
                    new W.RightBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = spec.TableBorder })))
                { Type = W.StyleValues.Table, StyleId = TableGrid });
        }

        // The footnotes and the lines of a table of contents.
        private static void AddNotes(W.Styles styles, int textWidth)
        {
            styles.Append(new W.Style(
                new W.StyleName { Val = "footnote text" }, new W.BasedOn { Val = Normal }, new W.UIPriority { Val = 99 }, new W.UnhideWhenUsed(),
                new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }),
                new W.StyleRunProperties(new W.FontSize { Val = "20" }, new W.FontSizeComplexScript { Val = "20" }))
                { Type = W.StyleValues.Paragraph, StyleId = FootnoteText });
            styles.Append(new W.Style(
                new W.StyleName { Val = "footnote reference" }, new W.BasedOn { Val = "DefaultParagraphFont" }, new W.UIPriority { Val = 99 }, new W.UnhideWhenUsed(),
                new W.StyleRunProperties(new W.VerticalTextAlignment { Val = W.VerticalPositionValues.Superscript }))
                { Type = W.StyleValues.Character, StyleId = FootnoteReference });
            styles.Append(new W.Style(
                new W.StyleName { Val = "annotation reference" }, new W.BasedOn { Val = "DefaultParagraphFont" }, new W.UIPriority { Val = 99 }, new W.SemiHidden(), new W.UnhideWhenUsed(),
                new W.StyleRunProperties(new W.FontSize { Val = "16" }, new W.FontSizeComplexScript { Val = "16" }))
                { Type = W.StyleValues.Character, StyleId = CommentReference });
            styles.Append(new W.Style(
                new W.StyleName { Val = "annotation text" }, new W.BasedOn { Val = Normal }, new W.UIPriority { Val = 99 }, new W.UnhideWhenUsed(),
                new W.StyleParagraphProperties(new W.SpacingBetweenLines { After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto }),
                new W.StyleRunProperties(new W.FontSize { Val = "20" }, new W.FontSizeComplexScript { Val = "20" }))
                { Type = W.StyleValues.Paragraph, StyleId = CommentText });
            for (var level = 1; level <= 3; level++)
            {
                styles.Append(new W.Style(
                    new W.StyleName { Val = "toc " + level }, new W.BasedOn { Val = Normal }, new W.NextParagraphStyle { Val = Normal }, new W.UIPriority { Val = 39 }, new W.UnhideWhenUsed(),
                    new W.StyleParagraphProperties(
                        new W.Tabs(new W.TabStop { Val = W.TabStopValues.Right, Leader = W.TabStopLeaderCharValues.Dot, Position = textWidth }),
                        new W.SpacingBetweenLines { After = "100" }, new W.Indentation { Left = (220 * (level - 1)).ToString() }))
                    { Type = W.StyleValues.Paragraph, StyleId = Toc(level) });
            }
        }

        // Three abstract definitions (bullets, numbers, task list) and the two instances that are shared; every numbered list gets
        // an instance of its own (NumberedInstance) so that it starts again at its own number.
        public static W.Numbering CreateNumbering()
        {
            var numbering = new W.Numbering();
            numbering.Append(Abstract(BulletAbstract, level => ("bullet", level % 3 == 0 ? "•" : level % 3 == 1 ? "◦" : "▪")));
            numbering.Append(Abstract(DecimalAbstract, level => (level % 3 == 0 ? "decimal" : level % 3 == 1 ? "lowerLetter" : "lowerRoman", "%" + (level + 1) + ".")));
            numbering.Append(Abstract(TaskAbstract, level => ("none", "")));
            numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = BulletAbstract }) { NumberID = BulletNum });
            numbering.Append(new W.NumberingInstance(new W.AbstractNumId { Val = TaskAbstract }) { NumberID = TaskNum });
            return numbering;
        }

        private static W.AbstractNum Abstract(int id, System.Func<int, (string Format, string Text)> level)
        {
            var abstractNum = new W.AbstractNum(new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
            for (var i = 0; i < 9; i++)
            {
                var (format, text) = level(i);
                var left = ListIndent * (i + 1);
                abstractNum.Append(new W.Level(
                    new W.StartNumberingValue { Val = 1 },
                    new W.NumberingFormat { Val = format switch
                    {
                        "bullet" => W.NumberFormatValues.Bullet, "decimal" => W.NumberFormatValues.Decimal, "lowerLetter" => W.NumberFormatValues.LowerLetter,
                        "lowerRoman" => W.NumberFormatValues.LowerRoman, _ => W.NumberFormatValues.None,
                    } },
                    new W.LevelText { Val = text },
                    new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                    new W.PreviousParagraphProperties(new W.Indentation { Left = left.ToString(), Hanging = ListHanging.ToString() }))
                    { LevelIndex = i });
            }
            return abstractNum;
        }

        // A numbered list that starts at the given number, independent of every other one.
        public static W.NumberingInstance NumberedInstance(int numId, int level, int start)
        {
            var instance = new W.NumberingInstance(new W.AbstractNumId { Val = DecimalAbstract }) { NumberID = numId };
            for (var i = 0; i < 9; i++)
                instance.Append(new W.LevelOverride(new W.StartOverrideNumberingValue { Val = i == level ? start : 1 }) { LevelIndex = i });
            return instance;
        }
    }
}
