using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Typedown.WinUI.Models
{
    // One row of the command palette (Ctrl+Shift+P): a menu command, or a command that has no menu item of its own.
    public class PaletteEntry
    {
        public string Title { get; init; }
        // Where the command lives, like "File › Export" (the top-level menu, and the sub-menus on the way).
        public string Category { get; init; }
        // "Ctrl+S", or empty
        public string Shortcut { get; init; } = "";
        public Visibility ShortcutVisibility => string.IsNullOrEmpty(Shortcut) ? Visibility.Collapsed : Visibility.Visible;
        // Either the menu item that is run, or an action.
        public MenuFlyoutItem Item { get; init; }
        public Action Action { get; init; }
        // Remembers the command between sessions (the recent ones come first)
        public string Key => Category + "|" + Title;
    }
}
