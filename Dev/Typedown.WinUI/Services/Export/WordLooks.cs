namespace Typedown.WinUI.Services.Export
{
    // The looks a Word export can have (File > Export > Word Document, the options). A look is the fonts, sizes, colors and spacing of the
    // styles of the file: the structure is the same, a reader of the file (Word, a screen reader) sees the same headings and lists.
    // Plain .NET (no WinUI).
    public enum WordLook
    {
        // Calibri, blue headings: what Word itself gives.
        Plain,
        // A serif body, strong headings, a rule under the main title: a report or a proposal.
        Report,
        // Times, black headings, close spacing: a letter or a memo (a business document).
        Business,
        // Arial, a teal accent, light table headers.
        Modern,
    }

    internal sealed record LookSpec(
        string BodyFont, int BodySize, int Line, int After,
        string HeadingFont, int[] HeadingSizes, string[] HeadingColors, bool[] HeadingItalic, int[] HeadingBefore, int[] HeadingAfter, bool TitleRule,
        string LinkColor, string QuoteBar, string QuoteText, string CodeFill, string TableBorder, string TableHeaderFill);

    internal static class WordLooks
    {
        private static readonly LookSpec Plain = new(
            "Calibri", 22, 259, 160,
            "Calibri Light", new[] { 32, 26, 24, 22, 22, 22 }, new[] { "2F5496", "2F5496", "1F3763", "2F5496", "2F5496", "1F3763" },
            new[] { false, false, false, true, false, false }, new[] { 360, 160, 160, 80, 80, 80 }, new[] { 80, 80, 80, 40, 40, 40 }, false,
            "0563C1", "BFBFBF", "595959", "F2F2F2", "auto", "F2F2F2");

        private static readonly LookSpec Report = new(
            "Cambria", 22, 276, 160,
            "Calibri", new[] { 40, 30, 26, 24, 22, 22 }, new[] { "1F3864", "1F3864", "1F3864", "1F3864", "595959", "595959" },
            new[] { false, false, false, false, false, true }, new[] { 480, 320, 240, 200, 160, 160 }, new[] { 120, 100, 80, 60, 40, 40 }, true,
            "1F4E79", "8EAADB", "404040", "F2F2F2", "BFBFBF", "D9E2F3");

        private static readonly LookSpec Business = new(
            "Times New Roman", 24, 240, 200,
            "Times New Roman", new[] { 28, 24, 24, 24, 24, 24 }, new[] { "000000", "000000", "000000", "000000", "000000", "000000" },
            new[] { false, false, false, true, true, true }, new[] { 240, 200, 200, 160, 160, 160 }, new[] { 80, 80, 60, 60, 60, 60 }, false,
            "0563C1", "7F7F7F", "262626", "F2F2F2", "auto", "F2F2F2");

        private static readonly LookSpec Modern = new(
            "Arial", 21, 264, 140,
            "Arial", new[] { 36, 28, 24, 22, 21, 21 }, new[] { "0B5563", "0B5563", "0B5563", "3B3B3B", "3B3B3B", "3B3B3B" },
            new[] { false, false, false, false, false, true }, new[] { 360, 280, 200, 160, 120, 120 }, new[] { 100, 80, 60, 40, 40, 40 }, false,
            "0B7285", "0B7285", "3B3B3B", "EEF4F5", "A6C4C9", "DCEBEE");

        public static LookSpec Of(WordLook look) => look switch
        {
            WordLook.Report => Report,
            WordLook.Business => Business,
            WordLook.Modern => Modern,
            _ => Plain,
        };
    }
}
