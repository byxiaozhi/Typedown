using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the Speech tab of the inspector pane. The same figures as the timing panel of the Speech card (the page
    // works them out and sends them, see MainWindow.SpeechTiming.cs), laid out for the narrow column beside the note: the time
    // against the budget, the pace, one row per section, the shape of the talk and the hints. Every click goes to the place in
    // the text. The page only works the figures out while Speech mode is on, so the tab has the switch at the top.
    public sealed partial class MainWindow
    {
        private NumberBox inspectorWpmBox;
        private CheckBox inspectorHeadingsBox;
        private ToggleSwitch inspectorSpeechToggle;
        private StackPanel inspectorSpeechOff;
        private StackPanel inspectorSpeechOn;
        private TextBlock inspectorSpeechSummary;
        private TextBlock inspectorSpeechBudget;
        private TextBlock inspectorSpeechEmpty;
        private StackPanel inspectorSpeechSections;
        private Canvas inspectorSpeechCanvas;
        private TextBlock inspectorSpeechHintsHeader;
        private StackPanel inspectorSpeechHints;
        private bool inspectorSpeechSyncing;

        private void BuildInspectorSpeechPanel()
        {
            string T(string key) => Locale.GetString(key);
            var stack = new StackPanel { Spacing = 6, Margin = new Thickness(16, 10, 16, 16) };

            inspectorSpeechToggle = new ToggleSwitch { Header = T("SpeechModeMenuItem_Text"), OnContent = T("ToggleOn"), OffContent = T("ToggleOff") };
            inspectorSpeechToggle.Toggled += InspectorSpeechToggle_Toggled;
            stack.Children.Add(inspectorSpeechToggle);

            inspectorSpeechOff = new StackPanel();
            inspectorSpeechOff.Children.Add(new TextBlock { Text = T("SpeechNavIntro"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(inspectorSpeechOff);

            inspectorSpeechOn = new StackPanel { Spacing = 6 };
            inspectorSpeechSummary = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            inspectorSpeechBudget = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            inspectorSpeechEmpty = new TextBlock { Text = T("SpeechTimingEmpty"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
            inspectorSpeechOn.Children.Add(inspectorSpeechSummary);
            inspectorSpeechOn.Children.Add(inspectorSpeechBudget);
            inspectorSpeechOn.Children.Add(inspectorSpeechEmpty);

            inspectorWpmBox = new NumberBox
            {
                Header = T("SpeechTimingWpm"),
                Minimum = 40,
                Maximum = 400,
                SmallChange = 5,
                LargeChange = 20,
                Value = settings.SpeechWpm,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                FontSize = 12,
                Width = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            inspectorWpmBox.ValueChanged += SpeechTimingWpmBox_ValueChanged;
            inspectorHeadingsBox = new CheckBox { Content = T("SpeechTimingHeadings"), IsChecked = settings.SpeechHeadingsSpoken, FontSize = 12, MinHeight = 0 };
            inspectorHeadingsBox.Click += (s, e) =>
            {
                settings.SpeechHeadingsSpoken = inspectorHeadingsBox.IsChecked == true;
                if (speechHeadingsBox != null) speechHeadingsBox.IsChecked = settings.SpeechHeadingsSpoken;
            };
            inspectorSpeechOn.Children.Add(inspectorWpmBox);
            inspectorSpeechOn.Children.Add(inspectorHeadingsBox);

            inspectorSpeechSections = new StackPanel { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
            inspectorSpeechOn.Children.Add(inspectorSpeechSections);

            inspectorSpeechCanvas = new Canvas { Width = ShapeWidth, Height = ShapeHeight, HorizontalAlignment = HorizontalAlignment.Left };
            inspectorSpeechOn.Children.Add(new TextBlock { Text = T("SpeechShapeHeader"), FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 10, 0, 0) });
            inspectorSpeechOn.Children.Add(new Border
            {
                Child = inspectorSpeechCanvas,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
            inspectorSpeechOn.Children.Add(new TextBlock { Text = T("SpeechShapeLegend"), FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });

            inspectorSpeechHintsHeader = new TextBlock { FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 10, 0, 0) };
            inspectorSpeechHints = new StackPanel { Spacing = 6 };
            inspectorSpeechOn.Children.Add(inspectorSpeechHintsHeader);
            inspectorSpeechOn.Children.Add(inspectorSpeechHints);
            inspectorSpeechOn.Children.Add(new TextBlock { Text = T("SpeechTimingNote"), FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            stack.Children.Add(inspectorSpeechOn);

            InspectorSpeechPanel.Children.Add(new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
            });
        }

        // The tab is built the first time it is shown.
        private void ShowInspectorSpeech()
        {
            if (inspectorSpeechToggle == null) BuildInspectorSpeechPanel();
            RenderInspectorSpeech();
        }

        // The switch at the top is the View menu's Speech mode, so it is set here the same way the menu does it.
        private void InspectorSpeechToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (inspectorSpeechSyncing) return;
            settings.SpeechMode = inspectorSpeechToggle.IsOn;
            SpeechModeMenuItem.IsChecked = settings.SpeechMode;
            UpdateSpeechCard();
            SyncSpeechNavToggle();
            Log($"SpeechMode: {settings.SpeechMode}");
            RenderInspectorSpeech();
        }

        // Shows the state of the setting and the last figures from the page; called when either changes.
        private void RenderInspectorSpeech()
        {
            if (inspectorSpeechToggle == null) return;
            inspectorSpeechSyncing = true;
            inspectorSpeechToggle.IsOn = settings.SpeechMode;
            inspectorHeadingsBox.IsChecked = settings.SpeechHeadingsSpoken;
            inspectorSpeechSyncing = false;
            inspectorSpeechOff.Visibility = settings.SpeechMode ? Visibility.Collapsed : Visibility.Visible;
            inspectorSpeechOn.Visibility = settings.SpeechMode ? Visibility.Visible : Visibility.Collapsed;
            if (!settings.SpeechMode) return;

            var data = speechTiming;
            var words = data?["words"]?.ToObject<int>() ?? 0;
            var empty = data == null || (words == 0 && ((data["sections"] as JArray)?.Count ?? 0) == 0);
            inspectorSpeechEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            inspectorSpeechSummary.Visibility = inspectorSpeechBudget.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            inspectorSpeechSections.Children.Clear();
            if (data == null)
            {
                RenderSpeechShape(inspectorSpeechCanvas, null);
                inspectorSpeechHintsHeader.Text = Locale.Format("SpeechHintsHeader", FillSpeechHints(inspectorSpeechHints, null).ToString());
                return;
            }
            ApplySpeechSummary(inspectorSpeechSummary, inspectorSpeechBudget, data);
            ShowSpeechWpm(data["wpm"]?.ToObject<double>() ?? 130);
            foreach (var section in data["sections"] as JArray ?? new JArray())
                inspectorSpeechSections.Children.Add(BuildSpeechSectionRow(section));
            RenderSpeechShape(inspectorSpeechCanvas, data["shape"] as JArray);
            inspectorSpeechHintsHeader.Text = Locale.Format("SpeechHintsHeader", FillSpeechHints(inspectorSpeechHints, data["hints"] as JArray).ToString());
        }
    }
}
