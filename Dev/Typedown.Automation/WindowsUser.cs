using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Typedown.Automation
{
    /// <summary>
    /// The current Windows user's SID string, which names the automation pipe. Asked of the process token directly:
    /// WindowsIdentity is not in netstandard2.0 or .NET Core 3.1 without another package, and typedownctl ships on the
    /// .NET Core 3.1 runtime the app carries (the app's SecurePipeListener does the same).
    /// </summary>
    public static class WindowsUser
    {
        public static string CurrentSid()
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
                    try { return Marshal.PtrToStringUni(text) ?? ""; }
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
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
