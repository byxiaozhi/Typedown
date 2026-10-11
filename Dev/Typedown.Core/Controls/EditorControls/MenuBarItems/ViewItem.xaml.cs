using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Typedown.Core.Utilities;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls.EditorControls.MenuBarItems
{
    public sealed partial class ViewItem : MenuBarItemBase
    {
        public ViewItem()
        {
            InitializeComponent();
            // The event is static: a closed window's menu must not stay reachable through it.
            Unloaded += (_, _) => ThemeFiles.Changed -= OnThemesChanged;
        }

        /// <summary>The theme entries, so a change can tick the right one without rebuilding the open menu.</summary>
        private readonly List<(muxc.RadioMenuFlyoutItem Item, string CustomId, Enums.AppTheme? BuiltIn)> themeItems = new();

        private void BuildThemeMenu()
        {
            if (Settings == null || ThemeSubMenu == null) return;
            ThemeSubMenu.Items.Clear();
            themeItems.Clear();
            foreach (var value in Enums.Enumerable.AppThemes)
            {
                var theme = value;
                var item = new muxc.RadioMenuFlyoutItem { Text = EnumName(theme), GroupName = "AppTheme" };
                item.Click += (_, _) => Settings.ApplyBuiltInTheme(theme);
                themeItems.Add((item, null, theme));
                ThemeSubMenu.Items.Add(item);
            }
            var customThemes = ThemeFiles.List();
            if (customThemes.Count > 0)
            {
                ThemeSubMenu.Items.Add(new MenuFlyoutSeparator());
                foreach (var custom in customThemes)
                {
                    var id = custom.Id;
                    var theme = custom;
                    var item = new muxc.RadioMenuFlyoutItem { Text = ThemeFiles.DisplayName(custom, customThemes), GroupName = "AppTheme" };
                    item.Click += (_, _) => Settings.ApplyCustomTheme(theme);
                    themeItems.Add((item, id, null));
                    ThemeSubMenu.Items.Add(item);
                }
            }
            ThemeSubMenu.Items.Add(new MenuFlyoutSeparator());
            var designer = new MenuFlyoutItem { Text = (Locale.GetString("View.CustomTheme.Title") ?? "Theme designer") + "…" };
            designer.Click += OnOpenThemeDesigner;
            ThemeSubMenu.Items.Add(designer);
            // Picks up a theme that was just added or edited without restarting. The rebuild waits for the click
            // to be over: the menu would otherwise be torn down while it is still on screen.
            var reload = new MenuFlyoutItem { Text = Locale.GetString("View.CustomTheme.Refresh") };
            reload.Click += (_, _) => _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, RefreshThemeMenu);
            ThemeSubMenu.Items.Add(reload);
            UpdateThemeChecks();
        }

        private bool editionItemsAdded;

        // An edition's own entries (EditionHooks.AddViewMenuItems), below the theme submenu. Once per menu: they are
        // the window's own, and the menu watches its commands only once.
        private void AddEditionItems()
        {
            if (editionItemsAdded || ViewModel == null) return;
            editionItemsAdded = true;
            var index = Items.IndexOf(ThemeSubMenu);
            if (index < 0) return;
            foreach (var item in EditionHooks.ViewMenuItems(ViewModel))
                Items.Insert(++index, item);
        }

        private void RefreshThemeMenu()
        {
            // A fresh submenu in its place (SubMenus): one that has been on screen keeps drawing its old entries.
            var fresh = new MenuFlyoutSubItem { Text = ThemeSubMenu.Text, Name = nameof(ThemeSubMenu) };
            if (SubMenus.Replace(Items, ThemeSubMenu, fresh)) ThemeSubMenu = fresh;
            BuildThemeMenu();
            // Re-apply the current theme too, so edits to the file that is already selected take effect —
            // the same as the settings page's refresh. Rebuilding the menu alone only picks up added or
            // renamed theme files, not changes to the CSS of the theme currently in use.
            Settings?.OnPropertyChanged(nameof(Settings.CustomTheme), null, Settings.CustomTheme);
        }

        // A folder of themes added or removed (ThemeFiles.AddFolder/RemoveFolder, by an edition): the menu shows the
        // themes there are now, as "Reload themes" would.
        private void OnThemesChanged() => _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, RefreshThemeMenu);

        /// <summary>The theme submenu's entries as shown, and its "Reload themes" item (the automation test host clicks it).</summary>
        public (IReadOnlyList<string> Entries, MenuFlyoutItem Reload) ThemeMenu()
        {
            var entries = ThemeSubMenu?.Items.OfType<MenuFlyoutItem>().Select(x => x.Text).ToList() ?? new List<string>();
            var reload = ThemeSubMenu?.Items.OfType<MenuFlyoutItem>().LastOrDefault();
            return (entries, reload);
        }

        private async void OnOpenThemeDesigner(object sender, Windows.UI.Xaml.RoutedEventArgs e)
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

        /// <summary>The name a theme goes by in the menu, from the enum's own locale attribute.</summary>
        private static string EnumName(Enums.AppTheme theme)
        {
            var field = typeof(Enums.AppTheme).GetField(theme.ToString());
            var attribute = field?.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute;
            return attribute?.Text ?? theme.ToString();
        }

        private void UpdateThemeChecks()
        {
            if (Settings == null) return;
            var custom = Settings.CustomTheme;
            foreach (var (item, id, builtIn) in themeItems)
                item.IsChecked = id != null
                    ? id == custom
                    : string.IsNullOrEmpty(custom) && builtIn == Settings.AppTheme;
        }

        protected override void OnRegisterShortcut()
        {
            BuildThemeMenu();
            AddEditionItems();
            ThemeFiles.Changed -= OnThemesChanged;
            ThemeFiles.Changed += OnThemesChanged;
            // Focus and typewriter mode both follow the caret, so they mean nothing in reading mode (which has
            // no caret) or in source mode (which is a plain text editor): grey them out instead of letting them
            // look enabled while doing nothing. The settings themselves are kept for when the mode is left.
            UpdateModeAvailability();
            Subscribe(Settings);
            RegisterWindowShortcut(Settings.ShortcutSidePane, SidePaneItem);
            RegisterWindowShortcut(Settings.ShortcutSourceCodeMode, SourceCodeModeItem);
            RegisterWindowShortcut(Settings.ShortcutFocusMode, FocusModeItem);
            RegisterWindowShortcut(Settings.ShortcutTypewriterMode, TypewriterModeItem);
            RegisterWindowShortcut(Settings.ShortcutStatusBar, StatusBarItem);
            RegisterWindowShortcut(Settings.ShortcutFullScreen, FullScreenItem);
            RegisterWindowShortcut(Settings.ShortcutReadOnlyMode, ReadOnlyModeItem);
            RegisterWindowShortcut(Settings.ShortcutNextTab, NextTabItem);
            RegisterWindowShortcut(Settings.ShortcutPreviousTab, PreviousTabItem);
            RegisterTabNumberShortcuts();
            // Bind the shortcut through the menu item so the key is shown next to it (it runs the item's own
            // command); the action overload leaves the entry with no visible shortcut.
            RegisterWindowShortcut(new Models.ShortcutKey(Windows.System.VirtualKeyModifiers.Menu, Windows.System.VirtualKey.Number0), LastUsedTabItem);
        }

        /// <summary>
        /// Alt+1..9 switch to a tab by position, 9 being the last one however many there are. Alt rather than
        /// Ctrl because Ctrl+1..6 already sets the heading level. They are fixed, not bindable, so they have no
        /// menu entry.
        /// </summary>
        private void RegisterTabNumberShortcuts()
        {
            var tabs = ViewModel?.TabsViewModel;
            if (tabs == null) return;
            for (var n = 1; n <= 9; n++)
            {
                var index = n == 9 ? int.MaxValue : n - 1;
                var key = new Models.ShortcutKey(Windows.System.VirtualKeyModifiers.Menu,
                    (Windows.System.VirtualKey)((int)Windows.System.VirtualKey.Number0 + n));
                RegisterWindowShortcut(key, () => tabs.SwitchTabIndexCommand.Execute(index));
            }
        }

        /// <summary>
        /// The settings outlive this control, so the subscription is remembered rather than looked up again on
        /// the way out: the data context is already gone by the time a torn-down menu bar unloads, and a
        /// subscription that cannot be found is a subscription that is never removed — the stale handler then
        /// runs on the next mode switch with no data context and takes the app down.
        /// </summary>
        private ViewModels.SettingsViewModel subscribed;

        private void Subscribe(ViewModels.SettingsViewModel settings)
        {
            if (subscribed == settings) return;
            Unsubscribe();
            subscribed = settings;
            if (subscribed != null)
                subscribed.PropertyChanged += OnSettingsChanged;
        }

        private void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.PropertyChanged -= OnSettingsChanged;
            subscribed = null;
        }

        private void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Settings == null)
            {
                Unsubscribe();
                return;
            }
            if (e.PropertyName is nameof(Settings.ReadOnly) or nameof(Settings.SourceCode))
                UpdateModeAvailability();
            if (e.PropertyName is nameof(Settings.AppTheme) or nameof(Settings.CustomTheme))
                UpdateThemeChecks();
        }

        private void UpdateModeAvailability()
        {
            if (Settings == null || FocusModeItem == null || TypewriterModeItem == null)
                return;
            var caretModes = !Settings.ReadOnly && !Settings.SourceCode;
            FocusModeItem.IsEnabled = caretModes;
            TypewriterModeItem.IsEnabled = caretModes;
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Unsubscribe();
            Bindings?.StopTracking();
        }
    }
}
