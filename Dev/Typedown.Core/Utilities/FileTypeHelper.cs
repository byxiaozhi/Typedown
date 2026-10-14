using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Typedown.Core.Utilities
{
    public static class FileTypeHelper
    {
        public static HashSet<string> Markdown { get; } = new() { ".md", ".markdown", ".mdown", ".mkdn", ".mkd", ".mdwn", ".txt", ".mdtxt", ".text", ".mdtext", ".rmd" };

        public static HashSet<string> Image = new() { ".jpeg", ".jpg", ".png", ".gif", ".svg", ".webp", ".jfif" };

        // Plain-text files the editor can open and edit as Markdown text. Shown in the file tree alongside
        // Markdown so a folder of notes with the odd .txt or .log is not half-hidden (a Store review).
        public static HashSet<string> PlainText { get; } = new() { ".txt", ".text", ".log", ".markdown" };

        public enum FileType
        {
            Unknown,
            Markdown,
            Image,
            PlainText
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
                if (PlainText.Contains(ex))
                    return FileType.PlainText;
            }
            return FileType.Unknown;
        }

        public static bool IsMarkdownFile(string fileName)
        {
            return GetFileType(fileName) == FileType.Markdown;
        }

        /// <summary>Markdown or a plain-text file the editor can open — what the file tree shows.</summary>
        public static bool IsEditableTextFile(string fileName)
        {
            var type = GetFileType(fileName);
            return type == FileType.Markdown || type == FileType.PlainText;
        }

        public static bool IsImageFile(string fileName)
        {
            return GetFileType(fileName) == FileType.Image;
        }
    }
}
