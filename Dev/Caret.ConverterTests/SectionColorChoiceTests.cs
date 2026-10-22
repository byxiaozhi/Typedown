using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;
using Xunit;

namespace Caret.ConverterTests
{
    // Settings > Appearance > Section colors: choosing a colour for one area, choosing another, and coming back.
    // (The window only draws what SectionColorChoice decides, so what the user can set and set back is tested here.)
    public class SectionColorChoiceTests
    {
        // The scheme's text colour in light and dark (Utilities/ColorSchemes.cs), which the guard measures against.
        public static IEnumerable<object[]> SchemeTexts() => new[]
        {
            new object[] { "copper", false, "#382A1B" }, new object[] { "copper", true, "#EDEEF2" },
            new object[] { "paper", false, "#1A1A1A" }, new object[] { "paper", true, "#FFFFFF" },
            new object[] { "sage", false, "#1F2A22" }, new object[] { "sage", true, "#E6EEE7" },
            new object[] { "harbour", false, "#16232F" }, new object[] { "harbour", true, "#E4ECF4" },
            new object[] { "graphite", false, "#1C1C1C" }, new object[] { "graphite", true, "#EDEDED" },
        };

        private const string WarmSand = "#EAD9C4", Sage = "#DCE7DA", Mist = "#DCE6EF";

        [Fact]
        public void The_reported_sequence_sand_then_another_colour_then_sand_again_works()
        {
            // Copper, light: the sidebar Mist, the page Warm sand, the page changed to Sage, the page back to Warm sand.
            var setting = "";
            setting = SectionColorChoice.With(setting, false, "side", Mist);
            setting = SectionColorChoice.With(setting, false, "page", WarmSand);
            Assert.Equal($"side={Mist};page={WarmSand}", setting);
            setting = SectionColorChoice.With(setting, false, "page", Sage);
            Assert.Equal($"side={Mist};page={Sage}", setting);
            setting = SectionColorChoice.With(setting, false, "page", WarmSand);
            Assert.Equal($"side={Mist};page={WarmSand}", setting);
            Assert.Equal(WarmSand, SectionColorChoice.Applied(setting, false, "page", "#382A1B"));
            Assert.Equal(Mist, SectionColorChoice.Applied(setting, false, "side", "#382A1B"));
        }

        [Theory]
        [MemberData(nameof(SchemeTexts))]
        public void Every_offered_colour_of_every_area_can_be_set_and_set_back(string scheme, bool dark, string text)
        {
            foreach (var section in SectionColorChoice.Sections)
            {
                var rows = SectionColorChoice.Rows(section, dark, text);
                Assert.Equal("", rows[0].Hex);                                // the default is always first and always allowed
                Assert.True(rows[0].Enabled, scheme);
                Assert.Equal(SectionColorChoice.Swatches(dark).Length + 1, rows.Count);
                foreach (var row in rows.Where(r => r.Hex != "" && r.Enabled))
                {
                    var setting = SectionColorChoice.With("", dark, section, row.Hex);
                    Assert.Equal(row.Hex, SectionColorChoice.Applied(setting, dark, section, text));
                    Assert.Equal(row.Hex, rows[SectionColorChoice.SelectedRow(rows, setting, dark, section)].Hex);
                    // and back to the colour scheme's own
                    var back = SectionColorChoice.With(setting, dark, section, "");
                    Assert.Equal("", back);
                    Assert.Null(SectionColorChoice.Applied(back, dark, section, text));
                    Assert.Equal(0, SectionColorChoice.SelectedRow(rows, back, dark, section));
                }
            }
        }

        [Theory]
        [MemberData(nameof(SchemeTexts))]
        public void A_colour_is_offered_exactly_when_its_text_reaches_4_5_to_1(string scheme, bool dark, string text)
        {
            foreach (var section in SectionColorChoice.Sections)
                foreach (var row in SectionColorChoice.Rows(section, dark, text).Skip(1))
                {
                    var verdict = SectionColorChoice.Check(row.Hex, section, text);
                    Assert.True(verdict.Ok == row.Enabled, $"{scheme} {(dark ? "dark" : "light")} {section} {row.Hex}");
                    Assert.Equal(verdict.Ratio >= 4.5, row.Enabled);
                }
        }

        [Fact]
        public void The_page_keeps_the_scheme_text_so_a_dark_page_colour_is_not_offered_under_dark_text()
        {
            Assert.False(SectionColorChoice.Check("#3B2A1E", "page", "#382A1B").Ok);   // Espresso under Copper's dark text
            Assert.True(SectionColorChoice.Check("#3B2A1E", "band", "#382A1B").Ok);    // an area with its own text may turn it light
            Assert.True(SectionColorChoice.Check(WarmSand, "page", "#382A1B").Ok);
        }

        [Fact]
        public void A_choice_that_became_unreadable_stays_chosen_but_is_not_applied()
        {
            // Espresso for the page was fine under a scheme with light text; under Copper's dark text it isn't.
            var setting = "page=#3B2A1E";
            var rows = SectionColorChoice.Rows("page", false, "#382A1B");
            var shown = rows[SectionColorChoice.SelectedRow(rows, setting, false, "page")];
            Assert.Equal("#3B2A1E", shown.Hex);                  // the list shows it, not "from the colour scheme"
            Assert.False(shown.Enabled);
            Assert.Null(SectionColorChoice.Applied(setting, false, "page", "#382A1B"));
            // choosing the default is then a real change of the list, and clears it
            Assert.Equal("", SectionColorChoice.With(setting, false, "page", ""));
        }

        [Fact]
        public void Colours_of_the_other_theme_are_ignored_not_kept()
        {
            // Light colours found in the dark setting (and the other way round) are what a list of the wrong
            // theme used to store; they must not read as a choice.
            Assert.Empty(SectionColorChoice.Parse($"page={Sage};side={Mist}", true));
            Assert.Empty(SectionColorChoice.Parse("page=#221A13", false));
            var rows = SectionColorChoice.Rows("page", true, "#EDEEF2");
            Assert.Equal(0, SectionColorChoice.SelectedRow(rows, $"page={Sage}", true, "page"));
            Assert.Null(SectionColorChoice.Applied($"page={Sage}", true, "page", "#EDEEF2"));
            // and the next change rewrites the setting without them
            Assert.Equal("side=#15201A", SectionColorChoice.With($"page={Sage}", true, "side", "#15201A"));
        }

        [Fact]
        public void The_theme_has_its_own_choice_and_changing_one_leaves_the_other()
        {
            var light = SectionColorChoice.With("", false, "page", WarmSand);
            var dark = SectionColorChoice.With("", true, "page", "#221A13");
            Assert.Equal($"page={WarmSand}", light);
            Assert.Equal("page=#221A13", dark);
            Assert.Equal(WarmSand, SectionColorChoice.Applied(light, false, "page", "#382A1B"));
            Assert.Equal("#221A13", SectionColorChoice.Applied(dark, true, "page", "#EDEEF2"));
        }

        [Fact]
        public void A_choice_stays_when_the_colour_scheme_changes_and_comes_back()
        {
            var setting = SectionColorChoice.With("", false, "page", WarmSand);
            foreach (var text in new[] { "#382A1B", "#1A1A1A", "#1F2A22", "#16232F", "#1C1C1C", "#382A1B" })
                Assert.Equal(WarmSand, SectionColorChoice.Applied(setting, false, "page", text));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("garbage")]
        [InlineData("page")]
        [InlineData("page=")]
        [InlineData("page=#EAD9C4=#DCE7DA")]
        [InlineData("unknown=#EAD9C4")]
        [InlineData("page=#ZZZZZZ")]
        [InlineData("page=#123456")]              // a colour that is no swatch
        [InlineData(";;;")]
        public void Anything_that_is_not_a_choice_is_ignored(string setting) =>
            Assert.Empty(SectionColorChoice.Parse(setting, false));

        [Fact]
        public void The_setting_reads_the_swatch_in_any_case_and_keeps_the_first_of_a_repeated_area()
        {
            var parsed = SectionColorChoice.Parse("page=#ead9c4;page=#DCE7DA;band=#DCE6EF", false);
            Assert.Equal(WarmSand, parsed["page"]);          // as the swatch spells it
            Assert.Equal(Mist, parsed["band"]);
            Assert.Equal(2, parsed.Count);
        }

        [Fact]
        public void The_setting_is_written_in_a_fixed_order()
        {
            var setting = SectionColorChoice.With(SectionColorChoice.With("", false, "status", Mist), false, "band", Sage);
            Assert.Equal($"band={Sage};status={Mist}", setting);
        }

        [Theory]
        [InlineData("#00FF00", true)]
        [InlineData("#abcdef", true)]
        [InlineData("00FF00", false)]
        [InlineData("#00FF0", false)]
        [InlineData("#00FF000", false)]
        [InlineData(null, false)]
        public void A_hex_colour_has_a_hash_and_six_digits(string value, bool expected) =>
            Assert.Equal(expected, SectionColorChoice.IsHexColor(value));

        [Fact]
        public void The_lists_have_names_for_every_swatch()
        {
            foreach (var swatch in SectionColorChoice.LightSwatches.Concat(SectionColorChoice.DarkSwatches))
            {
                Assert.True(SectionColorChoice.IsHexColor(swatch.Hex), swatch.Hex);
                Assert.False(string.IsNullOrEmpty(swatch.NameKey));
            }
            Assert.Equal(SectionColorChoice.LightSwatches.Length, SectionColorChoice.LightSwatches.Select(s => s.Hex).Distinct().Count());
            Assert.Equal(SectionColorChoice.DarkSwatches.Length, SectionColorChoice.DarkSwatches.Select(s => s.Hex).Distinct().Count());
        }
    }
}