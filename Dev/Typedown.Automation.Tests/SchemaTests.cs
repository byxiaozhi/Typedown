using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Json.Schema;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>
    /// docs/automation-schema/v1.json is the contract: its fixtures hold, what the server actually sends validates
    /// against it, and its error codes agree with the spec's table and with <see cref="AutomationErrorKind"/>.
    /// </summary>
    public class SchemaTests
    {
        private static readonly string Docs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs"));
        private static readonly Lazy<JsonObject> Root = new(() => (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Docs, "automation-schema/v1.json")))!);
        private static readonly Lazy<JsonSchema> Loaded = new(() =>
        {
            var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(Docs, "automation-schema/v1.json")));
            SchemaRegistry.Global.Register(schema);
            return schema;
        });

        private static EvaluationResults Evaluate(string def, JsonNode? instance)
        {
            _ = Loaded.Value;
            Assert.True(((JsonObject)Root.Value["$defs"]!).ContainsKey(def), $"no definition {def}");
            var wrapper = JsonSchema.FromText($"{{\"$ref\":\"https://typedown.local/automation/v1.json#/$defs/{def}\"}}");
            using var doc = System.Text.Json.JsonDocument.Parse(instance?.ToJsonString() ?? "null");
            return wrapper.Evaluate(doc.RootElement.Clone(), new EvaluationOptions { OutputFormat = OutputFormat.List });
        }

        /// <summary>For other tests: validates a Newtonsoft token against a definition of v1.json.</summary>
        internal static (bool valid, string errors) EvaluateFor(string def, JToken? instance)
        {
            var r = Evaluate(def, instance == null ? null : ToNode(instance));
            return (r.IsValid, Errors(r));
        }

        private static JsonNode? ToNode(JToken token) => JsonNode.Parse(token.ToString(Newtonsoft.Json.Formatting.None));

        private static string Errors(EvaluationResults r) =>
            string.Join("; ", (r.Details ?? new List<EvaluationResults>()).Where(d => d.Errors != null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")));

        public static IEnumerable<object[]> Fixtures(string set)
        {
            var file = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(Docs, "automation-schema/fixtures.json")))!;
            var i = 0;
            foreach (var item in (JsonArray)file[set]!)
                yield return new object[] { $"{i++}: {item!["def"]} {item["why"]}", item.ToJsonString() };
        }

        [Theory]
        [MemberData(nameof(Fixtures), "valid")]
        public void Valid_fixtures_validate(string name, string fixture)
        {
            var item = JsonNode.Parse(fixture)!;
            var result = Evaluate((string)item["def"]!, item["instance"]);
            Assert.True(result.IsValid, $"{name}: {Errors(result)}");
        }

        [Theory]
        [MemberData(nameof(Fixtures), "invalid")]
        public void Invalid_fixtures_are_refused(string name, string fixture)
        {
            var item = JsonNode.Parse(fixture)!;
            Assert.False(Evaluate((string)item["def"]!, item["instance"]).IsValid, name);
        }

        [Fact]
        public void Every_method_has_params_and_result_definitions()
        {
            var defs = (JsonObject)Root.Value["$defs"]!;
            var methods = defs.Select(p => p.Key).Where(k => k.EndsWith(".params")).Select(k => k[..^".params".Length]).ToList();
            Assert.Contains("document.replaceText", methods);
            foreach (var m in methods) Assert.True(defs.ContainsKey(m + ".result"), $"{m} has no result definition");
        }

        [Fact]
        public void Error_codes_agree_between_schema_spec_and_code()
        {
            var fromSchema = ((JsonObject)Root.Value["$defs"]!["error"]!["x-codes"]!).ToDictionary(p => p.Key, p => (int)p.Value!);
            var fromCode = Enum.GetValues(typeof(AutomationErrorKind)).Cast<AutomationErrorKind>().ToDictionary(k => k.ToString(), k => (int)k);
            foreach (var pair in fromCode) Assert.Equal(pair.Value, fromSchema[pair.Key]);
            var protocolOnly = fromSchema.Keys.Except(fromCode.Keys).OrderBy(k => k).ToArray();
            Assert.Equal(new[] { "internal_error", "invalid_request", "parse_error" }, protocolOnly);

            var spec = File.ReadAllText(Path.Combine(Docs, "automation-api-spec.md"));
            var rows = Regex.Matches(spec, @"^\|\s*`(-32\d{3})`\s*\|\s*`([a-z_]+)`\s*\|", RegexOptions.Multiline)
                .Cast<Match>().ToDictionary(m => m.Groups[2].Value, m => int.Parse(m.Groups[1].Value));
            Assert.Equal(fromCode.OrderBy(p => p.Key), rows.OrderBy(p => p.Key));
        }

        [Fact]
        public async Task What_the_server_sends_validates()
        {
            await using var h = new Harness();
            var init = await h.InitializeAsync(Scopes.AppRead, Scopes.DocumentRead, "made.up");
            var r = Evaluate("system.initialize.result", ToNode(init["result"]!));
            Assert.True(r.IsValid, Errors(r));
            Assert.True(Evaluate("response", ToNode(init)).IsValid);

            var ping = await h.CallAsync("system.ping");
            Assert.True(Evaluate("system.ping.result", ToNode(ping["result"]!)).IsValid);

            foreach (var reply in new[]
            {
                await h.CallAsync("document.save"),
                await h.CallAsync("nope"),
                await h.CallAsync("document.get", new JObject { ["documentId"] = 1 }),
                await h.CallAsync("system.initialize", Harness.InitParams()),
            })
            {
                var e = Evaluate("response", ToNode(reply));
                Assert.True(e.IsValid, reply.ToString() + " " + Errors(e));
                Assert.NotNull(reply["error"]);
            }
            foreach (var kind in Enum.GetValues(typeof(AutomationErrorKind)).Cast<AutomationErrorKind>())
            {
                var ex = new AutomationException(kind, "m", new Dictionary<string, object?> { ["revision"] = 3L });
                var error = new JObject { ["code"] = ex.Code, ["message"] = ex.Message, ["data"] = JsonRpcConnection.ErrorData(ex) };
                Assert.True(Evaluate("error", ToNode(error)).IsValid, kind.ToString());
            }
        }
    }
}
