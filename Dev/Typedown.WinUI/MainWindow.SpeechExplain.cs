using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace Typedown.WinUI
{
    // New since the fork: the explanation for an AI (docs/speech-marks-design.md, 6.3), two buttons in the Speech card and in
    // Settings > Speech marks. A speaker who pastes a talk with delivery marks into an AI should not have to hope it works out what
    // the braces mean: "Copy for AI" puts on the clipboard a text that says what the speech is, how the marks are written,
    // what every mark of Caret and of the speaker's own library means and what the planned times are, and then the speech;
    // "Copy explanation only" is the same without the speech, to paste once and then paste texts after it. The text is made
    // by the page (Muya/lib/parser/speechExplain.js, tested alone) from the same lists as the Speech card. Caret sends
    // nothing anywhere: the text only goes to the clipboard, and the speaker pastes it where they want.
    public sealed partial class MainWindow
    {
        private TextBlock speechExplainStatus;

        private void SpeechExplainUsedOnlyToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!suppressSettingsEvents) settings.SpeechExplainUsedOnly = SpeechExplainUsedOnlyToggle.IsOn;
        }

        private void SpeechExplainAsWordsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!suppressSettingsEvents) settings.SpeechExplainAsWords = SpeechExplainAsWordsToggle.IsOn;
        }

        private void SpeechExplainRehearsalToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!suppressSettingsEvents) settings.SpeechExplainRehearsal = SpeechExplainRehearsalToggle.IsOn;
        }

        private async void SpeechCopyForAiButton_Click(object sender, RoutedEventArgs e) => await CopySpeechExplanation(true, true);

        private async void SpeechCopyExplanationButton_Click(object sender, RoutedEventArgs e) => await CopySpeechExplanation(false, true);

        // The two buttons of the Speech card.
        private StackPanel BuildSpeechExplainPanel()
        {
            var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
            panel.Children.Add(new TextBlock { Text = Locale.GetString("SpeechExplainCardHeader"), FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 2, 0, 0) });
            var buttons = new SpeechWrapPanel { Spacing = 4 };
            buttons.Children.Add(SpeechCommandButton(Locale.GetString("SpeechCopyForAi"), async () => await CopySpeechExplanation(true, false)));
            buttons.Children.Add(SpeechCommandButton(Locale.GetString("SpeechCopyExplanation"), async () => await CopySpeechExplanation(false, false)));
            panel.Children.Add(buttons);
            // the answer is under the buttons, where the eyes are (the status line of the card is at its top)
            speechExplainStatus = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(speechExplainStatus, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            panel.Children.Add(speechExplainStatus);
            return panel;
        }

        private static Button SpeechCommandButton(string label, Func<Task> action)
        {
            var button = new Button { Content = label, Padding = new Thickness(8, 3, 8, 3), MinHeight = 0, FontSize = 12 };
            button.Click += async (s, e) => await action();
            return button;
        }

        private async Task ShowSpeechExplainStatus(string message)
        {
            if (speechExplainStatus == null) return;
            speechExplainStatus.Text = message;
            speechExplainStatus.Visibility = Visibility.Visible;
            await Task.Delay(6000);
            if (speechExplainStatus.Text == message) speechExplainStatus.Visibility = Visibility.Collapsed;
        }

        // Asks the page for the text and puts it on the clipboard (where it stays after Caret closes).
        private async Task CopySpeechExplanation(bool withText, bool fromSettings)
        {
            string message;
            try
            {
                // the last run of the rehearse mode of the teleprompter, from the file beside the speech
                var rehearsal = settings.SpeechExplainRehearsal && !startPageShown && activeDoc != null ? SpeechRehearsal.LastRunFor(activeDoc.Path) : "";
                var options = new { withText, usedOnly = settings.SpeechExplainUsedOnly, asWords = settings.SpeechExplainAsWords, rehearsal };
                var literal = JsonConvert.SerializeObject(JsonConvert.SerializeObject(options));
                var answer = await RunInPage($"window.__caretSpeech?window.__caretSpeech.explain({literal}):null");
                var text = string.IsNullOrEmpty(answer) || answer == "null" ? null : JsonConvert.DeserializeObject<string>(answer);
                if (string.IsNullOrEmpty(text))
                {
                    message = Locale.GetString("SpeechExplainFailed");
                }
                else
                {
                    var package = new DataPackage();
                    package.SetText(text);
                    Clipboard.SetContent(package);
                    try { Clipboard.Flush(); } catch { }
                    message = Locale.Format("SpeechExplainCopied", text.Length.ToString());
                }
            }
            catch (Exception ex)
            {
                Log($"Speech: the explanation was not copied: {ex.Message}");
                message = Locale.GetString("SpeechExplainFailed");
            }
            if (fromSettings) ShowSpeechSettingsStatus(message);
            else await ShowSpeechExplainStatus(message);
        }
    }
}
