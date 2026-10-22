using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // New since the fork: Settings > Speech marks, second half (docs/speech-marks-design.md, 3.7 B and 5.5): recipes, and the
    // library as a file. A recipe is a template of several marks in one click, `{pause 5s}{soft}{text}{/soft}{wait}`, where
    // {text} stands for the text it is applied to; it is listed under Mine in the Speech card. Using one writes ordinary
    // marks (and the definitions of the library's words that it uses), so nothing new is in the document. The rules are in
    // Services/SpeechLibrary.cs, and the editor applies the same ones when the recipe is used.
    public sealed partial class MainWindow
    {
        private List<SpeechRecipe> speechRecipes;

        private List<SpeechRecipe> SpeechRecipeList => speechRecipes ??= LoadSpeechRecipes();

        private List<SpeechRecipe> LoadSpeechRecipes()
        {
            try { return SpeechLibrary.ParseRecipes(settings.SpeechRecipes, SpeechLibraryMarks); }
            catch (Exception ex)
            {
                Log($"Speech: the recipes could not be read: {ex.Message}");
                return new List<SpeechRecipe>();
            }
        }

        // The recipe that ships with Caret: a joke and the room for the laugh after it. It cannot be changed; a copy can.
        private SpeechRecipe BuiltInJokeRecipe() => new()
        {
            Name = Locale.GetString("SpeechBtnJoke"),
            Template = "{tone: " + Locale.GetString("SpeechToneJoke") + "}{text}{/tone}{wait 3s: " + Locale.GetString("SpeechNoteLaugh") + "}",
        };

        private void SaveSpeechRecipes()
        {
            settings.SpeechRecipes = SpeechLibrary.SerializeRecipes(SpeechRecipeList);
            BuildSpeechCard();
            RebuildSpeechRecipesList();
        }

        // An entry of the Speech card and the ring for a recipe. It asks for the template, with the definitions of the library's words in it.
        private SpeechEntry SpeechRecipeEntry(SpeechRecipe recipe, IEnumerable<SpeechMark> library) => new()
        {
            Label = recipe.Name,
            Spec = new { template = recipe.Template, definitions = SpeechLibrary.RecipeDefinitions(recipe.Template, library).ToArray() },
            Written = recipe.Template,
            Kind = "recipe",
            Mine = true,
        };

        private void RebuildSpeechRecipesList()
        {
            SpeechRecipesListPanel.Children.Clear();
            var joke = BuiltInJokeRecipe();
            SpeechRecipesListPanel.Children.Add(new SettingsCard
            {
                Header = joke.Name + " · " + Locale.GetString("SpeechRecipeBuiltIn"),
                Description = joke.Template,
                Content = SpeechRowButton("", "SpeechDuplicateMark", () => _ = AddSpeechRecipe(new SpeechRecipe { Name = joke.Name + " 2", Template = joke.Template })),
            });
            var recipes = SpeechRecipeList;
            foreach (var recipe in recipes)
            {
                var current = recipe;
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                buttons.Children.Add(SpeechRowButton("", "SpeechEditMark", () => _ = EditSpeechRecipe(current)));
                buttons.Children.Add(SpeechRowButton("", "SpeechDuplicateMark", () => _ = AddSpeechRecipe(new SpeechRecipe { Name = current.Name + " 2", Template = current.Template })));
                buttons.Children.Add(SpeechRowButton("", "SpeechDeleteMark", () => { SpeechRecipeList.Remove(current); SaveSpeechRecipes(); }));
                SpeechRecipesListPanel.Children.Add(new SettingsCard { Header = recipe.Name, Description = recipe.Template, Content = buttons });
            }
        }

        private async void SpeechAddRecipeButton_Click(object sender, RoutedEventArgs e) =>
            await AddSpeechRecipe(new SpeechRecipe { Template = "{pause}{text}" });

        private async Task AddSpeechRecipe(SpeechRecipe start)
        {
            if (SpeechRecipeList.Count >= SpeechLibrary.MaxRecipes)
            {
                ShowSpeechSettingsStatus(Locale.GetString("SpeechErrRLimit"));
                return;
            }
            var made = await ShowSpeechRecipeDialog(start, true);
            if (made == null) return;
            SpeechRecipeList.Add(made);
            SaveSpeechRecipes();
        }

        private async Task EditSpeechRecipe(SpeechRecipe recipe)
        {
            var made = await ShowSpeechRecipeDialog(recipe.Clone(), false, recipe);
            if (made == null) return;
            var at = SpeechRecipeList.IndexOf(recipe);
            if (at < 0) return;
            SpeechRecipeList[at] = made;
            SaveSpeechRecipes();
        }

        private static string SpeechRecipeErrorKey(string code) => code switch
        {
            "rname" => "SpeechErrRName",
            "rtaken" => "SpeechErrRTaken",
            "rtext" => "SpeechErrRText",
            "ropen" => "SpeechErrROpen",
            "rforbidden" => "SpeechErrRForbidden",
            "rempty" => "SpeechErrREmpty",
            _ => "SpeechErrRUnknown",
        };

        // The dialog of a recipe: its name and the marks. What it writes on a sample sentence is shown as it is typed, and the
        // reason a recipe cannot be kept is at the top. Null on Cancel.
        private async Task<SpeechRecipe> ShowSpeechRecipeDialog(SpeechRecipe recipe, bool isNew, SpeechRecipe replacing = null)
        {
            string T(string key) => Locale.GetString(key);
            var others = SpeechRecipeList.Where(r => !ReferenceEquals(r, replacing)).ToList();
            var name = new TextBox { Header = T("SpeechFieldName"), Text = recipe.Name, MaxLength = 40 };
            var template = new TextBox { Header = T("SpeechFieldTemplate"), Text = recipe.Template, TextWrapping = TextWrapping.Wrap, MinHeight = 72, AcceptsReturn = false };
            var hint = new TextBlock { Text = T("SpeechTemplateHint"), FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
            var preview = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.9, IsTextSelectionEnabled = true };
            var error = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xCF, 0x22, 0x2E)) };

            SpeechRecipe Read() => new() { Name = SpeechLibrary.CleanRecipeName(name.Text), Template = (template.Text ?? "").Trim() };
            void Refresh()
            {
                error.Visibility = Visibility.Collapsed;
                var current = Read();
                var problem = SpeechLibrary.RecipeProblem(current.Template, SpeechLibraryMarks);
                preview.Text = problem == null ? Locale.Format("SpeechRecipePreview", SpeechLibrary.RecipePreview(current.Template, T("SpeechRecipeSample"))) : "";
            }
            template.TextChanged += (s, e) => Refresh();
            name.TextChanged += (s, e) => error.Visibility = Visibility.Collapsed;
            Refresh();

            var form = new StackPanel { Spacing = 10, MinWidth = 400 };
            foreach (var element in new UIElement[] { error, name, template, hint, preview }) form.Children.Add(element);
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = T(isNew ? "SpeechRecipeDialogTitleNew" : "SpeechRecipeDialogTitleEdit"),
                Content = form,
                PrimaryButtonText = T("OK"),
                CloseButtonText = T("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            SpeechRecipe result = null;
            dialog.PrimaryButtonClick += (s, e) =>
            {
                var made = Read();
                var problem = SpeechLibrary.CheckRecipe(made, others, SpeechLibraryMarks);
                if (problem != null)
                {
                    e.Cancel = true;
                    error.Text = T(SpeechRecipeErrorKey(problem));
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

        // --- the library as a file ---

        private async void SpeechExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker { SuggestedFileName = "caret-speech-marks" };
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.FileTypeChoices.Add(Locale.GetString("SettingsSpeech"), new List<string> { ".json" });
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;
                await File.WriteAllTextAsync(file.Path, SpeechLibrary.Export(SpeechLibraryMarks, SpeechRecipeList));
                ShowSpeechSettingsStatus(Locale.Format("SpeechExported", file.Name));
            }
            catch (Exception ex)
            {
                Log($"Speech: the library could not be exported: {ex.Message}");
                await ShowReviewMessage(Locale.GetString("SpeechExportFailed"));
            }
        }

        // Adds what the file holds to the library and leaves what is there; what cannot be added is counted.
        private async void SpeechImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.FileTypeFilter.Add(".json");
                var file = await picker.PickSingleFileAsync();
                if (file == null) return;
                var marks = SpeechLibraryMarks;
                var recipes = SpeechRecipeList;
                SpeechLibrary.ImportResult result;
                try { result = SpeechLibrary.Import(await File.ReadAllTextAsync(file.Path), marks, recipes); }
                catch (FormatException)
                {
                    await ShowReviewMessage(Locale.GetString("SpeechImportBad"));
                    return;
                }
                if (result.Marks > 0) SaveSpeechLibrary();
                if (result.Recipes > 0) SaveSpeechRecipes();
                await ShowReviewMessage(Locale.Format("SpeechImportDone", result.Marks.ToString(), result.Recipes.ToString(), result.Skipped.ToString()));
            }
            catch (Exception ex)
            {
                Log($"Speech: the library could not be imported: {ex.Message}");
                await ShowReviewMessage(Locale.GetString("SpeechImportFailed"));
            }
        }
    }
}
