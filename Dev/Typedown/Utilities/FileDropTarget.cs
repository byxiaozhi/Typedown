using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Typedown.Core.Utilities;
using Typedown.Windows;

namespace Typedown.Utilities
{
    /// <summary>
    /// OLE drop target for files dragged from Explorer. In the unpackaged build the XAML Islands DragEnter
    /// arrives with an empty DataPackageView (no formats), so Explorer drops were rejected. This wraps the
    /// drop target XAML registered on the island window: CF_HDROP drops of Markdown and image files (any number;
    /// other files among them are ignored) are handled here, everything else is forwarded to XAML unchanged (in-app drags keep working).
    /// </summary>
    [ComVisible(true)]
    public sealed class FileDropTarget : IOleDropTarget
    {
        private const uint DROPEFFECT_NONE = 0;
        private const uint DROPEFFECT_COPY = 1;
        private const uint DROPEFFECT_LINK = 4;
        private const short CF_HDROP = 15;

        private readonly MainWindow window;
        private readonly IOleDropTarget inner;
        private List<string> pendingFiles;

        private FileDropTarget(MainWindow window, IOleDropTarget inner)
        {
            this.window = window;
            this.inner = inner;
        }

        /// <summary>Wraps the drop targets of the window and its children. Safe to call more than once.</summary>
        public static void Install(MainWindow window)
        {
            // It runs a moment after the window loads (and again two seconds later). A window closed in between has
            // torn its island down, and the pointer XAML left in OleDropTargetInterface is no longer an object:
            // GetObjectForIUnknown on it is an access violation in the runtime, which no catch stops - opening a
            // window and closing it at once took the whole process down (E2E Q02, 2026-10-01).
            if (window.IsGoingAway || !IsWindow(window.Handle))
            {
                Log.Debug("FileDropTarget: the window is closing, nothing to install");
                return;
            }
            try
            {
                OleInitialize(IntPtr.Zero);
                var handles = new List<IntPtr> { window.Handle };
                EnumChildWindows(window.Handle, (h, _) => { handles.Add(h); return true; }, IntPtr.Zero);
                const int DRAGDROP_E_ALREADYREGISTERED = unchecked((int)0x80040101);
                var registered = 0;
                foreach (var hwnd in handles)
                {
                    if (GetPropW(hwnd, "Typedown.FileDropTarget") != IntPtr.Zero) continue;
                    var className = new StringBuilder(256);
                    GetClassNameW(hwnd, className, className.Capacity);
                    var existing = GetPropW(hwnd, "OleDropTargetInterface");
                    IOleDropTarget original = null;
                    if (existing != IntPtr.Zero)
                    {
                        // Wrap XAML's own target so in-app drags keep working.
                        original = (IOleDropTarget)Marshal.GetObjectForIUnknown(existing);
                        RevokeDragDrop(hwnd);
                    }
                    var target = new FileDropTarget(window, original);
                    var hr = RegisterDragDrop(hwnd, target);
                    Log.Debug($"FileDropTarget: hwnd=0x{hwnd.ToInt64():X} class='{className}' existing={(existing != IntPtr.Zero)} register=0x{hr:X}");
                    if (hr == 0)
                    {
                        SetPropW(hwnd, "Typedown.FileDropTarget", (IntPtr)1);
                        GC.KeepAlive(target);
                        registered++;
                    }
                    else if (hr == DRAGDROP_E_ALREADYREGISTERED && original == null)
                    {
                        // A target exists but is not exposed through the well-known property; leave it alone.
                        SetPropW(hwnd, "Typedown.FileDropTarget", (IntPtr)2);
                    }
                }
                Log.Debug($"FileDropTarget: registered on {registered} window(s)");
            }
            catch (Exception ex)
            {
                Log.Debug($"FileDropTarget.Install failed: {ex}");
            }
        }

        public int DragEnter(System.Runtime.InteropServices.ComTypes.IDataObject dataObject, uint keyState, POINTL point, ref uint effect)
        {
            pendingFiles = GetSupportedFiles(dataObject);
            if (pendingFiles != null)
            {
                effect = Effect(pendingFiles);
                return 0;
            }
            return inner != null ? inner.DragEnter(dataObject, keyState, point, ref effect) : SetNone(ref effect);
        }

        public int DragOver(uint keyState, POINTL point, ref uint effect)
        {
            if (pendingFiles != null)
            {
                effect = Effect(pendingFiles);
                return 0;
            }
            return inner != null ? inner.DragOver(keyState, point, ref effect) : SetNone(ref effect);
        }

        public int DragLeave()
        {
            if (pendingFiles != null)
            {
                pendingFiles = null;
                return 0;
            }
            return inner?.DragLeave() ?? 0;
        }

        public int Drop(System.Runtime.InteropServices.ComTypes.IDataObject dataObject, uint keyState, POINTL point, ref uint effect)
        {
            var files = pendingFiles ?? GetSupportedFiles(dataObject);
            pendingFiles = null;
            if (files == null)
                return inner != null ? inner.Drop(dataObject, keyState, point, ref effect) : SetNone(ref effect);
            effect = Effect(files);
            Log.Debug($"FileDropTarget: drop {files.Count} file(s): {string.Join(", ", files)}");
            _ = window.Dispatcher.RunAsync(async () =>
            {
                // async void on the UI thread: nothing above it would catch.
                try
                {
                    var app = window.AppViewModel;
                    if (app == null) return;
                    // The pictures into the document the drop was on first; then the Markdown files open.
                    await app.EditorViewModel.InsertLocalImagesAsync(files.Where(FileTypeHelper.IsImageFile).ToList());
                    foreach (var file in files.Where(FileTypeHelper.IsMarkdownFile))
                        app.FileViewModel.OpenFileCommand.Execute(file);
                }
                catch (Exception ex)
                {
                    Log.Debug($"FileDropTarget: handling the drop failed: {ex}");
                }
            });
            return 0;
        }

        private static int SetNone(ref uint effect)
        {
            effect = DROPEFFECT_NONE;
            return 0;
        }

        private static uint Effect(List<string> files) => files.Any(FileTypeHelper.IsImageFile) ? DROPEFFECT_COPY : DROPEFFECT_LINK;

        /// <summary>The dropped Markdown and image files, in the drop's order; null when there is none.</summary>
        private static List<string> GetSupportedFiles(System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
        {
            try
            {
                var files = GetDroppedFiles(dataObject).Where(f => FileTypeHelper.IsMarkdownFile(f) || FileTypeHelper.IsImageFile(f)).ToList();
                if (files.Count > 0)
                    return files;
            }
            catch (Exception ex)
            {
                Log.Debug($"FileDropTarget: reading CF_HDROP failed: {ex.Message}");
            }
            return null;
        }

        private static List<string> GetDroppedFiles(System.Runtime.InteropServices.ComTypes.IDataObject dataObject)
        {
            var result = new List<string>();
            if (dataObject == null) return result;
            var format = new FORMATETC { cfFormat = CF_HDROP, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
            if (dataObject.QueryGetData(ref format) != 0) return result;
            dataObject.GetData(ref format, out var medium);
            try
            {
                var hDrop = medium.unionmember;
                if (hDrop == IntPtr.Zero) return result;
                var count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    var length = DragQueryFileW(hDrop, i, null, 0);
                    var buffer = new StringBuilder((int)length + 1);
                    DragQueryFileW(hDrop, i, buffer, (uint)buffer.Capacity);
                    result.Add(buffer.ToString());
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
            return result;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr GetPropW(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern bool SetPropW(IntPtr hWnd, string lpString, IntPtr hData);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int OleInitialize(IntPtr pvReserved);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int RegisterDragDrop(IntPtr hwnd, IOleDropTarget pDropTarget);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern int RevokeDragDrop(IntPtr hwnd);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL
    {
        public int x;
        public int y;
    }

    [ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleDropTarget
    {
        [PreserveSig] int DragEnter([In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject pDataObj, [In] uint grfKeyState, [In] POINTL pt, [In, Out] ref uint pdwEffect);
        [PreserveSig] int DragOver([In] uint grfKeyState, [In] POINTL pt, [In, Out] ref uint pdwEffect);
        [PreserveSig] int DragLeave();
        [PreserveSig] int Drop([In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject pDataObj, [In] uint grfKeyState, [In] POINTL pt, [In, Out] ref uint pdwEffect);
    }
}
