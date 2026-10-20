using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.Foundation;

namespace Typedown.WinUI
{
    // New since the fork: speech marks (docs/speech-marks-design.md), delivery marks for a talk written in the text
    // itself: {pause 2s}, {slow}...{/slow}, {tone: dry irony}...{/tone}. The editor reads and draws them
    // (Typedown.Editor, Muya/lib/parser/speech.js) only while Speech mode is on. This file is the switch (View >
    // Speech mode, a setting like Split, off by default, never switched on by opening a file), the Speech card in the
    // sidebar (the list of marks, shown in Speech mode), and Edit > Remove all speech marks. The card only asks the
    // page for a mark (services/speechPage.ts); what is written, and where, is decided there by the rules in
    // speechEdit.js, which are tested alone. The Speech ring (MainWindow.SpeechRing.cs) is made from the same list.
    public sealed partial class MainWindow
    {
        private bool speechCardCollapsed;
        private TextBlock speechDefinedHeader;
        private Panel speechDefinedPanel;
        private DispatcherTimer speechStatusTimer;

        // Words of the built-in set: the card lists the others, the ones the document defines for itself.
        private static readonly HashSet<string> SpeechBuiltInWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "beat", "pause", "wait", "cue", "wpm", "budget", "slow", "fast", "loud", "soft", "emphasis", "tone", "define",
        };

        /// <summary>
        /// Applies the View menu's Speech mode setting and updates the Speech card and sidebar toggle.
        /// </summary>
        private void SpeechModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.SpeechMode = SpeechModeMenuItem.IsChecked;
            UpdateSpeechCard();
            SyncSpeechNavToggle();
            RenderInspectorSpeech();
            Log($"SpeechMode: {settings.SpeechMode}");
        }

        /// <summary>
        /// Applies a sidebar Speech mode change to the setting, View menu and Speech card,
        /// ignoring toggle events raised while settings events are suppressed.
        /// </summary>
        private void SpeechNavModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.SpeechMode = SpeechNavModeToggle.IsOn;
            SpeechModeMenuItem.IsChecked = settings.SpeechMode;
            UpdateSpeechCard();
            RenderInspectorSpeech();
            Log($"SpeechMode: {settings.SpeechMode}");
        }

        /// <summary>
        /// Copies the Speech mode setting to the sidebar toggle while suppressing settings events,
        /// then restores the previous suppression state.
        /// </summary>
        private void SyncSpeechNavToggle()
        {
            var before = suppressSettingsEvents;
            suppressSettingsEvents = true;
            SpeechNavModeToggle.IsOn = settings.SpeechMode;
            suppressSettingsEvents = before;
        }

        private void SpeechHeaderButton_Click(object sender, RoutedEventArgs e)
        {
            speechCardCollapsed = !speechCardCollapsed;
            UpdateSpeechCard();
        }

        private void UpdateSpeechCard()
        {
            SpeechSection.Visibility = settings.SpeechMode ? Visibility.Visible : Visibility.Collapsed;
            SpeechBody.Visibility = speechCardCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SpeechChevron.Glyph = speechCardCollapsed ? "" : "";
        }

        // One button: what it says, what it asks the page for, and the text it writes (the tooltip: the same in every
        // language, as it is in the file).
        private Button SpeechButton(string label, object spec, string written)
        {
            var button = new Button { Content = label, Padding = new Thickness(8, 3, 8, 3), MinHeight = 0, FontSize = 12 };
            ToolTipService.SetToolTip(button, written);
            AutomationProperties.SetName(button, label);
            button.Click += async (s, e) => await InsertSpeechMark(spec);
            return button;
        }

        /// <summary>
        /// Creates a Speech card button from an entry's label, insertion request and tooltip.
        /// </summary>
        private Button SpeechButton(SpeechEntry entry) => SpeechButton(entry.Label, entry.Spec, entry.Tooltip);

        private (TextBlock Header, Panel Buttons) SpeechGroup(string key, IEnumerable<Button> buttons)
        {
            var header = new TextBlock
            {
                Text = Locale.GetString(key),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 2),
                Opacity = 0.75,
            };
            var panel = new SpeechWrapPanel { Spacing = 4 };
            foreach (var button in buttons) panel.Children.Add(button);
            SpeechBody.Children.Add(header);
            SpeechBody.Children.Add(panel);
            return (header, panel);
        }

        // The marks that ship with Caret, and the user's own (Settings > Speech marks), in groups: the one list the Speech
        // card and the Speech ring (MainWindow.SpeechRing.cs) are both made from. The words written into the file for a
        // tone or a cue are in the language of the interface (the keywords never are, so a file can be exchanged): the
        // speaker can change them afterwards. Inside a group the order follows what the marks mean: pace from the slowest
        // to the fastest, time from the shortest to the longest, so a mark of the user's own that is slower than "slow" is
        // listed before it.
        private List<(string Id, List<SpeechEntry> Entries)> SpeechGroups()
        {
            // Looks up a Speech label or note in the current interface language.
            string T(string key) => Locale.GetString(key);
            var pause = T("SpeechLabelPause");
            var library = SpeechLibraryMarks;

            // the entries of a group: the built-in ones with their place, and the library marks that belong to it
            List<SpeechEntry> Listed(string group, params SpeechEntry[] builtIn)
            {
                var entries = builtIn.ToList();
                var number = 0;
                foreach (var mark in library.Where(m => SpeechLibrary.GroupOf(m) == group))
                {
                    // numbers only between marks of the kind the group is about; others after the built-in ones
                    var natural = (group == "pace" && mark.Kind == "pace") || (group == "time" && mark.Kind == "pause");
                    var entry = SpeechLibraryEntry(mark);
                    entry.Key = natural ? (mark.Kind == "pace" ? mark.Percent : mark.Seconds) : 1000 + number++;
                    entries.Add(entry);
                }
                return entries.OrderBy(e => e.Key).ToList();
            }

            // Builds a single-mark entry with its sort key, insertion request and applied-state match.
            SpeechEntry Point(double key, string label, string name, string kind, string written, string value = null, string text = null) => new()
            {
                Key = key,
                Label = label,
                Spec = new { name, value, text },
                Written = written,
                Kind = kind,
                Match = new { name, value, text },
            };
            // Builds a paired-mark entry with preview styling and an optional trailing mark;
            // the applied-state match describes the pair itself.
            SpeechEntry Pair(double key, string label, string name, string kind, string written, string style, double speed = 1, string text = null, object after = null) => new()
            {
                Key = key,
                Label = label,
                Spec = new { name, text, after },
                Written = written,
                Role = "pair",
                Kind = kind,
                Style = style,
                Speed = speed,
                Match = new { name, value = (string)null, text },
            };

            var mine = library.Where(m => m.Pinned).Select(SpeechLibraryEntry).ToList();
            // the recipes (several marks in one click) are listed under Mine too, the ones that still hold with the library
            mine.AddRange(SpeechRecipeList.Where(r => SpeechLibrary.RecipeProblem(r.Template, library) == null).Select(r => SpeechRecipeEntry(r, library)));

            var time = Listed("time",
                Point(0.5, T("SpeechLabelBeat"), "beat", "pause", "{beat}"),
                Point(1, pause, "pause", "pause", "{pause}"),
                Point(3, pause + " 3 s", "pause", "pause", "{pause 3s}", value: "3s"),
                Point(3.5, T("SpeechLabelWait"), "wait", "pause", "{wait}"));
            var pace = Listed("pace",
                Pair(75, T("SpeechBtnSlow"), "slow", "pace", "{slow}…{/slow}", "pace", 0.75),
                Pair(125, T("SpeechBtnFast"), "fast", "pace", "{fast}…{/fast}", "pace", 1.25));
            var volume = Listed("volume",
                Pair(0, T("SpeechBtnLoud"), "loud", "span", "{loud}…{/loud}", "loud"),
                Pair(1, T("SpeechBtnSoft"), "soft", "span", "{soft}…{/soft}", "soft"),
                Pair(2, T("SpeechBtnEmphasis"), "emphasis", "span", "{emphasis}…{/emphasis}", "emphasis"));

            var tones = new[] { "SpeechToneJoke", "SpeechToneIrony", "SpeechToneWarm", "SpeechToneSerious", "SpeechToneUrgent", "SpeechToneHumble" };
            var toneEntries = new List<SpeechEntry>
            {
                // a joke and the room for the laugh after it
                Pair(0, T("SpeechBtnJoke"), "tone", "span", $"{{tone: {T("SpeechToneJoke")}}}…{{/tone}}{{wait 3s: {T("SpeechNoteLaugh")}}}", "tone",
                    text: T("SpeechToneJoke"), after: new { name = "wait", value = "3s", text = T("SpeechNoteLaugh") }),
            };
            toneEntries.AddRange(tones.Select((key, i) => Pair(i + 1, T(key), "tone", "span", $"{{tone: {T(key)}}}…{{/tone}}", "tone", text: T(key))));
            var tone = Listed("tone", toneEntries.ToArray());

            var cues = new[] { "SpeechCueAudience", "SpeechCueSlide", "SpeechCueGesture", "SpeechCueObject", "SpeechCueDrink", "SpeechCueBreathe" };
            var cue = Listed("cue", cues.Select((key, i) => Point(i, T(key), "cue", "note", $"{{cue: {T(key)}}}", text: T(key))).ToArray());

            return new()
            {
                ("mine", mine),
                ("time", time),
                ("pace", pace),
                ("volume", volume),
                ("tone", tone),
                ("cue", cue),
            };
        }

        /// <summary>
        /// Returns the resource key for a Speech group heading, falling back to Cue for unknown IDs.
        /// </summary>
        private static string SpeechGroupKey(string id) => id switch
        {
            "mine" => "SpeechGroupMine",
            "time" => "SpeechGroupTime",
            "pace" => "SpeechGroupPace",
            "volume" => "SpeechGroupVolume",
            "tone" => "SpeechGroupTone",
            _ => "SpeechGroupCue",
        };

        /// <summary>
        /// Rebuilds the Speech card from the shared groups, creates an empty section for document
        /// definitions, updates visibility, and sends the refreshed ring catalog to the editor page.
        /// </summary>
        private void BuildSpeechCard()
        {
            SpeechBody.Children.Clear();
            SpeechBody.Children.Add(BuildSpeechTimingPanel());
            foreach (var (id, entries) in SpeechGroups())
            {
                if (id == "mine" && entries.Count == 0) continue;
                SpeechGroup(SpeechGroupKey(id), entries.Select(SpeechButton));
            }

            // The words this document defines for itself (filled in by UpdateSpeechDefinitions).
            (speechDefinedHeader, speechDefinedPanel) = SpeechGroup("SpeechGroupDefined", Array.Empty<Button>());
            speechDefinedHeader.Visibility = speechDefinedPanel.Visibility = Visibility.Collapsed;

            SpeechBody.Children.Add(BuildSpeechExplainPanel());
            SpeechBody.Children.Add(BuildSpeechStagePanel());
            SpeechBody.Children.Add(new TextBlock
            {
                Text = Locale.GetString("SpeechShortcutsHint"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
                Opacity = 0.75,
            });
            UpdateSpeechCard();
            PushSpeechRing();
        }

        // An entry for a mark of the user's own. It asks for the mark together with its definition, which the page writes into
        // the document when the document does not have it yet.
        private SpeechEntry SpeechLibraryEntry(SpeechMark mark)
        {
            var pair = mark.Kind is "pace" or "span";
            return new SpeechEntry
            {
                Label = mark.Icon.Length > 0 ? $"{mark.Icon} {mark.Name}" : mark.Name,
                Spec = new { name = mark.Name, definitions = new[] { SpeechLibrary.DefinitionLine(mark) } },
                Written = pair ? $"{{{mark.Name}}}…{{/{mark.Name}}}" : $"{{{mark.Name}}}",
                Meaning = SpeechLibrary.CleanMeaning(mark.Meaning),
                Role = pair ? "pair" : "point",
                Kind = mark.Kind,
                Style = "user",
                Speed = mark.Kind == "pace" ? mark.Percent / 100 : 1,
                PerWord = mark.PerWord,
                Color = mark.Color,
                Mine = true,
                Match = new { name = mark.Name, value = (string)null, text = (string)null },
            };
        }

        // The page lists the words the document defines ({define very-slow pace 50%: ...}): they are marks there, so they
        // are buttons here.
        private void UpdateSpeechDefinitions(JToken args)
        {
            try
            {
                var buttons = new List<Button>();
                foreach (var def in args?["defs"] as JArray ?? new JArray())
                {
                    var name = def["name"]?.ToString();
                    if (string.IsNullOrEmpty(name) || SpeechBuiltInWords.Contains(name)) continue;
                    var pair = def["kind"]?.ToString() is "pace" or "span";
                    var written = pair ? $"{{{name}}}…{{/{name}}}" : $"{{{name}}}";
                    var meaning = def["meaning"]?.ToString();
                    buttons.Add(SpeechButton(name, new { name }, string.IsNullOrEmpty(meaning) ? written : $"{written}\n{meaning}"));
                }
                speechDefinedPanel.Children.Clear();
                foreach (var button in buttons) speechDefinedPanel.Children.Add(button);
                speechDefinedHeader.Visibility = speechDefinedPanel.Visibility = buttons.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Log($"Speech: the definitions of the document were not listed: {ex.Message}");
            }
        }

        // A button of the card: asks the page to write the mark where the selection or the caret is.
        private async Task InsertSpeechMark(object spec)
        {
            try
            {
                var literal = JsonConvert.SerializeObject(JsonConvert.SerializeObject(spec));
                var answer = await RunInPage($"window.__caretSpeech?window.__caretSpeech.insert({literal}):null");
                var status = string.IsNullOrEmpty(answer) || answer == "null" ? "nofocus" : JsonConvert.DeserializeObject<string>(answer);
                if (status == "ok")
                {
                    SpeechStatusText.Visibility = Visibility.Collapsed;
                    EditorView.Focus(FocusState.Programmatic);
                    return;
                }
                ShowSpeechStatus(Locale.GetString(status switch
                {
                    "nothing" => "SpeechMsgNothing",
                    "unsafe" => "SpeechMsgUnsafe",
                    "several" => "SpeechMsgSeveral",
                    "unknown" => "SpeechMsgUnknown",
                    _ => "SpeechMsgNoFocus",
                }));
            }
            catch (Exception ex)
            {
                Log($"Speech: inserting a mark failed: {ex.Message}");
            }
        }

        // A short line under the buttons (a screen reader reads it as it appears); gone after a few seconds.
        private void ShowSpeechStatus(string message)
        {
            SpeechStatusText.Text = message;
            SpeechStatusText.Visibility = Visibility.Visible;
            speechStatusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            speechStatusTimer.Tick -= HideSpeechStatus;
            speechStatusTimer.Tick += HideSpeechStatus;
            speechStatusTimer.Stop();
            speechStatusTimer.Start();
        }

        private void HideSpeechStatus(object sender, object e)
        {
            speechStatusTimer.Stop();
            SpeechStatusText.Visibility = Visibility.Collapsed;
        }

        // Edit > Remove all speech marks: the marks and the definitions go, the spoken text stays; one step in Undo.
        // The page does the work (the same rules that draw the marks), on the text of the document as the host has it.
        private async void SpeechRemoveAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FlushEditor();
                var text = file.Markdown ?? "";
                var answer = await RunInPage($"window.__caretSpeech?window.__caretSpeech.strip({JsonConvert.SerializeObject(text)}):null");
                if (string.IsNullOrEmpty(answer) || answer == "null") return;
                var result = JsonConvert.DeserializeObject<string>(answer);
                if (result == text)
                {
                    await ShowReviewMessage(Locale.GetString("SpeechNoMarks"));
                    return;
                }
                file.ReplaceBuffer(result);
                history.ContentChange(result);
                PostMessage("SetMarkdown", new { text = result, cursor = activeDoc.Cursor, basePath = file.ImageBasePath });
                Log("Speech: all speech marks removed from the document");
            }
            catch (Exception ex)
            {
                Log($"Speech: removing the marks failed: {ex.Message}");
            }
        }
    }

    // One thing the Speech card and the Speech ring can put into the text: a mark of Caret, a mark of the user's own or a
    // recipe. `Spec` is what the page is asked for; `Match` says which mark in the text is this one (for the ring's lit
    // items); the rest is how the ring shows it.
    internal sealed class SpeechEntry
    {
        public double Key { get; set; }
        public string Label { get; set; } = "";
        public object Spec { get; set; }
        // the text it writes, the same in every language; Meaning is the user's own line about a mark of theirs
        public string Written { get; set; } = "";
        public string Meaning { get; set; } = "";
        // pair | point
        public string Role { get; set; } = "point";
        // pace | pause | span | note | recipe
        public string Kind { get; set; } = "";
        // loud | soft | emphasis | tone | pace | user: how the preview draws the text of a pair
        public string Style { get; set; } = "";
        public double Speed { get; set; } = 1;
        public double PerWord { get; set; }
        public string Color { get; set; } = "";
        public bool Mine { get; set; }
        public object Match { get; set; }

        public string Tooltip => Meaning.Length > 0 ? $"{Written}\n{Meaning}" : Written;
    }

    // Buttons side by side, wrapping to the next row when the sidebar is narrow (WinUI 3 has no such panel of its own).
    internal sealed class SpeechWrapPanel : Panel
    {
        public double Spacing { get; set; }

        protected override Size MeasureOverride(Size available)
        {
            double x = 0, y = 0, row = 0, widest = 0;
            foreach (var child in Children)
            {
                child.Measure(new Size(available.Width, double.PositiveInfinity));
                var size = child.DesiredSize;
                if (x > 0 && x + size.Width > available.Width) { y += row + Spacing; x = 0; row = 0; }
                x += size.Width + Spacing;
                row = Math.Max(row, size.Height);
                widest = Math.Max(widest, x - Spacing);
            }
            return new Size(Math.Min(widest, double.IsInfinity(available.Width) ? widest : available.Width), y + row);
        }

        protected override Size ArrangeOverride(Size final)
        {
            double x = 0, y = 0, row = 0;
            foreach (var child in Children)
            {
                var size = child.DesiredSize;
                if (x > 0 && x + size.Width > final.Width) { y += row + Spacing; x = 0; row = 0; }
                child.Arrange(new Rect(x, y, Math.Min(size.Width, final.Width), size.Height));
                x += size.Width + Spacing;
                row = Math.Max(row, size.Height);
            }
            return final;
        }
    }
}
