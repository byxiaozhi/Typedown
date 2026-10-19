using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.System;

namespace Typedown.WinUI
{
    // New since the fork: a command palette (Ctrl+Shift+P, View > Command Palette), as in VS Code and Windows Terminal.
    // The commands are the ones in the menus, read from the menu bar when the palette opens (so a command that is added
    // to a menu is in the palette without more work, in the language of the menus, with its shortcut and the menu it is
    // in), plus a few that have no menu item. Typing narrows the list (every word must be in the command or its menu, in
    // order inside a word); the commands used last come first when the box is empty. Enter runs the selected command.
    public sealed partial class MainWindow
    {
        private List<PaletteEntry> paletteAll = new();
        private int paletteRequest;

        private void CommandPaletteMenuItem_Click(object sender, RoutedEventArgs e) => ShowCommandPalette();

        private void CommandPaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ShowCommandPalette();
            args.Handled = true;
        }

        private void ShowCommandPalette()
        {
            Log("ShowCommandPalette called");
            paletteAll = CollectPaletteCommands();
            PaletteTextBox.Text = "";
            UpdatePaletteResults("");
            PalettePanel.Visibility = Visibility.Visible;
            PaletteTextBox.Focus(FocusState.Programmatic);
            PaletteTextBox.SelectAll();
        }

        private void HidePalette()
        {
            if (PalettePanel.Visibility != Visibility.Visible) return;
            PalettePanel.Visibility = Visibility.Collapsed;
        }

        // Every enabled command of the menu bar, then the commands that are not in a menu.
        private List<PaletteEntry> CollectPaletteCommands()
        {
            var list = new List<PaletteEntry>();
            foreach (var top in AppMenuBar.Items)
                WalkMenu(top.Items, top.Title, list);
            list.Add(new PaletteEntry
            {
                Title = Locale.GetString("PalettePrintSystem"),
                Category = AppMenuBar.Items.FirstOrDefault()?.Title ?? "",   // the File menu is the first one
                Shortcut = "",
                Action = () => ShowPrint(CoreWebView2PrintDialogKind.System),
            });
            return list;
        }

        private static void WalkMenu(IList<MenuFlyoutItemBase> items, string category, List<PaletteEntry> list)
        {
            foreach (var item in items)
            {
                switch (item)
                {
                    case MenuFlyoutSubItem sub when sub.Name == "OpenRecentMenu":
                        break;   // the recent files are Go to File (Ctrl+K)
                    case MenuFlyoutSubItem sub:
                        WalkMenu(sub.Items, category + " › " + sub.Text, list);
                        break;
                    case MenuFlyoutItem command when command.IsEnabled && !string.IsNullOrWhiteSpace(command.Text) && command.Name != "CommandPaletteMenuItem":
                        list.Add(new PaletteEntry { Title = command.Text.Trim(), Category = category, Shortcut = ShortcutText(command), Item = command });
                        break;
                }
            }
        }

        private static string ShortcutText(MenuFlyoutItem item)
        {
            if (!string.IsNullOrEmpty(item.KeyboardAcceleratorTextOverride)) return item.KeyboardAcceleratorTextOverride;
            var accelerator = item.KeyboardAccelerators.FirstOrDefault();
            if (accelerator == null) return "";
            var parts = new List<string>();
            if (accelerator.Modifiers.HasFlag(VirtualKeyModifiers.Control)) parts.Add("Ctrl");
            if (accelerator.Modifiers.HasFlag(VirtualKeyModifiers.Shift)) parts.Add("Shift");
            if (accelerator.Modifiers.HasFlag(VirtualKeyModifiers.Menu)) parts.Add("Alt");
            if (accelerator.Modifiers.HasFlag(VirtualKeyModifiers.Windows)) parts.Add("Win");
            parts.Add(accelerator.Key.ToString());
            return string.Join("+", parts);
        }

        private void UpdatePaletteResults(string query)
        {
            IEnumerable<PaletteEntry> rows;
            if (string.IsNullOrWhiteSpace(query))
            {
                // the ones that were run last first, then the menu order
                var recent = (settings.PaletteRecent ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                rows = paletteAll.OrderBy(e => { var i = recent.IndexOf(e.Key); return i < 0 ? int.MaxValue : i; });
            }
            else
            {
                rows = paletteAll.Select(e => (Entry: e, Score: PaletteSearch.Score(query, e.Title, e.Category))).Where(x => x.Score >= 0)
                    .OrderByDescending(x => x.Score).Select(x => x.Entry);
            }
            var shown = rows.Take(60).ToList();
            PaletteListView.ItemsSource = shown;
            PaletteListView.SelectedIndex = shown.Count > 0 ? 0 : -1;
            PaletteNoResultsText.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PaletteTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePaletteResults(PaletteTextBox.Text);

        private void MovePaletteSelection(int delta)
        {
            var count = PaletteListView.Items.Count;
            if (count == 0) return;
            PaletteListView.SelectedIndex = (PaletteListView.SelectedIndex + delta + count) % count;
            PaletteListView.ScrollIntoView(PaletteListView.SelectedItem);
        }

        private async void PaletteTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Escape:
                    ClosePalette();
                    e.Handled = true;
                    break;
                case VirtualKey.Tab:
                    e.Handled = true;   // the keyboard stays in the box (Up and Down move in the list); Tab would leave the palette
                    break;
                case VirtualKey.Down:
                    MovePaletteSelection(1);
                    e.Handled = true;
                    break;
                case VirtualKey.Up:
                    MovePaletteSelection(-1);
                    e.Handled = true;
                    break;
                case VirtualKey.PageDown:
                    MovePaletteSelection(6);
                    e.Handled = true;
                    break;
                case VirtualKey.PageUp:
                    MovePaletteSelection(-6);
                    e.Handled = true;
                    break;
                case VirtualKey.Enter:
                    e.Handled = true;
                    if (PaletteListView.SelectedItem is PaletteEntry entry) await RunPaletteEntry(entry);
                    break;
            }
        }

        // Esc: the palette closes and the keyboard goes back to the text.
        private void ClosePalette()
        {
            HidePalette();
            EditorView.Focus(FocusState.Programmatic);
            _ = FocusEditorTextAsync(findRequest);
        }

        // The list is not a tab stop, but a click on a row gives it the focus: Enter and Esc work from there as in the box.
        private async void PaletteListView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                if (PaletteListView.SelectedItem is PaletteEntry entry) await RunPaletteEntry(entry);
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                ClosePalette();
            }
        }

        private async void PaletteListView_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is PaletteEntry entry) await RunPaletteEntry(entry);
        }

        // Closes the palette, gives the focus back to the text (a format command works on the selection there), and runs the command.
        private async Task RunPaletteEntry(PaletteEntry entry)
        {
            var request = ++paletteRequest;
            HidePalette();
            RememberPaletteEntry(entry);
            EditorView.Focus(FocusState.Programmatic);
            await FocusEditorTextAsync(findRequest);
            if (request != paletteRequest) return;
            try
            {
                Log($"Palette: {entry.Key}");
                if (entry.Action != null) entry.Action();
                else if (entry.Item != null) InvokeMenuItem(entry.Item);
            }
            catch (Exception ex)
            {
                Log($"Palette: {entry.Key} failed: {ex}");
            }
        }

        // The same thing a click on the menu item does (its Click handler runs; a toggle or a radio item changes its state).
        private static void InvokeMenuItem(MenuFlyoutItem item)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(item) ?? FrameworkElementAutomationPeer.CreatePeerForElement(item);
            if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) invoke.Invoke();
            else if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle) toggle.Toggle();
        }

        private void RememberPaletteEntry(PaletteEntry entry)
        {
            var recent = (settings.PaletteRecent ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(k => k != entry.Key).ToList();
            recent.Insert(0, entry.Key);
            settings.PaletteRecent = string.Join("\n", recent.Take(8));
        }
    }
}
