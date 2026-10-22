using Typedown.WinUI.Utilities;
using Xunit;

namespace Caret.ConverterTests
{
    // Settings > Appearance: the live preview shows what the window shows (the section colours that pass the contrast guard over the colour
    // scheme), and a section is "Custom" only when a colour of its own is kept that the scheme does not give anyway.
    public class AppearancePreviewTests
    {
        // Copper in light and in dark (Utilities/ColorSchemes.cs).
        private static readonly AppearancePreview.Scheme CopperLight =
            new("#A5522A", "#F8EBDD", "#EDD5BA", "#382A1B", "#6B584B", "#5E4A3C", "#E3D5C6");

        private static readonly AppearancePreview.Scheme CopperDark =
            new("#B55E2A", "#0E1220", "#090C15", "#EDEEF2", "#A6B0C3", "#8D97AB", "#2B3550");

        private const string Mist = "#DCE6EF", Sage = "#DCE7DA", Espresso = "#3B2A1E", Night = "#111C28";

        [Fact]
        public void With_no_section_colours_the_preview_is_the_scheme()
        {
            var c = AppearancePreview.Resolve(CopperLight, "", dark: false);
            Assert.Equal("#EDD5BA", c.BandBackground);
            Assert.Equal("#5E4A3C", c.BandText);
            Assert.Equal("#F8EBDD", c.SideBackground);
            Assert.Equal("#382A1B", c.SideText);
            Assert.Equal("#6B584B", c.SideText2);
            Assert.NotEqual(c.SideBackground, c.SideSelected);
            Assert.Equal("#F8EBDD", c.PageBackground);
            Assert.Equal("#F8EBDD", c.StatusBackground);
            Assert.Equal("#A5522A", c.Accent);
            Assert.Equal("#E3D5C6", c.Border);
        }

        [Fact]
        public void A_section_colour_that_passes_the_guard_is_shown_with_the_text_the_guard_chose()
        {
            var c = AppearancePreview.Resolve(CopperLight, $"band={Mist};side={Sage}", dark: false);
            Assert.Equal(Mist, c.BandBackground);
            Assert.Equal(Sage, c.SideBackground);
            Assert.Equal("#F8EBDD", c.PageBackground); // the other areas keep the scheme's
            Assert.Equal("#F8EBDD", c.StatusBackground);
            Assert.NotEqual("#5E4A3C", c.BandText); // the guard's text for that area, not the scheme's tab text
            Assert.NotEqual(c.SideBackground, c.SideSelected);
        }

        [Fact]
        public void The_page_keeps_the_schemes_text_over_its_own_colour()
        {
            var c = AppearancePreview.Resolve(CopperLight, $"page={Mist}", dark: false);
            Assert.Equal(Mist, c.PageBackground);
            Assert.Equal("#382A1B", c.PageText);
            Assert.Equal("#6B584B", c.PageText2);
        }

        [Fact]
        public void A_colour_that_would_be_hard_to_read_is_not_shown_as_applied()
        {
            // Espresso under the scheme's dark text: the page must keep the scheme's colour (the window does)
            var c = AppearancePreview.Resolve(CopperLight, $"page={Espresso}", dark: false);
            Assert.Equal("#F8EBDD", c.PageBackground);
        }

        [Fact]
        public void A_colour_of_the_other_theme_is_ignored()
        {
            var c = AppearancePreview.Resolve(CopperLight, $"band={Night}", dark: false);
            Assert.Equal("#EDD5BA", c.BandBackground);
            Assert.False(AppearancePreview.AnyCustom($"band={Night}", false, CopperLight));
            var dark = AppearancePreview.Resolve(CopperDark, $"band={Night}", dark: true);
            Assert.Equal(Night, dark.BandBackground);
        }

        [Fact]
        public void A_section_is_custom_when_it_keeps_a_colour_the_scheme_does_not_give()
        {
            Assert.False(AppearancePreview.IsCustom("", false, "band", CopperLight));
            Assert.True(AppearancePreview.IsCustom($"band={Mist}", false, "band", CopperLight));
            Assert.False(AppearancePreview.IsCustom($"band={Mist}", false, "page", CopperLight));
            Assert.True(AppearancePreview.AnyCustom($"page={Sage}", false, CopperLight));
            Assert.False(AppearancePreview.AnyCustom("", false, CopperLight));
        }

        [Fact]
        public void A_kept_colour_that_equals_the_schemes_own_is_not_custom()
        {
            // a scheme whose page colour is Mist: keeping Mist for the page changes nothing
            var mistScheme = CopperLight with { Background = Mist };
            Assert.Equal(Mist, AppearancePreview.SchemeColor(mistScheme, "page"));
            Assert.False(AppearancePreview.IsCustom($"page={Mist}", false, "page", mistScheme));
            Assert.True(AppearancePreview.IsCustom($"page={Sage}", false, "page", mistScheme));
        }

        [Fact]
        public void Reset_to_scheme_leaves_no_custom_section()
        {
            var stored = SectionColorChoice.With($"band={Mist};page={Sage}", false, "band", "");
            Assert.True(AppearancePreview.AnyCustom(stored, false, CopperLight));
            stored = SectionColorChoice.With(stored, false, "page", "");
            Assert.Equal("", stored);
            Assert.False(AppearancePreview.AnyCustom(stored, false, CopperLight));
            Assert.Equal("#EDD5BA", AppearancePreview.Resolve(CopperLight, stored, false).BandBackground);
        }
    }
}
