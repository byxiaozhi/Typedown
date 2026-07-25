using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class EditorSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        public DecimalFormatter FontSizeFormatter { get; } = new() { FractionDigits = 0, NumberRounder = new IncrementNumberRounder { Increment = 0.1, RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp } };

        public DecimalFormatter LineHeightFormatter { get; } = new() { FractionDigits = 1, NumberRounder = new IncrementNumberRounder { Increment = 0.01, RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp } };

        public DecimalFormatter IntegerFormatter { get; } = new() { FractionDigits = 0, NumberRounder = new IncrementNumberRounder { Increment = 1, RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp } };

        // Every font family actually installed on this machine (evaluated once, lazily).
        private static readonly Lazy<List<string>> installedFontFamilies = new(() =>
        {
            using var installedFonts = new InstalledFontCollection();
            return installedFonts.Families
                .Select(f => f.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        });

        public ObservableCollection<string> FontFamilySuggestions { get; } = new();

        public static Dictionary<string, string> TextDirectionOptions { get; } = new()
        {
            { "auto", Locale.GetString("Editor.TextDirection.Auto") },
            { "ltr", Locale.GetString("Editor.TextDirection.Ltr") },
            { "rtl", Locale.GetString("Editor.TextDirection.Rtl") },
        };

        public static string GetTextDirectionDisplayName(string key) => TextDirectionOptions.TryGetValue(key, out var value) ? value : key;

        public EditorSetting()
        {
            InitializeComponent();
            UpdateFontFamilySuggestions(string.Empty);
        }

        private void UpdateFontFamilySuggestions(string filter)
        {
            var matches = string.IsNullOrEmpty(filter)
                ? installedFontFamilies.Value
                : installedFontFamilies.Value.Where(name => name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0);
            FontFamilySuggestions.Clear();
            foreach (var name in matches.Take(50))
                FontFamilySuggestions.Add(name);
        }

        private void OnFontFamilyTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                UpdateFontFamilySuggestions(sender.Text);
        }

        private void OnFontFamilySuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string fontFamily)
                Settings.FontFamily = fontFamily;
        }

        private void OnFontFamilyQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            Settings.FontFamily = args.ChosenSuggestion as string ?? args.QueryText;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
