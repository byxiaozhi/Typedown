using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Typedown.WinUI.Utilities
{
    // Colour schemes (interface review, phase 2). Themes/Caret.xaml is the Copper scheme; another scheme
    // is applied by replacing, in its Light and Dark dictionaries, every colour that plays a role in
    // Copper with the same role's colour in the chosen scheme — by value, so every brush built from a
    // role (including the WinUI keys Caret.xaml overrides) follows without listing brush keys here. The
    // windows then re-read their theme resources (Refresh).
    // Every scheme below was checked against WCAG 2.2 AA: text and tab text 4.5 : 1 or more on their
    // backgrounds, icons, the accent fill and the focus colour 3 : 1 or more, white on the accent 4.5.
    // A Windows contrast theme bypasses all of this: Caret.xaml has no HighContrast dictionary, so
    // WinUI uses the system colours.
    public static class ColorSchemes
    {
        public sealed record Palette(
            string Primary, string Secondary, string Background, string Surface, string SurfaceAlt,
            string Text, string Text2, string Border, string Focus, string TabStrip, string Command,
            string TabHover, string TabPressed, string TabText, string TabIcon, string TabSeparator);

        public sealed record Scheme(string Id, string NameKey, Palette Light, Palette Dark);

        public static IReadOnlyList<Scheme> All { get; } = new[]
        {
            new Scheme("copper", "SchemeCopper",
                new("#A5522A", "#D67A3C", "#F8EBDD", "#FFFFFF", "#F2DCC2", "#382A1B", "#6B584B", "#E3D5C6", "#A5522A", "#EDD5BA", "#F8EBDD", "#F4E2CF", "#F6E7D7", "#5E4A3C", "#7A6454", "#B08C6A"),
                new("#B55E2A", "#F59E0B", "#0E1220", "#161B2A", "#20283A", "#EDEEF2", "#A6B0C3", "#2B3550", "#F59E0B", "#090C15", "#151A28", "#121726", "#0F1320", "#8D97AB", "#7C879C", "#4A5878")),
            new Scheme("paper", "SchemePaper",
                new("#005FB8", "#005FB8", "#F9F9F9", "#FFFFFF", "#EBEBEB", "#1A1A1A", "#5D5D5D", "#E0E0E0", "#005FB8", "#E6E6E6", "#F9F9F9", "#F0F0F0", "#F4F4F4", "#4A4A4A", "#5D5D5D", "#ADADAD"),
                new("#2A74BD", "#60CDFF", "#1C1C1C", "#2B2B2B", "#333333", "#FFFFFF", "#C8C8C8", "#3A3A3A", "#60CDFF", "#121212", "#262626", "#1A1A1A", "#171717", "#B8B8B8", "#A0A0A0", "#555555")),
            new Scheme("sage", "SchemeSage",
                new("#3F6B4E", "#4F7F5E", "#EEF3EC", "#FFFFFF", "#DCE6D9", "#1F2A22", "#4D5E51", "#CAD6C6", "#3F6B4E", "#D5E0D3", "#EEF3EC", "#E2EAE0", "#E8EFE6", "#3E5143", "#55685A", "#93A896"),
                new("#4B7F5E", "#8FC3A2", "#101612", "#172019", "#1F2A22", "#E6EEE7", "#A9B8AC", "#26352A", "#8FC3A2", "#0A0F0C", "#172019", "#111914", "#0E1510", "#9DB0A1", "#8A9C8E", "#3E5446")),
            new Scheme("harbour", "SchemeHarbour",
                new("#1F5E8C", "#2F74A8", "#EEF3F8", "#FFFFFF", "#DAE5EF", "#16232F", "#4A5B6B", "#C8D6E3", "#1F5E8C", "#D3E0EC", "#EEF3F8", "#E0E9F2", "#E6EEF5", "#3A4C5E", "#50637A", "#8FA6BC"),
                new("#2F71A6", "#7DB8E3", "#0C151F", "#13202E", "#1B2A3B", "#E4ECF4", "#9FB2C4", "#22344A", "#7DB8E3", "#07101A", "#13202E", "#0E1824", "#0B141E", "#9CB0C3", "#8499AE", "#3A5270")),
            new Scheme("graphite", "SchemeGraphite",
                new("#A5522A", "#B8622E", "#F2F2F2", "#FFFFFF", "#E3E3E3", "#1C1C1C", "#525252", "#D0D0D0", "#A5522A", "#D9D9D9", "#F2F2F2", "#E6E6E6", "#ECECEC", "#474747", "#5C5C5C", "#9A9A9A"),
                new("#B55E2A", "#E8A365", "#1B1B1B", "#232323", "#2C2C2C", "#EDEDED", "#B3B3B3", "#2E2E2E", "#E8A365", "#111111", "#242424", "#181818", "#151515", "#A8A8A8", "#959595", "#4A4A4A")),
        };

        public static Scheme Find(string id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];

        // The palette in use for a theme: the scheme's, with the accent roles taken from Windows when
        // the accent source says so (see WithWindowsAccent).
        public static Palette Current(bool dark)
        {
            var scheme = Find(currentScheme);
            var palette = dark ? scheme.Dark : scheme.Light;
            return currentAccent == "windows" ? WithWindowsAccent(palette, dark) : palette;
        }

        private static string currentScheme = "copper";
        private static string currentAccent = "scheme";
        // How a scheme is applied, found the hard way:
        //  - Caret.xaml's dictionaries are never written to. Replacing an entry in an application theme
        //    dictionary made controls from other libraries (NavigationView's pane, the Settings cards)
        //    resolve against the Windows theme instead of the window's.
        //  - Its brushes are recoloured in place: those libraries hold on to the brush objects they
        //    found when they loaded, so a new brush under the same key would never reach them.
        //  - Plain colour values (the accent and its shades, which WinUI builds its own accent brushes
        //    from) can't be recoloured, so they go in a small dictionary merged on top, and the windows
        //    re-read their theme resources (Refresh).
        //  - Nothing is read before the first window has loaded: reading the dictionaries earlier fixed
        //    some resources to the Windows theme too. The default (Copper, its own accent, solid) touches
        //    nothing at all.
        private static ResourceDictionary overlay;
        private static Dictionary<SolidColorBrush, (Color Color, double Opacity)> originalBrushes;
        private static Dictionary<string, Dictionary<object, Color>> originalColors;

        // material: "solid", or "mica"/"micaalt", where the window's own surfaces (tab band, command row,
        // sidebar, status bar) turn translucent so the backdrop shows through; the page stays solid.
        public static void Apply(string schemeId, string accentSource, string material = "solid")
        {
            currentScheme = Find(schemeId).Id;
            currentAccent = accentSource == "windows" ? "windows" : "scheme";
            var isDefault = currentScheme == All[0].Id && currentAccent == "scheme" && material == "solid";
            if (isDefault && originalBrushes == null) return;
            var caret = CaretDictionary();
            if (caret == null) return;
            if (originalBrushes == null)
            {
                originalBrushes = new();
                originalColors = new();
                foreach (var (theme, value) in caret.ThemeDictionaries)
                {
                    var dict = (ResourceDictionary)value;
                    originalColors[(string)theme] = dict.Where(e => e.Value is Color).ToDictionary(e => e.Key, e => (Color)e.Value);
                    foreach (var brush in dict.Values.OfType<SolidColorBrush>())
                        originalBrushes.TryAdd(brush, (brush.Color, brush.Opacity));
                }
            }
            var merged = Application.Current.Resources.MergedDictionaries;
            if (overlay != null) merged.Remove(overlay);
            overlay = null;
            var next = new ResourceDictionary();
            var colorsChanged = false;
            foreach (var theme in new[] { "Light", "Dark" })
            {
                var copper = theme == "Dark" ? All[0].Dark : All[0].Light;
                var target = Current(theme == "Dark");
                var map = RoleMap(copper, target);
                var source = (ResourceDictionary)caret.ThemeDictionaries[theme];
                foreach (var (key, value) in source)
                {
                    if (value is not SolidColorBrush brush || !originalBrushes.TryGetValue(brush, out var original)) continue;
                    var skip = key is string k && k.StartsWith("SystemFillColor"); // status colours stay
                    brush.Color = !skip && map.TryGetValue(original.Color, out var mapped) ? mapped : original.Color;
                    brush.Opacity = original.Opacity;
                }
                if (material is "mica" or "micaalt")
                {
                    Translucent(source, "CaretWindowBackgroundBrush", 0.78);
                    Translucent(source, "CaretTabStripBrush", material == "micaalt" ? 0.6 : 0.72);
                    Translucent(source, "CaretCommandBarBrush", 0.86);
                }
                var colors = new ResourceDictionary();
                foreach (var (key, color) in originalColors[theme])
                    if (map.TryGetValue(color, out var mapped) && mapped != color) colors[key] = mapped;
                var accent = Parse(target.Primary);
                if (accent != Parse(copper.Primary))
                {
                    colors["SystemAccentColor"] = accent;
                    colors["SystemAccentColorLight1"] = Mix(accent, Microsoft.UI.Colors.White, 0.2);
                    colors["SystemAccentColorLight2"] = Mix(accent, Microsoft.UI.Colors.White, 0.4);
                    colors["SystemAccentColorLight3"] = Mix(accent, Microsoft.UI.Colors.White, 0.6);
                    colors["SystemAccentColorDark1"] = Mix(accent, Microsoft.UI.Colors.Black, 0.25);
                    colors["SystemAccentColorDark2"] = Mix(accent, Microsoft.UI.Colors.Black, 0.5);
                    colors["SystemAccentColorDark3"] = Mix(accent, Microsoft.UI.Colors.Black, 0.75);
                }
                colorsChanged |= colors.Count > 0;
                next.ThemeDictionaries[theme] = colors;
            }
            if (!colorsChanged) return;
            merged.Add(next);
            overlay = next;
        }

        private static void Translucent(ResourceDictionary source, string key, double opacity)
        {
            if (source.TryGetValue(key, out var value) && value is SolidColorBrush brush) brush.Opacity = opacity;
        }

        // Theme resources are looked up again when an element's theme changes, so each window flips
        // its root to the other theme and back.
        public static void Refresh(FrameworkElement root)
        {
            var requested = root.RequestedTheme;
            root.RequestedTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
            root.RequestedTheme = requested;
        }

        private static ResourceDictionary CaretDictionary() =>
            Application.Current.Resources.MergedDictionaries.FirstOrDefault(d =>
                d.ThemeDictionaries.TryGetValue("Light", out var light) && ((ResourceDictionary)light).ContainsKey("CaretPrimaryColor"));

        private static Dictionary<Color, Color> RoleMap(Palette from, Palette to)
        {
            var map = new Dictionary<Color, Color>();
            var fromValues = Roles(from);
            var toValues = Roles(to);
            for (var i = 0; i < fromValues.Length; i++)
                map.TryAdd(Parse(fromValues[i]), Parse(toValues[i])); // shared values keep their first role
            return map;
        }

        private static string[] Roles(Palette p) => new[]
        {
            p.Primary, p.Secondary, p.Background, p.Surface, p.SurfaceAlt, p.Text, p.Text2, p.Border, p.Focus,
            p.TabStrip, p.Command, p.TabHover, p.TabPressed, p.TabText, p.TabIcon, p.TabSeparator,
        };

        // "Windows accent": the accent roles come from the user's Windows accent colour, in the shade
        // that keeps its contrast here — a fill of 3 : 1 or more against the page (4.5 in light, where it
        // is also the accent text colour) with white text of
        // 4.5 or more on it, and an accent text/focus colour of 4.5 or more. When no shade of the
        // Windows accent does, the scheme's own accent stays.
        private static Palette WithWindowsAccent(Palette p, bool dark)
        {
            try
            {
                var ui = new UISettings();
                var shades = new[]
                {
                    UIColorType.AccentDark1, UIColorType.Accent, UIColorType.AccentDark2, UIColorType.AccentLight1,
                }.Select(ui.GetColorValue).ToList();
                var background = Parse(p.Background);
                // In light, the accent is also the accent text colour (links, the favourite star), so it
                // needs text contrast (4.5) there; in dark, the text colour is the lighter shade below.
                var fill = shades.FirstOrDefault(c => Contrast(c, background) >= (dark ? 3 : 4.5) && Contrast(Microsoft.UI.Colors.White, c) >= 4.5);
                var textShades = (dark
                    ? new[] { UIColorType.AccentLight2, UIColorType.AccentLight1, UIColorType.AccentLight3 }
                    : new[] { UIColorType.AccentDark1, UIColorType.AccentDark2, UIColorType.Accent }).Select(ui.GetColorValue);
                var text = textShades.FirstOrDefault(c => Contrast(c, background) >= 4.5 && Contrast(c, Parse(p.TabStrip)) >= 3);
                if (fill == default || text == default) return p;
                return p with { Primary = Hex(fill), Secondary = Hex(dark ? text : fill), Focus = Hex(text) };
            }
            catch
            {
                return p;
            }
        }

        public static double Contrast(Color a, Color b)
        {
            static double Channel(byte v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
            static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
            var (x, y) = (Luminance(a), Luminance(b));
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        public static Color Parse(string hex) => Color.FromArgb(0xFF,
            Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

        private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static Color Mix(Color a, Color b, double t) => Color.FromArgb(0xFF,
            (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
