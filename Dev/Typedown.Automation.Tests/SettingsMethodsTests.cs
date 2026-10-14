using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    internal sealed class FakeSettingsHost : ISettingsHost
    {
        public readonly Dictionary<string, JToken> Values = new()
        {
            ["appearance.theme"] = JObject.Parse("{\"kind\":\"builtIn\",\"id\":\"system\"}"),
            ["editor.fontSize"] = 16,
            ["editor.lineHeight"] = 1.6,
            ["editor.textDirection"] = "auto",
        };
        public long Revision { get; set; } = 7;
        public bool FailPersist;
        /// <summary>Settings this fake platform does not have (Supports answers false for them).</summary>
        public readonly HashSet<string> Missing = new() { "app.keepRunning" };
        public bool Supports(string key) => !Missing.Contains(key);
        public Task<JToken> GetAsync(string key, CancellationToken ct) => Task.FromResult(Values.TryGetValue(key, out var v) ? v : JValue.CreateNull());
        public Task<long> SetAsync(string key, JToken value, CancellationToken ct)
        {
            if (key == "appearance.theme" && (string?)value["kind"] == "custom" && (string?)value["id"] != "solarized")
                throw new AutomationException(AutomationErrorKind.setting_invalid, "no such theme", new Dictionary<string, object?> { ["key"] = key, ["reason"] = "unknownTheme" });
            Values[key] = value;
            Revision++;
            if (FailPersist) throw new AutomationException(AutomationErrorKind.persistence_failed, "disk", new Dictionary<string, object?> { ["settingsRevision"] = Revision });
            return Task.FromResult(Revision);
        }
    }

    public class SettingsMethodsTests
    {
        private static (Harness h, FakeSettingsHost host) Start()
        {
            var host = new FakeSettingsHost();
            var table = SettingsMethods.AddTo(new MethodTable(BuildTypes.Application), host, SettingsCatalog.Load());
            return (new Harness(table), host);
        }

        private static void Valid(string def, JToken? instance)
        {
            var r = SchemaTests.EvaluateFor(def, instance);
            Assert.True(r.valid, $"{def}: {r.errors}\n{instance}");
        }

        private static string Kind(JObject reply) => (string)reply["error"]!["data"]!["kind"]!;

        [Fact]
        public void The_catalog_is_the_settings_map_and_exposes_nothing_else()
        {
            var keys = SettingsCatalog.Load().Settings.Select(s => s.Key).ToArray();
            Assert.Equal(new[] { "appearance.theme", "editor.fontSize", "editor.lineHeight", "editor.textDirection" }, keys.Take(4));
            Assert.Contains("ui.language", keys);
            Assert.DoesNotContain(keys, k => k.Contains("hedgeDoc", StringComparison.OrdinalIgnoreCase) || k.Contains("automation", StringComparison.OrdinalIgnoreCase) || k.Contains("css", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(keys.Length, keys.Distinct().Count());
        }

        [Fact]
        public async Task Booleans_patterns_and_enums_are_checked_before_the_platform_sees_them()
        {
            var (h, host) = Start();
            await using var _h = h;
            await h.InitializeAsync(Scopes.SettingsRead, Scopes.SettingsWrite);
            async Task<JToken> Set(string key, JToken value) => await h.CallAsync("settings.set", new JObject { ["key"] = key, ["value"] = value, ["baseSettingsRevision"] = host.Revision });
            string? Reason(JToken reply) => (string?)reply["error"]?["data"]?["reason"];

            Assert.Equal("doesNotMatch", Reason(await Set("editor.paragraphMarkers", "yes")));
            Assert.Equal("doesNotMatch", Reason(await Set("editor.paragraphMarkers", 1)));
            Assert.Null((await Set("editor.paragraphMarkers", false))["error"]);
            Assert.Equal("doesNotMatch", Reason(await Set("editor.fontFamily", "Inter; } body { display: none")));
            Assert.Null((await Set("editor.fontFamily", "\"Noto Sans\", sans-serif"))["error"]);
            Assert.Equal("doesNotMatch", Reason(await Set("editor.areaWidth", "12px")));
            Assert.Null((await Set("editor.areaWidth", "90%"))["error"]);
            Assert.Equal("doesNotMatch", Reason(await Set("ui.language", "ZH")));
            Assert.Null((await Set("ui.language", "zh-Hans"))["error"]);
            Assert.Equal("doesNotMatch", Reason(await Set("status.wordCount", "lines")));
            Assert.Equal("doesNotMatch", Reason(await Set("editor.tabSize", 9)));
        }

        [Fact]
        public async Task A_setting_this_platform_does_not_have_is_neither_described_nor_readable()
        {
            var (h, host) = Start();
            await using var _h = h;
            await h.InitializeAsync(Scopes.SettingsRead, Scopes.SettingsWrite);
            var described = (JArray)(await h.CallAsync("settings.describe"))["result"]!["settings"]!;
            Assert.DoesNotContain(described, d => (string)d["key"]! == "app.keepRunning");
            Assert.Contains(described, d => (string)d["key"]! == "ui.language");
            var got = await h.CallAsync("settings.get", new JObject { ["keys"] = new JArray("app.keepRunning") });
            Assert.Equal("notOnThisPlatform", (string)got["error"]!["data"]!["reason"]!);
            var all = await h.CallAsync("settings.get");
            Assert.Null(all["result"]!["values"]!["app.keepRunning"]);
            var set = await h.CallAsync("settings.set", new JObject { ["key"] = "app.keepRunning", ["value"] = true, ["baseSettingsRevision"] = host.Revision });
            Assert.Equal("notOnThisPlatform", (string)set["error"]!["data"]!["reason"]!);
        }

        [Fact]
        public async Task Describe_and_get_need_settings_read()
        {
            var (h, _) = Start();
            await using var _h = h;
            await h.InitializeAsync(Scopes.SettingsRead);
            var described = await h.CallAsync("settings.describe");
            Valid("settings.describe.result", described["result"]);
            var got = await h.CallAsync("settings.get", new JObject { ["keys"] = new JArray("editor.fontSize") });
            Valid("settings.get.result", got["result"]);
            Assert.Equal(16, (int)got["result"]!["values"]!["editor.fontSize"]!);
            Assert.Equal(-32020, (int)(await h.CallAsync("settings.get", new JObject { ["keys"] = new JArray("HedgeDocPassword") }))["error"]!["code"]!);
            Assert.Equal("settings.write", (string)(await h.CallAsync("settings.set", new JObject { ["key"] = "editor.fontSize", ["value"] = 20, ["baseSettingsRevision"] = 7 }))["error"]!["data"]!["scope"]!);
        }

        [Fact]
        public async Task Set_validates_checks_the_revision_and_returns_the_new_one()
        {
            var (h, host) = Start();
            await using var _h = h;
            await h.InitializeAsync(Scopes.SettingsRead, Scopes.SettingsWrite);
            var ok = await h.CallAsync("settings.set", new JObject { ["key"] = "editor.fontSize", ["value"] = 20, ["baseSettingsRevision"] = 7 });
            Valid("settings.set.result", ok["result"]);
            Assert.Equal(8, (int)ok["result"]!["settingsRevision"]!);
            Assert.Equal(20, (int)host.Values["editor.fontSize"]);

            var stale = await h.CallAsync("settings.set", new JObject { ["key"] = "editor.fontSize", ["value"] = 21, ["baseSettingsRevision"] = 7 });
            Assert.Equal("revision_conflict", Kind(stale));
            Assert.Equal(8, (int)stale["error"]!["data"]!["settingsRevision"]!);

            foreach (var (key, value) in new (string, JToken)[] { ("editor.fontSize", 100), ("editor.fontSize", 12.5), ("editor.fontSize", "16"), ("editor.lineHeight", 0.5),
                ("editor.textDirection", "sideways"), ("appearance.theme", JObject.Parse("{\"kind\":\"builtIn\",\"id\":\"neon\"}")), ("appearance.theme", "dark") })
            {
                var bad = await h.CallAsync("settings.set", new JObject { ["key"] = key, ["value"] = value, ["baseSettingsRevision"] = 8 });
                Assert.True(Kind(bad) == "setting_invalid", $"{key}={value}: {bad}");
            }
            Assert.Equal(8, host.Revision);

            var rounded = await h.CallAsync("settings.set", new JObject { ["key"] = "editor.lineHeight", ["value"] = 1.73, ["baseSettingsRevision"] = 8 });
            Assert.Equal(1.7, (double)rounded["result"]!["value"]!, 6);
            var theme = await h.CallAsync("settings.set", new JObject { ["key"] = "appearance.theme", ["value"] = JObject.Parse("{\"kind\":\"custom\",\"id\":\"missing\"}"), ["baseSettingsRevision"] = 9 });
            Assert.Equal("setting_invalid", Kind(theme));
            Assert.Equal("setting_not_exposed", Kind(await h.CallAsync("settings.set", new JObject { ["key"] = "SourceCode", ["value"] = true, ["baseSettingsRevision"] = 9 })));
        }

        [Fact]
        public async Task A_value_applied_but_not_saved_says_so()
        {
            var (h, host) = Start();
            await using var _h = h;
            await h.InitializeAsync(Scopes.SettingsWrite);
            host.FailPersist = true;
            var reply = await h.CallAsync("settings.set", new JObject { ["key"] = "editor.textDirection", ["value"] = "rtl", ["baseSettingsRevision"] = 7 });
            Assert.Equal(-32022, (int)reply["error"]!["code"]!);
            Assert.Equal(8, (int)reply["error"]!["data"]!["settingsRevision"]!);
        }
    }
}
