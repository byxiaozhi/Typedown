using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The keyboard-first layer: the key tips (Alt) and the cheat sheet (Ctrl+Shift+/), without the window.
    public class KeyboardLayerTests
    {
        private static string AppRoot => Path.GetFullPath(Path.Combine(TestPaths.ProjectFolder, "..", "Typedown.WinUI"));

        public static IEnumerable<object[]> Languages => new[] { "en", "es", "fr", "pl", "pt" }.Select(l => new object[] { l });

        private static Dictionary<string, string> Strings(string language) =>
            XDocument.Load(Path.Combine(AppRoot, "Strings", language, "AppResources.resw")).Root.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));

        // --- Key tips ---

        private static KeyTipPlan.Request R(string id, string label, string preferred = null) => new(id, label, preferred);

        [Fact]
        public void The_menus_keep_their_keys_and_every_other_place_gets_a_free_letter_of_its_name()
        {
            var plan = KeyTipPlan.Assign(new[]
            {
                R("file", "File", "F"), R("edit", "Edit", "E"), R("help", "Help", "H"),
                R("home", "Home"), R("format", "Format"), R("recent", "Recent"),
            });
            Assert.Equal("F", plan["file"]);
            Assert.Equal("E", plan["edit"]);
            Assert.Equal("H", plan["help"]);
            Assert.Equal("O", plan["home"]);    // H is the Help menu's: the next letter of "Home"
            Assert.Equal("O", plan["home"]);
            Assert.Equal("R", plan["format"]);  // F is taken, then O (Home has it), then R
            Assert.Equal("C", plan["recent"]);  // R is Format's, E is Edit's, then C
            Assert.Equal(plan.Count, plan.Values.Distinct().Count());
        }

        [Fact]
        public void A_key_is_never_given_twice_and_the_same_input_gives_the_same_keys()
        {
            var requests = Enumerable.Range(0, 20).Select(i => R("id" + i, "Same name")).ToList();
            var first = KeyTipPlan.Assign(requests);
            var second = KeyTipPlan.Assign(requests);
            Assert.Equal(first.OrderBy(p => p.Key), second.OrderBy(p => p.Key));
            Assert.Equal(20, first.Values.Distinct().Count());
            Assert.All(first.Values, key => Assert.Matches("^[A-Z0-9]$", key));
        }

        [Fact]
        public void Accents_are_taken_off_and_a_letter_with_no_plain_key_is_skipped()
        {
            Assert.Equal(new[] { 'E', 'C', 'O', 'L', 'E' }, KeyTipPlan.Letters("École"));
            Assert.Equal(new[] { 'O', 'Z' }, KeyTipPlan.Letters("ŁóZ")); // the first letter has no plain form: left out
            Assert.Equal('A', KeyTipPlan.Key("a"));
            Assert.Equal('E', KeyTipPlan.Key("é"));
            Assert.Null(KeyTipPlan.Key("ł"));
            Assert.Null(KeyTipPlan.Key("AB"));
            Assert.Null(KeyTipPlan.Key(""));
            Assert.Null(KeyTipPlan.Key(null));
        }

        [Fact]
        public void A_name_with_no_usable_letters_gets_a_free_letter_or_digit()
        {
            var plan = KeyTipPlan.Assign(new[] { R("a", "ŁŁ"), R("b", "") });
            Assert.Equal("A", plan["a"]);
            Assert.Equal("B", plan["b"]);
        }

        [Fact]
        public void More_places_than_keys_leaves_the_last_ones_without_a_tip()
        {
            var plan = KeyTipPlan.Assign(Enumerable.Range(0, 40).Select(i => R("id" + i, "x")).ToList());
            Assert.Equal(36, plan.Count);
            Assert.Equal(36, plan.Values.Distinct().Count());
            Assert.DoesNotContain("id39", plan.Keys);
        }

        [Fact]
        public void Two_menus_with_the_same_preferred_key_do_not_share_it()
        {
            var plan = KeyTipPlan.Assign(new[] { R("one", "Alpha", "A"), R("two", "Beta", "A") });
            Assert.Equal("A", plan["one"]);
            Assert.Equal("B", plan["two"]);
        }

        // --- The shortcut catalog and the cheat sheet ---

        [Theory]
        [MemberData(nameof(Languages))]
        public void Every_name_in_the_catalog_and_every_group_has_a_string_in_every_language(string language)
        {
            var strings = Strings(language);
            var missing = ShortcutCatalog.All.Select(s => s.NameKey).Concat(ShortcutCatalog.Groups.Select(ShortcutCatalog.GroupNameKey))
                .Where(key => !strings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)).ToList();
            Assert.True(missing.Count == 0, language + ": " + string.Join(", ", missing));
        }

        [Fact]
        public void No_gesture_is_listed_twice_and_every_row_is_in_a_group_of_the_sheet()
        {
            var gestures = ShortcutCatalog.All.SelectMany(s => s.Gestures).ToList();
            Assert.Equal(gestures.Count, gestures.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(ShortcutCatalog.All, s => Assert.Contains(s.Group, ShortcutCatalog.Groups));
        }

        // The menus and the window's accelerators (MainWindow.xaml) show these gestures: each one must be on the sheet, so that a shortcut
        // added to a menu is not forgotten there.
        [Fact]
        public void Every_shortcut_the_menus_show_is_on_the_sheet()
        {
            var xaml = File.ReadAllText(Path.Combine(AppRoot, "MainWindow.xaml"));
            var en = Strings("en");
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(xaml, "KeyboardAcceleratorTextOverride=\"([^\"]+)\""))
            {
                var value = m.Groups[1].Value;
                var loc = Regex.Match(value, @"\{u:Loc Key=(\w+)\}");
                shown.Add(loc.Success ? en[loc.Groups[1].Value] : value);
            }
            foreach (Match m in Regex.Matches(xaml, "<KeyboardAccelerator Key=\"(\\w+)\"(?: Modifiers=\"([\\w,]+)\")?"))
            {
                var parts = new List<string>();
                var modifiers = m.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (modifiers.Contains("Control")) parts.Add("Ctrl");
                if (modifiers.Contains("Shift")) parts.Add("Shift");
                if (modifiers.Contains("Menu")) parts.Add("Alt");
                parts.Add(m.Groups[1].Value);
                shown.Add(string.Join("+", parts));
            }
            var listed = new HashSet<string>(ShortcutCatalog.All.SelectMany(s => s.Gestures), StringComparer.OrdinalIgnoreCase);
            var forgotten = shown.Where(g => !listed.Contains(g)).OrderBy(g => g).ToList();
            Assert.True(forgotten.Count == 0, "not on the sheet: " + string.Join(", ", forgotten));
            Assert.True(shown.Count >= 20, "the menus' shortcuts were not found (" + shown.Count + ")");
        }

        private static Func<string, string> English()
        {
            var en = Strings("en");
            return key => en[key];
        }

        [Fact]
        public void The_sheet_lists_each_group_under_its_name_and_leaves_out_an_empty_one()
        {
            var rows = ShortcutSheet.Build(English(), "");
            var headers = rows.Where(r => r.IsHeader).Select(r => r.Text).ToList();
            Assert.Equal(new[] { "Format", "Speech", "Find", "Files", "General" }, headers); // Review has no shortcut yet: not shown
            Assert.True(rows[0].IsHeader);
            Assert.False(rows[1].IsHeader);
            Assert.Equal(ShortcutCatalog.All.Length, rows.Count(r => !r.IsHeader));
        }

        [Fact]
        public void Typing_narrows_the_sheet_to_the_matching_shortcuts_with_their_groups()
        {
            var bold = ShortcutSheet.Build(English(), "bold");
            Assert.Equal(2, bold.Count);
            Assert.True(bold[0].IsHeader);
            Assert.Equal("Bold", bold[1].Text);
            Assert.Equal("Ctrl+B", bold[1].Keys);
        }

        [Fact]
        public void The_keys_can_be_searched_too_and_so_can_the_group()
        {
            var byKeys = ShortcutSheet.Build(English(), "ctrl+shift+p");
            Assert.Contains(byKeys, r => !r.IsHeader && r.Keys == "Ctrl+Shift+P");
            var byGroup = ShortcutSheet.Build(English(), "files");
            Assert.True(byGroup.Count(r => !r.IsHeader) >= ShortcutCatalog.All.Count(s => s.Group == "Files"));
            Assert.Single(byGroup, r => r.IsHeader);
        }

        [Fact]
        public void A_search_with_no_match_gives_an_empty_sheet()
        {
            Assert.Empty(ShortcutSheet.Build(English(), "zzzzqq"));
        }
    }
}
