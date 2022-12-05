using System.Collections.Specialized;
using System.IO;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;

namespace Typedown.Services
{
    internal class FileOperation: IFileOperation
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

        public void CutToClipboard(StringCollection files)
        {
            byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
            MemoryStream dropEffect = new();
            dropEffect.Write(moveEffect, 0, moveEffect.Length);
            System.Windows.DataObject data = new();
            data.SetFileDropList(files);
            data.SetData("Preferred DropEffect", dropEffect);
            System.Windows.Clipboard.Clear();
            System.Windows.Clipboard.SetDataObject(data, true);
        }

        public void CopyToClipboard(StringCollection files)
        {
            byte[] moveEffect = new byte[] { 5, 0, 0, 0 };
            MemoryStream dropEffect = new();
            dropEffect.Write(moveEffect, 0, moveEffect.Length);
            System.Windows.DataObject data = new();
            data.SetFileDropList(files);
            data.SetData("Preferred DropEffect", dropEffect);
            System.Windows.Clipboard.Clear();
            System.Windows.Clipboard.SetDataObject(data, true);
        }

        public bool IsPasteEnabled
        {
            get => System.Windows.Clipboard.ContainsData("Preferred DropEffect") && System.Windows.Clipboard.ContainsFileDropList();
        }

        public void PasteFromClipboard(string to)
        {
            if (IsPasteEnabled)
            {
                var files = System.Windows.Clipboard.GetFileDropList();
                var stream = (Stream)System.Windows.Clipboard.GetData("Preferred DropEffect");
                var effects = (System.Windows.DragDropEffects)stream.ReadByte();
                if (effects.HasFlag(System.Windows.DragDropEffects.Move))
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
