using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Typedown.WinUI.Utilities
{
    // Ported verbatim from Typedown.Core\Utilities\CommandLine.cs — no UWP dependencies.
    public static class CommandLine
    {
        public static string GetOpenFilePath(string[] commandLineArgs)
        {
            // A launch from the Explorer menu names files to convert, not a note to open.
            if (commandLineArgs?.Any(a => a is ConvertArgument or ConvertListArgument) == true) return null;
            return commandLineArgs?.Where(FileTypeHelper.IsMarkdownFile).FirstOrDefault();
        }

        // Caret --convert "a.docx" "b.pdf" "C:\Some folder": what File Explorer's "Convert to Markdown" starts
        // (Services/ExplorerCommandServer.cs). --convert-list "list.txt" names a text file with one path per
        // line, used when there are too many for a command line; it is deleted once read. Null when the
        // arguments ask for no conversion.
        public const string ConvertArgument = "--convert";
        public const string ConvertListArgument = "--convert-list";

        public static System.Collections.Generic.List<string> GetConvertPaths(string[] commandLineArgs)
        {
            if (commandLineArgs == null) return null;
            var start = Array.FindIndex(commandLineArgs, a => a is ConvertArgument or ConvertListArgument);
            if (start < 0) return null;
            var paths = new System.Collections.Generic.List<string>();
            if (commandLineArgs[start] == ConvertListArgument)
            {
                var list = start + 1 < commandLineArgs.Length ? commandLineArgs[start + 1] : null;
                try
                {
                    if (list != null && System.IO.File.Exists(list))
                    {
                        paths.AddRange(System.IO.File.ReadAllLines(list).Where(l => l.Length > 0));
                        System.IO.File.Delete(list);
                    }
                }
                catch { }
            }
            else
            {
                paths.AddRange(commandLineArgs.Skip(start + 1).TakeWhile(a => !a.StartsWith("--")));
            }
            return paths.Count > 0 ? paths : null;
        }

        // Splits a raw command-line string (as handed to us by
        // Microsoft.Windows.AppLifecycle.ILaunchActivatedEventArgs.Arguments on a redirected
        // activation — see Program.cs) into argv the same way the OS itself would, including quoted
        // paths with spaces. .NET has no public API for parsing an arbitrary command-line string
        // (Environment.GetCommandLineArgs() only parses this process's own), so this goes straight to
        // the same shell32 function the OS uses to build argv for a normal process launch.
        public static string[] Split(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) return Array.Empty<string>();
            var argv = CommandLineToArgvW(commandLine, out var argc);
            if (argv == IntPtr.Zero) return Array.Empty<string>();
            try
            {
                var result = new string[argc];
                for (var i = 0; i < argc; i++)
                    result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
                return result;
            }
            finally
            {
                // CommandLineToArgvW's return value is LocalAlloc'd, not HGlobalAlloc'd — LocalFree is
                // the correct release, not Marshal.FreeHGlobal (they aren't interchangeable, even
                // though both happen to be thin wrappers over the same process heap in practice).
                LocalFree(argv);
            }
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string commandLine, out int argc);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);
    }
}
