using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Utilities;
using Windows.System;

namespace Typedown.WinUI
{
    // Layout (interface review, phase 3): the three presets, where the sidebar sits and how wide, and
    // moving between the window's areas from the keyboard.
    //  - Classic: menu row, formatting toolbar row, View / Code / Split in the title bar (as before 1.5).
    //  - Streamlined: the toolbar beside the menu (one row, as in Notepad), the view switch in the status bar.
    //  - Distraction-free (F11): no command row until F10, Alt or the pointer at its edge brings it back,
    //    a narrow sidebar, a medium-width page and only the word count in the status bar.
    public sealed partial class MainWindow
    {
        private bool distractionFree;
        private bool commandRowRevealed;
        private bool sidebarPeek;

        private void ApplyLayout()
        {
            var preset = settings.LayoutPreset;
            var wasDistractionFree = distractionFree;
            distractionFree = preset == "distraction";
            var streamlined = preset != "classic";

            if (streamlined && ReferenceEquals(FormatToolbar.Child, FormatCommandBar))
            {
                FormatToolbar.Child = null;
                CommandRowToolbarHost.Child = FormatCommandBar;
            }
            else if (!streamlined && ReferenceEquals(CommandRowToolbarHost.Child, FormatCommandBar))
            {
                CommandRowToolbarHost.Child = null;
                FormatToolbar.Child = FormatCommandBar;
            }
            CommandRowSeparator.Visibility = streamlined ? Visibility.Visible : Visibility.Collapsed;
            CommandRow.BorderThickness = streamlined ? new Thickness(0, 0, 0, 1) : new Thickness(0);

            var viewModeHost = preset == "classic" ? TitleViewModeHost : StatusViewModeHost;
            if (!ReferenceEquals(viewModeHost.Child, ViewModeSwitch))
            {
                TitleViewModeHost.Child = null;
                StatusViewModeHost.Child = null;
                viewModeHost.Child = ViewModeSwitch;
            }

            if (!distractionFree) commandRowRevealed = false;
            CommandRow.Visibility = distractionFree && !commandRowRevealed ? Visibility.Collapsed : Visibility.Visible;
            StatusBarFormatText.Visibility = distractionFree ? Visibility.Collapsed : Visibility.Visible;
            StatusViewModeHost.Visibility = distractionFree ? Visibility.Collapsed : Visibility.Visible;
            UpdateToolbarVisibility();
            ApplySidebarLayout();
            if (distractionFree != wasDistractionFree) PushPageWidth();
        }

        // The formatting commands only apply in View mode (the source editor doesn't understand them).
        private void UpdateToolbarVisibility()
        {
            var view = CurrentViewMode == "view";
            FormatCommandBar.Visibility = view ? Visibility.Visible : Visibility.Collapsed;
            FormatToolbar.Visibility = view && ReferenceEquals(FormatToolbar.Child, FormatCommandBar) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Distraction-free reads at a medium width (900 px) unless the page is already narrower (a preset
        // or a custom width in px); the setting itself stays as it is.
        private string EffectivePageWidth =>
            !distractionFree || PixelWidth(settings.EditorAreaWidth) is < 900 ? settings.EditorAreaWidth : "900px";

        // "720px" → 720; anything else (a percentage, "100%", nonsense) → null, which counts as wider.
        private static double? PixelWidth(string width) =>
            width?.Trim() is { } w && w.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(w[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var px) && px > 0
                ? px : null;

        // Also what the editor gets at startup (GetSettings), so a window that opens in Distraction-free
        // starts at that width; and after the page-width setting changes, which posts the setting itself.
        private void PushPageWidth() =>
            PostMessage("SettingsChanged", new Dictionary<string, object> { { "EditorAreaWidth", EffectivePageWidth } });

        // --- Sidebar: left or right, full or narrow (icons only) ---

        private bool SidebarNarrow => (settings.SidebarRail || distractionFree) && !sidebarPeek;

        private void ApplySidebarLayout()
        {
            var visible = TocMenuItem.IsChecked;
            var right = settings.SidebarPosition == "right";
            var narrow = SidebarNarrow;
            var width = !visible ? 0 : narrow ? 48 : 260;
            Grid.SetColumn(TocPane, right ? 1 : 0);
            Grid.SetColumn(EditorArea, right ? 0 : 1);
            TocColumn.Width = right ? new GridLength(1, GridUnitType.Star) : new GridLength(width);
            EditorColumn.Width = right ? new GridLength(width) : new GridLength(1, GridUnitType.Star);
            TocPane.BorderThickness = right ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
            TocPane.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            NewNoteText.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            NewNoteButton.Margin = narrow ? new Thickness(4, 8, 4, 4) : new Thickness(8, 8, 8, 4);
            ToolTipService.SetToolTip(NewNoteButton, narrow ? Locale.GetString("NewNote") : null);
            NavCreateHeaderText.Visibility = NavLibraryHeaderButton.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            UpdateLibraryHeader(); // the narrow strip always shows the icons
            foreach (var item in NavItems)
            {
                var label = ((Panel)item.Content).Children.OfType<TextBlock>().First();
                label.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
                ToolTipService.SetToolTip(item, narrow ? label.Text : null);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, label.Text); // named even when only the icon shows
            }
            SidebarDivider.Visibility = SidebarLower.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            if (narrow)
            {
                foreach (var panel in new FrameworkElement[] { HomePanel, RecentNavListView, FavoritesPanel, TemplatesPanel, TrashPanel })
                    panel.Visibility = Visibility.Collapsed;
            }
            else ShowNavPanels(SelectedNavTag);
            ApplyDecorativeCardVisibility();

            var railAvailable = visible && (settings.SidebarRail || distractionFree);
            SidebarRailButton.Visibility = railAvailable ? Visibility.Visible : Visibility.Collapsed;
            SidebarRailGlyph.Glyph = narrow ? "" : "";
            var railTip = Locale.GetString(narrow ? "ExpandSidebar" : "CollapseSidebar");
            ToolTipService.SetToolTip(SidebarRailButton, railTip);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SidebarRailButton, railTip);
        }

        private void SidebarRailButton_Click(object sender, RoutedEventArgs e)
        {
            sidebarPeek = !sidebarPeek;
            ApplySidebarLayout();
        }

        // Choosing a list in the narrow sidebar widens it to show the list.
        private void PeekSidebarFor(string tag)
        {
            if (!SidebarNarrow || tag is null or "Convert" or "Emails") return;
            sidebarPeek = true;
            ApplySidebarLayout();
        }

        // --- Distraction-free: the command row on demand ---

        private void RevealCommandRow(bool focusMenu)
        {
            if (distractionFree && !commandRowRevealed)
            {
                commandRowRevealed = true;
                CommandRow.Visibility = Visibility.Visible;
            }
            if (focusMenu && AppMenuBar.Items.FirstOrDefault() is Control first) first.Focus(FocusState.Keyboard);
        }

        private void HideRevealedCommandRow()
        {
            if (!distractionFree || !commandRowRevealed || FocusIsWithin(CommandRow)) return;
            commandRowRevealed = false;
            CommandRow.Visibility = Visibility.Collapsed;
        }

        private void CommandRow_PointerExited(object sender, PointerRoutedEventArgs e) =>
            DispatcherQueue.TryEnqueue(HideRevealedCommandRow);

        // The pointer at the top edge of the page brings the command row back.
        private void MainArea_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (distractionFree && !commandRowRevealed && e.GetCurrentPoint(MainArea).Position.Y < 6) RevealCommandRow(false);
        }

        private void CommandRow_LostFocus(object sender, RoutedEventArgs e) =>
            DispatcherQueue.TryEnqueue(HideRevealedCommandRow);

        // F11: in and out of Distraction-free, full screen, back to the layout it came from.
        private DateTime lastLayoutToggle;

        private void ToggleDistractionFree()
        {
            // A held key repeats (the window's own F11 too): one switch per press.
            if ((DateTime.UtcNow - lastLayoutToggle).TotalMilliseconds < 600) return;
            lastLayoutToggle = DateTime.UtcNow;
            ChangeLayout(settings.LayoutPreset != "distraction" ? "distraction" : settings.LayoutBeforeDistraction is "classic" ? "classic" : "streamlined");
        }

        // Every change of layout (F11, Settings): where F11 goes back to, full screen, then the layout.
        private void ChangeLayout(string preset)
        {
            if (preset == "distraction" && settings.LayoutPreset != "distraction") settings.LayoutBeforeDistraction = settings.LayoutPreset;
            settings.LayoutPreset = preset;
            sidebarPeek = false;
            SyncPresenter();
            ApplyLayout();
            Log($"Layout: {preset}");
        }

        // Distraction-free is full screen; every other layout is an ordinary window (also at startup).
        private void SyncPresenter()
        {
            var kind = settings.LayoutPreset == "distraction" ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped;
            if (AppWindow.Presenter.Kind == kind) return;
            AppWindow.SetPresenter(kind);
            if (kind == AppWindowPresenterKind.Overlapped) ApplyTopmost(); // a new presenter: always on top again
        }

        // --- F6 / Shift+F6: tabs, command row, toolbar, sidebar, page, status bar ---

        private bool FocusIsWithin(UIElement area)
        {
            for (var e = FocusManager.GetFocusedElement(Content.XamlRoot) as DependencyObject; e != null; e = VisualTreeHelper.GetParent(e))
                if (ReferenceEquals(e, area)) return true;
            return false;
        }

        private void CycleArea(int direction)
        {
            var areas = new List<(UIElement Area, Func<bool> Focus)>
            {
                (AppTitleBar, () => DocumentTabView.Visibility == Visibility.Visible
                    && (DocumentTabView.SelectedItem as Control ?? DocumentTabView.ContainerFromIndex(DocumentTabView.SelectedIndex) as Control)?.Focus(FocusState.Keyboard) == true),
                (CommandRow, () => CommandRow.Visibility == Visibility.Visible && AppMenuBar.Items.FirstOrDefault() is Control c && c.Focus(FocusState.Keyboard)),
                (FormatCommandBar, () => ToolbarShown && FormatCommandBar.PrimaryCommands.OfType<Control>().FirstOrDefault()?.Focus(FocusState.Keyboard) == true),
                (TocPane, () => TocPane.Visibility == Visibility.Visible && FocusSidebar()),
                (EditorArea, () => !startPageShown && !SettingsPageShown && EditorView.Focus(FocusState.Keyboard)),
                (StatusBar, () => StatusBar.Visibility == Visibility.Visible && StatusBarWordCountButton.Focus(FocusState.Keyboard)),
            };
            // The toolbar sits inside the command row in Streamlined, so it's recognised first.
            var current = FocusIsWithin(FormatCommandBar) ? 2 : areas.FindIndex(a => FocusIsWithin(a.Area));
            if (current < 0) current = 4; // the page (WebView2 focus isn't in the XAML tree)
            for (var step = 1; step <= areas.Count; step++)
            {
                var next = ((current + direction * step) % areas.Count + areas.Count) % areas.Count;
                if (areas[next].Focus()) return;
            }
        }

        // In its own row (Classic) or in the command row (the other layouts), when either is showing.
        private bool ToolbarShown => FormatCommandBar.Visibility == Visibility.Visible
            && (ReferenceEquals(FormatToolbar.Child, FormatCommandBar) ? FormatToolbar.Visibility : CommandRow.Visibility) == Visibility.Visible;

        private bool FocusSidebar()
        {
            var selected = NavCreateListView.SelectedItem ?? NavLibraryListView.SelectedItem;
            if (selected is Control item && item.Focus(FocusState.Keyboard)) return true;
            return NewNoteButton.Focus(FocusState.Keyboard);
        }

        private void SetUpLayoutKeys()
        {
            void Key(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
            {
                var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
                accelerator.Invoked += (s, e) => { e.Handled = true; action(); };
                ((UIElement)Content).KeyboardAccelerators.Add(accelerator);
            }
            Key(VirtualKey.F6, VirtualKeyModifiers.None, () => CycleArea(+1));
            Key(VirtualKey.F6, VirtualKeyModifiers.Shift, () => CycleArea(-1));
            Key(VirtualKey.F10, VirtualKeyModifiers.None, () => RevealCommandRow(true));
            Key(VirtualKey.F11, VirtualKeyModifiers.None, ToggleDistractionFree);
        }
    }
}
