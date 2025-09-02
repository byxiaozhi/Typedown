using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;

namespace Typedown.Services
{
    internal class FileOperation : IFileOperation
    {
        public bool Delete(StringCollection files)
        {
            var pFrom = "";
            foreach (var file in files)
            {
                pFrom += file + "\0";
            }
            var shf = new PInvoke.SHFILEOPSTRUCT
            {
                wFunc = PInvoke.FileFuncFlags.FO_DELETE,
                fFlags = PInvoke.FILEOP_FLAGS.FOF_ALLOWUNDO,
                pFrom = pFrom
            };
            return PInvoke.SHFileOperation(ref shf) == 0;
        }

        public bool Copy(StringCollection files, string to)
        {
            var pFrom = "";
            foreach (var file in files)
            {
                pFrom += file + "\0";
            }
            var shf = new PInvoke.SHFILEOPSTRUCT
            {
                wFunc = PInvoke.FileFuncFlags.FO_COPY,
                fFlags = PInvoke.FILEOP_FLAGS.FOF_ALLOWUNDO,
                pFrom = pFrom,
                pTo = to + "\0"
            };
            return PInvoke.SHFileOperation(ref shf) == 0;
        }

        public bool Move(StringCollection files, string to)
        {
            var pFrom = "";
            foreach (var file in files)
            {
                pFrom += file + "\0";
            }
            var shf = new PInvoke.SHFILEOPSTRUCT
            {
                wFunc = PInvoke.FileFuncFlags.FO_MOVE,
                fFlags = PInvoke.FILEOP_FLAGS.FOF_ALLOWUNDO,
                pFrom = pFrom,
                pTo = to + "\0"
            };
            return PInvoke.SHFileOperation(ref shf) == 0;
        }

        public bool Rename(string from, string to)
        {
            var shf = new PInvoke.SHFILEOPSTRUCT
            {
                wFunc = PInvoke.FileFuncFlags.FO_RENAME,
                fFlags = PInvoke.FILEOP_FLAGS.FOF_ALLOWUNDO,
                pFrom = from + "\0",
                pTo = to + "\0"
            };
            return PInvoke.SHFileOperation(ref shf) == 0;
        }

        public async Task CutToClipboardAsync(StringCollection files)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            var storageItems = new List<global::Windows.Storage.IStorageItem>();

            foreach (string filePath in files)
            {
                var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                storageItems.Add(file);
            }

            dataPackage.SetStorageItems(storageItems);
            dataPackage.RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }

        public async Task CopyToClipboardAsync(StringCollection files)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            var storageItems = new List<global::Windows.Storage.IStorageItem>();

            foreach (string filePath in files)
            {
                var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                storageItems.Add(file);
            }

            dataPackage.SetStorageItems(storageItems);
            dataPackage.RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }

        public bool IsPasteEnabled
        {
            get
            {
                var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                return view.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems);
            }
        }

        public void PasteFromClipboard(string to)
        {
            if (IsPasteEnabled)
            {
                var view = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                var storageItems = view.GetStorageItemsAsync().GetResults();
                var files = new StringCollection();

                foreach (var item in storageItems)
                {
                    files.Add(item.Path);
                }

                if (view.RequestedOperation.HasFlag(global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move))
                {
                    Move(files, to);
                }
                else
                {
                    Copy(files, to);
                }
            }
        }

        public bool IsFilenameValid(string sourceFolder, string fileName)
        {
            var path = Path.Combine(sourceFolder, fileName);
            return !string.IsNullOrEmpty(fileName) &&
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                !File.Exists(path) && !Directory.Exists(path);
        }
    }
}