using System;
using System.Linq;

namespace Typedown.WinUI.Utilities
{
    // Settings > Appearance: the colours of the live preview (a small picture of the window) and which section colours are "Custom".
    // Plain .NET (no WinUI), so the tests compile it as it is. The preview must show what the window shows, so the rules here are the
    // window's (MainWindow.Sections.cs): a section colour is applied only when it passes the contrast guard, and then the text on that
    // area is the guard's; the page keeps the scheme's text.
    internal static class AppearancePreview
    {
        // The colours of the colour scheme the preview needs (Utilities/ColorSchemes.cs Palette), as "#RRGGBB".
        public sealed record Scheme(string Primary, string Background, string TabStrip, string Text, string Text2, string TabText, string Border);

        public sealed record Colors(
            string BandBackground, string BandText, string SideBackground, string SideText, string SideText2, string SideSelected,
            string PageBackground, string PageText, string PageText2, string StatusBackground, string StatusText, string Accent, string Border);

        // The colour the scheme itself gives an area: what "Reset to scheme" goes back to (the sidebar and the status bar are
        // transparent over the window, so it is the window's colour).
        public static string SchemeColor(Scheme scheme, string section) => section == "band" ? scheme.TabStrip : scheme.Background;

        public static Colors Resolve(Scheme scheme, string stored, bool dark)
        {
            string Applied(string section) => SectionColorChoice.Applied(stored, dark, section, scheme.Text);
            string Mixed(string a, string b, double t) => Mix(a, b, t);

            var band = Applied("band");
            var side = Applied("side");
            var page = Applied("page");
            var status = Applied("status");
            SectionColorChoice.Verdict? bandGuard = band == null ? null : SectionColorChoice.Check(band, "band", scheme.Text);
            SectionColorChoice.Verdict? sideGuard = side == null ? null : SectionColorChoice.Check(side, "side", scheme.Text);
            SectionColorChoice.Verdict? statusGuard = status == null ? null : SectionColorChoice.Check(status, "status", scheme.Text);
            var sideBackground = side ?? scheme.Background;
            var sideText = sideGuard?.Text ?? scheme.Text;
            return new Colors(
                BandBackground: band ?? scheme.TabStrip,
                BandText: bandGuard?.Text2 ?? scheme.TabText,
                SideBackground: sideBackground,
                SideText: sideText,
                SideText2: sideGuard?.Text2 ?? scheme.Text2,
                SideSelected: Mixed(sideBackground, sideText, 0.12),
                PageBackground: page ?? scheme.Background,
                PageText: scheme.Text,
                PageText2: scheme.Text2,
                StatusBackground: status ?? scheme.Background,
                StatusText: statusGuard?.Text2 ?? scheme.Text2,
                Accent: scheme.Primary,
                Border: scheme.Border);
        }

        // An area shows "Custom" when a colour of its own is kept and it is not the colour the scheme gives anyway.
        public static bool IsCustom(string stored, bool dark, string section, Scheme scheme) =>
            SectionColorChoice.Parse(stored, dark).TryGetValue(section, out var hex)
            && !string.Equals(hex, SchemeColor(scheme, section), StringComparison.OrdinalIgnoreCase);

        public static bool AnyCustom(string stored, bool dark, Scheme scheme) =>
            SectionColorChoice.Sections.Any(section => IsCustom(stored, dark, section, scheme));

        private static string Mix(string a, string b, double t)
        {
            int Channel(string hex, int at) => Convert.ToInt32(hex.Substring(at, 2), 16);
            string Part(int at) => ((int)Math.Round(Channel(a, at) + (Channel(b, at) - Channel(a, at)) * t)).ToString("X2");
            return "#" + Part(1) + Part(3) + Part(5);
        }
    }
}
