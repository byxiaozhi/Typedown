using System;
using System.IO;

namespace Caret.ConverterTests
{
    internal static class TestPaths
    {
        // The project folder, found from the test binaries (bin/Debug/net8.0/...) so the tests read and
        // update the committed samples rather than copies.
        public static string ProjectFolder { get; } = FindProjectFolder();

        public static string Samples => Path.Combine(ProjectFolder, "samples");

        private static string FindProjectFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Caret.ConverterTests.csproj"))) return dir.FullName;
            throw new DirectoryNotFoundException("Caret.ConverterTests.csproj not found above " + AppContext.BaseDirectory);
        }

        public static string NewTempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "caret-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
