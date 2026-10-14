using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

        /// <summary>
        /// Shows a picker, and when Windows cannot show it (E_FAIL from a second picker asked for while one is still
        /// opening, for instance - a double click on Open) says nothing was picked, with a line in the log. The
        /// exception went up an async void handler and ended the process with every window in it: twice in a row on a
        /// test machine, from File > Open.
        /// </summary>
        public static async Task<T> ShowAsync<T>(Func<Windows.Foundation.IAsyncOperation<T>> pick) where T : class
        {
            try
            {
                return await pick();
            }
            catch (Exception ex)
            {
                Log.Debug($"picker: Windows could not show it: {ex.GetType().Name} {ex.HResult:x8} {ex.Message}");
                return null;
            }
        }

        public static async Task<string> PickMarkdownFolderAsync(this nint window)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SetOwnerWindow(window);
            folderPicker.FileTypeFilter.Add("*");
            var folder = await ShowAsync(() => folderPicker.PickSingleFolderAsync());
            return folder?.Path;
        }

        public static async Task<string> PickMarkdownFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            FileTypeHelper.Markdown.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(window);
            var file = await ShowAsync(() => filePicker.PickSingleFileAsync());
            return file?.Path;
        }

        public static async Task<string> PickImageFileAsync(this nint window)
        {
            var filePicker = new FileOpenPicker();
            FileTypeHelper.Image.ToList().ForEach(filePicker.FileTypeFilter.Add);
            filePicker.SetOwnerWindow(window);
            var file = await ShowAsync(() => filePicker.PickSingleFileAsync());
            return file?.Path;
        }
    }
}
