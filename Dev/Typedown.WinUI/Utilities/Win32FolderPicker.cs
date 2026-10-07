using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.WinUI.Utilities
{
    // Windows.Storage.Pickers.FolderPicker throws COMException 0x80004005 (E_FAIL) reliably in this
    // unpackaged app — confirmed reproducible via direct testing, not a one-off. FileOpenPicker/
    // FileSavePicker (used elsewhere in MainWindow.xaml.cs) don't hit it; the difference is
    // StorageFolder vs. StorageFile, and StorageFolder marshalling back to an unpackaged process is a
    // documented WinRT-picker limitation that a picker property (SuggestedStartLocation, etc.) doesn't
    // fix. This talks to the exact same native Win32 folder-browse dialog through the plain
    // IFileOpenDialog COM interface instead, bypassing the WinRT broker layer that's failing.
    // Interop shape follows the well-known community pattern (Simon Mourier's "How do I use
    // OpenFileDialog to select a folder?") — every vtable member must stay in this exact order even
    // though most are never called, because COM interop dispatches by vtable slot index, not by name.
    public static class Win32FolderPicker
    {
        // dialog.Show(owner) is a blocking, non-awaitable COM call. Calling it straight from the UI
        // thread stalls WinUI 3's DispatcherQueue for as long as the dialog is open — which also stalls
        // WebView2's own message pump, since it shares the process. Running it on a dedicated STA
        // thread (the standard pattern for blocking COM dialogs in an otherwise-async app) keeps the
        // UI thread free and lets the caller await it like any other picker in this codebase.
        public static Task<string> PickFolderAsync(IntPtr owner)
        {
            var tcs = new TaskCompletionSource<string>();
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(PickFolder(owner)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private static string PickFolder(IntPtr owner)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                dialog.GetOptions(out var options);
                dialog.SetOptions(options | FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);
                var hr = dialog.Show(owner);
                if (hr != 0) return null; // cancelled (typically 0x800704C7) or failed
                dialog.GetResult(out var item);
                item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }

        [Flags]
        private enum FOS
        {
            FOS_PICKFOLDERS = 0x20,
            FOS_FORCEFILESYSTEM = 0x40,
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000,
        }

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW { }

        [ComImport, Guid("d57c7288-d4ad-4768-be02-9d969532d960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            [PreserveSig] int SetFileTypes();
            [PreserveSig] int SetFileTypeIndex(int iFileType);
            [PreserveSig] int GetFileTypeIndex(out int piFileType);
            [PreserveSig] int Advise();
            [PreserveSig] int Unadvise();
            [PreserveSig] int SetOptions(FOS fos);
            [PreserveSig] int GetOptions(out FOS pfos);
            [PreserveSig] int SetDefaultFolder(IShellItem psi);
            [PreserveSig] int SetFolder(IShellItem psi);
            [PreserveSig] int GetFolder(out IShellItem ppsi);
            [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
            [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            [PreserveSig] int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            [PreserveSig] int GetResult(out IShellItem ppsi);
            [PreserveSig] int AddPlace(IShellItem psi, int alignment);
            [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            [PreserveSig] int Close(int hr);
            [PreserveSig] int SetClientGuid();
            [PreserveSig] int ClearClientData();
            [PreserveSig] int SetFilter([MarshalAs(UnmanagedType.IUnknown)] object pFilter);
            [PreserveSig] int GetResults([MarshalAs(UnmanagedType.IUnknown)] out object ppenum);
            [PreserveSig] int GetSelectedItems([MarshalAs(UnmanagedType.IUnknown)] out object ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler();
            [PreserveSig] int GetParent();
            [PreserveSig] int GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            [PreserveSig] int GetAttributes();
            [PreserveSig] int Compare();
        }
    }
}
