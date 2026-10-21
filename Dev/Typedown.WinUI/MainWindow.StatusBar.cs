using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the live status bar (the UX proposal "Live status bar"). The bar says only what there is to say about the
    // note: when it was saved (or that it is not), its size, the time it takes to read, the time it takes to speak (in Speech mode), and the
    // review counts (when the note has changes, highlights or comments), then its format and the view switch on the right. A segment that has
    // nothing to say is not there, so the bar stays quiet for a plain note. The ones that tell more are buttons: they open the inspector pane
    // on the tab (History, Speech, Review) that shows the rest.
    public sealed partial class MainWindow
    {
        private DispatcherQueueTimer statusReviewTimer;

        private void SetStatusSegment(Button button, string text)
        {
            button.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            if (string.IsNullOrEmpty(text)) return;
            button.Content = text;
            AutomationProperties.SetName(button, text);
        }

        // "Saved 14:32" (when the file was last written), or "● Unsaved"; nothing for a new, empty note.
        private void UpdateStatusSaved()
        {
            string text = null;
            if (file != null && !startPageShown)
            {
                if (file.IsDirty) text = Locale.GetString("StatusUnsaved");
                else if (file.SavedAt is DateTime at && !string.IsNullOrEmpty(file.FilePath)) text = Locale.Format("StatusSaved", at.ToString("t"));
            }
            SetStatusSegment(StatusSavedButton, text);
        }

        // "3 min read": the words of the note at a steady speed (called with the word count).
        private void UpdateStatusReading()
        {
            var words = lastWordCount?["word"]?.ToObject<int>() ?? 0;
            if (words <= 0 || startPageShown)
            {
                StatusReadingText.Visibility = Visibility.Collapsed;
                return;
            }
            var minutes = StatusFacts.ReadingMinutes(words);
            StatusReadingText.Text = minutes < 1 ? Locale.GetString("StatusReadingUnder") : Locale.Format("StatusReading", minutes);
            StatusReadingText.Visibility = Visibility.Visible;
        }

        // "Estimated 2:36 of 2:00 ●" in Speech mode (the figures are the Speech tab's own); the dot is the light of the budget.
        private void UpdateStatusSpeaking()
        {
            var data = speechTiming;
            var words = data?["words"]?.ToObject<int>() ?? 0;
            if (!settings.SpeechMode || data == null || words == 0 || startPageShown)
            {
                StatusSpeakingButton.Visibility = Visibility.Collapsed;
                return;
            }
            var line = SpeechSummaryLine(data);
            var content = new TextBlock { FontSize = 12 };
            content.Inlines.Add(new Run { Text = line });
            var light = data["light"]?.ToString() ?? "";
            if (light.Length > 0) content.Inlines.Add(new Run { Text = " ●", Foreground = SpeechLightBrush(light) });
            StatusSpeakingButton.Content = content;
            AutomationProperties.SetName(StatusSpeakingButton, line + (light.Length > 0 ? ", " + SpeechBudgetText(data) : ""));
            StatusSpeakingButton.Visibility = Visibility.Visible;
        }

        // "Changes: 3 · Comments: 1" when the note has marks, from the same list the Review tab shows; looked at again a moment after the text changes.
        private void ScheduleStatusReview()
        {
            if (statusReviewTimer == null)
            {
                statusReviewTimer = DispatcherQueue.CreateTimer();
                statusReviewTimer.Interval = TimeSpan.FromMilliseconds(400);
                statusReviewTimer.IsRepeating = false;
                statusReviewTimer.Tick += (s, e) => UpdateStatusReview();
            }
            statusReviewTimer.Stop();
            statusReviewTimer.Start();
        }

        private void UpdateStatusReview()
        {
            var items = ReviewList.Build(inspectorText);
            SetStatusSegment(StatusReviewButton, items.Count == 0 || startPageShown ? null : ReviewSummaryText(items));
        }

        // The format on the right: "Markdown · UTF-8 · LF"; a file read in the legacy code page, or with CRLF or CR line ends, says what
        // saving changes it to (Caret writes UTF-8 and LF).
        private void UpdateEncodingStatus()
        {
            var legacy = file.LegacyEncodingName;
            var lineEnding = file.LegacyLineEnding;
            var parts = new List<string>();
            if (legacy == null)
            {
                parts.Add(Locale.GetString("Markdown"));
                parts.Add("UTF-8");
            }
            else
            {
                parts.Add(Locale.Format("StatusLegacyEncoding", legacy));   // "Markdown · Windows-1250"
            }
            parts.Add(lineEnding == null ? "LF" : lineEnding + " → LF");
            StatusBarFormatText.Text = string.Join(" · ", parts);
            var tips = new List<string>();
            if (legacy != null) tips.Add(Locale.Format("StatusLegacyEncodingTip", legacy));
            if (lineEnding != null) tips.Add(Locale.Format("StatusLineEndingTip", lineEnding));
            ToolTipService.SetToolTip(StatusBarFormatText, tips.Count == 0 ? null : string.Join("\n", tips));
        }

        // Everything the bar says about the note, looked at again (a note was opened, saved, or switched to).
        private void UpdateStatusBar()
        {
            // Home says nothing about the note it covers: no count, no format.
            StatusBarWordCountButton.Visibility = StatusBarFormatText.Visibility = startPageShown ? Visibility.Collapsed : Visibility.Visible;
            UpdateStatusSaved();
            UpdateEncodingStatus();
            UpdateStatusReading();
            UpdateStatusSpeaking();
            UpdateStatusReview();
        }

        // The inspector pane, opened (when it is not) on the tab that tells more.
        private void OpenInspectorOn(SelectorBarItem tab)
        {
            var wasShown = InspectorShown;
            if (!wasShown) OpenInspector();
            tab.IsSelected = true;
            // a pane that has only just appeared has not been laid out: the choice is made again once it has
            if (!wasShown) DispatcherQueue.TryEnqueue(() => { tab.IsSelected = true; });
        }

        private void StatusSavedButton_Click(object sender, RoutedEventArgs e) => OpenInspectorOn(InspectorHistoryTab);

        private void StatusSpeakingButton_Click(object sender, RoutedEventArgs e) => OpenInspectorOn(InspectorSpeechTab);

        private void StatusReviewButton_Click(object sender, RoutedEventArgs e) => OpenInspectorOn(InspectorReviewTab);
    }
}
