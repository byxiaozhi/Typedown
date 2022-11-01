using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Utilities
{
    public static class FileExtension
    {
        public static HashSet<string> Markdown { get; } = new() { ".md", ".markdown", ".mdown", ".mkdn", ".mkd", ".mdwn", ".txt", ".mdtxt", ".text", ".mdtext", ".rmd" };

        public static HashSet<string> Image = new() { ".jpeg", ".jpg", ".png", ".gif", ".svg", ".webp", ".jfif" };
    }
}
