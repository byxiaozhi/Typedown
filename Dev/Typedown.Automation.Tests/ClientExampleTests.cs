using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>
    /// docs/automation-examples/typedown_client.py against a real pipe server and the fake host (a Unix domain socket
    /// here; Windows runs the PowerShell example in E2E). Skipped where there is no python3.
    /// </summary>
    public class ClientExampleTests : IAsyncLifetime
    {
        private readonly string endpoint = "td-py-" + Guid.NewGuid().ToString("N");
        private readonly FakeHost host = new();
        private AutomationServer server = null!;

        public Task InitializeAsync()
        {
            var info = new ServerInfo { Version = "1.2.30", Platform = "test" };
            server = new AutomationServer(() => new PlainPipeListener(endpoint),
                () => new AutomationSession(info, DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, info.InstanceId)), info.MaxMessageBytes);
            server.Start();
            return WaitForSocket();
        }

        // Start returns before the listener exists; the example connects at once and would find no socket yet.
        private async Task WaitForSocket()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            var socket = Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + endpoint);
            for (var i = 0; i < 100 && !File.Exists(socket); i++) await Task.Delay(20);
        }

        public Task DisposeAsync() => server.StopAsync();

        private static string Example => Path.Combine(RepoRoot(), "docs", "automation-examples", "typedown_client.py");

        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "docs", "automation-api-spec.md"))) dir = Path.GetDirectoryName(dir);
            return dir ?? throw new InvalidOperationException("repository root not found");
        }

        private async Task<(int exit, string stdout, string stderr)?> Python(params string[] args)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Example);
            start.ArgumentList.Add("--socket");
            start.ArgumentList.Add(Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + endpoint));
            foreach (var a in args) start.ArgumentList.Add(a);
            Process process;
            try { process = Process.Start(start)!; }
            catch (System.ComponentModel.Win32Exception) { return null; }
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            return (process.ExitCode, await stdout, await stderr);
        }

        [Fact]
        public async Task Lists_and_reads()
        {
            var doc = host.Add("# Title\n\ntext é\n", "/tmp/a.md");
            var list = await Python("list");
            if (list == null) return;
            Assert.Equal(0, list.Value.exit);
            Assert.Contains(doc.DocumentId, list.Value.stdout);
            var read = await Python("read", doc.DocumentId);
            Assert.Equal("# Title\n\ntext é\n", read!.Value.stdout);
        }

        [Fact]
        public async Task A_conflict_is_read_again_and_decided_again_from_the_new_text()
        {
            var doc = host.Add("# Title\n\nold text, old news\n", "/tmp/a.md");
            // The reader types between the example's read and its write: the first write is a conflict.
            var typed = false;
            doc.AfterCapture = () => { if (!typed) { typed = true; doc.PageText = "# Title\n\nold text, old news, old habits\n"; } };
            var run = await Python("replace-text", doc.DocumentId, "old", "new");
            if (run == null) return;
            Assert.True(run.Value.exit == 0, run.Value.stderr);
            Assert.Contains("revision_conflict", run.Value.stderr);
            // All three occurrences, counted in the text read after the conflict - not the two it first saw.
            Assert.Equal("# Title\n\nnew text, new news, new habits\n", doc.Text);
        }
    }
}
