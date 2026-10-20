using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the shape of the talk and the hints (docs/speech-marks-design.md, 5.6), part of the timing panel of
    // the Speech card. The page works out both (Muya/lib/parser/speechTiming.js and speechHints.js, tested alone) and sends
    // them with the time; this draws them. Nothing here is advice on style: the bars show what is in the text, the hints
    // say plain facts, and a click on either goes to the place in the text.
    public sealed partial class MainWindow
    {
        private const double ShapeWidth = 200;
        private const double ShapeHeight = 150;

        private static Brush AccentBrush() =>
            Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accent) && accent is Brush brush
                ? brush
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x8B, 0x4A, 0x27));

        // Scrolls the editor to a paragraph or a heading and puts the caret there.
        private async Task SpeechGoTo(string prefix, int nth)
        {
            if (string.IsNullOrEmpty(prefix)) return;
            try
            {
                var found = await RunInPage($"window.__caretSpeech&&window.__caretSpeech.goTo({JsonConvert.SerializeObject(prefix)},{nth})");
                if (found != "true") Log($"Speech: no place found for \"{prefix}\" #{nth} (the page said {found})");
            }
            catch (Exception ex)
            {
                Log($"Speech: could not go to the paragraph: {ex.Message}");
            }
        }

        // The talk from top to bottom: a bar for each paragraph, as tall as it takes. Left or right of the middle is its pace
        // against yours (left slower, right faster), how dark it is is how loud, and the ticks on the right are its pauses (grey)
        // and the time given to the audience (accent colour).
        private void RenderSpeechShape(Canvas canvas, JArray bars)
        {
            canvas.Children.Clear();
            var list = (bars ?? new JArray()).ToList();
            var total = list.Sum(b => b["seconds"]?.ToObject<double>() ?? 0);
            ToolTipService.SetToolTip(canvas, null);
            AutomationProperties.SetName(canvas, Locale.Format("SpeechShapeName", list.Count.ToString()));
            if (list.Count == 0 || total <= 0) return;
            var accent = AccentBrush();
            var quiet = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            const double barWidth = 70;
            var done = 0.0;
            foreach (var bar in list)
            {
                var seconds = bar["seconds"]?.ToObject<double>() ?? 0;
                var speed = bar["speed"]?.ToObject<double>() ?? 1;
                var volume = bar["volume"]?.ToObject<double>() ?? 0;
                var pause = bar["pause"]?.ToObject<double>() ?? 0;
                var audience = bar["audience"]?.ToObject<double>() ?? 0;
                var top = ShapeHeight * done / total;
                var height = Math.Max(2, ShapeHeight * seconds / total - 1);
                done += seconds;
                var offset = Math.Clamp((speed - 1) / 0.5, -1, 1) * 45;
                var shown = new Rectangle
                {
                    Width = barWidth,
                    Height = height,
                    Fill = accent,
                    Opacity = Math.Clamp(0.55 + (volume >= 0 ? 0.45 : 0.3) * volume, 0.2, 1),
                    RadiusX = 1.5,
                    RadiusY = 1.5,
                };
                Canvas.SetLeft(shown, 100 - barWidth / 2 + offset);
                Canvas.SetTop(shown, top);
                var words = bar["words"]?.ToObject<int>() ?? 0;
                var text = Locale.Format("SpeechShapeBar", bar["clock"]?.ToString() ?? "", words.ToString());
                ToolTipService.SetToolTip(shown, text);
                AutomationProperties.SetName(shown, text);
                var prefix = bar["prefix"]?.ToString() ?? "";
                var nth = bar["nth"]?.ToObject<int>() ?? 0;
                shown.Tapped += async (s, e) => await SpeechGoTo(prefix, nth);
                canvas.Children.Add(shown);
                if (pause > 0.05)
                {
                    var tick = new Rectangle { Width = Math.Clamp(pause * 1.5, 2, 12), Height = height, Fill = quiet };
                    Canvas.SetLeft(tick, ShapeWidth - 14);
                    Canvas.SetTop(tick, top);
                    canvas.Children.Add(tick);
                }
                if (audience > 0.05)
                {
                    var tick = new Rectangle { Width = Math.Clamp(audience * 1.5, 2, 12), Height = height, Fill = accent };
                    Canvas.SetLeft(tick, ShapeWidth - 28);
                    Canvas.SetTop(tick, top);
                    canvas.Children.Add(tick);
                }
            }
        }

        private string SpeechHintText(JToken hint)
        {
            string T(string key) => Locale.GetString(key);
            var clock = hint["clock"]?.ToString() ?? "";
            var word = hint["word"]?.ToString() ?? "";
            var shown = (hint["closer"]?.ToObject<bool>() ?? false) ? "{/" + word + "}" : "{" + word + "}";
            switch (hint["kind"]?.ToString())
            {
                case "over":
                    if (hint["whole"]?.ToObject<bool>() ?? false) return Locale.Format("SpeechHintOverWhole", clock);
                    var title = hint["title"]?.ToString();
                    return Locale.Format("SpeechHintOver", string.IsNullOrEmpty(title) ? "…" : title, clock);
                case "joke": return T("SpeechHintJoke");
                case "fast": return Locale.Format("SpeechHintFast", clock);
                case "long": return Locale.Format("SpeechHintLong", clock);
                case "unknown":
                    return SpeechLibraryMarks.Any(m => string.Equals(m.Name, word, StringComparison.OrdinalIgnoreCase))
                        ? Locale.Format("SpeechHintLibrary", shown)
                        : Locale.Format("SpeechHintUnknown", shown);
                case "twice": return Locale.Format("SpeechHintTwice", hint["raw"]?.ToString() ?? "");
                case "baddef": return Locale.Format("SpeechHintBadDefinition", hint["raw"]?.ToString() ?? "");
                default: return "";
            }
        }

        // The hints: plain facts, each with a click that goes to the place; a word of the library that the document does not
        // define has a button that adds its definition.
        private void RenderSpeechHints(JArray hints) =>
            ((TextBlock)speechHintsExpander.Header).Text = Locale.Format("SpeechHintsHeader", FillSpeechHints(speechHintsList, hints).ToString());

        // Fills the list and says how many hints there are.
        private int FillSpeechHints(StackPanel target, JArray hints)
        {
            target.Children.Clear();
            var list = (hints ?? new JArray()).ToList();
            if (list.Count == 0)
            {
                target.Children.Add(new TextBlock { Text = Locale.GetString("SpeechHintsNone"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });
                return 0;
            }
            foreach (var hint in list)
            {
                var text = SpeechHintText(hint);
                if (text.Length == 0) continue;
                var row = new StackPanel { Spacing = 2, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                row.Children.Add(new TextBlock { Text = "• " + text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
                AutomationProperties.SetName(row, text);
                var prefix = hint["prefix"]?.ToString() ?? "";
                var nth = hint["nth"]?.ToObject<int>() ?? 0;
                row.Tapped += async (s, e) => await SpeechGoTo(prefix, nth);
                var word = hint["word"]?.ToString() ?? "";
                var mark = hint["kind"]?.ToString() == "unknown"
                    ? SpeechLibraryMarks.FirstOrDefault(m => string.Equals(m.Name, word, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (mark != null)
                {
                    var add = new Button { Content = Locale.GetString("SpeechHintAddDefinition"), FontSize = 12, Padding = new Thickness(8, 2, 8, 2), MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Left };
                    var line = SpeechLibrary.DefinitionLine(mark);
                    add.Click += async (s, e) => await RunInPage($"window.__caretSpeech&&window.__caretSpeech.addDefinition({JsonConvert.SerializeObject(line)})");
                    row.Children.Add(add);
                }
                target.Children.Add(row);
            }
            return list.Count;
        }
    }
}
