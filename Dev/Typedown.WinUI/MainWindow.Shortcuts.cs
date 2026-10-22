using System;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.System;

namespace Typedown.WinUI
{
    // New since the fork: the cheat sheet (Ctrl+Shift+/, Help > Keyboard shortcuts, the command palette): every keyboard shortcut of Caret by task (Format,
    // Speech, Find, Files, General), with a box that narrows it as you type. The list is Services/ShortcutCatalog.cs; Ctrl+/ stays the view-mode cycle.
    public sealed partial class MainWindow
    {
        // A row as the list draws it.
        private sealed class ShortcutRowView
        {
            public ShortcutRowView(ShortcutSheet.Row row)
            {
                Text = row.Text;
                Keys = row.Keys;
                Weight = row.IsHeader ? FontWeights.SemiBold : FontWeights.Normal;
                TextOpacity = row.IsHeader ? 0.75 : 1;
                RowMargin = row.IsHeader ? new Thickness(0, 8, 0, 0) : new Thickness(0);
            }

            public string Text { get; }
            public string Keys { get; }
            public Windows.UI.Text.FontWeight Weight { get; }
            public double TextOpacity { get; }
            public Thickness RowMargin { get; }
        }

        // Where the keyboard was in the window when the sheet opened over the Settings page (it goes back there, not to the text).
        private Control shortcutsReturnFocus;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private void SetUpShortcutSheet()
        {
            // Ctrl+Shift+/ from anywhere in the window (from the editor the page tells, by the character: HostShortcut). The "/" key of a US keyboard is virtual
            // key 0xBF; on another layout "/" is another key (Shift+7 on a Hungarian or German one), which the layout says: both are accelerators.
            var keys = new System.Collections.Generic.List<VirtualKey> { (VirtualKey)0xBF };
            var scan = VkKeyScan('/');
            if (scan != -1 && (scan & 0xFF) is var vk and not 0xBF && vk != 0) keys.Add((VirtualKey)vk);
            foreach (var key in keys)
            {
                var accelerator = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift };
                accelerator.Invoked += (_, args) =>
                {
                    ToggleShortcutSheet();
                    args.Handled = true;
                };
                ((UIElement)Content).KeyboardAccelerators.Add(accelerator);
            }
        }

        private void ShortcutsMenuItem_Click(object sender, RoutedEventArgs e) => ShowShortcutSheet();

        private void ToggleShortcutSheet()
        {
            if (ShortcutsPanel.Visibility == Visibility.Visible) CloseShortcutSheet();
            else ShowShortcutSheet();
        }

        private void ShowShortcutSheet()
        {
            HideKeyTips(restore: false);
            if (ShortcutsPanel.Visibility != Visibility.Visible)
            {
                // the control of the window that had the keyboard (a sidebar item, a button of the Convert page, a Settings control), not the editor
                // (a web page: it is taken by the editor's own way back)
                var focused = FocusManager.GetFocusedElement(Content.XamlRoot) as Control;
                shortcutsReturnFocus = focused == null || ReferenceEquals(focused, EditorView) ? null : focused;
            }
            ShortcutsTextBox.Text = "";
            UpdateShortcutRows("");
            ShortcutsPanel.Visibility = Visibility.Visible;
            ShortcutsTextBox.Focus(FocusState.Programmatic);
        }

        private void CloseShortcutSheet()
        {
            if (ShortcutsPanel.Visibility != Visibility.Visible) return;
            ShortcutsPanel.Visibility = Visibility.Collapsed;
            if (shortcutsReturnFocus is { IsLoaded: true } control && control.Focus(FocusState.Programmatic)) return;
            EditorView.Focus(FocusState.Programmatic);
            _ = FocusEditorTextAsync(findRequest);
        }

        private void UpdateShortcutRows(string query)
        {
            var rows = ShortcutSheet.Build(key => Locale.GetString(key), query).Select(r => new ShortcutRowView(r)).ToList();
            ShortcutsListView.ItemsSource = rows;
            ShortcutsNoResultsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShortcutsTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateShortcutRows(ShortcutsTextBox.Text);

        // Esc closes it from anywhere in it; Down from the box goes into the list (the arrow keys and Page Up/Down scroll it there).
        private void ShortcutsPanel_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Escape) return;
            e.Handled = true;
            CloseShortcutSheet();
        }

        private void ShortcutsTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Down || ShortcutsListView.Items.Count == 0) return;
            e.Handled = true;
            (ShortcutsListView.ContainerFromIndex(0) as Control)?.Focus(FocusState.Keyboard);
        }
    }
}
