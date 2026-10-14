using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>docs/automation-fixtures/settings-map.json accounts for every stored Windows setting.</summary>
    public class SettingsMapTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        private static JObject Map() => JObject.Parse(File.ReadAllText(Path.Combine(Root, "docs/automation-fixtures/settings-map.json")));

        /// <summary>Properties of the Windows SettingsViewModel that are stored in the settings file.</summary>
        private static string[] StoredSettings() =>
            Regex.Matches(File.ReadAllText(Path.Combine(Root, "Dev/Typedown.Core/ViewModels/SettingsViewModel.cs")),
                    @"public\s+[\w<>?.,\s]+?\s+(\w+)\s*\{\s*get\s*=>[^;]*GetSettingValue")
                .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

        [Fact]
        public void Every_stored_setting_is_exposed_or_has_a_reason_not_to_be()
        {
            var map = Map();
            var exposed = map["exposed"]!.SelectMany(e => e["windows"]!["properties"]!.Select(p => (string)p!)).ToList();
            var withheld = map["notExposed"]!.SelectMany(g => g["windows"]!.Select(p => (string)p!)).ToList();
            var stored = StoredSettings();
            Assert.Contains("HedgeDocPassword", stored);
            Assert.True(stored.Length > 60, $"only {stored.Length} settings found; the pattern no longer matches the source");
            foreach (var name in stored)
                Assert.True(exposed.Contains(name) || withheld.Contains(name), $"{name} is neither exposed nor withheld in settings-map.json");
            foreach (var name in exposed.Concat(withheld))
                Assert.Contains(name, stored);
            Assert.Empty(exposed.Intersect(withheld));
            Assert.Equal(withheld.Count, withheld.Distinct().Count());
        }

        [Fact]
        public void The_settings_revision_counts_the_exposed_properties_and_not_the_window_state()
        {
            var map = Map();
            var windows = SettingsCatalog.Parse(map).StoredProperties("windows");
            Assert.Equal(map["exposed"]!.SelectMany(e => e["windows"]!["properties"]!.Select(p => (string)p!)).Distinct().Count(), windows.Count);
            foreach (var name in new[] { "FontSize", "AppTheme", "CustomTheme", "Language" }) Assert.Contains(name, windows);
            foreach (var name in new[] { "StartupPlacement", "SidePaneWidth", "SidePaneOpen", "SourceCode", "AllowLocalAutomation" }) Assert.DoesNotContain(name, windows);
            var uno = SettingsCatalog.Parse(map).StoredProperties("uno");
            foreach (var name in new[] { "FontSize", "Language" }) Assert.Contains(name, uno);
            // What the running application reads is the embedded copy: the same as the document.
            Assert.True(windows.SetEquals(SettingsCatalog.Load().StoredProperties("windows")));
            Assert.True(uno.SetEquals(SettingsCatalog.Load().StoredProperties("uno")));
        }

        [Fact]
        public void Secrets_window_state_and_modes_are_never_exposed()
        {
            var exposed = Map()["exposed"]!.SelectMany(e => e["windows"]!["properties"]!.Select(p => (string)p!)).ToList();
            foreach (var name in new[] { "HedgeDocPassword", "SourceCode", "ReadOnly", "StartupPlacement", "CustomCss" })
                Assert.DoesNotContain(name, exposed);
            foreach (var name in exposed)
                Assert.False(Typedown.Core.Services.SettingsScope.IsWindowLocal(name), $"{name} is window-local and cannot be an application setting");
        }

        [Fact]
        public void Every_exposed_key_is_fully_described()
        {
            foreach (var entry in Map()["exposed"]!)
            {
                var key = (string)entry["key"]!;
                Assert.Matches(@"^[a-z]+\.[a-zA-Z]+$", key);
                Assert.Equal("application", (string)entry["scope"]!);
                foreach (var field in new[] { "type", "windows", "uno", "visibleIn", "sensitivity", "restartRequired" })
                    Assert.True(entry[field] != null, $"{key} has no {field}");
                Assert.NotEmpty(entry["windows"]!["properties"]!);
                Assert.False(string.IsNullOrEmpty((string?)entry["windows"]!["apply"]), $"{key} has no Windows apply path");
            }
        }
    }
}
