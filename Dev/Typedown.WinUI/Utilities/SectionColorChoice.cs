using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Typedown.WinUI.Utilities
{
    // Settings > Appearance > Section colors: what can be chosen for each area and what a stored choice means.
    // Plain .NET (no WinUI), so the tests compile it as it is; the window only draws what this decides.
    //
    // How it works:
    //  - The colour scheme sets every colour of the window. A section colour is one area's own colour over it
    //    (the title bar and tabs, the sidebar, the editor page, the status bar); "From the color scheme" is
    //    no override, and the way back from any colour.
    //  - A choice is stored per theme (light and dark each have their own list of swatches) as the swatch's
    //    colour, not its position, and it stays when the colour scheme changes.
    //  - A swatch whose text would reach less than 4.5 : 1 (WCAG 2.2 AA) against the scheme's text isn't offered
    //    (it is listed, disabled, with the reason). A choice that has become unreadable (another scheme) is kept
    //    and shown as it is, with its contrast, but isn't applied: the area keeps the scheme's colour until a
    //    readable colour or the default is chosen.
    //  - Only a colour of the theme's own list is accepted from the setting; anything else (edited by hand, an
    //    older format, a colour of the other theme) is ignored rather than trusted.
    internal static class SectionColorChoice
    {
        public sealed record Swatch(string NameKey, string Hex);

        public static readonly Swatch[] LightSwatches =
        {
            new("SwatchWarmSand", "#EAD9C4"), new("SchemeSage", "#DCE7DA"), new("SwatchMist", "#DCE6EF"), new("SwatchStone", "#E4E2DE"),
            new("SwatchLavender", "#E6E1F0"), new("SchemeCopper", "#A5522A"), new("SwatchEspresso", "#3B2A1E"),
        };

        public static readonly Swatch[] DarkSwatches =
        {
            new("SwatchUmber", "#221A13"), new("SwatchMoss", "#15201A"), new("SwatchNight", "#111C28"), new("SwatchCharcoal", "#1E1E1E"),
            new("SwatchPlum", "#1C1626"), new("SchemeCopper", "#8F4A22"), new("SchemePaper", "#F3F3F3"),
        };

        public static readonly string[] Sections = { "band", "side", "page", "status" };

        public static Swatch[] Swatches(bool dark) => dark ? DarkSwatches : LightSwatches;

        public static bool IsHexColor(string value) => System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^#[0-9A-Fa-f]{6}$");

        // "band=#EAD9C4;page=#DCE6EF" -> section -> the swatch's colour, as the swatch spells it.
        public static Dictionary<string, string> Parse(string setting, bool dark)
        {
            var result = new Dictionary<string, string>();
            foreach (var part in (setting ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=');
                if (pair.Length != 2 || !Sections.Contains(pair[0]) || result.ContainsKey(pair[0])) continue;
                var swatch = Swatches(dark).FirstOrDefault(s => string.Equals(s.Hex, pair[1], StringComparison.OrdinalIgnoreCase));
                if (swatch != null) result[pair[0]] = swatch.Hex;
            }
            return result;
        }

        public static string Format(Dictionary<string, string> colors) =>
            string.Join(";", Sections.Where(colors.ContainsKey).Select(s => $"{s}={colors[s]}"));

        // The setting after choosing a colour for an area ("" or null: the colour scheme's own).
        public static string With(string setting, bool dark, string section, string hex)
        {
            var colors = Parse(setting, dark);
            if (string.IsNullOrEmpty(hex)) colors.Remove(section);
            else colors[section] = hex;
            return Format(colors);
        }

        public readonly record struct Verdict(bool Ok, string Text, string Text2, double Ratio);

        // The contrast guard. Text on the area is the scheme's text or its opposite, whichever reaches 4.5 : 1
        // first; the page keeps the scheme's text, which the editor draws. Secondary text is the text colour
        // softened while it stays at 4.5 or more.
        public static Verdict Check(string hex, string section, string schemeText)
        {
            var background = Rgb.Parse(hex);
            var text = Rgb.Parse(schemeText);
            var inverse = Rgb.Contrast(text, Rgb.White) > Rgb.Contrast(text, Rgb.Black) ? Rgb.White : Rgb.Parse("#141414");
            var candidates = section == "page" ? new[] { text } : new[] { text, inverse };
            foreach (var candidate in candidates)
            {
                var ratio = Rgb.Contrast(candidate, background);
                if (ratio < 4.5) continue;
                var soft = Rgb.Mix(candidate, background, 0.28);
                return new Verdict(true, candidate.ToString(), (Rgb.Contrast(soft, background) >= 4.5 ? soft : candidate).ToString(), ratio);
            }
            return new Verdict(false, text.ToString(), text.ToString(), candidates.Max(c => Rgb.Contrast(c, background)));
        }

        // The colour an area gets: the stored choice when it passes the guard, else null (the scheme's own).
        public static string Applied(string setting, bool dark, string section, string schemeText) =>
            Parse(setting, dark).TryGetValue(section, out var hex) && Check(hex, section, schemeText).Ok ? hex : null;

        public sealed record Row(string NameKey, string Hex, bool Enabled, double Ratio);

        // The list of one area: the default first, then the theme's swatches, each with whether it can be used.
        public static List<Row> Rows(string section, bool dark, string schemeText)
        {
            var rows = new List<Row> { new(null, "", true, 0) };
            foreach (var swatch in Swatches(dark))
            {
                var verdict = Check(swatch.Hex, section, schemeText);
                rows.Add(new Row(swatch.NameKey, swatch.Hex, verdict.Ok, verdict.Ratio));
            }
            return rows;
        }

        // Which row shows as chosen: the stored colour's own, even when it can't be used now (so the list never
        // says "default" while another colour is kept, and choosing the default is a real change), else the default.
        public static int SelectedRow(List<Row> rows, string setting, bool dark, string section) =>
            Parse(setting, dark).TryGetValue(section, out var hex) ? Math.Max(0, rows.FindIndex(r => r.Hex == hex)) : 0;

        private readonly record struct Rgb(byte R, byte G, byte B)
        {
            public static readonly Rgb White = new(255, 255, 255);
            public static readonly Rgb Black = new(0, 0, 0);

            public static Rgb Parse(string hex) => new(
                Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

            public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}");

            public static Rgb Mix(Rgb a, Rgb b, double t) => new(
                (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

            public static double Contrast(Rgb a, Rgb b)
            {
                static double Channel(byte v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
                static double Luminance(Rgb c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
                var (x, y) = (Luminance(a), Luminance(b));
                return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
            }
        }
    }
}