using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// A submenu whose entries change while the window is open. One that has been on screen goes on drawing the entries
    /// it had then, whatever its Items say later: entries added or renamed did not appear, and radio entries made anew
    /// showed no tick (or the old one) until the main page was built again, by a visit to the settings. So a submenu
    /// already shown is never emptied and filled again: a tick is moved on the entries there are, and other changes
    /// put a fresh submenu in its place (<see cref="Replace"/>).
    /// </summary>
    public static class SubMenus
    {
        /// <summary>
        /// <paramref name="fresh"/> in the place of <paramref name="old"/> among <paramref name="items"/>, removed and
        /// inserted (the menu bar's flyout follows insertions and removals, not a replacement). False when it is not there.
        /// </summary>
        public static bool Replace(IList<MenuFlyoutItemBase> items, MenuFlyoutSubItem old, MenuFlyoutSubItem fresh)
        {
            var index = items?.IndexOf(old) ?? -1;
            if (index < 0) return false;
            items.RemoveAt(index);
            items.Insert(index, fresh);
            return true;
        }

        /// <summary>The menu bar entry whose items hold <paramref name="menu"/>, looked for under <paramref name="root"/> (a window's content).</summary>
        public static muxc.MenuBarItem FindMenuBarItem(DependencyObject root, MenuFlyoutSubItem menu)
        {
            if (root == null) return null;
            if (root is muxc.MenuBarItem item && item.Items.Contains(menu)) return item;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
                if (FindMenuBarItem(VisualTreeHelper.GetChild(root, i), menu) is { } found) return found;
            return null;
        }
    }
}
