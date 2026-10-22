using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the teleprompter and the speaking clock (docs/speech-marks-design.md, step 2), opened from the View menu
    // or from the Speech card. Each is a window of its own (TeleprompterWindow.cs) that shows the page of the editor's bundle in
    // its teleprompter or clock mode; this file gives it the text of the document it was opened for (and sends it again, a moment
    // after the last change, while the document is edited) with the words of the page in the language of the interface.
    public sealed partial class MainWindow
    {
        private readonly List<(TeleprompterWindow Window, DocumentTab Doc)> teleprompters = new();
        private DispatcherTimer teleprompterTimer;
        private string lastEditorText;

        private async void TeleprompterMenuItem_Click(object sender, RoutedEventArgs e) => await OpenTeleprompter(false);

        private async void SpeakingClockMenuItem_Click(object sender, RoutedEventArgs e) => await OpenTeleprompter(true);

        private async Task OpenTeleprompter(bool clockOnly)
        {
            try
            {
                if (startPageShown) return;
                // the text of the tab on screen, with every edit the editor has made
                await FlushEditor();
                var doc = activeDoc;
                var title = Locale.GetString(clockOnly ? "SpeakingClockWindowTitle" : "TeleprompterWindowTitle") + " - " + doc.DisplayName;
                var window = new TeleprompterWindow(clockOnly, title, () => BuildTeleprompterScript(doc));
                window.Message = (name, args) => TeleprompterMessage(window, doc, name, args);
                teleprompters.Add((window, doc));
                window.Closed += (s, args) => teleprompters.RemoveAll(t => ReferenceEquals(t.Window, window));
                window.Activate();
            }
            catch (Exception ex)
            {
                Log($"Teleprompter: could not open: {ex.Message}");
            }
        }

        private Task<object> BuildTeleprompterScript(DocumentTab doc)
        {
            string T(string key) => Locale.GetString(key);
            var text = ReferenceEquals(doc, activeDoc) && lastEditorText != null ? lastEditorText : doc.File.Markdown ?? "";
            object script = new
            {
                markdown = text,
                wpm = settings.SpeechWpm,
                headingsSpoken = settings.SpeechHeadingsSpoken,
                styles = SpeechLibrary.Styles(SpeechLibraryMarks),
                title = doc.DisplayName,
                labels = new Dictionary<string, string>
                {
                    ["start"] = T("TpStart"), ["stop"] = T("TpStop"), ["elapsed"] = T("TpElapsed"), ["left"] = T("TpLeft"), ["over"] = T("TpOver"),
                    ["planned"] = T("TpPlanned"), ["ahead"] = T("TpAhead"), ["behind"] = T("TpBehind"), ["onPlan"] = T("TpOnPlan"),
                    ["pause"] = T("TpPause"), ["audience"] = T("TpAudience"), ["pauseIn"] = T("TpPauseIn"), ["audienceIn"] = T("TpAudienceIn"),
                    ["auto"] = T("TpAuto"), ["step"] = T("TpStep"), ["next"] = T("TpNext"), ["back"] = T("TpBack"), ["mirror"] = T("TpMirror"), ["flip"] = T("TpFlip"),
                    ["dark"] = T("TpDark"), ["light"] = T("TpLight"), ["fullscreen"] = T("TpFullscreen"), ["clock"] = T("TpClock"),
                    ["speed"] = T("TpSpeed"), ["wpmShort"] = T("TpWpmShort"), ["size"] = T("TpSize"), ["end"] = T("TpEnd"), ["empty"] = T("TpEmpty"), ["waiting"] = T("TpWaiting"),
                    ["help"] = T("TpHelp"), ["section"] = T("TpSection"),
                    ["options"] = T("TpOptions"), ["focus"] = T("TpFocus"), ["countdown"] = T("TpCountdown"), ["off"] = T("TpOff"), ["paceMode"] = T("TpPaceMode"), ["paceFollow"] = T("TpPaceFollow"), ["paceSteady"] = T("TpPaceSteady"), ["width"] = T("TpWidth"), ["spacing"] = T("TpSpacing"), ["colors"] = T("TpColors"), ["paletteStandard"] = T("TpPaletteStandard"), ["paletteYellow"] = T("TpPaletteYellow"),
                    ["paletteGreen"] = T("TpPaletteGreen"), ["paletteWhite"] = T("TpPaletteWhite"), ["paletteBlack"] = T("TpPaletteBlack"), ["resetDisplay"] = T("TpResetDisplay"), ["finishBy"] = T("TpFinishBy"),
                    ["ends"] = T("TpEnds"), ["early"] = T("TpEarly"), ["late"] = T("TpLate"), ["sections"] = T("TpSections"), ["sectionsEmpty"] = T("TpSectionsEmpty"),
                    ["rehearse"] = T("TpRehearse"), ["rehearseArmed"] = T("TpRehearseArmed"), ["rehearsing"] = T("TpRehearsing"),
                    ["rehearsePaused"] = T("TpRehearsePaused"), ["finish"] = T("TpFinish"), ["rehearseHelp"] = T("TpRehearseHelp"),
                    ["doneTitle"] = T("TpDoneTitle"), ["doneNothing"] = T("TpDoneNothing"), ["paceIs"] = T("TpPaceIs"), ["wpm"] = T("TpWpm"),
                    ["paceNone"] = T("TpPaceNone"), ["adopt"] = T("TpAdopt"), ["again"] = T("TpAgain"), ["read"] = T("TpReadIn"), ["close"] = T("TpClose"),
                },
            };
            return Task.FromResult(script);
        }

        // The document is being edited: the windows that show it are sent the new text a moment after the last change.
        private void TeleprompterTextChanged(string text)
        {
            lastEditorText = text;
            if (teleprompters.Count == 0) return;
            teleprompterTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            teleprompterTimer.Tick -= TeleprompterTimer_Tick;
            teleprompterTimer.Tick += TeleprompterTimer_Tick;
            teleprompterTimer.Stop();
            teleprompterTimer.Start();
        }

        private async void TeleprompterTimer_Tick(object sender, object e)
        {
            teleprompterTimer.Stop();
            // every window, not only those of the tab on screen: a tab left within the half second must not keep its old text
            foreach (var (window, _) in teleprompters.ToList())
            {
                await window.PushAsync();
            }
        }

        // The rehearsal (docs/speech-marks-design.md, step 3): the page has worked out the block of Markdown of the run from the keys the
        // speaker pressed. It goes at the end of "<name>.rehearsal.md" beside the speech (a document with no file yet: to the clipboard),
        // and the speaker can take the measured pace as the pace of the talk. Nothing leaves the PC and nothing but that file is written.
        private async Task TeleprompterMessage(TeleprompterWindow window, DocumentTab doc, string name, Newtonsoft.Json.Linq.JToken args)
        {
            try
            {
                switch (name)
                {
                    case "RehearsalDone":
                    {
                        var block = args?["markdown"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(block)) return;
                        string message;
                        var path = doc.Path;
                        if (string.IsNullOrEmpty(path))
                        {
                            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                            package.SetText(block);
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                            try { Windows.ApplicationModel.DataTransfer.Clipboard.Flush(); } catch { }
                            message = Locale.GetString("TpCopiedNoFile");
                        }
                        else
                        {
                            try
                            {
                                var saved = SpeechRehearsal.Save(path, block, "Rehearsals of " + System.IO.Path.GetFileNameWithoutExtension(path));
                                message = Locale.Format("TpSavedIn", System.IO.Path.GetFileName(saved));
                            }
                            catch (Exception ex)
                            {
                                Log($"Speech: the rehearsal was not saved: {ex.Message}");
                                message = Locale.Format("TpNotSaved", ex.Message);
                            }
                        }
                        window.Post("RehearsalSaved", new { message });
                        break;
                    }
                    case "AdoptWpm":
                    {
                        var wpm = (int?)args?["wpm"] ?? 0;
                        string message;
                        if (wpm < 40 || wpm > 400) return;
                        if (!ReferenceEquals(doc, activeDoc) || startPageShown)
                        {
                            message = Locale.GetString("TpAdoptElsewhere");
                        }
                        else
                        {
                            var answer = await RunInPage($"window.__caretSpeech?window.__caretSpeech.setWpm({wpm}):null");
                            message = answer != null && answer.Contains("ok") ? Locale.Format("TpAdopted", wpm.ToString()) : Locale.GetString("TpAdoptFailed");
                        }
                        window.Post("PaceAdopted", new { message });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Teleprompter: a message was not handled: {ex.Message}");
            }
        }

        // The windows go with the one they were opened from.
        private void CloseTeleprompters()
        {
            foreach (var (window, _) in teleprompters.ToList())
            {
                try { window.Close(); } catch { }
            }
            teleprompters.Clear();
        }

        // Two buttons of the Speech card, under the ones for an AI.
        private StackPanel BuildSpeechStagePanel()
        {
            var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
            panel.Children.Add(new TextBlock { Text = Locale.GetString("SpeechStageHeader"), FontSize = 11, Opacity = 0.75, Margin = new Thickness(0, 2, 0, 0) });
            var buttons = new SpeechWrapPanel { Spacing = 4 };
            buttons.Children.Add(SpeechCommandButton(Locale.GetString("TeleprompterButton"), async () => await OpenTeleprompter(false)));
            buttons.Children.Add(SpeechCommandButton(Locale.GetString("SpeakingClockButton"), async () => await OpenTeleprompter(true)));
            panel.Children.Add(buttons);
            return panel;
        }
    }
}
