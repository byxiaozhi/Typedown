using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the timing panel of the Speech card (docs/speech-marks-design.md, 4 and 5.6): how long the talk takes
    // against its budget, the words per minute, and one row per section with its light. The page does the arithmetic (it
    // reads the marks: Muya/lib/parser/speechTiming.js, tested alone) and sends the figures, already as m:ss, when the text
    // has stopped changing; this only shows them. Nothing here is a measurement: it is words and seconds worked out from the
    // text, and the panel says so.
    public sealed partial class MainWindow
    {
        private TextBlock speechTimingSummary;
        private TextBlock speechTimingBudget;
        private StackPanel speechTimingRows;
        private NumberBox speechWpmBox;
        private CheckBox speechHeadingsBox;
        private TextBlock speechTimingEmpty;
        private Expander speechHintsExpander;
        private StackPanel speechHintsList;
        private Canvas speechShapeCanvas;
        private TextBlock speechShapeLegend;
        private DispatcherTimer speechWpmTimer;
        private bool suppressSpeechWpm;
        private double speechWpmChosen;
        private JToken speechTiming;

        // The panel at the top of the card. Built with the card; the figures come from the page.
        private StackPanel BuildSpeechTimingPanel()
        {
            string T(string key) => Locale.GetString(key);
            var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 6) };
            panel.Children.Add(new TextBlock { Text = T("SpeechTimingHeader"), FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 8, 0, 0) });
            speechTimingSummary = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            speechTimingBudget = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            speechTimingEmpty = new TextBlock { Text = T("SpeechTimingEmpty"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(speechTimingSummary);
            panel.Children.Add(speechTimingBudget);
            panel.Children.Add(speechTimingEmpty);

            speechWpmBox = new NumberBox
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
            speechWpmBox.ValueChanged += SpeechTimingWpmBox_ValueChanged;
            speechHeadingsBox = new CheckBox { Content = T("SpeechTimingHeadings"), IsChecked = settings.SpeechHeadingsSpoken, FontSize = 12, MinHeight = 0 };
            speechHeadingsBox.Click += (s, e) => { settings.SpeechHeadingsSpoken = speechHeadingsBox.IsChecked == true; RenderInspectorSpeech(); };
            // the pace, the headings and the sections are in a part that opens, so the card stays short
            var details = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            details.Children.Add(speechWpmBox);
            details.Children.Add(speechHeadingsBox);
            speechTimingRows = new StackPanel { Spacing = 2 };
            details.Children.Add(new ScrollViewer
            {
                Content = speechTimingRows,
                MaxHeight = 170,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
            });
            details.Children.Add(new TextBlock { Text = T("SpeechTimingNote"), FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Expander Part(string header, UIElement content) => new()
            {
                Header = new TextBlock { Text = header, FontSize = 12 },
                Content = content,
                IsExpanded = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 4, 8, 8),
            };
            panel.Children.Add(Part(T("SpeechTimingDetails"), details));

            // the shape of the talk: one bar per paragraph, as tall as it is long
            speechShapeCanvas = new Canvas { Width = ShapeWidth, Height = ShapeHeight, HorizontalAlignment = HorizontalAlignment.Left };
            speechShapeLegend = new TextBlock { Text = T("SpeechShapeLegend"), FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            var shape = new StackPanel { Spacing = 2 };
            shape.Children.Add(new Border { Child = speechShapeCanvas, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)), HorizontalAlignment = HorizontalAlignment.Left });
            shape.Children.Add(speechShapeLegend);
            panel.Children.Add(Part(T("SpeechShapeHeader"), shape));

            // plain facts about the talk
            speechHintsList = new StackPanel { Spacing = 6 };
            speechHintsExpander = Part(Locale.Format("SpeechHintsHeader", "0"), speechHintsList);
            panel.Children.Add(speechHintsExpander);
            RenderSpeechTiming();
            return panel;
        }

        private void UpdateSpeechTiming(JToken args)
        {
            speechTiming = args;
            RenderSpeechTiming();
            RenderInspectorSpeech();
            UpdateStatusSpeaking();
        }

        // The words per minute are written into the document ({wpm N}) a moment after the last change of the box.
        private void SpeechTimingWpmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (suppressSpeechWpm || double.IsNaN(args.NewValue)) return;
            speechWpmChosen = args.NewValue;
            speechWpmTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            speechWpmTimer.Tick -= WriteSpeechWpm;
            speechWpmTimer.Tick += WriteSpeechWpm;
            speechWpmTimer.Stop();
            speechWpmTimer.Start();
        }

        private async void WriteSpeechWpm(object sender, object e)
        {
            speechWpmTimer.Stop();
            try
            {
                var value = (int)Math.Round(speechWpmChosen);
                await RunInPage($"window.__caretSpeech?window.__caretSpeech.setWpm({value}):null");
            }
            catch (Exception ex)
            {
                Log($"Speech: the words per minute were not written: {ex.Message}");
            }
        }

        private static Brush SpeechLightBrush(string light) => new SolidColorBrush(light switch
        {
            "green" => Windows.UI.Color.FromArgb(255, 0x2E, 0x8B, 0x4E),
            "amber" => Windows.UI.Color.FromArgb(255, 0xC4, 0x7A, 0x00),
            _ => Windows.UI.Color.FromArgb(255, 0xCF, 0x22, 0x2E),
        });

        // "over by 0:20" or "0:40 to spare", with the light in front of it; the words say it, the colour only helps.
        private string SpeechBudgetText(JToken x) =>
            (x["over"]?.ToObject<double>() ?? 0) > 0
                ? Locale.Format("SpeechTimingOver", x["overClock"]?.ToString())
                : Locale.Format("SpeechTimingLeft", x["overClock"]?.ToString());

        // The time against the budget as two lines: "2:10 of 5:00" with the words in the tooltip, and "● 2:50 to spare" in the light's colour.
        private static string SpeechSummaryLine(JToken data)
        {
            var clock = data["clock"]?.ToString() ?? "0:00";
            var budgetClock = data["budgetClock"]?.ToString() ?? "";
            return budgetClock.Length > 0 ? Locale.Format("SpeechTimingLineBudget", clock, budgetClock) : Locale.Format("SpeechTimingLine", clock);
        }

        private void ApplySpeechSummary(TextBlock summary, TextBlock budget, JToken data)
        {
            var words = data["words"]?.ToObject<int>() ?? 0;
            summary.Text = SpeechSummaryLine(data);
            ToolTipService.SetToolTip(summary, Locale.Format("SpeechTimingWords", words.ToString()));
            var light = data["light"]?.ToString() ?? "";
            if (light.Length > 0)
            {
                budget.Text = "● " + SpeechBudgetText(data);
                budget.Foreground = SpeechLightBrush(light);
                budget.Visibility = Visibility.Visible;
            }
            else
            {
                budget.Visibility = Visibility.Collapsed;
            }
        }

        // Both boxes (the card's and the inspector's) show the pace in force; they are set here without writing it again.
        private void ShowSpeechWpm(double wpm)
        {
            suppressSpeechWpm = true;
            foreach (var box in new[] { speechWpmBox, inspectorWpmBox })
                if (box != null && Math.Abs(box.Value - wpm) > 0.01 && box.FocusState == FocusState.Unfocused) box.Value = wpm;
            suppressSpeechWpm = false;
        }

        // One row of the sections list: the title, its time and its light; a click goes to the heading.
        private Button BuildSpeechSectionRow(JToken section)
        {
            var title = section["title"]?.ToString() ?? "";
            var level = Math.Max(1, section["level"]?.ToObject<int>() ?? 1);
            var sectionClock = section["clock"]?.ToString() ?? "";
            var sectionLight = section["light"]?.ToString() ?? "";
            var hasBudget = sectionLight.Length > 0;
            var line = hasBudget
                ? $"{title}: {Locale.Format("SpeechTimingLineBudget", sectionClock, section["budgetClock"]?.ToString())}, {SpeechBudgetText(section)}"
                : $"{title}: {sectionClock}";
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock
            {
                Text = title.Length > 0 ? title : "…",
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness((level - 1) * 8, 0, 0, 0),
            };
            var time = new TextBlock { Text = sectionClock, FontSize = 12, FontFamily = new FontFamily("Segoe UI Variable Text") };
            Grid.SetColumn(time, 1);
            row.Children.Add(name);
            row.Children.Add(time);
            if (hasBudget)
            {
                var dot = new TextBlock { Text = "●", FontSize = 11, Foreground = SpeechLightBrush(sectionLight) };
                Grid.SetColumn(dot, 2);
                row.Children.Add(dot);
            }
            // a press on a row goes to the heading
            var button = SpeechTarget(row, line, section["prefix"]?.ToString() ?? "", section["nth"]?.ToObject<int>() ?? 0);
            ToolTipService.SetToolTip(button, line);
            return button;
        }

        private void RenderSpeechTiming()
        {
            if (speechTimingSummary == null) return;
            var data = speechTiming;
            var words = data?["words"]?.ToObject<int>() ?? 0;
            var empty = data == null || (words == 0 && ((data["sections"] as JArray)?.Count ?? 0) == 0);
            speechTimingEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            speechTimingSummary.Visibility = speechTimingBudget.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            speechTimingRows.Children.Clear();
            if (data == null) { RenderSpeechShape(speechShapeCanvas, null); RenderSpeechHints(null); return; }
            RenderSpeechShape(speechShapeCanvas, data["shape"] as JArray);
            RenderSpeechHints(data["hints"] as JArray);

            ApplySpeechSummary(speechTimingSummary, speechTimingBudget, data);

            // the box shows the pace in force; it is changed here without writing it again
            ShowSpeechWpm(data["wpm"]?.ToObject<double>() ?? 130);

            foreach (var section in data["sections"] as JArray ?? new JArray())
                speechTimingRows.Children.Add(BuildSpeechSectionRow(section));
        }
    }
}
