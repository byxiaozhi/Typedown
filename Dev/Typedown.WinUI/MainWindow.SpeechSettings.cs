using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.UI;

namespace Typedown.WinUI
{
    // New since the fork: Settings > Speech marks (docs/speech-marks-design.md, 5.5). How fast you speak, and the marks of
    // your own: add, edit, duplicate, delete, reorder, and a starting set. The rules of a mark are in
    // Services/SpeechLibrary.cs (tested without WinUI). The library is kept in the user's Settings; it shows in the Speech
    // card (MainWindow.Speech.cs) and styles the marks in the editor; a document never depends on it, because using a
    // mark writes its definition into the document.
    public sealed partial class MainWindow
    {
        private List<SpeechMark> speechLibrary;

        private List<SpeechMark> SpeechLibraryMarks => speechLibrary ??= LoadSpeechLibrary();

        // A damaged setting (a hand-edited Settings file) is an empty library, never a window that does not open.
        private List<SpeechMark> LoadSpeechLibrary()
        {
            try { return SpeechLibrary.Parse(settings.SpeechMarks); }
            catch (Exception ex)
            {
                Log($"Speech: the library of marks could not be read: {ex.Message}");
                return new List<SpeechMark>();
            }
        }

        // The colours of the swatches, as the light theme draws them (the editor has a pair of theme colours for each).
        private static readonly Dictionary<string, Color> SpeechSwatches = new()
        {
            ["red"] = Color.FromArgb(255, 0xCF, 0x22, 0x2E),
            ["orange"] = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            ["yellow"] = Color.FromArgb(255, 0xD6, 0xA3, 0x00),
            ["green"] = Color.FromArgb(255, 0x2E, 0xA0, 0x43),
            ["teal"] = Color.FromArgb(255, 0x0D, 0x94, 0x88),
            ["blue"] = Color.FromArgb(255, 0x1B, 0x4A, 0xA0),
            ["purple"] = Color.FromArgb(255, 0x7C, 0x3A, 0xED),
            ["pink"] = Color.FromArgb(255, 0xDB, 0x27, 0x77),
        };

        private static string Pascal(string name) => string.Concat(name.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

        private void LoadSpeechSettings()
        {
            SpeechWpmBox.Value = settings.SpeechWpm;
            SpeechExplainUsedOnlyToggle.IsOn = settings.SpeechExplainUsedOnly;
            SpeechExplainAsWordsToggle.IsOn = settings.SpeechExplainAsWords;
            SpeechExplainRehearsalToggle.IsOn = settings.SpeechExplainRehearsal;
            SpeechSettingsStatusText.Visibility = Visibility.Collapsed;
            RebuildSpeechMarksList();
            RebuildSpeechRecipesList();
        }

        private void SpeechWpmBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!suppressSettingsEvents && !double.IsNaN(args.NewValue)) settings.SpeechWpm = (int)Math.Round(args.NewValue);
        }

        // The library is saved, the card in the sidebar and the list here are built again, and the editor is told how to
        // draw the marks (colour and symbol).
        private void SaveSpeechLibrary()
        {
            settings.SpeechMarks = SpeechLibrary.Serialize(SpeechLibraryMarks);
            BuildSpeechCard();
            RebuildSpeechMarksList();
            PostMessage("SettingsChanged", new Dictionary<string, object> { { "speechStyles", SpeechLibrary.Styles(SpeechLibraryMarks) } });
        }

        private void ShowSpeechSettingsStatus(string message)
        {
            SpeechSettingsStatusText.Text = message;
            SpeechSettingsStatusText.Visibility = Visibility.Visible;
        }

        private string SpeechSummary(SpeechMark mark) => mark.Kind switch
        {
            "pace" => SpeechLibrary.Number(mark.Percent) + " %" + (mark.PerWord > 0 ? " +" + SpeechLibrary.Number(mark.PerWord) + " s" : ""),
            "pause" => SpeechLibrary.Number(mark.Seconds) + " s",
            "note" => Locale.GetString("SpeechKindNoteShort"),
            _ => Locale.GetString("SpeechKindSpanShort"),
        };

        private Button SpeechRowButton(string glyph, string tipKey, Action action, bool enabled = true)
        {
            var button = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 14 }, Padding = new Thickness(8, 6, 8, 6), IsEnabled = enabled };
            var tip = Locale.GetString(tipKey);
            ToolTipService.SetToolTip(button, tip);
            AutomationProperties.SetName(button, tip);
            button.Click += (s, e) => action();
            return button;
        }

        private void RebuildSpeechMarksList()
        {
            SpeechMarksListPanel.Children.Clear();
            var marks = SpeechLibraryMarks;
            if (marks.Count == 0)
            {
                SpeechMarksListPanel.Children.Add(new TextBlock { Text = Locale.GetString("SpeechNoMarksYet"), Margin = new Thickness(4, 8, 4, 8), Opacity = 0.75 });
                return;
            }
            for (var i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                var index = i;
                var icon = new FontIcon { Glyph = mark.Icon.Length > 0 ? mark.Icon : "◌", FontFamily = new FontFamily("Segoe UI Symbol") };
                if (SpeechSwatches.TryGetValue(mark.Color, out var color)) icon.Foreground = new SolidColorBrush(color);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                buttons.Children.Add(SpeechRowButton("", "SpeechEditMark", () => _ = EditSpeechMark(mark)));
                buttons.Children.Add(SpeechRowButton("", "SpeechDuplicateMark", () => _ = DuplicateSpeechMark(mark)));
                buttons.Children.Add(SpeechRowButton("", "SpeechMoveUp", () => MoveSpeechMark(index, -1), index > 0));
                buttons.Children.Add(SpeechRowButton("", "SpeechMoveDown", () => MoveSpeechMark(index, +1), index < marks.Count - 1));
                buttons.Children.Add(SpeechRowButton("", "SpeechDeleteMark", () => DeleteSpeechMark(mark)));
                SpeechMarksListPanel.Children.Add(new SettingsCard
                {
                    Header = mark.Name,
                    Description = SpeechSummary(mark) + " · " + SpeechLibrary.CleanMeaning(mark.Meaning),
                    HeaderIcon = icon,
                    Content = buttons,
                });
            }
        }

        private void MoveSpeechMark(int index, int by)
        {
            var marks = SpeechLibraryMarks;
            var to = index + by;
            if (to < 0 || to >= marks.Count) return;
            (marks[index], marks[to]) = (marks[to], marks[index]);
            SaveSpeechLibrary();
        }

        // The recipes that a change of the library would break (a word they use gone or no longer used the same way): the
        // change is refused, so a recipe is never lost without the user knowing.
        private string RecipesNewlyBrokenBy(IEnumerable<SpeechMark> newLibrary)
        {
            var broken = SpeechRecipeList.Where(r => SpeechLibrary.RecipeProblem(r.Template, SpeechLibraryMarks) == null
                && SpeechLibrary.RecipeProblem(r.Template, newLibrary) != null).Select(r => r.Name);
            return string.Join(", ", broken);
        }

        private void DeleteSpeechMark(SpeechMark mark)
        {
            var broken = RecipesNewlyBrokenBy(SpeechLibraryMarks.Where(m => !ReferenceEquals(m, mark)).ToList());
            if (broken.Length > 0)
            {
                ShowSpeechSettingsStatus(Locale.Format("SpeechMarkInUse", broken));
                return;
            }
            SpeechLibraryMarks.Remove(mark);
            SaveSpeechLibrary();
        }

        private async void SpeechAddMarkButton_Click(object sender, RoutedEventArgs e) => await AddSpeechMark(new SpeechMark { Kind = "span" });

        private async Task AddSpeechMark(SpeechMark start)
        {
            if (SpeechLibraryMarks.Count >= SpeechLibrary.MaxMarks)
            {
                ShowSpeechSettingsStatus(Locale.GetString("SpeechErrLimit"));
                return;
            }
            var made = await ShowSpeechMarkDialog(start, true);
            if (made == null) return;
            SpeechLibraryMarks.Add(made);
            SaveSpeechLibrary();
        }

        private async Task EditSpeechMark(SpeechMark mark)
        {
            var made = await ShowSpeechMarkDialog(mark.Clone(), false, mark);
            if (made == null) return;
            var at = SpeechLibraryMarks.IndexOf(mark);
            if (at < 0) return;
            var after = SpeechLibraryMarks.ToList();
            after[at] = made;
            var broken = RecipesNewlyBrokenBy(after);
            if (broken.Length > 0)
            {
                ShowSpeechSettingsStatus(Locale.Format("SpeechMarkInUse", broken));
                return;
            }
            SpeechLibraryMarks[at] = made;
            SaveSpeechLibrary();
        }

        // A copy to start from: the dialog asks for its name, which cannot be the same.
        private async Task DuplicateSpeechMark(SpeechMark mark)
        {
            var copy = mark.Clone();
            var name = mark.Name + "-2";
            copy.Name = name.Length <= 24 ? name : "";
            await AddSpeechMark(copy);
        }

        // The starting set: the ones that are not there yet, with their meanings in the language of the interface.
        private void SpeechStartersButton_Click(object sender, RoutedEventArgs e)
        {
            var marks = SpeechLibraryMarks;
            var added = 0;
            foreach (var starter in SpeechLibrary.Starters(name => Locale.GetString("SpeechStarter" + Pascal(name))))
            {
                if (marks.Count >= SpeechLibrary.MaxMarks) break;
                if (SpeechLibrary.Check(starter, marks) != null) continue;
                marks.Add(starter);
                added++;
            }
            if (added > 0) SaveSpeechLibrary();
            ShowSpeechSettingsStatus(added > 0 ? Locale.Format("SpeechStartersAdded", added.ToString()) : Locale.GetString("SpeechStartersNone"));
        }

        private static string SpeechErrorKey(string code) => code switch
        {
            "name" => "SpeechErrName",
            "builtin" => "SpeechErrBuiltin",
            "taken" => "SpeechErrTaken",
            "meaning" => "SpeechErrMeaning",
            "pace" => "SpeechErrPace",
            "perword" => "SpeechErrPerWord",
            "pause" => "SpeechErrPause",
            _ => "SpeechErrName",
        };

        // The dialog of a mark: its name, kind and numbers, what it means, where it is listed and how it is drawn. The mark is
        // checked when OK is pressed, and the dialog stays open with the reason when it cannot be kept. Null on Cancel.
        private async Task<SpeechMark> ShowSpeechMarkDialog(SpeechMark mark, bool isNew, SpeechMark replacing = null)
        {
            string T(string key) => Locale.GetString(key);
            var others = SpeechLibraryMarks.Where(m => !ReferenceEquals(m, replacing)).ToList();

            var name = new TextBox { Header = T("SpeechFieldName"), PlaceholderText = "very-slow", Text = mark.Name, MaxLength = 24 };
            var kind = new ComboBox { Header = T("SpeechFieldKind"), HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var k in SpeechLibrary.Kinds) kind.Items.Add(new ComboBoxItem { Content = T("SpeechKind" + Pascal(k)), Tag = k });
            kind.SelectedIndex = Math.Max(0, Array.IndexOf(SpeechLibrary.Kinds, mark.Kind));

            var percent = new NumberBox { Header = T("SpeechFieldPercent"), Minimum = 10, Maximum = 300, SmallChange = 5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Value = mark.Percent };
            var perWord = new NumberBox { Header = T("SpeechFieldPerWord"), Minimum = 0, Maximum = 2, SmallChange = 0.1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Value = mark.PerWord };
            var paceFields = new StackPanel { Spacing = 8 };
            paceFields.Children.Add(percent);
            paceFields.Children.Add(perWord);
            var seconds = new NumberBox { Header = T("SpeechFieldSeconds"), Minimum = 0.1, Maximum = 600, SmallChange = 0.5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Value = mark.Seconds };

            var meaning = new TextBox { Header = T("SpeechFieldMeaning"), Text = mark.Meaning, TextWrapping = TextWrapping.Wrap, MinHeight = 64 };
            var meaningHint = new TextBlock { Text = T("SpeechFieldMeaningHint"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };

            var group = new ComboBox { Header = T("SpeechFieldGroup"), HorizontalAlignment = HorizontalAlignment.Stretch };
            group.Items.Add(new ComboBoxItem { Content = T("SpeechGroupAuto"), Tag = "" });
            foreach (var g in SpeechLibrary.Groups) group.Items.Add(new ComboBoxItem { Content = T("SpeechGroup" + Pascal(g)), Tag = g });
            group.SelectedIndex = Math.Max(0, Array.IndexOf(SpeechLibrary.Groups, mark.Group) + 1);

            // colour and symbol: a row of toggle buttons, one chosen at a time (the first is "none")
            string chosenColor = mark.Color, chosenIcon = mark.Icon;
            var colorRow = new SpeechWrapPanel { Spacing = 4 };
            var iconRow = new SpeechWrapPanel { Spacing = 4 };
            var colorButtons = new List<ToggleButton>();
            var iconButtons = new List<ToggleButton>();
            ToggleButton Choice(string text, Brush background, string value, string accessibleName, List<ToggleButton> all, Action<string> pick, bool selected)
            {
                var button = new ToggleButton { Content = text, Width = 32, Height = 32, Padding = new Thickness(0), IsChecked = selected, Tag = value };
                if (background != null)
                {
                    // a swatch keeps its colour when it is the chosen one, and shows a check mark instead
                    button.Background = background;
                    foreach (var state in new[] { "", "PointerOver", "Pressed" }) button.Resources["ToggleButtonBackgroundChecked" + state] = background;
                    foreach (var state in new[] { "", "PointerOver", "Pressed" }) button.Resources["ToggleButtonForegroundChecked" + state] = new SolidColorBrush(Colors.White);
                    button.Content = selected ? "✓" : "";
                }
                AutomationProperties.SetName(button, accessibleName);
                ToolTipService.SetToolTip(button, accessibleName);
                button.Click += (s, e) =>
                {
                    foreach (var other in all)
                    {
                        other.IsChecked = ReferenceEquals(other, button);
                        if (other.Tag is string tag && SpeechSwatches.ContainsKey(tag)) other.Content = other.IsChecked == true ? "✓" : "";
                    }
                    pick(value);
                };
                all.Add(button);
                return button;
            }
            colorRow.Children.Add(Choice("–", null, "", T("SpeechNone"), colorButtons, v => chosenColor = v, chosenColor == ""));
            foreach (var c in SpeechLibrary.Colors)
                colorRow.Children.Add(Choice("", new SolidColorBrush(SpeechSwatches[c]), c, T("Color" + Pascal(c)), colorButtons, v => chosenColor = v, chosenColor == c));
            iconRow.Children.Add(Choice("–", null, "", T("SpeechNone"), iconButtons, v => chosenIcon = v, chosenIcon == ""));
            foreach (var i in SpeechLibrary.Icons)
                iconRow.Children.Add(Choice(i, null, i, i, iconButtons, v => chosenIcon = v, chosenIcon == i));

            var pinned = new CheckBox { Content = T("SpeechFieldPinned"), IsChecked = mark.Pinned };
            var preview = new TextBlock { FontSize = 12, Opacity = 0.85, TextWrapping = TextWrapping.Wrap };
            var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Foreground = new SolidColorBrush(Color.FromArgb(255, 0xCF, 0x22, 0x2E)) };

            SpeechMark Read() => new()
            {
                Name = SpeechLibrary.CleanName(name.Text),
                Kind = (kind.SelectedItem as ComboBoxItem)?.Tag as string ?? "span",
                Percent = double.IsNaN(percent.Value) ? 0 : percent.Value,
                PerWord = double.IsNaN(perWord.Value) ? 0 : perWord.Value,
                Seconds = double.IsNaN(seconds.Value) ? 0 : seconds.Value,
                Meaning = SpeechLibrary.CleanMeaning(meaning.Text),
                Group = (group.SelectedItem as ComboBoxItem)?.Tag as string ?? "",
                Color = chosenColor,
                Icon = chosenIcon,
                Pinned = pinned.IsChecked == true,
            };

            // what the mark does to the time of a ten-word sentence at the speed that is set, from the same rules as the estimate
            void Refresh()
            {
                var current = Read();
                paceFields.Visibility = current.Kind == "pace" ? Visibility.Visible : Visibility.Collapsed;
                seconds.Visibility = current.Kind == "pause" ? Visibility.Visible : Visibility.Collapsed;
                var wpm = Math.Max(40, settings.SpeechWpm);
                var plain = 10.0 / wpm * 60;
                preview.Text = current.Kind switch
                {
                    "pace" when current.Percent > 0 => Locale.Format("SpeechPreviewPace",
                        (plain / (current.Percent / 100) + current.PerWord * 10).ToString("0.#"), plain.ToString("0.#")),
                    "pause" => Locale.Format("SpeechPreviewPause", SpeechLibrary.Number(current.Seconds)),
                    _ => T("SpeechPreviewNoTime"),
                };
            }
            kind.SelectionChanged += (s, e) => Refresh();
            percent.ValueChanged += (s, e) => Refresh();
            perWord.ValueChanged += (s, e) => Refresh();
            seconds.ValueChanged += (s, e) => Refresh();
            Refresh();

            var form = new StackPanel { Spacing = 10, MinWidth = 380 };
            // the reason a mark cannot be kept is first, and what the mark does is under its numbers: both must be seen
            // without scrolling the form
            foreach (var element in new UIElement[]
            {
                error, name, kind, paceFields, seconds, preview, meaning, meaningHint, group,
                new TextBlock { Text = T("SpeechFieldColor"), Margin = new Thickness(0, 4, 0, 0) }, colorRow,
                new TextBlock { Text = T("SpeechFieldIcon"), Margin = new Thickness(0, 4, 0, 0) }, iconRow,
                pinned,
            }) form.Children.Add(element);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = T(isNew ? "SpeechDialogTitleNew" : "SpeechDialogTitleEdit"),
                Content = new ScrollViewer { Content = form, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                PrimaryButtonText = T("OK"),
                CloseButtonText = T("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            SpeechMark result = null;
            dialog.PrimaryButtonClick += (s, e) =>
            {
                var made = Read();
                var problem = SpeechLibrary.Check(made, others);
                if (problem != null)
                {
                    e.Cancel = true;
                    error.Text = T(SpeechErrorKey(problem));
                    error.Visibility = Visibility.Visible;
                    return;
                }
                result = made;
            };
            var focused = false;
            dialog.Opened += (s, e) => { if (!focused) { focused = true; name.Focus(FocusState.Programmatic); } };
            await dialog.ShowAsync();
            return result;
        }
    }
}
