using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Universal.Utilities
{
    public static partial class PInvoke
    {
        public enum FileFuncFlags : uint
        {
            FO_MOVE = 0x1,
            FO_COPY = 0x2,
            FO_DELETE = 0x3,
            FO_RENAME = 0x4
        }

        [Flags]
        public enum FILEOP_FLAGS : ushort
        {
            FOF_MULTIDESTFILES = 0x1,
            FOF_CONFIRMMOUSE = 0x2,
            FOF_SILENT = 0x4,
            FOF_RENAMEONCOLLISION = 0x8,
            FOF_NOCONFIRMATION = 0x10,
            FOF_WANTMAPPINGHANDLE = 0x20,
            FOF_ALLOWUNDO = 0x40,
            FOF_FILESONLY = 0x80,
            FOF_SIMPLEPROGRESS = 0x100,
            FOF_NOCONFIRMMKDIR = 0x200,
            FOF_NOERRORUI = 0x400,
            FOF_NOCOPYSECURITYATTRIBS = 0x800,
            FOF_NORECURSION = 0x1000,
            FOF_NO_CONNECTED_ELEMENTS = 0x2000,
            FOF_WANTNUKEWARNING = 0x4000,
            FOF_NORECURSEREPARSE = 0x8000
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT32
        {
            public IntPtr hwnd;
            public FileFuncFlags wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public FILEOP_FLAGS fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT64
        {
            public IntPtr hwnd;
            public FileFuncFlags wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public FILEOP_FLAGS fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public FileFuncFlags wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public FILEOP_FLAGS fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", EntryPoint = "SHFileOperation", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true, ThrowOnUnmappableChar = true)]
        private static extern int SHFileOperation32(ref SHFILEOPSTRUCT32 lpFileOp);

        [DllImport("shell32.dll", EntryPoint = "SHFileOperation", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true, ThrowOnUnmappableChar = true)]
        private static extern int SHFileOperation64(ref SHFILEOPSTRUCT64 lpFileOp);

        public static int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp)
        {
            if (IntPtr.Size == 8)
            {
                var data = new SHFILEOPSTRUCT64
                {
                    hwnd = lpFileOp.hwnd,
                    wFunc = lpFileOp.wFunc,
                    pFrom = lpFileOp.pFrom,
                    pTo = lpFileOp.pTo,
                    fFlags = lpFileOp.fFlags,
                    fAnyOperationsAborted = lpFileOp.fAnyOperationsAborted,
                    hNameMappings = lpFileOp.hNameMappings,
                    lpszProgressTitle = lpFileOp.lpszProgressTitle,
                };
                return SHFileOperation64(ref data);
            }
            else
            {
                var data = new SHFILEOPSTRUCT32
                {
                    hwnd = lpFileOp.hwnd,
                    wFunc = lpFileOp.wFunc,
                    pFrom = lpFileOp.pFrom,
                    pTo = lpFileOp.pTo,
                    fFlags = lpFileOp.fFlags,
                    fAnyOperationsAborted = lpFileOp.fAnyOperationsAborted,
                    hNameMappings = lpFileOp.hNameMappings,
                    lpszProgressTitle = lpFileOp.lpszProgressTitle,
                };
                return SHFileOperation32(ref data);
            }
        }
    }
}
