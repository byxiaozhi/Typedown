using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.Core.Utilities
{
    public static class CommandLine
    {
        public static string GetOpenFilePath(string[] commandLineArgs)
        {
            return commandLineArgs?.Where(FileTypeHelper.IsMarkdownFile).FirstOrDefault();
        }
    }
}
