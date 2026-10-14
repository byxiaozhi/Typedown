using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Typedown.Mcp;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>The MCP server (typedownctl mcp) against a real pipe server and the fake host, message by message.</summary>
    public class McpTests : IAsyncLifetime
    {
        private readonly string endpoint = "td-mcp-" + Guid.NewGuid().ToString("N");
        private readonly FakeHost host = new();
        private readonly FakeViewHost view = new();
        private AutomationServer server = null!;
        private FakeDocument doc = null!;
        private McpServer mcp = null!;
        private int id;

        public Task InitializeAsync()
        {
            var info = new ServerInfo { Version = "1.2.30", Platform = "test" };
            server = new AutomationServer(() => new PlainPipeListener(endpoint),
                () => new AutomationSession(info, ViewMethods.AddTo(DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, info.InstanceId), view)), info.MaxMessageBytes);
            doc = host.Add("# Title\n\nold text\n", "/tmp/a.md");
            server.Start();
            mcp = New(endpoint);
            return Task.CompletedTask;
        }

        public Task DisposeAsync() => server.StopAsync();

        private static McpServer New(string endpoint) =>
            new(TextReader.Null, TextWriter.Null, name => new TypedownConnection(endpoint, "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c", name, Cli.Cli.ConnectPipeAsync));

        private async Task<JObject> Send(string method, JObject? parameters = null, McpServer? server = null) =>
            (await (server ?? mcp).HandleAsync(new JObject { ["jsonrpc"] = "2.0", ["id"] = ++id, ["method"] = method, ["params"] = parameters ?? new JObject() }, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(20)))!;

        private async Task<JObject> Tool(string name, JObject arguments, McpServer? server = null) =>
            (JObject)(await Send("tools/call", new JObject { ["name"] = name, ["arguments"] = arguments }, server))["result"]!;

        [Fact]
        public async Task Initialize_negotiates_the_version_and_lists_the_tools()
        {
            var init = await Send("initialize", new JObject { ["protocolVersion"] = "2025-03-26", ["capabilities"] = new JObject(), ["clientInfo"] = new JObject { ["name"] = "Agent‮", ["version"] = "1" } });
            Assert.Equal("2025-03-26", (string)init["result"]!["protocolVersion"]!);
            Assert.NotNull(init["result"]!["capabilities"]!["tools"]);
            Assert.Equal("Agent (MCP)", mcp.ClientName);
            var unknownVersion = await Send("initialize", new JObject { ["protocolVersion"] = "1999-01-01" }, New(endpoint));
            Assert.Equal(McpServer.ProtocolVersions[0], (string)unknownVersion["result"]!["protocolVersion"]!);

            var tools = (JArray)(await Send("tools/list"))["result"]!["tools"]!;
            Assert.Equal(new[] { "typedown_list_documents", "typedown_read_document", "typedown_replace_text", "typedown_replace_document", "typedown_save_document",
                "typedown_close_document", "typedown_get_view", "typedown_set_view" },
                tools.Select(t => (string)t["name"]!));
            foreach (var t in tools)
            {
                Assert.Equal("object", (string)t["inputSchema"]!["type"]!);
                foreach (var required in (JArray)t["inputSchema"]!["required"]!)
                    Assert.NotNull(t["inputSchema"]!["properties"]![(string)required!]);
            }
            Assert.Empty((JObject)(await Send("ping"))["result"]!);
            Assert.Equal(-32601, (int)(await Send("resources/list"))["error"]!["code"]!);
            Assert.Null(await mcp.HandleAsync(new JObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" }, CancellationToken.None));
        }

        [Fact]
        public async Task Read_then_replace_text_then_save()
        {
            var list = await Tool("typedown_list_documents", new JObject());
            Assert.False((bool)list["isError"]!);
            Assert.Equal(doc.DocumentId, (string)list["structuredContent"]!["documents"]![0]!["documentId"]!);

            var read = await Tool("typedown_read_document", new JObject { ["documentId"] = doc.DocumentId, ["includeHeadings"] = true });
            Assert.Equal("# Title\n\nold text\n", (string)read["structuredContent"]!["text"]!);
            Assert.Equal("Title", (string)read["structuredContent"]!["headings"]![0]!["text"]!);
            var revision = (long)read["structuredContent"]!["revision"]!;

            var write = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = revision, ["find"] = "old", ["replacement"] = "new", ["save"] = true });
            Assert.False((bool)write["isError"]!);
            Assert.Equal(revision + 1, (long)write["structuredContent"]!["revision"]!);
            Assert.True((bool)write["structuredContent"]!["saved"]!);
            Assert.Equal("# Title\n\nnew text\n", doc.Text);
            // The text content carries the same result for clients that ignore structuredContent.
            Assert.Equal(revision + 1, (long)JObject.Parse((string)write["content"]![0]!["text"]!)["revision"]!);

            var whole = await Tool("typedown_replace_document", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = revision + 1, ["text"] = "# Whole\n" });
            Assert.False((bool)whole["isError"]!);
            Assert.Equal("# Whole\n", doc.Text);
            var saved = await Tool("typedown_save_document", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = revision + 2 });
            Assert.False((bool)saved["isError"]!);
        }

        [Fact]
        public async Task ScrollToChange_reveals_the_change()
        {
            var write = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "old", ["replacement"] = "new", ["scrollToChange"] = true });
            Assert.False((bool)write["isError"]!);
            Assert.True(doc.LastApply!.ScrollToChange);
            Assert.Equal(Reveal.Document, host.Reveals[^1]);
            write = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 1, ["find"] = "new", ["replacement"] = "old", ["reveal"] = true });
            Assert.False((bool)write["isError"]!);
            Assert.False(doc.LastApply!.ScrollToChange);
            write = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 2, ["find"] = "old", ["replacement"] = "new" });
            Assert.False((bool)write["isError"]!);
            Assert.False(doc.LastApply!.ScrollToChange);
            Assert.Equal(Reveal.None, host.Reveals[^1]);
        }

        [Fact]
        public async Task The_view_tools_read_and_change_the_window()
        {
            var got = await Tool("typedown_get_view", new JObject { ["windowId"] = FakeHost.WindowId });
            Assert.Equal("visual", (string)got["structuredContent"]!["mode"]!);
            var set = await Tool("typedown_set_view", new JObject { ["windowId"] = FakeHost.WindowId, ["mode"] = "source", ["sidePane"] = new JObject { ["open"] = true, ["page"] = "outline" } });
            Assert.False((bool)set["isError"]!);
            Assert.Equal("source", (string)set["structuredContent"]!["mode"]!);
            Assert.Equal("outline", (string)set["structuredContent"]!["sidePane"]!["page"]!);
            var bad = await Tool("typedown_set_view", new JObject { ["windowId"] = FakeHost.WindowId, ["bounds"] = new JObject { ["width"] = 100 } });
            Assert.True((bool)bad["isError"]!);
            Assert.Equal("invalid_params", (string)bad["structuredContent"]!["error"]!["data"]!["kind"]!);
        }

        [Fact]
        public async Task A_conflict_is_the_same_error_typedownctl_reports_and_says_to_read_again()
        {
            await Tool("typedown_replace_document", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "# moved on\n" });
            var stale = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "moved", ["replacement"] = "x" });
            Assert.True((bool)stale["isError"]!);
            var error = (JObject)stale["structuredContent"]!["error"]!;
            Assert.Equal("revision_conflict", (string)error["data"]!["kind"]!);
            Assert.Contains("typedown_read_document", (string)stale["structuredContent"]!["next"]!);

            var output = new StringWriter();
            var exit = await new Cli.Cli(TextReader.Null, output, TextWriter.Null).RunAsync(new[] { "--json", "--endpoint", endpoint, "--client-id", "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c",
                "replace-text", doc.DocumentId, "--base-revision", "0", "--find", "moved", "--replacement", "x", "--expected-count", "1" });
            Assert.Equal(Cli.Cli.Conflict, exit);
            Assert.True(JToken.DeepEquals(JObject.Parse(output.ToString())["error"], error), $"CLI {output} vs MCP {error}");
        }

        [Fact]
        public async Task A_match_count_mismatch_writes_nothing_and_is_not_retried()
        {
            var calls = doc.Calls.Count;
            var result = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["find"] = "t", ["replacement"] = "T" });
            Assert.True((bool)result["isError"]!);
            Assert.Equal("match_count_mismatch", (string)result["structuredContent"]!["error"]!["data"]!["kind"]!);
            Assert.Contains("occurs 3 time(s), not 1", (string)result["structuredContent"]!["next"]!);
            Assert.Equal("# Title\n\nold text\n", doc.Text);
            Assert.Equal(0, doc.Revision);
            Assert.Equal(1, doc.Calls.Skip(calls).Count(c => c == "flush")); // one attempt, no second one
            Assert.DoesNotContain("apply", doc.Calls.Skip(calls));
        }

        [Fact]
        public async Task Bad_arguments_and_a_missing_typedown_are_tool_errors()
        {
            var missing = await Tool("typedown_replace_text", new JObject { ["documentId"] = doc.DocumentId, ["find"] = "old", ["replacement"] = "new" });
            Assert.True((bool)missing["isError"]!);
            Assert.Equal("baseRevision", (string)missing["structuredContent"]!["error"]!["data"]!["field"]!);

            var away = New("td-mcp-nobody-" + Guid.NewGuid().ToString("N"));
            var result = await Tool("typedown_read_document", new JObject { ["documentId"] = doc.DocumentId }, away);
            Assert.True((bool)result["isError"]!);
            Assert.Equal("not_connected", (string)result["structuredContent"]!["error"]!["data"]!["kind"]!);
            Assert.Contains("Allow local automation", (string)result["structuredContent"]!["next"]!);
        }

        [Fact]
        public async Task Runs_over_stdio_one_message_per_line()
        {
            var input = new StringReader(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"clientInfo\":{\"name\":\"t\"}}}\n" +
                "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n" +
                "not json\n" +
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"typedown_read_document\",\"arguments\":{\"documentId\":\"" + doc.DocumentId + "\"}}}\n");
            var output = new StringWriter();
            await new McpServer(input, output, name => new TypedownConnection(endpoint, "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c", name, Cli.Cli.ConnectPipeAsync))
                .RunAsync().WaitAsync(TimeSpan.FromSeconds(20));
            var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(JObject.Parse).ToList();
            Assert.Equal(3, lines.Count);
            Assert.Equal(1, (int)lines[0]["id"]!);
            Assert.Equal(-32700, (int)lines[1]["error"]!["code"]!);
            Assert.Equal("# Title\n\nold text\n", (string)lines[2]["result"]!["structuredContent"]!["text"]!);
        }
    }
}
