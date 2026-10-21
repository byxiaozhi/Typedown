using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // New since the fork: what the conversion tray's drop overlay and Compare need to know that has nothing to do with a window.
    public static class ConvertDrop
    {
        // Old binary Office formats: taken in so that the row can say why it cannot be converted (save as .docx / .xlsx / .pptx).
        public static readonly string[] LegacyOffice = { ".doc", ".xls", ".ppt", ".dot", ".xlt", ".pot" };

        public static readonly string[] Email = { ".msg", ".eml" };

        // A drag over the window is a conversion when it holds a folder or a file the Convert page would take (so that dropping
        // a note, a picture or a zip anywhere shows no "Convert to Markdown").
        public static bool Accepts(IEnumerable<(string Path, bool IsFolder)> items) =>
            items.Any(i => i.IsFolder || IsConvertible(i.Path));

        public static bool IsConvertible(string path) =>
            DocumentConverter.IsSupported(path) || LegacyOffice.Contains(Path.GetExtension(path ?? "").ToLowerInvariant());

        public static bool IsEmail(string path) => Email.Contains(Path.GetExtension(path ?? "").ToLowerInvariant());
    }

    // The placeholders a masked email has in the place of names, addresses, numbers and IDs ("[PERSON-2]", "[EMAIL-1]"): found
    // in the Markdown so that Compare can mark them and say how many of each there are.
    public static class RedactionMarks
    {
        // The order the kinds are listed in.
        public static readonly string[] Kinds = { "PERSON", "EMAIL", "PHONE", "ID", "IBAN", "CARD" };

        private static readonly Regex Mark = new(@"\[(?<kind>PERSON|EMAIL|PHONE|IBAN|CARD|ID)-(?<n>\d+)\]", RegexOptions.CultureInvariant);

        public static IReadOnlyList<(int Start, int Length)> Find(string text) =>
            string.IsNullOrEmpty(text) ? Array.Empty<(int, int)>() : Mark.Matches(text).Select(m => (m.Index, m.Length)).ToList();

        // How many different values of each kind were masked ("[PERSON-1]" twice is one person).
        public static IReadOnlyList<(string Kind, int Count)> Count(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<(string, int)>();
            var seen = Mark.Matches(text).Select(m => m.Value).Distinct().ToList();
            return Kinds
                .Select(kind => (Kind: kind, Count: seen.Count(v => v.StartsWith("[" + kind + "-", StringComparison.Ordinal))))
                .Where(k => k.Count > 0)
                .ToList();
        }
    }
}
