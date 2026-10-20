using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the History tab of the inspector pane. The versions a note has had each time it was saved (Services/
    // NoteHistory.cs keeps them in the app's data folder, on this PC only), newest first: when, how much longer or shorter, and
    // the first line that changed. A version that is selected shows what differs from the text now, and can be brought back
    // (after one confirmation; the text as it is now is kept first, and Undo brings it back as well).
    public sealed partial class MainWindow
    {
        private const int HistoryDiffLines = 60;

        private List<HistoryEntry> inspectorHistoryEntries = new();
        private string inspectorHistoryPath;
        private string inspectorHistoryLatest;
        private string inspectorHistorySelectedId;
        private string inspectorHistorySelectedText;
        private DispatcherQueueTimer inspectorHistoryTimer;
        private bool inspectorHistoryBuilding;
        private bool inspectorHistorySyncing;
        private int inspectorHistoryRequest;

        private bool InspectorOnHistory => InspectorTabs.SelectedItem == InspectorHistoryTab;

        private void ShowInspectorHistory() => _ = RefreshInspectorHistory(force: true);

        // The note changed or was saved: the list is looked at again a moment later, once (the diff of the selected version too).
        private void ScheduleInspectorHistory()
        {
            if (!InspectorShown || !InspectorOnHistory) return;
            if (inspectorHistoryTimer == null)
            {
                inspectorHistoryTimer = DispatcherQueue.CreateTimer();
                inspectorHistoryTimer.Interval = TimeSpan.FromMilliseconds(400);
                inspectorHistoryTimer.IsRepeating = false;
                inspectorHistoryTimer.Tick += (s, e) => { if (InspectorShown && InspectorOnHistory) _ = RefreshInspectorHistory(force: false); };
            }
            inspectorHistoryTimer.Stop();
            inspectorHistoryTimer.Start();
        }

        private async Task RefreshInspectorHistory(bool force)
        {
            var request = ++inspectorHistoryRequest;
            var path = file.FilePath;
            inspectorHistorySyncing = true;
            InspectorHistoryToggle.IsOn = settings.KeepLocalHistory;
            inspectorHistorySyncing = false;
            InspectorHistoryInfo.Text = string.Format(Locale.GetString("InspectorHistoryInfo"), NoteHistory.DefaultMaxVersions, NoteHistory.DefaultMaxDays);

            if (string.IsNullOrEmpty(path))
            {
                inspectorHistoryPath = null;
                inspectorHistoryLatest = null;
                inspectorHistoryEntries = new List<HistoryEntry>();
                ShowHistoryList("InspectorHistoryUntitled");
                return;
            }

            // a cheap look (no file is read): the list is read again only when the note or its newest version is another one
            var latest = LocalHistory.Notes.LatestId(path);
            if (!force && path == inspectorHistoryPath && latest == inspectorHistoryLatest)
            {
                await RenderInspectorHistoryDetail();
                return;
            }

            var entries = await Task.Run(() => LocalHistory.Notes.List(path));
            if (request != inspectorHistoryRequest || path != file.FilePath || !InspectorShown || !InspectorOnHistory) return;
            inspectorHistoryPath = path;
            inspectorHistoryLatest = latest;
            inspectorHistoryEntries = entries;
            ShowHistoryList(entries.Count > 0 ? null : settings.KeepLocalHistory ? "InspectorHistoryNone" : "InspectorHistoryOff");
            await RenderInspectorHistoryDetail();
        }

        // The rows of the list (or the one line that says why there are none); the selected version stays selected when it is still there.
        private void ShowHistoryList(string emptyKey)
        {
            inspectorHistoryBuilding = true;
            InspectorHistoryListView.Items.Clear();
            FrameworkElement selected = null;
            foreach (var entry in inspectorHistoryEntries)
            {
                var row = HistoryRow(entry);
                InspectorHistoryListView.Items.Add(row);
                if (entry.Id == inspectorHistorySelectedId) selected = row;
            }
            if (selected != null) InspectorHistoryListView.SelectedItem = selected;
            else
            {
                inspectorHistorySelectedId = null;
                inspectorHistorySelectedText = null;
            }
            inspectorHistoryBuilding = false;
            InspectorHistoryEmpty.Text = emptyKey == null ? "" : Locale.GetString(emptyKey);
            InspectorHistoryEmpty.Visibility = emptyKey == null ? Visibility.Collapsed : Visibility.Visible;
            InspectorHistoryDelete.Visibility = inspectorHistoryEntries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // One version: when it was saved and how its length changed, and the first line that is not as it was.
        private static FrameworkElement HistoryRow(HistoryEntry entry)
        {
            var delta = entry.IsFirst
                ? string.Format(Locale.GetString("InspectorHistoryDelta"), entry.Length)
                : string.Format(Locale.GetString("InspectorHistoryDelta"), (entry.Delta > 0 ? "+" : entry.Delta < 0 ? "−" : "±") + Math.Abs(entry.Delta));
            var change = entry.IsFirst ? Locale.GetString("InspectorHistoryFirst") : entry.FirstChange;
            var row = new StackPanel { Spacing = 2, Margin = new Thickness(4, 5, 4, 5), Tag = entry.Id };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            head.Children.Add(new TextBlock { Text = entry.Time.ToString("g"), FontSize = 12, FontWeight = FontWeights.SemiBold });
            head.Children.Add(new TextBlock { Text = delta, FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(head);
            row.Children.Add(new TextBlock { Text = change, FontSize = 12, Opacity = 0.7, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis });
            AutomationProperties.SetName(row, $"{entry.Time:g} {delta} {change}");
            return row;
        }

        private async void InspectorHistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (inspectorHistoryBuilding) return;
            inspectorHistorySelectedId = (InspectorHistoryListView.SelectedItem as FrameworkElement)?.Tag as string;
            inspectorHistorySelectedText = null;
            await RenderInspectorHistoryDetail();
        }

        // What differs between the selected version and the text now (the removed lines are in the version and not in the text,
        // the added ones the other way round), and the buttons that act on the version.
        private async Task RenderInspectorHistoryDetail()
        {
            var id = inspectorHistorySelectedId;
            var path = inspectorHistoryPath;
            if (id == null || path == null)
            {
                InspectorHistoryDetail.Visibility = Visibility.Collapsed;
                return;
            }
            var request = ++inspectorHistoryRequest;
            var current = file.Markdown ?? "";
            var text = inspectorHistorySelectedText ??= await Task.Run(() => LocalHistory.Notes.Read(path, id));
            if (text == null)
            {
                InspectorHistoryDetail.Visibility = Visibility.Collapsed;
                return;
            }
            var changes = await Task.Run(() =>
            {
                var list = LineDiff.Changes(text, current, HistoryDiffLines, out var more);
                return (list, more);
            });
            if (request != inspectorHistoryRequest || id != inspectorHistorySelectedId) return;

            InspectorHistoryDiff.Children.Clear();
            InspectorHistoryDiffHeader.Text = Locale.GetString(changes.list.Count == 0 ? "InspectorHistoryIdentical" : "InspectorHistoryDiffHeader");
            foreach (var line in changes.list)
            {
                var removed = line.Kind == DiffKind.Removed;
                InspectorHistoryDiff.Children.Add(new Border
                {
                    Background = new SolidColorBrush(removed ? Windows.UI.Color.FromArgb(0x40, 0xE0, 0x60, 0x5A) : Windows.UI.Color.FromArgb(0x40, 0x3F, 0xAE, 0x6B)),
                    Padding = new Thickness(6, 1, 6, 1),
                    Child = new TextBlock
                    {
                        Text = (removed ? "− " : "+ ") + (line.Text.Length == 0 ? " " : line.Text),
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                    },
                });
            }
            if (changes.more > 0)
                InspectorHistoryDiff.Children.Add(new TextBlock { Text = string.Format(Locale.GetString("InspectorHistoryMoreLines"), changes.more), FontSize = 11, Opacity = 0.7, Margin = new Thickness(6, 2, 0, 0) });
            InspectorHistoryDiffScroll.Visibility = changes.list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            InspectorHistoryRestoreButton.IsEnabled = changes.list.Count > 0;
            InspectorHistoryDetail.Visibility = Visibility.Visible;
        }

        private void InspectorHistoryToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (inspectorHistorySyncing) return;
            settings.KeepLocalHistory = InspectorHistoryToggle.IsOn;
            _ = RefreshInspectorHistory(force: true);
        }

        private async void InspectorHistoryRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var id = inspectorHistorySelectedId;
                var path = file.FilePath;
                var entry = inspectorHistoryEntries.FirstOrDefault(x => x.Id == id);
                if (id == null || entry == null || string.IsNullOrEmpty(path)) return;
                var text = await Task.Run(() => LocalHistory.Notes.Read(path, id));
                if (text == null) return;

                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("InspectorHistoryRestoreTitle"),
                    Content = new TextBlock { Text = string.Format(Locale.GetString("InspectorHistoryRestoreBody"), entry.Time.ToString("g")), TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = Locale.GetString("InspectorHistoryRestore"),
                    CloseButtonText = Locale.GetString("Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

                await FlushEditor();
                if (file.FilePath != path) return;
                var current = file.Markdown ?? "";
                if (current != text)
                {
                    // the text as it is now becomes a version of its own first, so that nothing is lost by going back
                    if (settings.KeepLocalHistory) await Task.Run(() => LocalHistory.Notes.Record(path, current, checkpoint: true));
                    file.ReplaceBuffer(text);
                    history.ContentChange(text);
                    // in like Undo's SetMarkdown, and one step in the undo history
                    PostMessage("SetMarkdown", new { text, cursor = activeDoc.Cursor, basePath = file.ImageBasePath });
                    Log($"History: restored the version of {entry.Time:O}");
                }
                await RefreshInspectorHistory(force: true);
            }
            catch (Exception ex)
            {
                Log($"History: restore failed: {ex.Message}");
            }
        }

        private async void InspectorHistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = file.FilePath;
                if (string.IsNullOrEmpty(path)) return;
                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("InspectorHistoryDeleteTitle"),
                    Content = new TextBlock { Text = Locale.GetString("InspectorHistoryDeleteBody"), TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = Locale.GetString("InspectorHistoryDelete"),
                    CloseButtonText = Locale.GetString("Cancel"),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                await Task.Run(() => LocalHistory.Notes.Clear(path));
                inspectorHistorySelectedId = null;
                inspectorHistorySelectedText = null;
                await RefreshInspectorHistory(force: true);
            }
            catch (Exception ex)
            {
                Log($"History: delete failed: {ex.Message}");
            }
        }
    }
}
