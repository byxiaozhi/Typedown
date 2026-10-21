using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Models;

namespace Typedown.WinUI
{
    // New since the fork: Home as a working dashboard. The page shown when no note is open answers three questions in three columns (one under the other
    // when the window is narrow): what was I doing (Continue: the pinned and the recent files), what can I start (Start: a new note, open a file or a folder,
    // a template, Outlook emails, and a card for Convert, where a drop anywhere on the window converts), and where do I work (Folders: the pinned folders,
    // then the ones opened last: Services/FolderHistory.cs). The rows are 44 px; the pointer over a row, or the keys in it, show its pin, and a pinned row
    // keeps it. A pinned file is a favourite, as in the sidebar.
    public sealed partial class MainWindow
    {
        // A window this wide takes three columns (the content is at most 1180 px, with 80 px of margin).
        private const double HomeWideWidth = 980;

        private bool homeWide = true;

        private void StartPage_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateHomeLayout(e.NewSize.Width);

        // Three columns, or one under the other: each column spans all three, one row each.
        private void UpdateHomeLayout(double width)
        {
            var wide = width >= HomeWideWidth;
            if (wide == homeWide && HomeColumns.RowSpacing == (wide ? 0 : 28)) return;
            homeWide = wide;
            HomeColumns.RowSpacing = wide ? 0 : 28;
            var columns = new FrameworkElement[] { HomeContinueColumn, HomeStartColumn, HomeFoldersColumn };
            for (var i = 0; i < columns.Length; i++)
            {
                Grid.SetColumn(columns[i], wide ? i : 0);
                Grid.SetColumnSpan(columns[i], wide ? 1 : 3);
                Grid.SetRow(columns[i], wide ? 0 : i);
            }
        }

        // The house button left of the tabs, View > Home and Ctrl+Shift+H: Home is shown over the document that is open, and the same again goes back
        // to it. Nothing is closed and nothing is saved or lost: the document, its tab and its undo history stay where they are. With no document open
        // (the blank one behind Home) there is nothing to go back to and nothing happens.
        private void HomeButton_Click(object sender, RoutedEventArgs e) => ToggleHome();

        private void HomeMenuItem_Click(object sender, RoutedEventArgs e) => ToggleHome();

        private void HomeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ToggleHome();
            args.Handled = true;
        }

        private void ToggleHome()
        {
            if (!editorReady) { UpdateHomeButton(); return; }
            if (startPageShown)
            {
                if (documents.Contains(activeDoc)) HideStartPage();
                UpdateHomeButton();
                return;
            }
            if (SettingsPageShown) HideSettingsPage();
            if (ConvertPage.Visibility == Visibility.Visible) CloseConvertPage();
            ShowStartPage();
            Log("Home: shown over the document");
        }

        // The button is pressed in while Home is on screen.
        private void UpdateHomeButton() => HomeButton.IsChecked = startPageShown;

        // The Folders column: the pinned folders, then the ones opened last, the ones that are still there.
        private void RefreshHomeFolders()
        {
            var shelf = folderHistory.Shelf(System.IO.Directory.Exists);
            HomeFolderList.ItemsSource = shelf.Select(f => new HomeFolderEntry(f.Path, f.Pinned)).ToList();
            HomeNoFoldersText.Visibility = shelf.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void HomeStart_ItemClick(object sender, ItemClickEventArgs e)
        {
            // The list reports what is inside the row (its stack of icon and text), not the row that carries the tag.
            string tag = null;
            for (var element = e.ClickedItem as DependencyObject; element != null && tag == null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
                if (element is ListViewItem { Tag: string rowTag }) tag = rowTag;
            switch (tag)
            {
                case "new": StartNewNote_Click(this, null); break;
                case "open": StartOpenFile_Click(this, null); break;
                case "folder": OpenFolderMenuItem_Click(this, null); break;
                case "template": SelectNav("Templates"); break; // the sidebar's list of templates
                case "emails": HomeEmailsButton_Click(this, null); break;
            }
        }

        private void HomeFolder_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not HomeFolderEntry entry) return;
            if (System.IO.Directory.Exists(entry.FullPath))
            {
                settings.FolderCollapsed = false; // a folder opened on purpose is shown, as File > Open Folder does
                OpenFolderTree(entry.FullPath);
            }
            else RefreshHomeFolders(); // it went since the list was drawn
        }

        private void HomeFolderPin_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).Tag is string path) folderHistory.TogglePinned(path);
            if (folderHistory.LastError != null) Log($"Home: the folders could not be saved: {folderHistory.LastError}");
            RefreshHomeFolders();
        }

        // The pin of a row is there for a pinned row; for the others when the pointer is over the row or the keys are in it (it stays in the tab
        // order whatever its opacity, so the keyboard and a screen reader always reach it).
        private void HomeRow_Reveal(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid row) return;
            if (e is PointerRoutedEventArgs) row.Tag = true;
            ShowRowPin(row);
        }

        private void HomeRow_Rest(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid row) return;
            if (e is PointerRoutedEventArgs) row.Tag = false;
            // focus moves in two steps (lost here, then got there): looked at once it has settled
            DispatcherQueue.TryEnqueue(() => ShowRowPin(row));
        }

        private void ShowRowPin(Grid row)
        {
            var inside = row.Tag is true || row.DataContext switch { StartPageEntry s => s.IsPinned, HomeFolderEntry f => f.IsPinned, _ => false };
            for (var f = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot) as DependencyObject; f != null && !inside; f = VisualTreeHelper.GetParent(f))
                inside = f == row;
            if (row.FindName("PinButton") is UIElement pin) pin.Opacity = inside ? 1 : 0;
        }
    }
}
