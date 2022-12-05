using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Typedown.Core.Utilities
{
    public static class FileTypeHelper
    {
        public static HashSet<string> Markdown { get; } = new() { ".md", ".markdown", ".mdown", ".mkdn", ".mkd", ".mdwn", ".txt", ".mdtxt", ".text", ".mdtext", ".rmd" };

        public static HashSet<string> Image = new() { ".jpeg", ".jpg", ".png", ".gif", ".svg", ".webp", ".jfif" };

        public enum FileType
        {
            Unknown,
            Markdown,
            Image
        }

        public static FileType GetFileType(string fileName)
        {
            if(!string.IsNullOrEmpty(fileName) && fileName.Contains('.'))
            {
                var ex = Path.GetExtension(fileName).ToLower();
                if (Markdown.Contains(ex))
                    return FileType.Markdown;
                if (Image.Contains(ex))
                    return FileType.Image;
            }
            return FileType.Unknown;
        }

        public static bool IsMarkdownFile(string fileName)
        {
            return GetFileType(fileName) == FileType.Markdown;
        }

        public static bool IsImageFile(string fileName)
        {
            return GetFileType(fileName) == FileType.Image;
        }
    }
}
