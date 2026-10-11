using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// An entry disabled while its menu is closed (Cut once nothing is selected, Focus mode as reading mode comes on)
    /// takes its disabled colours from Windows' theme on XAML islands, not the window's: white on a light menu under dark
    /// Windows, next to invisible on a dark one under light Windows. Every disabled entry of a watched menu is put into
    /// its disabled state again as it is shown, where it takes the window's (RD02).
    /// </summary>
    public static class DisabledMenuLook
    {
        private static readonly ConditionalWeakTable<MenuFlyoutItemBase, object> watched = new();

        /// <summary>The menu's entries, as it opens: entries added since are watched too.</summary>
        public static void Watch(MenuFlyout menu) => menu.Opening += (_, _) => Watch(menu.Items);

        public static void Watch(IEnumerable<MenuFlyoutItemBase> items)
        {
            foreach (var item in items)
            {
                if (watched.TryGetValue(item, out _)) continue;
                watched.Add(item, null);
                item.Loaded += OnLoaded;
                // A submenu's entries: now, and again each time it is shown (some are made while the app runs).
                if (item is MenuFlyoutSubItem sub) Watch(sub.Items);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutSubItem sub) Watch(sub.Items);
            if (sender is not Control item || item.IsEnabled)
                return;
            VisualStateManager.GoToState(item, "Normal", false);
            VisualStateManager.GoToState(item, "Disabled", false);
        }
    }
}
