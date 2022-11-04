using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Typedown.Universal.Utilities
{
    public static partial class PInvoke
    {
        public enum ShellFileOperation
        {
            FO_MOVE = 1,
            FO_COPY,
            FO_DELETE,
            FO_RENAME
        }

        public enum FILEOP_FLAGS : uint
        {
            FOF_MULTIDESTFILES = 0x1u,
            [Obsolete]
            FOF_CONFIRMMOUSE = 0x2u,
            FOF_SILENT = 0x4u,
            FOF_RENAMEONCOLLISION = 0x8u,
            FOF_NOCONFIRMATION = 0x10u,
            FOF_WANTMAPPINGHANDLE = 0x20u,
            FOF_ALLOWUNDO = 0x40u,
            FOF_FILESONLY = 0x80u,
            FOF_SIMPLEPROGRESS = 0x100u,
            FOF_NOCONFIRMMKDIR = 0x200u,
            FOF_NOERRORUI = 0x400u,
            FOF_NOCOPYSECURITYATTRIBS = 0x800u,
            FOF_NORECURSION = 0x1000u,
            FOF_NO_CONNECTED_ELEMENTS = 0x2000u,
            FOF_WANTNUKEWARNING = 0x4000u,
            FOF_NORECURSEREPARSE = 0x8000u,
            FOF_NO_UI = 0x614u,
            FOFX_NOSKIPJUNCTIONS = 0x10000u,
            FOFX_PREFERHARDLINK = 0x20000u,
            FOFX_SHOWELEVATIONPROMPT = 0x40000u,
            FOFX_RECYCLEONDELETE = 0x80000u,
            FOFX_EARLYFAILURE = 0x100000u,
            FOFX_PRESERVEFILEEXTENSIONS = 0x200000u,
            FOFX_KEEPNEWERFILE = 0x400000u,
            FOFX_NOCOPYHOOKS = 0x800000u,
            FOFX_NOMINIMIZEBOX = 0x1000000u,
            FOFX_MOVEACLSACROSSVOLUMES = 0x2000000u,
            FOFX_DONTDISPLAYSOURCEPATH = 0x4000000u,
            FOFX_DONTDISPLAYDESTPATH = 0x8000000u,
            FOFX_REQUIREELEVATION = 0x10000000u,
            FOFX_ADDUNDORECORD = 0x20000000u,
            FOFX_COPYASDOWNLOAD = 0x40000000u,
            FOFX_DONTDISPLAYLOCATIONS = 0x80000000u
        }

        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public ShellFileOperation wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public FILEOP_FLAGS fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", ExactSpelling = true)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
    }
}
