using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;
using Windows.UI.ViewManagement;

namespace Typedown.WinUI
{
    // The Settings page (interface review, phase 2): a page in the window rather than a dialog, with
    // categories, search, and the appearance options — colour scheme, accent source, window material.
    public sealed partial class MainWindow
    {
        private bool SettingsPageShown => SettingsPage.Visibility == Visibility.Visible;

        private void ShowSettingsPage()
        {
            LoadSettingsIntoDialog();
            SettingsPage.Visibility = Visibility.Visible;
            SizeSettingsContent();
            SettingsSearchBox.Text = "";
            // The editor can't take the keyboard (and Esc) while it's covered.
            EditorView.Visibility = Visibility.Collapsed;
            if (SettingsNavList.SelectedItem == null) SettingsNavList.SelectedIndex = 0;
            else ShowSettingsCategory(((ListViewItem)SettingsNavList.SelectedItem).Tag as string);
            SettingsPage.UpdateLayout();
            (SettingsNavList.ContainerFromItem(SettingsNavList.SelectedItem) as Control)?.Focus(FocusState.Programmatic);
            Log("Settings: opened");
        }

        // The column of cards: as wide as the window leaves (up to 1000 px, as in Windows Settings), set
        // from the window's own width — measured by the layout, it came out wider than the window.
        private void SizeSettingsContent()
        {
            var available = ((FrameworkElement)Content).ActualWidth - 280 - 72 - 20;
            SettingsContent.Width = System.Math.Clamp(available, 280, 1000);
        }

        private void HideSettingsPage()
        {
            if (!SettingsPageShown) return;
            SettingsPage.Visibility = Visibility.Collapsed;
            EditorView.Visibility = Visibility.Visible;
            if (!startPageShown) EditorView.Focus(FocusState.Programmatic);
        }

        private void SettingsBack_Click(object sender, RoutedEventArgs e) => HideSettingsPage();

        // Esc returns to the document. An open drop-down lives in a popup outside the page, so it gets
        // its Esc first; an open list of search suggestions closes before the page does.
        private void SettingsPage_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Escape || SettingsSearchBox.IsSuggestionListOpen) return;
            e.Handled = true;
            HideSettingsPage();
        }

        private void SettingsSearchAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            SettingsSearchBox.Focus(FocusState.Keyboard);
        }

        private void SettingsNavList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            ShowSettingsCategory((SettingsNavList.SelectedItem as ListViewItem)?.Tag as string);

        private IEnumerable<ListViewItem> SettingsNavItems => SettingsNavList.Items.OfType<ListViewItem>();

        private string SettingsCategoryName(string tag) =>
            Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(SettingsNavItems.First(i => i.Tag as string == tag));

        private IEnumerable<StackPanel> SettingsPanels => new[]
        {
            SettingsGeneralPanel, SettingsAppearancePanel, SettingsLayoutPanel, SettingsEditorPanel, SettingsSpeechPanel, SettingsTabsPanel,
            SettingsConvertPanel, SettingsImagesPanel, SettingsKeyboardPanel, SettingsAboutPanel,
        };

        private void ShowSettingsCategory(string tag)
        {
            if (tag == null) return;
            foreach (var panel in SettingsPanels)
                panel.Visibility = panel.Tag as string == tag ? Visibility.Visible : Visibility.Collapsed;
            SettingsPageTitle.Text = SettingsCategoryName(tag);
            SettingsNoResultsText.Visibility = Visibility.Collapsed;
            SettingsScroll.ChangeView(null, 0, null, true);
        }

        // --- Search: every card, by its heading and description ---

        private sealed record SettingsHit(string Header, string Category, string CategoryTag, FrameworkElement Card)
        {
            public override string ToString() => $"{Header} · {Category}";
        }

        private List<SettingsHit> FindSettings(string query)
        {
            var hits = new List<SettingsHit>();
            foreach (var panel in SettingsPanels)
            {
                var tag = panel.Tag as string;
                var category = SettingsCategoryName(tag);
                foreach (var card in Descendants(panel).OfType<SettingsCard>().Cast<FrameworkElement>().Concat(Descendants(panel).OfType<SettingsExpander>()))
                {
                    var (header, description) = card switch
                    {
                        SettingsCard c => (c.Header as string, c.Description as string),
                        SettingsExpander e => (e.Header as string, e.Description as string),
                        _ => (null, null),
                    };
                    if (string.IsNullOrEmpty(header)) continue;
                    if (header.Contains(query, System.StringComparison.CurrentCultureIgnoreCase)
                        || (description?.Contains(query, System.StringComparison.CurrentCultureIgnoreCase) ?? false)
                        || category.Contains(query, System.StringComparison.CurrentCultureIgnoreCase))
                        hits.Add(new SettingsHit(header, category, tag, card));
                }
            }
            return hits;
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            // Logical children of the panels (the pages that aren't shown have no visual tree yet).
            foreach (var child in root switch
            {
                Panel p => p.Children.Cast<DependencyObject>(),
                SettingsExpander e => e.Items.OfType<DependencyObject>(),
                _ => Enumerable.Empty<DependencyObject>(),
            })
            {
                yield return child;
                foreach (var d in Descendants(child)) yield return d;
            }
        }

        private void SettingsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var query = sender.Text.Trim();
            sender.ItemsSource = query.Length == 0 ? null
                : FindSettings(query) is { Count: > 0 } hits ? hits
                : new List<string> { Locale.GetString("SettingsNoResults") };
        }

        private void SettingsSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is SettingsHit hit) sender.Text = hit.Header;
        }

        private void SettingsSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            var hit = args.ChosenSuggestion as SettingsHit ?? FindSettings(args.QueryText.Trim()).FirstOrDefault();
            if (hit == null) return;
            SettingsNavList.SelectedItem = SettingsNavItems.First(i => i.Tag as string == hit.CategoryTag);
            ShowSettingsCategory(hit.CategoryTag);
            // Scrolled to by its position, not BringIntoView (which also scrolled sideways).
            DispatcherQueue.TryEnqueue(() =>
            {
                var top = hit.Card.TransformToVisual((UIElement)SettingsScroll.Content).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
                SettingsScroll.ChangeView(0, System.Math.Max(0, top - 24), null);
                if (hit.Card is Control control) control.Focus(FocusState.Keyboard);
            });
        }

        // --- Appearance ---

        private bool fillingSchemes;

        private void LoadAppearanceSettings()
        {
            FillSchemeGrid();
            ColorSchemeNameText.Text = Locale.GetString(ColorSchemes.Find(settings.ColorScheme).NameKey);
            AccentSourceComboBox.SelectedIndex = settings.AccentSource == "windows" ? 1 : 0;
            WindowMaterialComboBox.SelectedIndex = settings.WindowMaterial switch { "mica" => 1, "micaalt" => 2, _ => 0 };
            WindowMaterialComboBox.IsEnabled = Config.IsMicaSupported;
            WindowMaterialCard.Description = Locale.GetString(Config.IsMicaSupported ? "WindowMaterialDescription" : "WindowMaterialUnsupported");
            // A Windows contrast theme overrides every colour here (Caret.xaml has no HighContrast
            // dictionary, so WinUI uses the system's): say so, and don't offer what wouldn't show.
            var contrast = new AccessibilitySettings().HighContrast;
            ContrastThemeInfoBar.IsOpen = contrast;
            ColorSchemeExpander.IsEnabled = AccentSourceCard.IsEnabled = WindowMaterialCard.IsEnabled = !contrast;
            // The picture draws the scheme's colours, which a contrast theme replaces: no picture of what would not show.
            AppearancePreviewCard.Visibility = contrast ? Visibility.Collapsed : Visibility.Visible;
            RefreshAppearancePreview();
        }

        // The scheme pictures are drawn in the theme that is showing, so they are built again when it changes.
        private bool schemeGridDark;

        private void FillSchemeGrid()
        {
            fillingSchemes = true;
            var dark = IsDark;
            if (ColorSchemeGridView.Items.Count == 0 || schemeGridDark != dark)
            {
                ColorSchemeGridView.Items.Clear();
                foreach (var scheme in ColorSchemes.All)
                    ColorSchemeGridView.Items.Add(BuildSchemeCard(scheme));
                schemeGridDark = dark;
            }
            ColorSchemeGridView.SelectedItem = ColorSchemeGridView.Items.OfType<FrameworkElement>().FirstOrDefault(i => i.Tag as string == settings.ColorScheme)
                                               ?? ColorSchemeGridView.Items[0];
            fillingSchemes = false;
            UpdateSchemeCards();
        }

        // The theme changed while Settings is open: the scheme pictures and the section lists are those of the
        // theme it was opened in, so both are drawn again for the new one.
        private void RefreshAppearanceForTheme()
        {
            if (!SettingsPageShown) return;
            FillSchemeGrid();
            LoadSectionColorSettings();
        }

        private void ColorSchemeGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fillingSchemes || suppressSettingsEvents || ColorSchemeGridView.SelectedItem is not FrameworkElement item) return;
            settings.ColorScheme = (string)item.Tag;
            ColorSchemeNameText.Text = Locale.GetString(ColorSchemes.Find(settings.ColorScheme).NameKey);
            ApplyAppearance();
            UpdateSchemeCards();
            LoadSectionColorSettings(); // also draws the preview: the guard and the "From the colour scheme" swatches depend on the scheme
        }

        private void AccentSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.AccentSource = (AccentSourceComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "scheme";
            ApplyAppearance();
            RefreshAppearancePreview(); // the accent shows in the picture
        }

        private void WindowMaterialComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.WindowMaterial = (WindowMaterialComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "solid";
            ApplyAppearance();
        }

        // Colours are application-wide, so every window takes them. A change made in Settings is also
        // copied into every window's settings (which saves it); a refresh after the Windows accent
        // changed saves nothing, so a default an administrator set (Config.PolicyDefault*) isn't turned
        // into the user's own choice.
        private void ApplyAppearance(bool save = true)
        {
            ColorSchemes.Apply(settings.ColorScheme, settings.AccentSource, Config.IsMicaSupported ? settings.WindowMaterial : "solid");
            foreach (var window in openWindows.ToList())
            {
                if (save)
                {
                    window.settings.ColorScheme = settings.ColorScheme;
                    window.settings.AccentSource = settings.AccentSource;
                    window.settings.WindowMaterial = settings.WindowMaterial;
                }
                ColorSchemes.Refresh((FrameworkElement)window.Content);
                window.ApplySectionColors(); // the guard depends on the scheme's text colour
                window.ApplyBackdrop();
                window.ApplyEditorBackground();
                window.PushThemeToEditor();
            }
            Log($"Appearance: scheme={settings.ColorScheme}, accent={settings.AccentSource}, material={settings.WindowMaterial}");
        }

        // --- Layout ---

        private void LoadLayoutSettings()
        {
            LayoutPresetComboBox.SelectedIndex = settings.LayoutPreset switch { "streamlined" => 1, "distraction" => 2, _ => 0 };
            DensityComboBox.SelectedIndex = settings.AppCompactMode ? 1 : 0;
            SidebarPositionComboBox.SelectedIndex = settings.SidebarPosition == "right" ? 1 : 0;
            SidebarRailToggle.IsOn = settings.SidebarRail;
        }

        private void LayoutPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            ChangeLayout((LayoutPresetComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "streamlined");
        }

        // Compact sizing is merged into the app's resources as it starts (App.xaml.cs).
        private void DensityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!suppressSettingsEvents) settings.AppCompactMode = DensityComboBox.SelectedIndex == 1;
        }

        private void SidebarPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.SidebarPosition = SidebarPositionComboBox.SelectedIndex == 1 ? "right" : "left";
            ApplySidebarLayout();
        }

        private void SidebarRailToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.SidebarRail = SidebarRailToggle.IsOn;
            sidebarPeek = false;
            ApplySidebarLayout();
        }

        private void StatusBarToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.StatusBarOpen = StatusBarToggle.IsOn;
            ApplyStatusBarVisibility();
        }

        private void DecorativeCardToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.ShowDecorativeCard = DecorativeCardToggle.IsOn;
            ApplyDecorativeCardVisibility();
        }

        // The sidebar's illustrated card: a setting, and it gives its space to the lists in a short window.
        private void ApplyDecorativeCardVisibility() =>
            DecorativeCard.Visibility = settings.ShowDecorativeCard && !SidebarNarrow && ((FrameworkElement)Content).ActualHeight is var h && (h == 0 || h >= 800)
                ? Visibility.Visible : Visibility.Collapsed;

        // --- Editor ---

        private static readonly string[] PageWidthPresets = { "720px", "900px", "1200px", "100%" };

        private void LoadPageWidth()
        {
            var index = System.Array.IndexOf(PageWidthPresets, settings.EditorAreaWidth);
            PageWidthComboBox.SelectedIndex = index >= 0 ? index : 4;
            EditorAreaWidthBox.Text = settings.EditorAreaWidth;
            EditorAreaWidthBox.Visibility = index >= 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PageWidthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tag = (PageWidthComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
            EditorAreaWidthBox.Visibility = tag == "custom" ? Visibility.Visible : Visibility.Collapsed;
            if (suppressSettingsEvents || tag == null) return;
            if (tag == "custom") EditorAreaWidthBox.Focus(FocusState.Programmatic);
            else
            {
                settings.EditorAreaWidth = tag;
                PushPageWidth(); // Distraction-free keeps its own width
            }
        }

        private void TypewriterToggle_Toggled(object sender, RoutedEventArgs e) { if (!suppressSettingsEvents) settings.Typewriter = TypewriterToggle.IsOn; }

        private void SelectionToolbarToggle_Toggled(object sender, RoutedEventArgs e) { if (!suppressSettingsEvents) settings.SelectionToolbar = SelectionToolbarToggle.IsOn; }

        // The document map (a strip at the right edge of the note, drawn by the page): the switch is in Settings and in the View menu;
        // either one sets the other.
        private void DocumentMapToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.DocumentMap = DocumentMapToggle.IsOn;
            SyncDocumentMapToggles();
        }

        private void DocumentMapMenuItem_Click(object sender, RoutedEventArgs e)
        {
            settings.DocumentMap = DocumentMapMenuItem.IsChecked;
            SyncDocumentMapToggles();
        }

        private void SyncDocumentMapToggles()
        {
            var before = suppressSettingsEvents;
            suppressSettingsEvents = true;
            DocumentMapToggle.IsOn = DocumentMapMenuItem.IsChecked = settings.DocumentMap;
            suppressSettingsEvents = before;
        }

        // The switch is in Settings and in the Review panel of the sidebar; either one sets the other.
        private void ReviewMarksToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (suppressSettingsEvents) return;
            settings.ShowReviewMarks = ((ToggleSwitch)sender).IsOn;
            SyncReviewMarksToggles();
        }

        private void SyncReviewMarksToggles()
        {
            var before = suppressSettingsEvents;
            suppressSettingsEvents = true;
            ReviewMarksToggle.IsOn = ReviewPanelMarksToggle.IsOn = settings.ShowReviewMarks;
            suppressSettingsEvents = before;
        }

        private void FocusModeToggle_Toggled(object sender, RoutedEventArgs e) { if (!suppressSettingsEvents) settings.FocusMode = FocusModeToggle.IsOn; }

        // --- Convert ---

        private void SettingsOpenConvert_Click(object sender, RoutedEventArgs e)
        {
            HideSettingsPage();
            HomeConvertButton_Click(sender, e);
        }
    }
}
