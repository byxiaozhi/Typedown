using System;
using System.Collections.Generic;
using System.Linq;

namespace Typedown.WinUI.Services
{
    // New since the fork: the keyboard shortcuts of Caret as one list, grouped by task, for the cheat sheet (Ctrl+Shift+/, Help > Keyboard
    // shortcuts). Plain .NET: a name is a string of the interface (a resource key), so the sheet speaks the language of the menus. The tests
    // check that every shortcut the menus show is in this list (ShortcutCatalogTests), so a new one cannot be forgotten.
    public sealed record Shortcut(string Group, string NameKey, string Keys, string[] Also = null)
    {
        // Every key combination this row stands for (the display text may fold several: "F3 / Shift+F3"; `Also` adds the ones it leaves out).
        public IEnumerable<string> Gestures => Keys.Split(" / ").Concat(Also ?? Array.Empty<string>());
    }

    public static class ShortcutCatalog
    {
        // The order of the groups on the sheet. A group with no shortcut is not shown.
        public static readonly string[] Groups = { "Format", "Review", "Speech", "Find", "Files", "General" };

        public static string GroupNameKey(string group) => "ShortcutGroup" + group;

        public static readonly Shortcut[] All =
        {
            new("Format", "Bold", "Ctrl+B"),
            new("Format", "Italic", "Ctrl+I"),
            new("Format", "Underline", "Ctrl+U"),
            new("Format", "ShortcutHeadings", "Ctrl+1 … Ctrl+6", new[] { "Ctrl+1", "Ctrl+2", "Ctrl+3", "Ctrl+4", "Ctrl+5", "Ctrl+6" }),
            new("Format", "Paragraph", "Ctrl+0"),
            new("Format", "TaskList", "Ctrl+Shift+X"),
            new("Format", "Quote", "Ctrl+Shift+Q"),
            new("Format", "CodeBlock", "Ctrl+Shift+K"),
            new("Format", "TableMenu", "Ctrl+Shift+T"),
            new("Format", "ShortcutMiniToolbar", "Alt+Down"),

            new("Speech", "ShortcutSpeechPause", "Ctrl+Shift+."),
            new("Speech", "ShortcutSpeechBeat", "Ctrl+Shift+,"),
            new("Speech", "ShortcutSpeechEmphasis", "Ctrl+Shift+E"),

            new("Find", "FindReplace", "Ctrl+F"),
            new("Find", "Replace", "Ctrl+H"),
            new("Find", "ShortcutFindNext", "F3 / Shift+F3"),

            new("Files", "New", "Ctrl+N"),
            new("Files", "NewWindow", "Ctrl+Shift+N"),
            new("Files", "Open", "Ctrl+O"),
            new("Files", "GoToFile", "Ctrl+K"),
            new("Files", "Save", "Ctrl+S"),
            new("Files", "SaveAs", "Ctrl+Shift+S"),
            new("Files", "Print", "Ctrl+P"),
            new("Files", "ShortcutNewTab", "Ctrl+T"),
            new("Files", "NextPreviousTab", "Ctrl+Tab / Ctrl+Shift+Tab", new[] { "Ctrl+PageDown", "Ctrl+PageUp" }),
            new("Files", "CloseDocumentMenuItem", "Ctrl+W"),
            new("Files", "CloseWindow", "Ctrl+Shift+W"),

            new("General", "UndoMenuItem_Text", "Ctrl+Z"),
            new("General", "RedoMenuItem_Text", "Ctrl+Y"),
            new("General", "Cut", "Ctrl+X"),
            new("General", "CopyMenuItem_Text", "Ctrl+C"),
            new("General", "PasteMenuItem_Text", "Ctrl+V"),
            new("General", "SelectAllMenuItem_Text", "Ctrl+A"),
            new("General", "ShortcutCommandPalette", "Ctrl+Shift+P"),
            new("General", "InspectorMenuItem_Text", "Ctrl+Shift+I"),
            new("General", "SettingsDialog_Title", "Ctrl+,"),
            new("General", "ViewModeMenu", "Ctrl+/"),
            new("General", "MoveBetweenAreas", "F6 / Shift+F6"),
            new("General", "LayoutDistraction", "F11"),
            new("General", "ShortcutKeyTips", "Alt"),
            new("General", "ShortcutSheet", "Ctrl+Shift+/"),
        };
    }

    // What the cheat sheet shows: the shortcuts of each group under the group's name, narrowed by what is typed (every word of it in the
    // name, the group or the keys, as in the command palette).
    public static class ShortcutSheet
    {
        public sealed record Row(string Text, string Keys, bool IsHeader);

        public static List<Row> Build(Func<string, string> name, string query)
        {
            var rows = new List<Row>();
            foreach (var group in ShortcutCatalog.Groups)
            {
                var groupName = name(ShortcutCatalog.GroupNameKey(group));
                var matches = ShortcutCatalog.All.Where(s => s.Group == group)
                    .Select(s => (Shortcut: s, Name: name(s.NameKey)))
                    .Where(x => string.IsNullOrWhiteSpace(query) || PaletteSearch.Score(query, x.Name, groupName + " " + x.Shortcut.Keys) >= 0)
                    .ToList();
                if (matches.Count == 0) continue;
                rows.Add(new Row(groupName, "", true));
                rows.AddRange(matches.Select(x => new Row(x.Name, x.Shortcut.Keys, false)));
            }
            return rows;
        }
    }
}
