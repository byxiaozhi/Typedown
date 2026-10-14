using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>
    /// v1 is frozen (docs/automation-api-spec.md, section 6): the current schema may only add to the frozen copy - new
    /// definitions, new optional properties, new result enum values - never remove, rename, retype or tighten.
    /// </summary>
    public class SchemaCompatibilityTests
    {
        private static string Docs(params string[] parts)
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "docs", "automation-api-spec.md"))) dir = Path.GetDirectoryName(dir);
            return Path.Combine(new[] { dir!, "docs" }.Concat(parts).ToArray());
        }

        public static List<string> Breaks(JObject frozen, JObject current)
        {
            var problems = new List<string>();
            var frozenDefs = (JObject)frozen["$defs"]!;
            var currentDefs = (JObject)current["$defs"]!;
            foreach (var def in frozenDefs.Properties())
            {
                if (currentDefs[def.Name] is not JToken now) { problems.Add($"{def.Name}: removed"); continue; }
                Compare(def.Name, def.Value, now, def.Name.EndsWith(".params"), problems);
            }
            return problems;
        }

        private static void Compare(string at, JToken was, JToken now, bool input, List<string> problems)
        {
            if (was is not JObject w || now is not JObject n) { if (!JToken.DeepEquals(was, now)) problems.Add($"{at}: changed"); return; }
            if (!JToken.DeepEquals(w["type"], n["type"])) problems.Add($"{at}: type {w["type"]} became {n["type"]}");
            if (!JToken.DeepEquals(w["$ref"], n["$ref"])) problems.Add($"{at}: $ref changed");
            if (!JToken.DeepEquals(w["const"], n["const"])) problems.Add($"{at}: const changed");
            var wasRequired = (w["required"] as JArray)?.Select(r => (string)r!).ToHashSet() ?? new HashSet<string>();
            var nowRequired = (n["required"] as JArray)?.Select(r => (string)r!).ToHashSet() ?? new HashSet<string>();
            // Parameters: nothing newly required. Results: nothing no longer guaranteed.
            if (input) foreach (var r in nowRequired.Except(wasRequired)) problems.Add($"{at}: '{r}' became required");
            else foreach (var r in wasRequired.Except(nowRequired)) problems.Add($"{at}: '{r}' is no longer always present");
            if (w["enum"] is JArray we)
            {
                var ne = (n["enum"] as JArray)?.Select(v => v.ToString()).ToHashSet() ?? new HashSet<string>();
                foreach (var v in we.Select(v => v.ToString()).Where(v => !ne.Contains(v))) problems.Add($"{at}: enum value '{v}' removed");
            }
            foreach (var bound in new[] { "minimum", "maximum", "minLength", "maxLength", "maxItems", "minItems" })
                if (w[bound] != null && !JToken.DeepEquals(w[bound], n[bound])) problems.Add($"{at}: {bound} {w[bound]} became {n[bound]?.ToString() ?? "unset"}");
            if (w["properties"] is JObject wp)
            {
                var np = n["properties"] as JObject;
                foreach (var p in wp.Properties())
                {
                    if (np?[p.Name] is not JToken np2) { problems.Add($"{at}.{p.Name}: removed"); continue; }
                    Compare($"{at}.{p.Name}", p.Value, np2, input, problems);
                }
            }
            if (w["items"] != null) Compare($"{at}[]", w["items"]!, n["items"] ?? new JObject(), input, problems);
            foreach (var combinator in new[] { "oneOf", "anyOf", "allOf" })
            {
                if (w[combinator] is not JArray wc) continue;
                if (n[combinator] is not JArray nc || nc.Count < wc.Count) { problems.Add($"{at}: {combinator} lost alternatives"); continue; }
                for (var i = 0; i < wc.Count; i++) Compare($"{at}.{combinator}[{i}]", wc[i], nc[i], input, problems);
            }
        }

        [Fact]
        public void The_v1_schema_only_adds_to_the_frozen_copy()
        {
            var frozen = JObject.Parse(File.ReadAllText(Docs("automation-schema", "v1-frozen.json")));
            var current = JObject.Parse(File.ReadAllText(Docs("automation-schema", "v1.json")));
            var breaks = Breaks(frozen, current);
            Assert.True(breaks.Count == 0, "v1 is frozen (spec section 6):\n" + string.Join("\n", breaks));
        }

        [Fact]
        public void The_check_catches_what_section_6_forbids()
        {
            var frozen = JObject.Parse(File.ReadAllText(Docs("automation-schema", "v1-frozen.json")));
            JObject Changed(Action<JObject> change) { var c = (JObject)frozen.DeepClone(); change((JObject)c["$defs"]!); return c; }

            Assert.Empty(Breaks(frozen, frozen));
            // Allowed: a new optional parameter, a new method.
            Assert.Empty(Breaks(frozen, Changed(d => { ((JObject)d["document.replace.params"]!["properties"]!)["newOption"] = new JObject { ["type"] = "boolean" }; d["document.brandNew.params"] = new JObject { ["type"] = "object" }; })));
            // Forbidden: each of these must be reported.
            Assert.NotEmpty(Breaks(frozen, Changed(d => d.Remove("document.replace.params"))));
            Assert.NotEmpty(Breaks(frozen, Changed(d => ((JArray)d["document.replace.params"]!["required"]!).Add("newOption"))));
            Assert.NotEmpty(Breaks(frozen, Changed(d => ((JObject)d["document.replace.params"]!["properties"]!).Remove("text"))));
            Assert.NotEmpty(Breaks(frozen, Changed(d => d["document.replace.params"]!["properties"]!["text"]!["type"] = "number")));
            Assert.NotEmpty(Breaks(frozen, Changed(d => ((JArray)d["document.get.result"]!["allOf"]![1]!["required"]!).RemoveAt(0))));
            Assert.NotEmpty(Breaks(frozen, Changed(d => ((JArray)d["document.replace.params"]!["properties"]!["normalizationPolicy"]!["enum"]!).RemoveAt(1))));
        }
    }
}
