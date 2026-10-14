using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    /// <summary>One externally visible setting, as docs/automation-fixtures/settings-map.json describes it.</summary>
    public sealed class SettingDescription
    {
        public string Key { get; set; } = "";
        public string Scope { get; set; } = "application";
        public JObject Type { get; set; } = new();
        public JArray VisibleIn { get; set; } = new();
        public bool RestartRequired { get; set; }
        public string Sensitivity { get; set; } = "none";
        public double? Rounding { get; set; }

        /// <summary>The stored properties behind the setting on each platform ("windows", "uno"), as the map lists them.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Properties { get; set; } = new Dictionary<string, IReadOnlyList<string>>();
    }

    /// <summary>
    /// The settings the API may touch - read from settings-map.json (embedded), never by reflection. Values are
    /// checked against each setting's type here, before any platform code sees them.
    /// </summary>
    public sealed class SettingsCatalog
    {
        public IReadOnlyList<SettingDescription> Settings { get; }

        private SettingsCatalog(IReadOnlyList<SettingDescription> settings) => Settings = settings;

        public static SettingsCatalog Load()
        {
            using var stream = typeof(SettingsCatalog).Assembly.GetManifestResourceStream("settings-map.json")
                ?? throw new InvalidOperationException("settings-map.json is not embedded");
            using var reader = new StreamReader(stream);
            return Parse(JObject.Parse(reader.ReadToEnd()));
        }

        public static SettingsCatalog Parse(JObject map) =>
            new(map["exposed"]!.Select(e => new SettingDescription
            {
                Key = (string)e["key"]!,
                Scope = (string?)e["scope"] ?? "application",
                Type = (JObject)e["type"]!,
                VisibleIn = (JArray?)e["visibleIn"] ?? new JArray(),
                RestartRequired = (bool?)e["restartRequired"] ?? false,
                Sensitivity = (string?)e["sensitivity"] ?? "none",
                Rounding = ((string?)e["rounding"])?.StartsWith("to 0.1") == true ? 0.1 : null,
                Properties = new[] { "windows", "uno" }.ToDictionary(platform => platform,
                    platform => (IReadOnlyList<string>)(((e[platform] as JObject)?["properties"] as JArray)?.Select(n => (string)n!).ToList() ?? new List<string>())),
            }).ToList());

        /// <summary>
        /// The stored properties behind every exposed setting on a platform: the changes settingsRevision counts. A
        /// property only the application keeps (a window's placement, the recent files) is not among them.
        /// </summary>
        public ISet<string> StoredProperties(string platform) =>
            new HashSet<string>(Settings.SelectMany(s => s.Properties.TryGetValue(platform, out var names) ? names : Array.Empty<string>()), StringComparer.Ordinal);

        public SettingDescription Find(string key) =>
            Settings.FirstOrDefault(s => s.Key == key)
            ?? throw new AutomationException(AutomationErrorKind.setting_not_exposed, $"'{key}' is not an external setting.", new Dictionary<string, object?> { ["key"] = key });

        /// <summary>The value made canonical (rounded), or <c>setting_invalid</c> naming why it does not fit.</summary>
        public JToken Validate(SettingDescription setting, JToken? value)
        {
            if (value == null) throw Invalid(setting, "required");
            var checkedValue = Check(setting.Type, value, setting) ?? throw Invalid(setting, "doesNotMatch");
            if (setting.Rounding is double step && checkedValue.Type is JTokenType.Float or JTokenType.Integer)
                checkedValue = new JValue(Math.Round(Math.Round((double)checkedValue / step) * step, 6));
            return checkedValue;
        }

        private static AutomationException Invalid(SettingDescription setting, string reason) =>
            new(AutomationErrorKind.setting_invalid, $"The value for '{setting.Key}' is not valid: {reason}.",
                new Dictionary<string, object?> { ["key"] = setting.Key, ["reason"] = reason, ["allowed"] = setting.Type });

        // The subset of JSON Schema the map uses: type boolean/integer/number/string/object with minimum/maximum,
        // minLength/maxLength/pattern, enum, const, properties, required and oneOf. Returns the value when it fits, null
        // when not.
        private static JToken? Check(JObject schema, JToken value, SettingDescription setting)
        {
            if (schema["oneOf"] is JArray options)
            {
                var matches = options.OfType<JObject>().Select(o => Check(o, value, setting)).Where(v => v != null).ToList();
                return matches.Count == 1 ? matches[0] : null;
            }
            if (schema["enum"] is JArray allowed)
                return allowed.Any(a => JToken.DeepEquals(a, value)) ? value : null;
            if (schema["const"] is JToken constant)
                return JToken.DeepEquals(constant, value) ? value : null;
            switch ((string?)schema["type"])
            {
                case "boolean":
                    return value.Type == JTokenType.Boolean ? value : null;
                case "integer":
                    if (value.Type != JTokenType.Integer && !(value.Type == JTokenType.Float && Math.Floor((double)value) == (double)value)) return null;
                    return InRange(schema, (double)value) ? new JValue((long)(double)value) : null;
                case "number":
                    if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return null;
                    return InRange(schema, (double)value) ? new JValue((double)value) : null;
                case "string":
                    if (value.Type != JTokenType.String) return null;
                    var text = (string)value!;
                    if (text.Length < ((int?)schema["minLength"] ?? 0) || text.Length > ((int?)schema["maxLength"] ?? int.MaxValue)) return null;
                    if (schema["pattern"] is JToken pattern && !System.Text.RegularExpressions.Regex.IsMatch(text, (string)pattern!)) return null;
                    return value;
                case "object":
                case null when schema["properties"] != null:
                    if (!(value is JObject obj)) return null;
                    foreach (var name in (schema["required"] as JArray)?.Select(r => (string)r!) ?? Enumerable.Empty<string>())
                        if (!obj.ContainsKey(name)) return null;
                    var result = new JObject();
                    foreach (var property in (schema["properties"] as JObject)?.Properties() ?? Enumerable.Empty<JProperty>())
                    {
                        if (!obj.TryGetValue(property.Name, out var v)) continue;
                        var ok = Check((JObject)property.Value, v, setting);
                        if (ok == null) return null;
                        result[property.Name] = ok;
                    }
                    return result;
                default:
                    return value;
            }
        }

        private static bool InRange(JObject schema, double v) =>
            (schema["minimum"] == null || v >= (double)schema["minimum"]!) && (schema["maximum"] == null || v <= (double)schema["maximum"]!);
    }

    /// <summary>The application's settings as the API sees them (Windows: the shared settings store).</summary>
    public interface ISettingsHost
    {
        /// <summary>
        /// Advances with every change of an exposed setting, from any window or the API - and only those: a property the
        /// application keeps for itself (a window moved, so its placement saved) does not, or a client's next
        /// settings.set would be refused as stale with none of its settings changed.
        /// </summary>
        long Revision { get; }

        /// <summary>
        /// Whether this platform has the setting at all (some exist on Windows only): settings.describe lists only these,
        /// and get/set of another answers setting_not_exposed with reason notOnThisPlatform.
        /// </summary>
        bool Supports(string key);

        /// <summary>The external value of an exposed setting.</summary>
        Task<JToken> GetAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// Applies an already validated value to every window and persists it; returns the new revision. Throws
        /// <c>setting_invalid</c> for a value the platform refuses (an unknown custom theme) and
        /// <c>persistence_failed</c> when the value is applied but could not be saved.
        /// </summary>
        Task<long> SetAsync(string key, JToken value, CancellationToken cancellationToken);
    }

    /// <summary>settings.describe/get/set (docs/automation-api-spec.md, section 3.3).</summary>
    public static class SettingsMethods
    {
        // One gate per host, shared by every connection: each connection builds its own method table.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ISettingsHost, SemaphoreSlim> gates = new();

        public static MethodTable AddTo(MethodTable table, ISettingsHost host, SettingsCatalog catalog, Action<string, string>? onWrite = null)
        {
            var gate = gates.GetValue(host, _ => new SemaphoreSlim(1, 1));
            // A setting this platform does not have is as unknown here as one the map withholds, with the reason said.
            SettingDescription Find(string key)
            {
                var setting = catalog.Find(key);
                if (!host.Supports(setting.Key))
                    throw new AutomationException(AutomationErrorKind.setting_not_exposed, $"'{key}' does not exist on this platform.",
                        new Dictionary<string, object?> { ["key"] = key, ["reason"] = "notOnThisPlatform" });
                return setting;
            }
            table.Add(new MethodDescriptor("settings.describe", Scopes.SettingsRead, "settings.describe/1", (c, ct) =>
                Task.FromResult<JToken?>(new JObject
                {
                    ["settingsRevision"] = host.Revision,
                    ["settings"] = new JArray(catalog.Settings.Where(s => host.Supports(s.Key)).Select(s => new JObject
                    {
                        ["key"] = s.Key,
                        ["scope"] = s.Scope,
                        ["type"] = s.Type,
                        ["visibleIn"] = s.VisibleIn,
                        ["restartRequired"] = s.RestartRequired,
                    })),
                })));
            table.Add(new MethodDescriptor("settings.get", Scopes.SettingsRead, "settings.get/1", async (c, ct) =>
            {
                var keys = c.Params.OptionalStringArray("keys") ?? catalog.Settings.Where(s => host.Supports(s.Key)).Select(s => s.Key).ToList();
                var values = new JObject();
                var revision = host.Revision;
                foreach (var key in keys) values[Find(key).Key] = await host.GetAsync(key, ct).ConfigureAwait(false);
                return new JObject { ["settingsRevision"] = revision, ["values"] = values };
            }));
            table.Add(new MethodDescriptor("settings.set", Scopes.SettingsWrite, "settings.set/1", async (c, ct) =>
            {
                var p = c.Params;
                var setting = Find(p.RequiredString("key", allowEmpty: false));
                var value = catalog.Validate(setting, (c.Request.Params as JObject)?["value"]);
                var baseRevision = p.RequiredInteger("baseSettingsRevision");
                p.OptionalString("clientOperationId");
                // One set at a time: the revision check and the change must not interleave with another client's.
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (host.Revision != baseRevision)
                        throw new AutomationException(AutomationErrorKind.revision_conflict, "The settings changed since baseSettingsRevision.",
                            new Dictionary<string, object?> { ["settingsRevision"] = host.Revision });
                    var revision = await host.SetAsync(setting.Key, value, ct).ConfigureAwait(false);
                    try { onWrite?.Invoke(AutomationServer.DisplayName(c.Session.Client?.Name), setting.Key); } catch { }
                    return new JObject { ["settingsRevision"] = revision, ["operationId"] = Guid.NewGuid().ToString("N"), ["key"] = setting.Key, ["value"] = value };
                }
                finally
                {
                    gate.Release();
                }
            }));
            return table;
        }
    }
}
