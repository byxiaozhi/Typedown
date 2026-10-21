using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services.Conversion;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.ViewManagement;

namespace Typedown.WinUI
{
    // New since the fork: the conversion tray. Converting stops being a trip to the Convert page: files dragged over the window (on any
    // page, over the editor too) show a full-window "Convert to Markdown" overlay, the drop converts them, and a flyout at the bottom right
    // lists the jobs (the same list as the Convert page, which stays for batch work): a progress bar, then Open, Show in folder and Compare;
    // a failed job says why, with Retry. Compare puts the source (as plain text) and the Markdown side by side, with the choices that were
    // applied (and, for an email, the values that were masked) in view, so what the converter did can be checked before it is trusted.
    public sealed partial class MainWindow
    {
        // Set on the data of a drag from the folder tree, so that moving a file between folders is not taken for a conversion.
        private const string FolderDragKey = "Caret.FolderItem";

        private bool trayWanted;
        private bool trayClosed;
        // Counts the drags: the answer for an earlier one (read after its drag has gone) is dropped.
        private int dropRound;

        private void SetUpConversionTray()
        {
            ConversionItem.HighContrast = new AccessibilitySettings().HighContrast;
            ConvertTrayList.ItemsSource = conversions;
            conversions.CollectionChanged += (_, _) => UpdateTray();
            // handledEventsToo: the editor (a WebView2) answers a drag of its own and may mark it handled before it reaches the window.
            var root = (UIElement)Content;
            root.AddHandler(UIElement.DragEnterEvent, new DragEventHandler(Window_DragEnterOrOver), true);
            root.AddHandler(UIElement.DragOverEvent, new DragEventHandler(Window_DragEnterOrOver), true);
            root.AddHandler(UIElement.DragLeaveEvent, new DragEventHandler(Window_DragLeave), true);
        }

        // The tray shows the jobs of the drops it took, until it is closed; the Convert page and Compare have the jobs in front already.
        private void UpdateTray()
        {
            ConvertTray.Visibility = trayWanted && !trayClosed && conversions.Count > 0
                && ConvertPage.Visibility != Visibility.Visible && ConvertComparePanel.Visibility != Visibility.Visible
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- The drop ---

        // A drag is asked about once (the first enter or over that reaches the window), not at every move.
        private bool dragAsked;

        private async void Window_DragEnterOrOver(object sender, DragEventArgs e)
        {
            // The Convert page has its own drop zone; a tab or a folder-tree row dragged inside the app is not a file from outside.
            if (dragAsked || ConvertDropOverlay.Visibility == Visibility.Visible || ConvertPage.Visibility == Visibility.Visible) return;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            if (e.DataView.Properties.ContainsKey(TabDragKey) || e.DataView.Properties.ContainsKey(FolderDragKey)) return;
            dragAsked = true;
            var round = ++dropRound;
            var deferral = e.GetDeferral();
            var accepted = false;
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                accepted = round == dropRound && ConvertDrop.Accepts(items.Select(i => (i.Path, i.IsOfType(StorageItemTypes.Folder))).ToList());
                if (accepted) e.AcceptedOperation = DataPackageOperation.Copy;
            }
            catch (Exception ex)
            {
                Log($"Tray: couldn't read the dragged items: {ex.Message}");
            }
            finally { deferral.Complete(); }
            if (accepted) ConvertDropOverlay.Visibility = Visibility.Visible;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            if (ConvertDropOverlay.Visibility != Visibility.Visible) dragAsked = false;
        }

        private void ConvertDropOverlay_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Locale.GetString("ConvertDropCaption");
            e.Handled = true;
        }

        private void ConvertDropOverlay_DragLeave(object sender, DragEventArgs e)
        {
            dropRound++;
            dragAsked = false;
            ConvertDropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void ConvertDropOverlay_Drop(object sender, DragEventArgs e)
        {
            dropRound++;
            dragAsked = false;
            ConvertDropOverlay.Visibility = Visibility.Collapsed;
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            e.Handled = true;
            var deferral = e.GetDeferral();
            List<string> paths;
            try { paths = (await e.DataView.GetStorageItemsAsync()).Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList(); }
            finally { deferral.Complete(); }
            await ConvertPathsAsync(paths, tray: true);
        }

        // The overlay stays only while a drag is over it: a click is the way out if a drag ever ended without telling.
        private void ConvertDropOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            dragAsked = false;
            ConvertDropOverlay.Visibility = Visibility.Collapsed;
        }

        // --- The tray ---

        private void ConvertTrayClose_Click(object sender, RoutedEventArgs e)
        {
            trayClosed = true;
            UpdateTray();
            EditorView.Focus(FocusState.Programmatic);
        }

        // Esc closes it when the keys are in it.
        private void ConvertTray_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Escape) return;
            e.Handled = true;
            ConvertTrayClose_Click(sender, e);
        }

        // Finished and failed jobs go; the ones still running stay.
        private void ConvertTrayClear_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in conversions.Where(c => c.Succeeded || c.Failed).ToList()) conversions.Remove(item);
            if (conversions.Count == 0) ConvertSummaryPanel.Visibility = Visibility.Collapsed;
            UpdateConvertSummary();
        }

        private async void TrayRetry_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ConversionItem { Failed: true } item) await RetryConversionAsync(item);
        }

        // The Open / Show in folder / Compare of a row are there when the pointer is over the row, when the keys are in it (a tap holds it
        // too); they stay in the tab order whatever their opacity, so the keyboard and a screen reader always reach them.
        private void TrayRow_Reveal(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid row) return;
            if (e is PointerRoutedEventArgs) row.Tag = true;
            ShowRowActions(row);
        }

        private void TrayRow_Rest(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid row) return;
            if (e is PointerRoutedEventArgs) row.Tag = false;
            // Focus moves in two steps (lost here, then got there): looked at once it has settled.
            DispatcherQueue.TryEnqueue(() => ShowRowActions(row));
        }

        private void ShowRowActions(Grid row)
        {
            var inside = row.Tag is true;
            for (var e = FocusManager.GetFocusedElement(Content.XamlRoot) as DependencyObject; e != null && !inside; e = VisualTreeHelper.GetParent(e))
                inside = e == row;
            if (row.FindName("RowActions") is UIElement actions) actions.Opacity = inside ? 1 : 0;
        }

        // --- Compare ---

        private ConversionItem compareItem;
        private int compareRound;
        private bool compareUpdating;
        private ScrollViewer sourceScroller, markdownScroller;

        private async void TrayCompare_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is ConversionItem { Succeeded: true } item) await ShowCompareAsync(item);
        }

        private void ConvertCompareBack_Click(object sender, RoutedEventArgs e) => CloseCompare();

        private void ConvertComparePanel_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Escape) return;
            e.Handled = true;
            CloseCompare();
        }

        private void CloseCompare()
        {
            compareRound++;
            ConvertComparePanel.Visibility = Visibility.Collapsed;
            CompareSourceList.ItemsSource = null;
            CompareMarkdownList.ItemsSource = null;
            UpdateTray();
            EditorView.Focus(FocusState.Programmatic);
        }

        private void CompareJobCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!compareUpdating && CompareJobCombo.SelectedItem is ConversionItem item && item != compareItem) _ = ShowCompareAsync(item, fromCombo: true);
        }

        private async Task ShowCompareAsync(ConversionItem item, bool fromCombo = false)
        {
            var round = ++compareRound;
            compareItem = item;

            // The list is not set again by a choice in it: that would take the keys out of the panel (Esc closes it).
            if (!fromCombo)
            {
                compareUpdating = true;
                var jobs = conversions.Where(c => c.Succeeded).Reverse().ToList();
                CompareJobCombo.ItemsSource = jobs;
                CompareJobCombo.SelectedItem = item;
                CompareJobCombo.Visibility = jobs.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                compareUpdating = false;
            }

            CompareBadge.Background = item.BadgeBrush;
            CompareBadgeText.Text = item.Badge;
            CompareNameText.Text = item.Name;
            CompareOpenButton.Tag = CompareRevealButton.Tag = item;
            CompareSourceHeader.Text = Locale.GetString("CompareSource") + " · " + item.Name;
            CompareMarkdownHeader.Text = Locale.GetString("CompareMarkdown") + " · " + Path.GetFileName(item.OutputPath);
            ShowCompareChoices(item);

            CompareMarkdownList.ItemsSource = (item.Markdown ?? "").Replace("\r\n", "\n").Split('\n');
            CompareSourceList.ItemsSource = new[] { "…" };
            ConvertComparePanel.Visibility = Visibility.Visible;
            UpdateTray();
            HookCompareScrolling();
            if (!fromCombo) CompareBackButton.Focus(FocusState.Programmatic);

            string[] lines;
            var truncated = false;
            string failure = null;
            try
            {
                var preview = await Task.Run(() => SourceText.Read(item.SourcePath));
                lines = preview.Text.Split('\n');
                truncated = preview.Truncated;
            }
            catch (Exception ex)
            {
                Log($"Compare: couldn't read {item.SourcePath}: {ex.Message}");
                lines = Array.Empty<string>();
                failure = FriendlyConversionError(ex);
            }
            if (round != compareRound) return;
            CompareSourceList.ItemsSource = lines;
            var note = new List<string>();
            if (failure != null) note.Add(Locale.Format("CompareNoSource", failure));
            if (truncated) note.Add(Locale.GetString("CompareTruncated"));
            if (item.Masked && RedactionMarks.Find(item.Markdown).Count > 0) note.Add(Locale.GetString("CompareMarksLegend"));
            CompareNoteText.Text = string.Join(" ", note);
            CompareNoteText.Visibility = note.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // The choices that were applied to this job, as chips; for a masked email, how many of each kind of value were masked.
        private void ShowCompareChoices(ConversionItem item)
        {
            CompareImagesText.Text = !item.ImagesExtracted ? Locale.GetString("CompareImagesOff")
                : item.ImageCount > 0 ? Locale.Format("CompareImagesOn", item.ImageCount) : Locale.GetString("CompareImagesNone");
            CompareMaskedChip.Visibility = item.IsEmail ? Visibility.Visible : Visibility.Collapsed;
            var counts = item.IsEmail && item.Masked ? RedactionMarks.Count(item.Markdown) : (IReadOnlyList<(string Kind, int Count)>)Array.Empty<(string, int)>();
            CompareMaskedText.Text = !item.Masked ? Locale.GetString("CompareMaskedOff")
                : counts.Count == 0 ? Locale.GetString("CompareMaskedNone") : Locale.GetString("CompareMaskedOn");
            CompareCountsText.Text = string.Join(" · ", counts.Select(c => Locale.GetString("CompareKind" + char.ToUpperInvariant(c.Kind[0]) + c.Kind.Substring(1).ToLowerInvariant()) + ": " + c.Count));
            CompareCountsText.Visibility = counts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            CompareNoteText.Visibility = Visibility.Collapsed;
        }

        // A masked value ("[PERSON-2]") in a line of the Markdown is bold and underlined (not only coloured: a contrast theme or a colour
        // blind reader sees it too).
        private void CompareMarkdownList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.ItemContainer.ContentTemplateRoot is not TextBlock block) return;
            var line = args.Item as string ?? "";
            block.Inlines.Clear();
            var marks = compareItem is { Masked: true } ? RedactionMarks.Find(line) : Array.Empty<(int Start, int Length)>();
            var position = 0;
            foreach (var (start, length) in marks)
            {
                if (start > position) block.Inlines.Add(new Run { Text = line.Substring(position, start - position) });
                var mark = new Underline();
                mark.Inlines.Add(new Run { Text = line.Substring(start, length), FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                block.Inlines.Add(mark);
                position = start + length;
            }
            if (position < line.Length || marks.Count == 0) block.Inlines.Add(new Run { Text = line.Substring(position) });
        }

        // The two texts scroll together (by the share of each that has gone by), so a place in one is found in the other.
        private void HookCompareScrolling()
        {
            if (sourceScroller == null && FindScroller(CompareSourceList) is { } source)
            {
                sourceScroller = source;
                source.ViewChanged += (_, _) => SyncScroll(sourceScroller, markdownScroller);
            }
            if (markdownScroller == null && FindScroller(CompareMarkdownList) is { } markdown)
            {
                markdownScroller = markdown;
                markdown.ViewChanged += (_, _) => SyncScroll(markdownScroller, sourceScroller);
            }
        }

        private static void SyncScroll(ScrollViewer from, ScrollViewer to)
        {
            if (from == null || to == null || from.ScrollableHeight <= 0 || to.ScrollableHeight <= 0) return;
            var target = from.VerticalOffset / from.ScrollableHeight * to.ScrollableHeight;
            if (Math.Abs(target - to.VerticalOffset) > 2) to.ChangeView(null, target, null, true);
        }

        private static ScrollViewer FindScroller(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer viewer) return viewer;
                if (FindScroller(child) is { } inner) return inner;
            }
            return null;
        }
    }
}
