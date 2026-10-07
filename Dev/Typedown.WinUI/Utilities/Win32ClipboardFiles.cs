using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Typedown.WinUI.Utilities
{
    // Reads a copied file list straight from the Win32 clipboard (CF_HDROP + "Preferred DropEffect").
    // Needed because DataPackageView.GetStorageItemsAsync fails with DV_E_FORMATETC (0x80040064) in this
    // unpackaged app for files copied in File Explorer, even though Contains(StorageItems) reports true —
    // it only works for DataPackages Caret created itself. Used as the fallback when that call throws.
    internal static class Win32ClipboardFiles
    {
        private const uint CF_HDROP = 15;
        private const int DROPEFFECT_MOVE = 2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);

        [DllImport("ole32.dll")]
        private static extern int OleGetClipboard(out System.Runtime.InteropServices.ComTypes.IDataObject dataObject);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM medium);

        // File Explorer's own Copy puts an OLE data object with async rendering on the clipboard; its
        // CF_HDROP isn't reachable through raw GetClipboardData from this process (NULL, no error) but
        // is through OLE, the way WinForms' Clipboard.GetFileDropList reads it. Tried first.
        private static bool TryGetViaOle(out string[] paths, out bool isMove)
        {
            paths = Array.Empty<string>();
            isMove = false;
            if (OleGetClipboard(out var dataObject) != 0 || dataObject == null) return false;
            var format = new System.Runtime.InteropServices.ComTypes.FORMATETC
            {
                cfFormat = (short)CF_HDROP,
                dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL,
            };
            System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
            try { dataObject.GetData(ref format, out medium); }
            catch (COMException) { return false; }
            try
            {
                paths = ReadDropFiles(medium.unionmember);
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
            // Cut in Explorer marks the same data object with a move effect
            isMove = (ReadDropEffect(dataObject) & DROPEFFECT_MOVE) != 0;
            return paths.Length > 0;
        }

        private static int ReadDropEffect(System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
        {
            var format = new System.Runtime.InteropServices.ComTypes.FORMATETC
            {
                cfFormat = unchecked((short)RegisterClipboardFormat("Preferred DropEffect")),
                dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL,
            };
            System.Runtime.InteropServices.ComTypes.STGMEDIUM medium;
            try { dataObject.GetData(ref format, out medium); }
            catch (COMException) { return 0; } // no effect offered: a copy
            try
            {
                var pointer = GlobalLock(medium.unionmember);
                if (pointer == IntPtr.Zero) return 0;
                try { return Marshal.ReadInt32(pointer); }
                finally { GlobalUnlock(medium.unionmember); }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }

        private static string[] ReadDropFiles(IntPtr hDrop)
        {
            var count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            var result = new string[count];
            for (uint i = 0; i < count; i++)
            {
                var length = DragQueryFile(hDrop, i, null, 0);
                var buffer = new StringBuilder((int)length + 1);
                DragQueryFile(hDrop, i, buffer, length + 1);
                result[i] = buffer.ToString();
            }
            return result;
        }

        public static bool TryGet(IntPtr owner, out string[] paths, out bool isMove, out string failure)
        {
            isMove = false;
            failure = null;
            if (TryGetViaOle(out paths, out isMove)) return true;
            return TryGetViaWin32(owner, out paths, out isMove, out failure);
        }

        private static bool TryGetViaWin32(IntPtr owner, out string[] paths, out bool isMove, out string failure)
        {
            paths = Array.Empty<string>();
            isMove = false;
            failure = null;
            // Another process can hold the clipboard open for a moment; retry briefly.
            for (var attempt = 0; !OpenClipboard(owner); attempt++)
            {
                if (attempt >= 5)
                {
                    failure = $"OpenClipboard failed ({Marshal.GetLastWin32Error()})";
                    return false;
                }
                Thread.Sleep(20);
            }
            try
            {
                var hDrop = GetClipboardData(CF_HDROP);
                if (hDrop == IntPtr.Zero)
                {
                    failure = $"no CF_HDROP ({Marshal.GetLastWin32Error()})";
                    return false;
                }
                paths = ReadDropFiles(hDrop);
                var effect = GetClipboardData(RegisterClipboardFormat("Preferred DropEffect"));
                if (effect != IntPtr.Zero)
                {
                    var pointer = GlobalLock(effect);
                    if (pointer != IntPtr.Zero)
                    {
                        isMove = (Marshal.ReadInt32(pointer) & DROPEFFECT_MOVE) != 0;
                        GlobalUnlock(effect);
                    }
                }
                return paths.Length > 0;
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
