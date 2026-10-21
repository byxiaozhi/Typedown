using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

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
            // the two cards under the first one: side by side, or one under the other
            Grid.SetColumn(HomeTipCard, wide ? 1 : 0);
            Grid.SetRow(HomeTipCard, wide ? 0 : 1);
            Grid.SetColumnSpan(HomeCaptureCard, wide ? 1 : 2);
            Grid.SetColumnSpan(HomeTipCard, wide ? 1 : 2);
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
                if (documents.Contains(activeDoc))
                {
                    HideStartPage();
                    EditorView.Focus(FocusState.Programmatic); // typing goes to the document again
                }
                UpdateHomeButton();
                return;
            }
            if (SettingsPageShown) HideSettingsPage();
            if (ConvertPage.Visibility == Visibility.Visible) CloseConvertPage();
            CloseFindForHome();
            ShowStartPage();
            // The editor may have had the keyboard (Ctrl+Shift+H from the text): it goes to Home, so that typing cannot change the covered note.
            var target = StartPageRecentList.Items.Count > 0 ? StartPageRecentList : HomeStartList;
            if (!target.Focus(FocusState.Programmatic)) HomeButton.Focus(FocusState.Programmatic); // its rows are not laid out yet: the button takes it
            Log("Home: shown over the document");
        }

        // The Find panel belongs to the note: it is closed when Home goes over the note (as HideFindReplace does, but without giving the
        // keyboard back to the text, which Home is about to take).
        private void CloseFindForHome()
        {
            if (FindReplacePanel.Visibility != Visibility.Visible) return;
            findRequest++;
            FindReplacePanel.Visibility = Visibility.Collapsed;
            FindCountText.Text = "";
            RenderInspectorFind(null);
            PostMessage("SearchOpenChange", new { open = 0 });
        }

        // The button is pressed in while Home is on screen.
        private void UpdateHomeButton() => HomeButton.IsChecked = startPageShown;

        // --- The top of Home: the note to pick up, a line to jot down, the tip of the day, the comments that wait ---

        private int homeTopVersion;
        private string homeResumePath;

        // Called with the list of Continue. The files are read off the window's thread, and what comes back is shown only if no newer
        // refresh has started (the page is refreshed often).
        private void RefreshHomeTop(System.Collections.Generic.IReadOnlyList<StartPageEntry> files)
        {
            var version = ++homeTopVersion;
            // the note to pick up: the one opened last that is still there
            homeResumePath = recentFiles.Files.FirstOrDefault(System.IO.File.Exists);
            if (homeResumePath == null) HomeResumeCard.Visibility = Visibility.Collapsed;
            else
            {
                var entry = new StartPageEntry(homeResumePath, false);
                HomeResumeName.Text = entry.Name;
                HomeResumeWhere.Text = string.Join(" · ", new[] { entry.Folder, entry.Modified }.Where(s => !string.IsNullOrEmpty(s)));
                HomeResumeExcerpt.Text = "";
                HomeResumeCard.Visibility = Visibility.Visible;
            }
            ShowHomeTip();
            var resume = homeResumePath;
            var paths = files.Select(f => f.FullPath).ToList();
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var excerpt = resume == null ? "" : ReadHomeExcerpt(resume);
                var inbox = new System.Collections.Generic.List<HomeInboxEntry>();
                foreach (var path in paths)
                {
                    var count = CountCommentsIn(path);
                    if (count > 0) inbox.Add(new HomeInboxEntry(path, count));
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (version != homeTopVersion) return;
                    HomeResumeExcerpt.Text = excerpt;
                    HomeResumeExcerpt.Visibility = excerpt.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                    var ordered = inbox.OrderByDescending(e => e.Comments).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    HomeInboxList.ItemsSource = ordered.Take(3).ToList();
                    HomeInboxSummary.Text = Locale.Format("HomeInboxSummary", ordered.Sum(e => e.Comments), ordered.Count);
                    HomeInboxCard.Visibility = ordered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                });
            });
        }

        // The start of the file, enough to say what it is about (a file that cannot be read says nothing).
        private static string ReadHomeExcerpt(string path)
        {
            try
            {
                using var reader = new System.IO.StreamReader(path, System.Text.Encoding.UTF8, true);
                var buffer = new char[16384];
                var read = reader.Read(buffer, 0, buffer.Length);
                return HomeFacts.Excerpt(new string(buffer, 0, read));
            }
            catch { return ""; }
        }

        // Comments are looked for in files up to a megabyte: a bigger file is not a note.
        private static int CountCommentsIn(string path)
        {
            try
            {
                if (new System.IO.FileInfo(path).Length > 1_000_000) return 0;
                return HomeFacts.CountComments(System.IO.File.ReadAllText(path));
            }
            catch { return 0; }
        }

        private async void HomeResume_Click(object sender, RoutedEventArgs e)
        {
            if (homeResumePath != null) await OpenRecentFile(homeResumePath);
        }

        private async void HomeInbox_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is HomeInboxEntry entry) await OpenRecentFile(entry.FullPath);
        }

        // One line typed here becomes a new note (in a tab of its own, with Home over a document).
        private async void HomeCaptureBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            e.Handled = true;
            var text = HomeCaptureBox.Text.Trim();
            if (text.Length == 0) return;
            HomeCaptureBox.Text = "";
            if (!await MakeRoomForDocument()) { HomeCaptureBox.Text = text; return; }
            file.NewFile();
            file.ApplyRecoveredBackup(text + "\n");
            UpdateTitle();
            Log("Home: a quick note was started");
        }

        // The tip of the day: one shortcut of the cheat sheet's list, another each day.
        private void ShowHomeTip()
        {
            var all = ShortcutCatalog.All;
            var index = HomeFacts.TipIndex(DateTime.Now, all.Length);
            if (index < 0) { HomeTipCard.Visibility = Visibility.Collapsed; return; }
            var tip = all[index];
            HomeTipText.Inlines.Clear();
            HomeTipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = Locale.GetString(tip.NameKey) + ": " });
            HomeTipText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = tip.Keys, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            HomeTipCard.Visibility = Visibility.Visible;
        }

        private void HomeTipShortcuts_Click(object sender, RoutedEventArgs e) => ToggleShortcutSheet();

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
