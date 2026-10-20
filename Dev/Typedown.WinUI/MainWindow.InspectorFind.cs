using System;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the Find tab of the inspector pane. The Find panel (Ctrl+F) stays the place where the term is typed;
    // the tab lists every match of it with the words around it, so that all of them can be seen at once. The editor (the
    // visual one or the Code view) reports the matches with each search, Find next and Replace (MainWindow.xaml.cs,
    // UpdateFindCount); a click on a row makes that match the current one, as Find next would, and the editor shows it.
    public sealed partial class MainWindow
    {
        // What the list shows now: a second report with the same matches only moves the mark (the list keeps its place).
        private string inspectorFindShown = "";

        private bool InspectorOnFind => InspectorTabs.SelectedItem == InspectorFindTab;

        private void ShowInspectorFind() => RenderInspectorFind(null);

        private void InspectorFindOpen_Click(object sender, RoutedEventArgs e) => ShowFindReplace();

        private void InspectorFindListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FrameworkElement { Tag: int index })
                PostMessage("Find", new { action = "goto", index });
        }

        // The editor's report for the term in the box: `args` is the report, or null for "show what the list has now" and
        // for a Find panel that has been closed (the list is emptied).
        private void RenderInspectorFind(JToken args)
        {
            var open = FindReplacePanel.Visibility == Visibility.Visible && FindTextBox.Text.Length > 0;
            InspectorFindEmpty.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            InspectorFindSummary.Visibility = InspectorFindListView.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            if (!open)
            {
                inspectorFindShown = "";
                InspectorFindListView.Items.Clear();
                return;
            }
            if (args == null) return;

            var count = args["count"]?.ToObject<int>() ?? 0;
            var index = args["index"]?.ToObject<int>() ?? -1;
            var matches = args["matches"] as JArray ?? new JArray();
            InspectorFindSummary.Text = count == 0
                ? Locale.GetString("FindNoResults")
                : string.Format(Locale.GetString("InspectorFindMatches"), count)
                    + (count > matches.Count ? " · " + string.Format(Locale.GetString("InspectorFindCapped"), matches.Count, count) : "");

            var key = (args["value"]?.ToString() ?? "") + "\n" + matches.ToString(Newtonsoft.Json.Formatting.None);
            if (key != inspectorFindShown)
            {
                inspectorFindShown = key;
                InspectorFindListView.Items.Clear();
                var number = 0;
                foreach (var match in matches)
                    InspectorFindListView.Items.Add(FindRow(match, number++));
            }
            if (index >= 0 && index < InspectorFindListView.Items.Count)
            {
                InspectorFindListView.SelectedIndex = index;
                InspectorFindListView.ScrollIntoView(InspectorFindListView.Items[index]);
            }
            else
            {
                InspectorFindListView.SelectedIndex = -1;
            }
        }

        // One match: the words before it, the match itself (bold, on the accent colour) and the words after it.
        private static TextBlock FindRow(JToken match, int number)
        {
            var row = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 4, 4, 4),
                Tag = number,
            };
            var before = match["before"]?.ToString() ?? "";
            var found = match["match"]?.ToString() ?? "";
            var after = match["after"]?.ToString() ?? "";
            row.Inlines.Add(new Run { Text = before, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xB3, 0x80, 0x80, 0x80)) });
            row.Inlines.Add(new Run { Text = found, FontWeight = FontWeights.SemiBold, Foreground = AccentBrush() });
            row.Inlines.Add(new Run { Text = after, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xB3, 0x80, 0x80, 0x80)) });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row, before + found + after);
            return row;
        }
    }
}
