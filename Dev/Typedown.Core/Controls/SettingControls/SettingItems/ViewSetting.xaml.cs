using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.SettingControls.SettingItems
{
    public sealed partial class ViewSetting : UserControl
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        /// <summary>The themes in the list, in the same order; the first entry is "no custom theme".</summary>
        private readonly List<CustomTheme> themes = new();

        private bool fillingThemes;

        public ViewSetting()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                FillThemes();
                if (Settings != null) Settings.PropertyChanged += OnSettingsChanged;
            };
            Unloaded += (_, _) =>
            {
                if (Settings != null) Settings.PropertyChanged -= OnSettingsChanged;
            };
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Settings == null) return;
            if (e.PropertyName is not (nameof(Settings.AppTheme) or nameof(Settings.CustomTheme))) return;
            // The entries were built under the theme in force at the time and keep its text colour — white
            // text on a light list after a switch to the light theme. Building them again picks up the new one,
            // and moves the selection to whatever the theme is now (the View menu can change it too).
            FillThemes();
        }

        /// <summary>
        /// The built-in themes first, then the ones from the themes folder — one list, one choice. The index
        /// into <see cref="themes"/> is the entry's position minus the number of built-in ones.
        /// </summary>
        private void FillThemes()
        {
            if (Settings == null) return;
            fillingThemes = true;
            try
            {
                ThemeFiles.EnsureFolder();
                themes.Clear();
                themes.AddRange(ThemeFiles.List());
                ThemeBox.Items.Clear();
                foreach (var builtIn in Enums.Enumerable.AppThemes) ThemeBox.Items.Add(EnumName(builtIn));
                foreach (var theme in themes) ThemeBox.Items.Add(ThemeFiles.DisplayName(theme, themes));
                var index = themes.FindIndex(x => x.Id == Settings.CustomTheme);
                ThemeBox.SelectedIndex = index < 0
                    ? Enums.Enumerable.AppThemes.ToList().IndexOf(Settings.AppTheme)
                    : Enums.Enumerable.AppThemes.Count + index;
            }
            catch (Exception ex)
            {
                Log.Debug($"fill themes: {ex.Message}");
            }
            finally
            {
                fillingThemes = false;
            }
        }

        private static string EnumName(Enums.AppTheme theme)
        {
            var field = typeof(Enums.AppTheme).GetField(theme.ToString());
            var attribute = field?.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
            return attribute?.Text ?? theme.ToString();
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fillingThemes || Settings == null) return;
            var builtInCount = Enums.Enumerable.AppThemes.Count;
            var index = ThemeBox.SelectedIndex;
            if (index < 0) return;
            if (index < builtInCount)
                Settings.ApplyBuiltInTheme(Enums.Enumerable.AppThemes[index]);
            else if (index - builtInCount < themes.Count)
                Settings.ApplyCustomTheme(themes[index - builtInCount]);
        }

        /// <summary>Opens the document that explains the theme format, which ships next to the app.</summary>
        private void OnOpenThemeDocument(object sender, RoutedEventArgs e)
        {
            try
            {
                ThemeFiles.EnsureFolder();
                var path = File.Exists(ThemeFiles.DocumentPath)
                    ? ThemeFiles.DocumentPath
                    : System.IO.Path.Combine(ThemeFiles.BundledFolder, "custom-theme.md");
                if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Debug($"open theme document: {ex.Message}");
            }
        }

        private void OnOpenThemeFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                ThemeFiles.EnsureFolder();
                Process.Start(new ProcessStartInfo(ThemeFiles.Folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Debug($"open theme folder: {ex.Message}");
            }
        }

        private async void OnOpenThemeDesigner(object sender, RoutedEventArgs e)
        {
            try
            {
                await ThemeFiles.OpenDesignerAsync(Settings?.CustomTheme);
            }
            catch (Exception ex)
            {
                Log.Debug($"open theme designer: {ex.Message}");
            }
        }

        private void OnRefreshThemes(object sender, RoutedEventArgs e)
        {
            FillThemes();
            // re-apply, so edits to the file that is already selected show up
            Settings?.OnPropertyChanged(nameof(Settings.CustomTheme), null, Settings.CustomTheme);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
