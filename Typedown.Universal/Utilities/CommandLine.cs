using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Universal.Utilities
{
    public static class CommandLine
    {
        public static string GetOpenFilePath(string[] commandLineArgs)
        {
            return commandLineArgs?.Where(x => FileExtension.Markdown.Where(x.EndsWith).Any()).FirstOrDefault();
        }
    }
}
