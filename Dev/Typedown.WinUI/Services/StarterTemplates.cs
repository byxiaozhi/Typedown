using System;
using System.IO;
using System.Linq;

namespace Typedown.WinUI.Services
{
    // The starter templates: Markdown files that ship with Caret, in each language of the interface (Templates\<language>\*.md next
    // to the program), written to the user's Templates folder, where a template is just a .md file. They are added once (when
    // the Templates list is first opened) and on request ("Add starter templates"), and a file that is already there is never
    // overwritten: a template the user changed, renamed back or deleted is theirs.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal static class StarterTemplates
    {
        // Raised when the set changes in a way that an installed copy should get the new files (a new template, a new language).
        public const int Version = 1;

        // The language's folder, or English when Caret has no templates in it.
        public static string SourceFolder(string root, string language)
        {
            var own = Path.Combine(root, "Templates", language ?? "");
            return !string.IsNullOrEmpty(language) && Directory.Exists(own) ? own : Path.Combine(root, "Templates", "en");
        }

        // The names of the templates in a folder, in order.
        public static string[] Names(string sourceFolder) =>
            Directory.Exists(sourceFolder)
                ? Directory.GetFiles(sourceFolder, "*.md").Select(Path.GetFileName).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray()
                : Array.Empty<string>();

        // Copies the templates that are not in the target folder yet; returns how many were added.
        public static int Install(string sourceFolder, string targetFolder)
        {
            var added = 0;
            Directory.CreateDirectory(targetFolder);
            foreach (var name in Names(sourceFolder))
            {
                var target = Path.Combine(targetFolder, name);
                if (File.Exists(target)) continue;
                File.Copy(Path.Combine(sourceFolder, name), target);
                added++;
            }
            return added;
        }
    }
}