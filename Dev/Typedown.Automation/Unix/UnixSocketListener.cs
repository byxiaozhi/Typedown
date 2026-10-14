#if NET7_0_OR_GREATER
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    /// <summary>
    /// The automation endpoint on Linux and macOS (docs/automation-api-spec.md, section 5): a Unix domain socket in a
    /// directory only the current user can enter, the socket itself 0600 from the moment it exists, and on Linux every
    /// peer's uid checked against ours. A socket file left behind by a crashed instance is replaced; one that still
    /// answers belongs to a running instance and is left alone. Not part of the netstandard2.0 build: the CLI, the Uno
    /// port and the tests compile this file themselves.
    /// </summary>
    internal sealed class UnixSocketListener : IConnectionListener
    {
        public const string ApplicationFile = "automation.v1.sock";
        public const string TestHostFile = "automation-test-host.v1.sock";

        private readonly Socket socket;

        private UnixSocketListener(Socket socket, string path)
        {
            this.socket = socket;
            Path = path;
        }

        public string Path { get; }

        /// <summary>$XDG_RUNTIME_DIR/typedown/&lt;file&gt;, or /tmp/typedown-&lt;uid&gt;/&lt;file&gt; when there is no runtime directory.</summary>
        public static string DefaultPath(string buildType)
        {
            var file = buildType == BuildTypes.AutomationTestHost ? TestHostFile : ApplicationFile;
            var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            var fallback = System.IO.Path.Combine("/tmp", "typedown-" + geteuid(), file);
            if (string.IsNullOrEmpty(runtime) || !System.IO.Path.IsPathRooted(runtime)) return fallback;
            var path = System.IO.Path.Combine(runtime, "typedown", file);
            return Fits(path) ? path : fallback;
        }

        /// <summary>Socket addresses are short (108 bytes on Linux, 104 on macOS, with the terminating zero).</summary>
        public const int MaxPathBytes = 103;

        public static bool Fits(string path) => System.Text.Encoding.UTF8.GetByteCount(path) <= MaxPathBytes;

        /// <exception cref="InvalidOperationException">Another instance is listening, or the directory is not safe to use.</exception>
        public static UnixSocketListener Open(string path)
        {
            if (!Fits(path)) throw new InvalidOperationException($"The socket path is longer than {MaxPathBytes} bytes: {path}");
            var dir = System.IO.Path.GetDirectoryName(path) ?? throw new ArgumentException("The socket path has no directory.", nameof(path));
            EnsurePrivateDirectory(dir);
            if (File.Exists(path) || IsSocketFile(path))
            {
                if (Answers(path)) throw new InvalidOperationException("Another " + Brand.Name + " is already listening on " + path);
                File.Delete(path); // left behind by an instance that did not shut down
            }
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                socket.Bind(new UnixDomainSocketEndPoint(path));
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                socket.Listen(16);
                return new UnixSocketListener(socket, path);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static void EnsurePrivateDirectory(string dir)
        {
            var info = new DirectoryInfo(dir);
            if (!info.Exists)
            {
                Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                info.Refresh();
            }
            // Someone else's directory, a link, or one others can enter could let them take or reach the socket.
            if (info.LinkTarget != null) throw new InvalidOperationException(dir + " is a link; refusing to put the automation socket there.");
            var mode = File.GetUnixFileMode(dir);
            if ((mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                throw new InvalidOperationException($"{dir} can be entered by other users ({mode}); refusing to put the automation socket there.");
        }

        private static bool IsSocketFile(string path)
        {
            try { return (File.GetAttributes(path) & System.IO.FileAttributes.Directory) == 0; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        private static bool Answers(string path)
        {
            using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try { probe.Connect(new UnixDomainSocketEndPoint(path)); return true; }
            catch (SocketException) { return false; }
        }

        public async Task<Stream> AcceptAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var client = await socket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                if (PeerIsCurrentUser(client)) return new NetworkStream(client, ownsSocket: true);
                client.Dispose();
            }
        }

        // SO_PEERCRED (Linux): struct ucred { pid_t pid; uid_t uid; gid_t gid; }. Elsewhere the directory is the guard.
        private static bool PeerIsCurrentUser(Socket client)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return true;
            var credentials = new byte[12];
            try
            {
                if (client.GetRawSocketOption(1 /* SOL_SOCKET */, 17 /* SO_PEERCRED */, credentials) < 12) return false;
            }
            catch (SocketException) { return false; }
            return BitConverter.ToUInt32(credentials, 4) == geteuid();
        }

        public void Dispose()
        {
            socket.Dispose();
            try { File.Delete(Path); } catch { }
        }

        /// <summary>A client connection to a socket path (typedownctl and typedownctl mcp on Linux and macOS).</summary>
        public static async Task<Stream> ConnectAsync(string path, CancellationToken cancellationToken)
        {
            if (!Fits(path)) throw new IOException($"The socket path is longer than {MaxPathBytes} bytes: {path}");
            var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await client.ConnectAsync(new UnixDomainSocketEndPoint(path), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(client, ownsSocket: true);
            }
            catch (SocketException e)
            {
                client.Dispose();
                throw new IOException($"Cannot connect to {path}: {e.Message}", e);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        [DllImport("libc")]
        private static extern uint geteuid();
    }
}
#endif
