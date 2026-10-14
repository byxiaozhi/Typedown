using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>A server connection with a session on one end of a pipe; the test speaks raw frames on the other.</summary>
    internal sealed class Harness : IAsyncDisposable
    {
        public readonly DuplexStream Client;
        public readonly MessageFraming ClientFraming;
        public readonly JsonRpcConnection Server;
        public readonly AutomationSession Session;
        public readonly Task Running;
        private int nextId;

        public Harness(MethodTable? methods = null, string buildType = BuildTypes.Application, long maxMessageBytes = 1 << 20)
        {
            var (server, client) = TestPipes.Create();
            Client = client;
            ClientFraming = new MessageFraming(client, 1 << 22);
            methods ??= StandardMethods(buildType);
            Session = new AutomationSession(new ServerInfo { Version = "1.2.30", Commit = "abc", Platform = "test", BuildType = buildType, MaxMessageBytes = maxMessageBytes }, methods);
            Server = new JsonRpcConnection(new MessageFraming(server, maxMessageBytes), Session);
            Running = Server.RunAsync();
        }

        public static MethodTable StandardMethods(string buildType = BuildTypes.Application) =>
            new MethodTable(buildType)
                .Add(new MethodDescriptor("window.list", Scopes.AppRead, "window.list/1", (_, _) => Task.FromResult<JToken?>(new JArray())))
                .Add(new MethodDescriptor("document.get", Scopes.DocumentRead, "document.get/1", (c, _) => Task.FromResult<JToken?>(new JObject { ["documentId"] = c.Params.RequiredString("documentId", false) })))
                .Add(new MethodDescriptor("document.save", Scopes.DocumentSave, "document.save/1", (_, _) => Task.FromResult<JToken?>(new JObject())))
                .Add(new MethodDescriptor("document.replaceText", Scopes.DocumentWrite, "document.replaceText/1", (_, _) => Task.FromResult<JToken?>(new JObject())));

        public Task SendRawAsync(string json) => TestPipes.SendRawAsync(Client, TestPipes.Frame(json));

        public async Task<JObject> CallAsync(string method, JObject? parameters = null)
        {
            var id = Interlocked.Increment(ref nextId);
            var message = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
            if (parameters != null) message["params"] = parameters;
            await SendRawAsync(message.ToString(Newtonsoft.Json.Formatting.None));
            var reply = await TestPipes.ReadMessageAsync(ClientFraming);
            Assert.Equal(id, reply.Value<int>("id"));
            return reply;
        }

        public static JObject InitParams(params string[] scopes) => new()
        {
            ["apiVersion"] = 1,
            ["client"] = new JObject { ["id"] = "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c", ["name"] = "tests", ["version"] = "1" },
            ["requestedScopes"] = new JArray(scopes),
            ["futureField"] = "ignored",
        };

        public Task<JObject> InitializeAsync(params string[] scopes) => CallAsync("system.initialize", InitParams(scopes));

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Running.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public class SessionTests
    {
        private static string Kind(JObject reply) => (string)reply["error"]!["data"]!["kind"]!;
        private static int Code(JObject reply) => (int)reply["error"]!["code"]!;

        [Fact]
        public async Task Initialize_reports_the_build_scopes_and_methods()
        {
            await using var h = new Harness();
            var result = (JObject)(await h.InitializeAsync(Scopes.AppRead, Scopes.DocumentRead, Scopes.SettingsWrite, "made.up"))["result"]!;
            Assert.Equal(1, (int)result["apiVersion"]!);
            Assert.Equal("application", (string)result["server"]!["buildType"]!);
            Assert.Equal(new[] { "app.read", "document.read" }, result["grantedScopes"]!.Select(s => (string)s!));
            Assert.Equal("notAvailable", (string)result["deniedScopes"]!.Single(d => (string)d["scope"]! == "settings.write")["reason"]!);
            Assert.Equal("unknown", (string)result["deniedScopes"]!.Single(d => (string)d["scope"]! == "made.up")["reason"]!);
            Assert.Contains(result["methods"]!, m => (string)m["name"]! == "document.get" && (string)m["schema"]! == "document.get/1");
            Assert.True((bool)result["capabilities"]!["replaceText"]!);
            Assert.False((bool)result["capabilities"]!["serverRequests"]!);
            Assert.Equal(32, ((string)result["clientSessionId"]!).Length);
        }

        [Fact]
        public async Task Nothing_but_initialize_is_served_before_it()
        {
            await using var h = new Harness();
            var reply = await h.CallAsync("system.ping");
            Assert.Equal(-32001, Code(reply));
            Assert.Equal("not_initialized", Kind(reply));
            reply = await h.CallAsync("document.get", new JObject { ["documentId"] = "d" });
            Assert.Equal("not_initialized", Kind(reply));
        }

        [Fact]
        public async Task A_wrong_version_or_client_is_refused_and_initialize_can_be_retried()
        {
            await using var h = new Harness();
            var bad = Harness.InitParams();
            bad["apiVersion"] = 2;
            var reply = await h.CallAsync("system.initialize", bad);
            Assert.Equal(-32002, Code(reply));
            Assert.Equal(new[] { 1 }, reply["error"]!["data"]!["supportedVersions"]!.Select(v => (int)v));

            bad = Harness.InitParams();
            bad["client"]!["id"] = "not-a-uuid";
            reply = await h.CallAsync("system.initialize", bad);
            Assert.Equal("invalid_params", Kind(reply));
            Assert.Equal("client.id", (string)reply["error"]!["data"]!["field"]!);

            bad = Harness.InitParams();
            bad["apiVersion"] = "1";
            Assert.Equal("notAnInteger", (string)(await h.CallAsync("system.initialize", bad))["error"]!["data"]!["reason"]!);

            Assert.NotNull((await h.InitializeAsync())["result"]);
            reply = await h.InitializeAsync();
            Assert.Equal("alreadyInitialized", (string)reply["error"]!["data"]!["reason"]!);
        }

        [Fact]
        public async Task Methods_need_their_scope_and_unknown_ones_are_method_not_found()
        {
            await using var h = new Harness();
            await h.InitializeAsync(Scopes.DocumentRead);
            Assert.Equal("d1", (string)(await h.CallAsync("document.get", new JObject { ["documentId"] = "d1" }))["result"]!["documentId"]!);
            var reply = await h.CallAsync("document.save");
            Assert.Equal(-32003, Code(reply));
            Assert.Equal("document.save", (string)reply["error"]!["data"]!["scope"]!);
            reply = await h.CallAsync("document.explode");
            Assert.Equal(-32601, Code(reply));
            Assert.Equal("method_not_found", Kind(reply));
            reply = await h.CallAsync("test.barrier");
            Assert.Equal("method_not_found", Kind(reply));
            var ping = await h.CallAsync("system.ping");
            Assert.Equal(1, (int)ping["result"]!["apiVersion"]!);
        }

        [Fact]
        public void An_application_build_cannot_register_test_methods()
        {
            Assert.Throws<InvalidOperationException>(() => new MethodTable(BuildTypes.Application)
                .Add(new MethodDescriptor("test.barrier", null, "test.barrier/1", (_, _) => Task.FromResult<JToken?>(null))));
            new MethodTable(BuildTypes.AutomationTestHost)
                .Add(new MethodDescriptor("test.barrier", null, "test.barrier/1", (_, _) => Task.FromResult<JToken?>(null)));
        }

        [Fact]
        public async Task Params_errors_name_the_field_and_ignore_unknown_fields()
        {
            await using var h = new Harness();
            await h.InitializeAsync(Scopes.DocumentRead);
            var reply = await h.CallAsync("document.get", new JObject { ["unknown"] = 1 });
            Assert.Equal(-32602, Code(reply));
            Assert.Equal("documentId", (string)reply["error"]!["data"]!["field"]!);
            Assert.Equal("required", (string)reply["error"]!["data"]!["reason"]!);
            reply = await h.CallAsync("document.get", new JObject { ["documentId"] = 5 });
            Assert.Equal("notAString", (string)reply["error"]!["data"]!["reason"]!);
            reply = await h.CallAsync("document.get", new JObject { ["documentId"] = "d", ["extra"] = new JObject() });
            Assert.NotNull(reply["result"]);
        }

        [Theory]
        [InlineData("{not json", -32700, "parse_error")]
        [InlineData("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"system.ping\"}]", -32600, "invalid_request")]
        [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"system.ping\"}", -32600, "invalid_request")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":5}", -32600, "invalid_request")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":{},\"method\":\"system.ping\"}", -32600, "invalid_request")]
        [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"system.ping\",\"params\":3}", -32600, "invalid_request")]
        [InlineData("{} {}", -32700, "parse_error")]
        public async Task Bad_messages_get_structured_errors_and_the_connection_goes_on(string raw, int code, string kind)
        {
            await using var h = new Harness();
            await h.SendRawAsync(raw);
            var reply = await TestPipes.ReadMessageAsync(h.ClientFraming);
            Assert.Equal(code, Code(reply));
            Assert.Equal(kind, Kind(reply));
            Assert.NotNull((await h.InitializeAsync())["result"]);
        }

        [Fact]
        public async Task Invalid_utf8_is_a_parse_error()
        {
            await using var h = new Harness();
            var body = new byte[] { (byte)'{', (byte)'"', 0xff, (byte)'"', (byte)':', (byte)'1', (byte)'}' };
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await TestPipes.SendRawAsync(h.Client, header.Concat(body).ToArray());
            Assert.Equal("parse_error", Kind(await TestPipes.ReadMessageAsync(h.ClientFraming)));
        }

        [Fact]
        public async Task A_message_over_the_limit_is_answered_and_skipped()
        {
            await using var h = new Harness(maxMessageBytes: 400);
            await h.SendRawAsync("{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"system.initialize\",\"params\":{\"pad\":\"" + new string('x', 500) + "\"}}");
            var reply = await TestPipes.ReadMessageAsync(h.ClientFraming);
            Assert.Equal(-32030, Code(reply));
            Assert.Equal(400, (int)reply["error"]!["data"]!["maxMessageBytes"]!);
            Assert.Equal(JTokenType.Null, reply["id"]!.Type);
            Assert.NotNull((await h.CallAsync("system.initialize", Harness.InitParams()))["result"]);
        }

        [Fact]
        public async Task A_handler_crash_is_an_internal_error_without_details()
        {
            var methods = new MethodTable(BuildTypes.Application)
                .Add(new MethodDescriptor("app.getState", Scopes.AppRead, "app.getState/1", (_, _) => throw new InvalidOperationException("secret path C:\\Users\\x")));
            await using var h = new Harness(methods);
            await h.InitializeAsync(Scopes.AppRead);
            var reply = await h.CallAsync("app.getState");
            Assert.Equal(-32603, Code(reply));
            Assert.DoesNotContain("secret", reply.ToString());
        }

        [Fact]
        public async Task A_slow_request_does_not_hold_up_the_next_one()
        {
            var release = new TaskCompletionSource<bool>();
            var methods = new MethodTable(BuildTypes.Application)
                .Add(new MethodDescriptor("app.getState", Scopes.AppRead, "app.getState/1", async (_, _) => { await release.Task; return new JObject { ["slow"] = true }; }))
                .Add(new MethodDescriptor("window.list", Scopes.AppRead, "window.list/1", (_, _) => Task.FromResult<JToken?>(new JArray())));
            await using var h = new Harness(methods);
            await h.InitializeAsync(Scopes.AppRead);
            await h.SendRawAsync("{\"jsonrpc\":\"2.0\",\"id\":100,\"method\":\"app.getState\"}");
            var fast = await h.CallAsync("window.list");
            Assert.Equal(JTokenType.Array, fast["result"]!.Type);
            release.SetResult(true);
            var slow = await TestPipes.ReadMessageAsync(h.ClientFraming);
            Assert.Equal(100, (int)slow["id"]!);
        }

        [Fact]
        public async Task Closing_the_connection_cancels_running_handlers()
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var methods = new MethodTable(BuildTypes.Application)
                .Add(new MethodDescriptor("app.getState", Scopes.AppRead, "app.getState/1", async (_, ct) =>
                {
                    started.SetResult(true);
                    try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { cancelled.SetResult(true); throw; }
                    return null;
                }));
            var h = new Harness(methods);
            await h.InitializeAsync(Scopes.AppRead);
            await h.SendRawAsync("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"app.getState\"}");
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await h.DisposeAsync();
            Assert.True(await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task Requests_flow_both_ways_on_one_connection()
        {
            var (serverStream, clientStream) = TestPipes.Create();
            var server = new JsonRpcConnection(new MessageFraming(serverStream, 1 << 20), new Echo("server"));
            var client = new JsonRpcConnection(new MessageFraming(clientStream, 1 << 20), new Echo("client"));
            var runs = new[] { server.RunAsync(), client.RunAsync() };
            var fromServer = await server.SendRequestAsync("ask", new JObject { ["q"] = 1 });
            Assert.Equal("client", (string)fromServer!["by"]!);
            var fromClient = await client.SendRequestAsync("ask", null);
            Assert.Equal("server", (string)fromClient!["by"]!);
            var failing = await Assert.ThrowsAsync<JsonRpcRemoteException>(() => client.SendRequestAsync("fail", null));
            Assert.Equal(-32015, failing.Code);

            var hanging = server.SendRequestAsync("hang", null);
            clientStream.Dispose();
            await Assert.ThrowsAsync<System.IO.IOException>(() => hanging.WaitAsync(TimeSpan.FromSeconds(10)));
            await Task.WhenAll(runs).WaitAsync(TimeSpan.FromSeconds(10));
        }

        private sealed class Echo : IJsonRpcHandler
        {
            private readonly string name;
            public Echo(string name) => this.name = name;
            public async Task<JToken?> HandleRequestAsync(JsonRpcRequest request, CancellationToken ct)
            {
                if (request.Method == "fail") throw new AutomationException(AutomationErrorKind.read_only, "no");
                if (request.Method == "hang") await Task.Delay(Timeout.Infinite, ct);
                return new JObject { ["by"] = name };
            }
            public Task HandleNotificationAsync(JsonRpcRequest notification, CancellationToken ct) => Task.CompletedTask;
        }
    }
}
