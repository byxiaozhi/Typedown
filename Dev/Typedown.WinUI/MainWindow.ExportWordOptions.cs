using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Typedown.WinUI.Services.Export;
using Typedown.WinUI.Utilities;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Typedown.WinUI
{
    // New since the fork: the options of File > Export > Word (docs/word-export-design.md, phase 4): the look, the page, the header and
    // the footer, a table of contents, and a template of the user. What was chosen is kept for the next export.
    public sealed partial class MainWindow
    {
        private sealed class WordChoices
        {
            public int Look { get; set; }

            // The index in the list of sizes (Letter, A4, Legal); -1 until chosen: A4, or Letter where the PC uses non-metric units.
            public int PageSize { get; set; } = -1;

            public bool Landscape { get; set; }

            public int Margins { get; set; }

            public string HeaderText { get; set; } = "";

            public string FooterText { get; set; } = "";

            public bool PageNumbers { get; set; } = true;

            public bool TableOfContents { get; set; }

            public string TemplatePath { get; set; } = "";
        }

        private static readonly WordPageSize[] WordPageSizes = { WordPageSize.Letter, WordPageSize.A4, WordPageSize.Legal };

        private WordChoices LoadWordChoices()
        {
            try
            {
                return JsonConvert.DeserializeObject<WordChoices>(settings.WordExportChoices) ?? new WordChoices();
            }
            catch (JsonException)
            {
                return new WordChoices();
            }
        }

        private static ComboBox WordCombo(string header, int selected, params string[] items)
        {
            var combo = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var item in items) combo.Items.Add(item);
            combo.SelectedIndex = Math.Clamp(selected, 0, items.Length - 1);
            return combo;
        }

        // The dialog; null when the user cancels. The choices are saved when they accept.
        private async Task<WordChoices> AskWordOptions()
        {
            var choices = LoadWordChoices();
            string T(string key) => Locale.GetString(key);
            var template = !string.IsNullOrEmpty(choices.TemplatePath) && File.Exists(choices.TemplatePath) ? choices.TemplatePath : "";
            var size = choices.PageSize >= 0 ? choices.PageSize : RegionInfo.CurrentRegion.IsMetric ? 1 : 0;

            var look = WordCombo(T("WordLookLabel"), choices.Look, T("WordLookPlain"), T("WordLookReport"), T("WordLookBusiness"), T("WordLookModern"));
            var pageSize = WordCombo(T("PageSize"), size, T("Letter85X11In"), T("A4827X1169"), T("Legal85X14In"));
            var orientation = WordCombo(T("Orientation"), choices.Landscape ? 1 : 0, T("Portrait"), T("Landscape"));
            var margins = WordCombo(T("WordMarginsLabel"), choices.Margins, T("WordMarginsNormal"), T("WordMarginsNarrow"), T("WordMarginsWide"));
            var header = new TextBox { Header = T("WordHeaderLabel"), Text = choices.HeaderText ?? "", MaxLength = 200 };
            var footer = new TextBox { Header = T("WordFooterLabel"), Text = choices.FooterText ?? "", MaxLength = 200 };
            var hint = new TextBlock { Text = T("WordHeaderFooterHint"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75 };
            var numbers = new CheckBox { Content = T("WordPageNumbers"), IsChecked = choices.PageNumbers };
            var contents = new CheckBox { Content = T("WordToc"), IsChecked = choices.TableOfContents };

            var templateName = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            var choose = new Button { Content = T("WordTemplateChoose") };
            var clear = new Button { Content = T("WordTemplateClear") };
            var note = new TextBlock { Text = T("WordTemplateNote"), TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75 };
            var templateRow = new Grid { ColumnSpacing = 8 };
            templateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            templateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            templateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(choose, 1);
            Grid.SetColumn(clear, 2);
            templateRow.Children.Add(templateName);
            templateRow.Children.Add(choose);
            templateRow.Children.Add(clear);

            void Refresh()
            {
                templateName.Text = template.Length == 0 ? T("WordTemplateNone") : Path.GetFileName(template);
                clear.IsEnabled = template.Length > 0;
                note.Visibility = template.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                foreach (var control in new Control[] { look, pageSize, orientation, margins }) control.IsEnabled = template.Length == 0;
            }
            choose.Click += async (s, e) =>
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.FileTypeFilter.Add(".docx");
                picker.FileTypeFilter.Add(".dotx");
                var picked = await picker.PickSingleFileAsync();
                if (picked != null) { template = picked.Path; Refresh(); }
            };
            clear.Click += (s, e) => { template = ""; Refresh(); };
            Refresh();

            // two boxes to a row: the dialog is short enough to show the template without scrolling
            Grid Pair(ComboBox left, ComboBox right)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(right, 1);
                row.Children.Add(left);
                row.Children.Add(right);
                return row;
            }
            var stack = new StackPanel { Spacing = 10, MinWidth = 420 };
            foreach (var element in new UIElement[] { Pair(look, margins), Pair(pageSize, orientation), header, footer, hint, numbers, contents, new TextBlock { Text = T("WordTemplateLabel") }, templateRow, note })
                stack.Children.Add(element);
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = T("WordOptionsTitle"),
                Content = new ScrollViewer { MaxHeight = 560, Content = stack, Padding = new Thickness(0, 0, 12, 0) },
                PrimaryButtonText = T("WordOptionsPrimary"),
                CloseButtonText = T("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

            choices.Look = look.SelectedIndex;
            choices.PageSize = pageSize.SelectedIndex;
            choices.Landscape = orientation.SelectedIndex == 1;
            choices.Margins = margins.SelectedIndex;
            choices.HeaderText = header.Text.Trim();
            choices.FooterText = footer.Text.Trim();
            choices.PageNumbers = numbers.IsChecked == true;
            choices.TableOfContents = contents.IsChecked == true;
            choices.TemplatePath = template;
            settings.WordExportChoices = JsonConvert.SerializeObject(choices);
            return choices;
        }
    }
}
