using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Automation;

namespace Typedown.Services.Automation
{
    /// <summary>
    /// The automation endpoint on Windows (docs/automation-api-spec.md, section 5.1): a named pipe whose ACL names only
    /// the current user's SID (not the default ACL), that refuses remote clients, and whose first instance is created
    /// with FILE_FLAG_FIRST_PIPE_INSTANCE, so a process that took the name first is not served as if it were us - the
    /// listener fails instead and the service reports it.
    /// </summary>
    internal sealed class SecurePipeListener : IConnectionListener
    {
        private const uint PIPE_ACCESS_DUPLEX = 0x3, FILE_FLAG_OVERLAPPED = 0x40000000, FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000;
        private const uint PIPE_TYPE_BYTE = 0x0, PIPE_READMODE_BYTE = 0x0, PIPE_WAIT = 0x0, PIPE_REJECT_REMOTE_CLIENTS = 0x8;
        private const uint PIPE_UNLIMITED_INSTANCES = 255;

        private readonly string name;
        private readonly string sid;
        private bool first = true;
        private volatile NamedPipeServerStream waiting;
        private volatile bool disposed;

        public SecurePipeListener(string name, string sid)
        {
            this.name = name;
            this.sid = sid;
        }

        public async Task<Stream> AcceptAsync(CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SecurePipeListener));
            var server = new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, CreatePipe(first));
            first = false;
            waiting = server;
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                return server;
            }
            catch
            {
                server.Dispose();
                throw;
            }
            finally
            {
                waiting = null;
            }
        }

        private SafePipeHandle CreatePipe(bool firstInstance)
        {
            // Protected DACL: generic all for the current user and nobody else.
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW($"D:P(A;;GA;;;{sid})", 1, out var descriptor, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var attributes = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), lpSecurityDescriptor = descriptor, bInheritHandle = 0 };
                var handle = CreateNamedPipeW(@"\\.\pipe\" + name,
                    PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | (firstInstance ? FILE_FLAG_FIRST_PIPE_INSTANCE : 0),
                    PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                    PIPE_UNLIMITED_INSTANCES, 65536, 65536, 0, ref attributes);
                if (handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    // ERROR_ACCESS_DENIED on the first instance: someone else already holds this name.
                    throw error == 5 && firstInstance
                        ? new UnauthorizedAccessException($"Another process already owns the automation pipe '{name}'.")
                        : new Win32Exception(error);
                }
                return handle;
            }
            finally
            {
                LocalFree(descriptor);
            }
        }

        /// <summary>The current user's SID as a string (S-1-5-21-...), read from the process token.</summary>
        public static string CurrentUserSid()
        {
            if (!OpenProcessToken(GetCurrentProcess(), 0x0008 /* TOKEN_QUERY */, out var token))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                GetTokenInformation(token, 1 /* TokenUser */, IntPtr.Zero, 0, out var length);
                var buffer = Marshal.AllocHGlobal(length);
                try
                {
                    if (!GetTokenInformation(token, 1, buffer, length, out _))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var sid = Marshal.ReadIntPtr(buffer); // TOKEN_USER.User.Sid
                    if (!ConvertSidToStringSidW(sid, out var text))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    try { return Marshal.PtrToStringUni(text); }
                    finally { LocalFree(text); }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr stringSid);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        public void Dispose()
        {
            disposed = true;
            try { waiting?.Dispose(); } catch { }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string stringSecurityDescriptor, uint revision, out IntPtr securityDescriptor, IntPtr securityDescriptorSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafePipeHandle CreateNamedPipeW(string name, uint openMode, uint pipeMode, uint maxInstances, uint outBufferSize, uint inBufferSize, uint defaultTimeout, ref SECURITY_ATTRIBUTES securityAttributes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
