using System.Runtime.InteropServices;

namespace Typedown.Core.Utilities
{
    public static partial class PInvoke
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OSVERSIONINFOEXW
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("ntdll.dll", ExactSpelling = true)]
        private static extern int RtlGetVersion(ref OSVERSIONINFOEXW versionInfo);

        /// <summary>
        /// Real Windows build number, independent of the application's compatibility manifest
        /// (unlike GetVersionEx-based APIs). Returns 0 if the call fails.
        /// </summary>
        public static int GetWindowsBuildNumber()
        {
            try
            {
                var info = new OSVERSIONINFOEXW { dwOSVersionInfoSize = Marshal.SizeOf<OSVERSIONINFOEXW>() };
                return RtlGetVersion(ref info) == 0 ? info.dwBuildNumber : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
