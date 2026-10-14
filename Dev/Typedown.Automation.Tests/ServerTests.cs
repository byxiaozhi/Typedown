using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>A named-pipe listener without the Windows ACL (on Linux .NET maps it to a Unix socket).</summary>
    internal sealed class PlainPipeListener : IConnectionListener
    {
        private readonly string name;
        private NamedPipeServerStream? waiting;
        public PlainPipeListener(string name) => this.name = name;

        public async Task<Stream> AcceptAsync(CancellationToken cancellationToken)
        {
            var server = new NamedPipeServerStream(name, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            waiting = server;
            try { await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false); }
            catch { server.Dispose(); throw; }
            waiting = null;
            return server;
        }

        public void Dispose() { try { waiting?.Dispose(); } catch { } }
    }

    public class ServerTests
    {
        private static readonly string[] AllScopes = { Scopes.AppRead, Scopes.DocumentRead, Scopes.DocumentWrite, Scopes.DocumentSave, Scopes.WindowFocus };

        private static async Task<(NamedPipeClientStream pipe, MessageFraming framing)> ConnectAsync(string name)
        {
            var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(5000);
            return (pipe, new MessageFraming(pipe, 1 << 22));
        }

        private static async Task<JObject> CallAsync(MessageFraming framing, Stream pipe, int id, string method, JObject? p = null)
        {
            var message = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
            if (p != null) message["params"] = p;
            await framing.WriteAsync(Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None)));
            return await TestPipes.ReadMessageAsync(framing);
        }

        private static (AutomationServer server, FakeHost host, ConcurrentQueue<(string client, string doc)> writes, ConcurrentQueue<AutomationActivity> activity) Build(string name, int max = 8)
        {
            var host = new FakeHost();
            var writes = new ConcurrentQueue<(string, string)>();
            var info = new ServerInfo { Version = "1.2.30", Platform = "test" };
            var server = new AutomationServer(() => new PlainPipeListener(name),
                () => new AutomationSession(info, DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, info.InstanceId, (c, d) => writes.Enqueue((c, d)))),
                info.MaxMessageBytes, max);
            var activity = new ConcurrentQueue<AutomationActivity>();
            server.ActivityChanged += a => activity.Enqueue(a);
            return (server, host, writes, activity);
        }

        [Fact]
        public async Task A_client_writes_through_a_real_pipe_and_the_app_hears_who_it_was()
        {
            var name = "td-test-" + Guid.NewGuid().ToString("N");
            var (server, host, writes, activity) = Build(name);
            var doc = host.Add("old\n", "/tmp/a.md");
            server.Start();
            try
            {
                var (pipe, framing) = await ConnectAsync(name);
                using (pipe)
                {
                    var init = Harness.InitParams(AllScopes);
                    init["client"]!["name"] = "agent‮\nwith a very long name that goes on and on and on";
                    await CallAsync(framing, pipe, 1, "system.initialize", init);
                    var reply = await CallAsync(framing, pipe, 2, "document.replace", new JObject { ["documentId"] = doc.DocumentId, ["baseRevision"] = 0, ["text"] = "new\n" });
                    Assert.Equal(1, (int)reply["result"]!["revision"]!);
                    Assert.True(writes.TryDequeue(out var write));
                    Assert.Equal(doc.DocumentId, write.doc);
                    Assert.DoesNotContain('‮', write.client);
                    Assert.DoesNotContain('\n', write.client);
                    Assert.True(write.client.Length <= 40);
                    Assert.Equal(1, server.Activity.ConnectionCount);
                }
                await WaitUntil(() => server.Activity.ConnectionCount == 0);
                Assert.Contains(activity, a => a.ConnectionCount == 1);
                Assert.Equal(0, activity.Last().ConnectionCount);
            }
            finally { await server.StopAsync(); }
        }

        [Fact]
        public async Task Connections_beyond_the_limit_are_closed()
        {
            var name = "td-test-" + Guid.NewGuid().ToString("N");
            var (server, _, _, _) = Build(name, max: 2);
            server.Start();
            try
            {
                var a = await ConnectAsync(name);
                var b = await ConnectAsync(name);
                await WaitUntil(() => server.Activity.ConnectionCount == 2);
                var c = await ConnectAsync(name);
                // The third is accepted and dropped at once: reading it ends.
                var frame = await c.framing.ReadAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(FrameStatus.EndOfStream, frame.Status);
                Assert.Equal(1, (int)(await CallAsync(a.framing, a.pipe, 1, "system.initialize", Harness.InitParams()))["result"]!["apiVersion"]!);
                foreach (var p in new[] { a.pipe, b.pipe, c.pipe }) p.Dispose();
            }
            finally { await server.StopAsync(); }
        }

        [Fact]
        public async Task Stopping_closes_every_connection_and_the_endpoint()
        {
            var name = "td-test-" + Guid.NewGuid().ToString("N");
            var (server, _, _, _) = Build(name);
            server.Start();
            var (pipe, framing) = await ConnectAsync(name);
            await WaitUntil(() => server.Activity.ConnectionCount == 1);
            await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(server.IsRunning);
            var frame = await framing.ReadAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(FrameStatus.EndOfStream, frame.Status);
            pipe.Dispose();
            using var late = new NamedPipeClientStream(".", name, PipeDirection.InOut);
            Assert.ThrowsAny<Exception>(() => late.Connect(300));
        }

        private sealed class BrokenListener : IConnectionListener
        {
            public Task<Stream> AcceptAsync(CancellationToken cancellationToken) => throw new UnauthorizedAccessException("the name is taken");
            public void Dispose() { }
        }

        [Fact]
        public async Task A_listener_that_cannot_listen_is_reported_not_retried_forever()
        {
            var info = new ServerInfo();
            var server = new AutomationServer(() => new BrokenListener(), () => new AutomationSession(info, new MethodTable(BuildTypes.Application)), info.MaxMessageBytes);
            var failed = new TaskCompletionSource<Exception>();
            server.ListenerFailed += e => failed.TrySetResult(e);
            server.Start();
            var error = await failed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsType<UnauthorizedAccessException>(error);
            await server.StopAsync();
        }

        [Fact]
        public void Endpoint_names_separate_users_and_the_test_host()
        {
            var app = AutomationEndpoint.PipeName(BuildTypes.Application, "S-1-5-21-1-2-3-1001");
            var test = AutomationEndpoint.PipeName(BuildTypes.AutomationTestHost, "S-1-5-21-1-2-3-1001");
            Assert.Equal(Brand.Name + ".Automation.v1.S-1-5-21-1-2-3-1001", app);
            Assert.NotEqual(app, test);
            Assert.NotEqual(app, AutomationEndpoint.PipeName(BuildTypes.Application, "S-1-5-21-1-2-3-1002"));
            Assert.Equal(Brand.Name + ".Automation.v1.1000x", AutomationEndpoint.PipeName(BuildTypes.Application, "1000/../x")); // only letters, digits and -
            Assert.Throws<ArgumentException>(() => AutomationEndpoint.PipeName(BuildTypes.Application, " "));
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(25);
            Assert.True(condition());
        }
    }
}
