using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Typedown.Core.ViewModels;
using Windows.Storage.Pickers;

namespace Typedown.Core.Utilities
{
    public static class FilePickersExtensions
    {
        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }

        public static void SetOwnerWindow(this FileOpenPicker picker, nint hWnd)
        {
            (picker as object as IInitializeWithWindow).Initialize(hWnd);
        }

        public static void SetOwnerWindow(this FileSavePicker picker, nint hWnd)
        {
            (picker as object as IInitializeWithWindow).Initialize(hWnd);
        }

        public static void SetOwnerWindow(this FolderPicker picker, nint hWnd)
        {
            (picker as object as IInitializeWithWindow).Initialize(hWnd);
        }

        public static async Task<string> PickMarkdownFolderAsync(this nint window)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SetOwnerWindow(window);
            FileTypeHelper.Markdown.ToList().ForEach(folderPicker.FileTypeFilter.Add);
            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }

        public static async Task<string> PickMarkdownFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            FileTypeHelper.Markdown.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(window);
            var file = await filePicker.PickSingleFileAsync();
            return file?.Path;
        }

        public static async Task<string> PickImageFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            FileTypeHelper.Image.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(window);
            var file = await filePicker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
