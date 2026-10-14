using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>The Linux/macOS endpoint (Uno): private directory, 0600 socket, stale files, a second instance.</summary>
    public class UnixSocketListenerTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "td-uds-" + Guid.NewGuid().ToString("N")[..8]);
        private static bool OnUnix => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public void Dispose()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private string SocketPath => Path.Combine(root, "typedown", UnixSocketListener.ApplicationFile);

        [Fact]
        public async Task The_socket_is_private_and_serves_the_api_to_typedownctl()
        {
            if (!OnUnix) return;
            var host = new FakeHost();
            var doc = host.Add("# Title\n", "/tmp/a.md");
            var info = new ServerInfo { Version = "1.2.30", Platform = "test" };
            UnixSocketListener? opened = null;
            var server = new AutomationServer(() => opened = UnixSocketListener.Open(SocketPath),
                () => new AutomationSession(info, DocumentMethods.AddTo(new MethodTable(BuildTypes.Application), host, info.InstanceId)), info.MaxMessageBytes);
            server.Start();
            try
            {
                for (var i = 0; i < 50 && !File.Exists(SocketPath); i++) await Task.Delay(20);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SocketPath));
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(SocketPath)!));

                var output = new StringWriter();
                var exit = await new Cli.Cli(TextReader.Null, output, TextWriter.Null).RunAsync(new[] { "--json", "--endpoint", SocketPath, "--client-id", "5c05cf79-35ef-4ff5-9dce-cf3fcb63b42c", "documents" }).WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(0, exit);
                Assert.Equal(doc.DocumentId, (string)JObject.Parse(output.ToString())["documents"]![0]!["documentId"]!);

                // A second instance finds the first one answering and leaves it alone.
                Assert.Throws<InvalidOperationException>(() => UnixSocketListener.Open(SocketPath));
                Assert.True(File.Exists(SocketPath));
            }
            finally
            {
                await server.StopAsync();
            }
            Assert.False(File.Exists(SocketPath), "the socket file is removed on shutdown");
        }

        [Fact]
        public void A_socket_left_by_a_crash_is_replaced()
        {
            if (!OnUnix) return;
            // A crash leaves the socket file with nothing listening on it (.NET removes its own on close, so a process
            // that exits without cleaning up makes one; without python, a plain file stands in).
            Directory.CreateDirectory(Path.GetDirectoryName(SocketPath)!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            try
            {
                using var python = System.Diagnostics.Process.Start("python3", new[] { "-c", $"import socket; socket.socket(socket.AF_UNIX).bind('{SocketPath}')" });
                python.WaitForExit(10000);
            }
            catch (System.ComponentModel.Win32Exception) { File.WriteAllText(SocketPath, ""); }
            Assert.True(File.Exists(SocketPath));
            using var second = UnixSocketListener.Open(SocketPath);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SocketPath));
        }

        [Fact]
        public void A_directory_others_can_enter_is_refused()
        {
            if (!OnUnix) return;
            var dir = Path.GetDirectoryName(SocketPath)!;
            Directory.CreateDirectory(dir);
            File.SetUnixFileMode(dir, (UnixFileMode)Convert.ToInt32("777", 8));
            Assert.Throws<InvalidOperationException>(() => UnixSocketListener.Open(SocketPath));
            Assert.False(File.Exists(SocketPath));
        }

        [Fact]
        public async Task A_path_too_long_for_a_socket_is_a_clear_error_not_a_crash()
        {
            if (!OnUnix) return;
            var longPath = Path.Combine(root, new string('d', 120), "automation.v1.sock");
            Assert.Throws<InvalidOperationException>(() => UnixSocketListener.Open(longPath));
            await Assert.ThrowsAsync<IOException>(() => UnixSocketListener.ConnectAsync(longPath, default));
            var output = new StringWriter();
            var exit = await new Cli.Cli(TextReader.Null, output, TextWriter.Null).RunAsync(new[] { "--json", "--endpoint", longPath, "documents" });
            Assert.Equal(Cli.Cli.NotRunning, exit);
        }

        [Fact]
        public void The_default_path_is_in_the_runtime_directory()
        {
            if (!OnUnix) return;
            var saved = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            try
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", "/run/user/4242");
                Assert.Equal("/run/user/4242/typedown/automation.v1.sock", UnixSocketListener.DefaultPath(BuildTypes.Application));
                Assert.Equal("/run/user/4242/typedown/automation-test-host.v1.sock", UnixSocketListener.DefaultPath(BuildTypes.AutomationTestHost));
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", "/run/" + new string('x', 120));
                Assert.StartsWith("/tmp/typedown-", UnixSocketListener.DefaultPath(BuildTypes.Application));
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", null);
                Assert.StartsWith("/tmp/typedown-", UnixSocketListener.DefaultPath(BuildTypes.Application));
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", saved);
            }
        }
    }
}
