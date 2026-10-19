using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Typedown.WinUI.Models;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the inspector pane (View > Inspector pane, Ctrl+Shift+I), a column beside the note, so that the note stays
    // in sight while it is read from the side. Two tabs for now: Outline (the headings, the list the sidebar has too) and Review (the
    // changes and comments of the note in the order of the text; a click goes to the mark). It opens on request, like the review
    // marks themselves, and never by itself.
    public sealed partial class MainWindow
    {
        private readonly ObservableCollection<ReviewListEntry> inspectorReview = new();
        private string inspectorText = "";
        private DispatcherQueueTimer inspectorTimer;
        private bool inspectorWired;

        private bool InspectorShown => InspectorPane.Visibility == Visibility.Visible;
        private bool InspectorOnReview => InspectorTabs.SelectedItem == InspectorReviewTab;

        private void InspectorMenuItem_Click(object sender, RoutedEventArgs e) => ToggleInspector();

        private void InspectorClose_Click(object sender, RoutedEventArgs e) => CloseInspector();

        private void InspectorAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ToggleInspector();
            args.Handled = true;
        }

        private void ToggleInspector()
        {
            if (InspectorShown) CloseInspector();
            else OpenInspector();
        }

        private void OpenInspector()
        {
            if (!inspectorWired)
            {
                inspectorWired = true;
                InspectorTocListView.ItemsSource = tocEntries;
                InspectorReviewListView.ItemsSource = inspectorReview;
                tocEntries.CollectionChanged += (s, e) => UpdateInspectorOutlineEmpty();
            }
            inspectorText = file.Markdown ?? "";
            InspectorPane.Visibility = Visibility.Visible;
            InspectorColumn.Width = GridLength.Auto;
            InspectorMenuItem.IsChecked = true;
            ShowInspectorTab();
            Log("Inspector opened");
        }

        private void CloseInspector()
        {
            InspectorPane.Visibility = Visibility.Collapsed;
            InspectorMenuItem.IsChecked = false;
            if (!startPageShown) EditorView.Focus(FocusState.Programmatic);
        }

        private void InspectorTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args) => ShowInspectorTab();

        private void ShowInspectorTab()
        {
            var review = InspectorOnReview;
            InspectorOutlinePanel.Visibility = review ? Visibility.Collapsed : Visibility.Visible;
            InspectorReviewPanel.Visibility = review ? Visibility.Visible : Visibility.Collapsed;
            if (review) RefreshInspectorReview();
            else UpdateInspectorOutlineEmpty();
        }

        private void UpdateInspectorOutlineEmpty() =>
            InspectorOutlineEmptyText.Visibility = tocEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Called with the text of the note whenever it changes or another one is loaded; the Review list follows a moment later, once.
        private void InspectorTextChanged(string text)
        {
            inspectorText = text;
            if (!InspectorShown || !InspectorOnReview) return;
            if (inspectorTimer == null)
            {
                inspectorTimer = DispatcherQueue.CreateTimer();
                inspectorTimer.Interval = TimeSpan.FromMilliseconds(300);
                inspectorTimer.IsRepeating = false;
                inspectorTimer.Tick += (s, e) => { if (InspectorShown && InspectorOnReview) RefreshInspectorReview(); };
            }
            inspectorTimer.Stop();
            inspectorTimer.Start();
        }

        private void RefreshInspectorReview()
        {
            var items = ReviewList.Build(inspectorText);
            inspectorReview.Clear();
            foreach (var item in items)
            {
                inspectorReview.Add(new ReviewListEntry
                {
                    Label = Locale.GetString(item.Kind switch
                    {
                        "add" => "InspectorKindAdded",
                        "del" => "InspectorKindDeleted",
                        "sub" => "InspectorKindReplaced",
                        "mark" => "InspectorKindHighlight",
                        _ => "InspectorKindComment",
                    }),
                    Meta = item.Meta,
                    Snippet = item.Snippet,
                    Raw = item.Raw,
                    Nth = item.Nth,
                    Tint = ReviewListEntry.TintOf(item.Kind),
                });
            }
            var changes = items.Count(i => i.Kind is "add" or "del" or "sub");
            var comments = items.Count(i => i.Kind == "comment");
            var highlights = items.Count - changes - comments;
            var summary = string.Format(Locale.GetString("InspectorReviewSummary"), changes, comments);
            if (highlights > 0) summary += " · " + string.Format(Locale.GetString("InspectorReviewSummaryHighlights"), highlights);
            InspectorReviewSummaryText.Text = items.Count == 0 ? "" : summary;
            InspectorReviewEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InspectorReviewListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReviewListEntry entry)
                PostMessage("ScrollToText", new { text = entry.Raw, nth = entry.Nth });
        }
    }
}
