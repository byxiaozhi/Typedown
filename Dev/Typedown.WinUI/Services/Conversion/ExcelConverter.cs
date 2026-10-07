using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace Typedown.WinUI.Services.Conversion
{
    // .xlsx → one "## Sheet" section with a pipe table per visible sheet. Values are what Excel shows
    // for the common cases (dates as dates, percentages as percentages, formulas as their last computed
    // result), and empty rows and columns are dropped — they carry no meaning and only cost tokens.
    internal static class ExcelConverter
    {
        public static string Convert(Stream stream, ConversionContext context)
        {
            using var doc = SpreadsheetDocument.Open(stream, false);
            var workbook = doc.WorkbookPart;
            if (workbook?.Workbook?.Sheets == null) return "";
            var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<S.SharedStringItem>()
                .Select(i => i.InnerText).ToArray() ?? Array.Empty<string>();
            var formats = new NumberFormats(workbook.WorkbookStylesPart?.Stylesheet, workbook.Workbook.WorkbookProperties?.Date1904?.Value == true);

            var sb = new StringBuilder();
            var visibleSheets = workbook.Workbook.Sheets.Elements<S.Sheet>()
                .Where(s => s.State == null || s.State.Value == S.SheetStateValues.Visible).ToList();
            foreach (var sheet in visibleSheets)
            {
                if (sheet.Id?.Value == null || workbook.GetPartById(sheet.Id.Value) is not WorksheetPart part) continue;
                var parts = ReadParts(part, sharedStrings, formats);
                if (parts.Count == 0) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("## ").Append(MarkdownText.EscapeInline(sheet.Name?.Value ?? ""));
                foreach (var block in parts) sb.Append("\n\n").Append(block);
            }
            if (sb.Length == 0) context.Warnings.Add(ConversionWarning.NoData);
            return sb.ToString();
        }

        // The sheet as blocks of Markdown: tables, and the lone lines that stand by themselves between blank rows (a title above
        // a table, a note under it) as paragraphs, so a title is not taken for the heading of the table.
        private static List<string> ReadParts(WorksheetPart part, string[] sharedStrings, NumberFormats formats)
        {
            var parts = new List<string>();
            var sheetData = part.Worksheet?.GetFirstChild<S.SheetData>();
            if (sheetData == null) return parts;
            var grid = new List<(uint Index, Dictionary<int, string> Cells)>();
            uint rowNumber = 0;
            // Hidden rows and columns (a filter, a helper column) are not what Excel shows, so they are not in the text, like hidden sheets.
            var hiddenColumns = new HashSet<int>();
            foreach (var column in part.Worksheet.Elements<S.Columns>().SelectMany(c => c.Elements<S.Column>()).Where(c => c.Hidden?.Value == true))
                for (var c = (int)(column.Min?.Value ?? 1); c <= (int)Math.Min(column.Max?.Value ?? 1, 16384); c++) hiddenColumns.Add(c - 1);
            foreach (var row in sheetData.Elements<S.Row>())
            {
                rowNumber = row.RowIndex?.Value ?? rowNumber + 1;
                if (row.Hidden?.Value == true) continue;
                var cells = new Dictionary<int, string>();
                var nextColumn = 0;
                foreach (var cell in row.Elements<S.Cell>())
                {
                    var column = cell.CellReference?.Value is string reference ? ColumnIndex(reference) : nextColumn;
                    nextColumn = column + 1;
                    if (hiddenColumns.Contains(column)) continue;
                    var value = CellText(cell, sharedStrings, formats);
                    if (!string.IsNullOrWhiteSpace(value)) cells[column] = value.Trim();
                }
                if (cells.Count > 0) grid.Add((rowNumber, cells));
            }
            var table = new List<Dictionary<int, string>>();
            void FlushTable()
            {
                if (table.Count == 0) return;
                var usedColumns = table.SelectMany(r => r.Keys).Distinct().OrderBy(c => c).ToList();
                parts.Add(MarkdownText.Table(table.Select(r => (IReadOnlyList<string>)usedColumns.Select(c => r.TryGetValue(c, out var v) ? v : "").ToList()).ToList()));
                table.Clear();
            }
            for (var i = 0; i < grid.Count; i++)
            {
                var (index, cells) = grid[i];
                var aloneAbove = i == 0 || grid[i - 1].Index + 1 < index;
                var aloneBelow = i == grid.Count - 1 || index + 1 < grid[i + 1].Index;
                if (cells.Count == 1 && aloneAbove && aloneBelow)
                {
                    FlushTable();
                    parts.Add(MarkdownText.EscapeBlockStart(MarkdownText.EscapeInline(cells.Values.First()).Replace("\n", "  \n")));
                }
                else table.Add(cells);
            }
            FlushTable();
            return parts;
        }

        private static int ColumnIndex(string reference)
        {
            var index = 0;
            foreach (var c in reference)
            {
                if (!char.IsLetter(c)) break;
                index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            return index - 1;
        }

        private static string CellText(S.Cell cell, string[] sharedStrings, NumberFormats formats)
        {
            var raw = cell.CellValue?.Text;
            var type = cell.DataType?.Value;
            if (type == S.CellValues.SharedString)
                return int.TryParse(raw, out var i) && i >= 0 && i < sharedStrings.Length ? sharedStrings[i] : "";
            if (type == S.CellValues.InlineString) return cell.InlineString?.InnerText ?? "";
            if (type == S.CellValues.Boolean) return raw == "1" ? "TRUE" : "FALSE";
            if (type == S.CellValues.String || type == S.CellValues.Error) return raw ?? "";
            // A formula saved without its result (by a script rather than by Excel) is shown as the formula.
            if (string.IsNullOrEmpty(raw)) return cell.CellFormula?.Text is { Length: > 0 } formula ? "=" + formula : "";
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return raw;
            return formats.Format(number, cell.StyleIndex?.Value ?? 0);
        }

        private sealed class NumberFormats
        {
            private readonly List<uint> cellFormatIds = new();
            private readonly Dictionary<uint, string> customCodes = new();

            private readonly bool date1904;

            public NumberFormats(S.Stylesheet stylesheet, bool date1904 = false)
            {
                this.date1904 = date1904;
                if (stylesheet?.CellFormats != null)
                    cellFormatIds.AddRange(stylesheet.CellFormats.Elements<S.CellFormat>().Select(f => f.NumberFormatId?.Value ?? 0));
                if (stylesheet?.NumberingFormats != null)
                    foreach (var f in stylesheet.NumberingFormats.Elements<S.NumberingFormat>())
                        if (f.NumberFormatId?.Value is uint id) customCodes[id] = f.FormatCode?.Value ?? "";
            }

            public string Format(double value, uint styleIndex)
            {
                var formatId = styleIndex < cellFormatIds.Count ? cellFormatIds[(int)styleIndex] : 0u;
                customCodes.TryGetValue(formatId, out var code);
                if (IsDate(formatId, code) && value > -657435 && value < 2958466)
                {
                    var date = DateTime.FromOADate(date1904 && value >= 0 ? value + 1462 : value); // a workbook from the Mac starts counting in 1904
                    var hasTime = Math.Abs(value % 1) > 1e-9 || (code != null && Regex.IsMatch(StripLiterals(code), "[hs]", RegexOptions.IgnoreCase));
                    var hasDate = value >= 1 || (code != null && Regex.IsMatch(StripLiterals(code), "[dy]", RegexOptions.IgnoreCase)) || formatId is >= 14 and <= 17 or 22;
                    if (!hasDate) return date.ToString("HH:mm", CultureInfo.InvariantCulture);
                    return date.ToString(hasTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                return Numeric(value, formatId, code);
            }

            private static readonly Dictionary<uint, string> BuiltIn = new()
            {
                [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00", [9] = "0%", [10] = "0.00%",
                [37] = "#,##0_);(#,##0)", [38] = "#,##0_);[Red](#,##0)", [39] = "#,##0.00_);(#,##0.00)", [40] = "#,##0.00_);[Red](#,##0.00)",
            };

            private static string General(double value) => value.ToString("G15", CultureInfo.InvariantCulture);

            // What the cell's number format shows: decimals, thousands separators, a currency sign or unit, brackets for negatives,
            // percentages. Formats this does not read (fractions, scientific, text) fall back to the plain number.
            private static string Numeric(double value, uint formatId, string code)
            {
                code ??= BuiltIn.GetValueOrDefault(formatId);
                if (string.IsNullOrEmpty(code) || code.Equals("General", StringComparison.OrdinalIgnoreCase)) return General(value);
                var sections = SplitSections(code);
                var section = sections[0];
                var ownSign = false;
                if (value < 0 && sections.Count > 1) { section = sections[1]; ownSign = true; }
                else if (value == 0 && sections.Count > 2) section = sections[2];
                var text = RenderSection(Math.Abs(value), section);
                if (text == null) return General(value);
                return value < 0 && !ownSign ? "-" + text : text;
            }

            private static List<string> SplitSections(string code)
            {
                var sections = new List<string>();
                var current = new StringBuilder();
                var quoted = false;
                var bracket = false;
                for (var i = 0; i < code.Length; i++)
                {
                    var c = code[i];
                    if (c == '\\' && i + 1 < code.Length) { current.Append(c).Append(code[++i]); continue; }
                    if (c == '"' && !bracket) quoted = !quoted;
                    else if (c == '[' && !quoted) bracket = true;
                    else if (c == ']' && !quoted) bracket = false;
                    if (c == ';' && !quoted && !bracket) { sections.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
                sections.Add(current.ToString());
                return sections;
            }

            private static string RenderSection(double value, string section)
            {
                if (Regex.IsMatch(StripLiterals(section), "[0#?]/[0#?]|[0#?]E[+-]|@") || section.Contains("General", StringComparison.OrdinalIgnoreCase)) return null;
                var prefix = new StringBuilder();
                var suffix = new StringBuilder();
                var core = new StringBuilder();
                var percent = false;
                var phase = 0; // 0 before the digits, 1 in them, 2 after
                for (var i = 0; i < section.Length; i++)
                {
                    var c = section[i];
                    string literal = null;
                    if (c == '"')
                    {
                        var end = section.IndexOf('"', i + 1);
                        if (end < 0) end = section.Length;
                        literal = section.Substring(i + 1, end - i - 1);
                        i = end;
                    }
                    else if (c == '\\' && i + 1 < section.Length) literal = section[++i].ToString();
                    else if (c == '[')
                    {
                        var end = section.IndexOf(']', i + 1);
                        if (end < 0) end = section.Length - 1;
                        var inside = section.Substring(i + 1, end - i - 1);
                        i = end;
                        if (inside.StartsWith('$')) literal = inside.Substring(1).Split('-')[0]; // [$€-407]: the sign, then the locale
                        else continue; // [Red], [>100]
                    }
                    else if (c == '_' || c == '*') { i++; continue; } // padding and fill, not text
                    else if (c is '0' or '#' or '?' || (c is '.' or ',' && phase == 1) || (c == '.' && phase == 0 && i + 1 < section.Length && section[i + 1] is '0' or '#' or '?'))
                    {
                        if (phase == 2) return null; // digits after text ("00-00")
                        phase = 1;
                        core.Append(c);
                        continue;
                    }
                    else
                    {
                        if (c == '%') percent = true;
                        literal = c.ToString();
                    }
                    if (phase == 1) phase = 2;
                    (phase == 0 ? prefix : suffix).Append(literal);
                }
                var pattern = core.ToString();
                if (pattern.Length == 0 || pattern.EndsWith(',') || pattern.Contains(",.") || pattern.EndsWith('.')) return null;
                var decimals = pattern.Contains('.') ? pattern.Substring(pattern.IndexOf('.') + 1).Count(ch => ch is '0' or '#' or '?') : 0;
                pattern = pattern.Replace('?', '#');
                if (percent) value *= 100;
                if (value > 1e27) return null;
                var number = Math.Round((decimal)value, Math.Min(decimals, 28), MidpointRounding.AwayFromZero);
                return prefix + number.ToString(pattern, CultureInfo.InvariantCulture) + suffix;
            }

            private static bool IsDate(uint id, string code)
            {
                if (id is >= 14 and <= 22 or >= 45 and <= 47) return true;
                if (string.IsNullOrEmpty(code)) return false;
                var bare = StripLiterals(code);
                return Regex.IsMatch(bare, "[dmyhs]", RegexOptions.IgnoreCase) && !Regex.IsMatch(bare, "[0#?]");
            }

            // Removes quoted text, escaped characters and [colour]/[locale] sections from a format code.
            private static string StripLiterals(string code) => Regex.Replace(code, "\"[^\"]*\"|\\\\.|\\[[^\\]]*\\]|_.|\\*.", "");
        }
    }

    // .csv → a pipe table. The delimiter is detected (French and Spanish Excel save with ";").
    internal static class CsvConverter
    {
        public static string Convert(Stream stream, ConversionContext context)
        {
            // Excel's "CSV (delimited)" is saved in the system's ANSI code page, not UTF-8 — Windows-1250
            // on a Polish or Hungarian PC, Windows-1252 on a French or Spanish one. TextFileEncoding
            // tries UTF-8 (and byte-order marks) first and falls back to that code page.
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var text = Utilities.TextFileEncoding.Decode(buffer.ToArray()).Text;
            var delimiter = DetectDelimiter(text);
            var rows = Parse(text, delimiter).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
            if (rows.Count == 0)
            {
                context.Warnings.Add(ConversionWarning.NoData);
                return "";
            }
            return MarkdownText.Table(rows.Select(r => (IReadOnlyList<string>)r.Select(c => c.Trim()).ToList()).ToList());
        }

        private static char DetectDelimiter(string text)
        {
            var firstLine = text.Split('\n').FirstOrDefault() ?? "";
            var candidates = new[] { ',', ';', '\t', '|' };
            return candidates.OrderByDescending(c => CountOutsideQuotes(firstLine, c)).First();
        }

        private static int CountOutsideQuotes(string line, char c)
        {
            var count = 0;
            var quoted = false;
            foreach (var ch in line)
            {
                if (ch == '"') quoted = !quoted;
                else if (ch == c && !quoted) count++;
            }
            return count;
        }

        private static IEnumerable<List<string>> Parse(string text, char delimiter)
        {
            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Clear();
                    yield return row;
                    row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); yield return row; }
        }
    }
}
