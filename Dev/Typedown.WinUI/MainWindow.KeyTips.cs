using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.System;
using Windows.UI.ViewManagement;
using Windows.Foundation;

namespace Typedown.WinUI
{
    // New since the fork: key tips, as in Office. Alt pressed and released shows a small badge with a key on every menu, every group of the toolbar
    // and every item of the sidebar; the key does what a click would (a menu opens, the keyboard goes to the group, the item is chosen). Alt and the
    // key together do it at once. Esc hides the badges and the keyboard goes back where it was; an arrow key, Enter or F10 hands the keyboard to the menu
    // bar, which is what Alt alone did before. Which key goes where is decided by Services/KeyTipPlan.cs: the menus keep the keys they had.
    public sealed partial class MainWindow
    {
        // `Usable`: it can be seen and used now. Every target has its key whether it can or not, so that a key stays the same when a page covers the toolbar or the window
        // is narrower.
        private sealed record KeyTipTarget(string Id, FrameworkElement Element, string Label, string Preferred, bool AtRight, bool Usable, Action Activate);

        private List<(KeyTipTarget Target, string Key)> keyTips = new();
        private bool keyTipsShown;
        private bool keyTipsFromEditor;
        private bool altAlone;
        // The scroll areas the badges sit in: a scroll moves what the badges point at, so it puts them away.
        private readonly List<ScrollViewer> keyTipScrollers = new();
        // Where the keyboard was in the window (not the editor) when the tips came up: Esc gives it back.
        private Control keyTipReturnFocus;

        private static readonly string[] MenuAccessKeyNames = { "AccessKeyFile", "AccessKeyEdit", "AccessKeyParagraph", "AccessKeyFormat", "AccessKeyReview", "AccessKeyView", "AccessKeyHelp" };

        private void SetUpKeyboardLayer()
        {
            var root = (UIElement)Content;
            // Alt pressed and released on its own, while the keyboard is in the window: the tips (from the editor, the page tells: HostShortcut).
            root.PreviewKeyDown += (_, e) =>
            {
                if (e.Key is VirtualKey.Menu or VirtualKey.LeftMenu)
                {
                    altAlone = !e.KeyStatus.WasKeyDown;
                    return;
                }
                altAlone = false;
                // Alt and a key together: what that key's tip does, at once (AltGr is Ctrl+Alt: never ours).
                if (e.KeyStatus.IsMenuKeyDown && !CtrlIsDown() && !keyTipsShown && KeyOfVirtualKey(e.Key) is { } key && ActivateKeyTip(key, fromEditor: false))
                    e.Handled = true;
            };
            root.PreviewKeyUp += (_, e) =>
            {
                if (e.Key is not (VirtualKey.Menu or VirtualKey.LeftMenu) || !altAlone) return;
                altAlone = false;
                if (ShowKeyTips(fromEditor: false)) e.Handled = true;
            };
            // The wheel, a resize: what the badges were placed on has moved.
            root.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((_, _) => { if (keyTipsShown) HideKeyTips(restore: true); }), true);
            ((FrameworkElement)root).SizeChanged += (_, _) => { if (keyTipsShown) HideKeyTips(restore: true); };
            // A click anywhere puts the badges away (the click does its own thing).
            root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => { if (keyTipsShown) HideKeyTips(restore: false); }), true);
            // A menu that is open takes the keys in its own popup, not through the window: Alt and another menu's letter goes from one menu to the next, as it does in
            // any Windows app (the items of every menu and sub-menu listen).
            foreach (var menu in AppMenuBar.Items) ListenForAltKeys(menu.Items);
            // What a screen reader says about the menus: Alt and the letter.
            for (var i = 0; i < AppMenuBar.Items.Count && i < MenuAccessKeyNames.Length; i++)
                AutomationProperties.SetAccessKey(AppMenuBar.Items[i], "Alt+" + Locale.GetString(MenuAccessKeyNames[i]));
        }

        private void ListenForAltKeys(IList<MenuFlyoutItemBase> items)
        {
            foreach (var item in items)
            {
                if (item is MenuFlyoutSubItem sub) ListenForAltKeys(sub.Items);
                item.PreviewKeyDown += (_, e) =>
                {
                    if (e.KeyStatus.IsMenuKeyDown && !CtrlIsDown() && KeyOfVirtualKey(e.Key) is { } key && ActivateKeyTip(key, fromEditor: false)) e.Handled = true;
                };
            }
        }

        private static bool CtrlIsDown() => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // The letter or digit of a key, or null.
        private static string KeyOfVirtualKey(VirtualKey key)
        {
            if (key >= VirtualKey.A && key <= VirtualKey.Z) return ((char)('A' + (key - VirtualKey.A))).ToString();
            if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9) return ((char)('0' + (key - VirtualKey.Number0))).ToString();
            if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9) return ((char)('0' + (key - VirtualKey.NumberPad0))).ToString();
            return null;
        }

        // Only what can be seen and used now: not behind a page (Settings), not collapsed, not in the toolbar's overflow.
        private bool KeyTipsAvailable => !SettingsPageShown && PalettePanel.Visibility != Visibility.Visible && ShortcutsPanel.Visibility != Visibility.Visible
            && ConvertDropOverlay.Visibility != Visibility.Visible;

        // Can be seen: not collapsed, and most of it inside the window and inside every scroll area around it (a sidebar item that was scrolled out of its list
        // is in the tree but has no badge, as it cannot be reached where it was).
        private static bool OnScreen(FrameworkElement element)
        {
            if (element == null || element.ActualWidth <= 0 || element.ActualHeight <= 0 || element.XamlRoot?.Content is not FrameworkElement root) return false;
            for (DependencyObject d = element; d != null; d = VisualTreeHelper.GetParent(d))
                if (d is UIElement { Visibility: Visibility.Collapsed }) return false;
            KeyTipPlan.Box BoxOf(FrameworkElement e)
            {
                var r = e.TransformToVisual(root).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
                return new KeyTipPlan.Box(r.X, r.Y, r.Width, r.Height);
            }
            var areas = new List<KeyTipPlan.Box> { new(0, 0, root.ActualWidth, root.ActualHeight) };
            areas.AddRange(ScrollersAround(element).Select(BoxOf));
            return KeyTipPlan.Reachable(BoxOf(element), areas);
        }

        private static IEnumerable<ScrollViewer> ScrollersAround(DependencyObject element)
        {
            for (var d = VisualTreeHelper.GetParent(element); d != null; d = VisualTreeHelper.GetParent(d))
                if (d is ScrollViewer viewer) yield return viewer;
        }

        private List<KeyTipTarget> CollectKeyTips()
        {
            var list = new List<KeyTipTarget>();
            // the menus
            for (var i = 0; i < AppMenuBar.Items.Count; i++)
            {
                var item = AppMenuBar.Items[i];
                var preferred = i < MenuAccessKeyNames.Length ? Locale.GetString(MenuAccessKeyNames[i]) : null;
                list.Add(new KeyTipTarget("menu" + i, item, item.Title, preferred, false, OnScreen(item), () => OpenMenu(item)));
            }
            // the groups of the toolbar (between its separators): the keyboard goes to the first button of the group. A page over the editor (Convert, Compare, the start
            // page) covers the toolbar: it is there but cannot be used.
            var toolbarCovered = ConvertPage.Visibility == Visibility.Visible || ConvertComparePanel.Visibility == Visibility.Visible || startPageShown;
            var groups = new List<List<AppBarButton>> { new() };
            foreach (var command in FormatCommandBar.PrimaryCommands)
            {
                if (command is AppBarSeparator) { if (groups[^1].Count > 0) groups.Add(new()); }
                else if (command is AppBarButton button) groups[^1].Add(button);
            }
            var number = 0;
            foreach (var group in groups.Where(g => g.Count > 0))
            {
                var first = group[0];
                list.Add(new KeyTipTarget("group" + number++, first, first.Label, null, false, !toolbarCovered && !first.IsInOverflow && OnScreen(first), () => first.Focus(FocusState.Keyboard)));
            }
            // the house button left of the tabs
            list.Add(new KeyTipTarget("buttonHome", HomeButton, Locale.GetString("Home"), null, false, OnScreen(HomeButton), () => ToggleHome()));
            // the sidebar: the buttons at its top, then its navigation items
            foreach (var (button, label) in new[] { ((Button)NewNoteButton, Locale.GetString("NewNote")), (SaveNoteButton, Locale.GetString("SaveMenuItem_Text")) })
                list.Add(new KeyTipTarget("button" + button.Name, button, label, null, false, OnScreen(button), () => Invoke(button)));
            foreach (var item in NavItems)
            {
                var tag = item.Tag as string;
                if (tag == null) continue;
                var label = (item.Content as Panel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? tag;
                list.Add(new KeyTipTarget("nav" + tag, item, label, null, true, OnScreen(item), () => { SelectNav(tag); item.Focus(FocusState.Keyboard); }));
            }
            return list;
        }

        private static void Invoke(FrameworkElement element)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
            if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke) invoke.Invoke();
        }

        private static void OpenMenu(MenuBarItem item)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(item) ?? FrameworkElementAutomationPeer.CreatePeerForElement(item);
            if (peer?.GetPattern(PatternInterface.ExpandCollapse) is IExpandCollapseProvider expand) expand.Expand();
            else item.Focus(FocusState.Keyboard);
        }

        private List<(KeyTipTarget Target, string Key)> PlanKeyTips()
        {
            var targets = CollectKeyTips();
            var plan = KeyTipPlan.Assign(targets.Select(t => new KeyTipPlan.Request(t.Id, t.Label, t.Preferred)).ToList());
            return targets.Where(t => t.Usable && plan.ContainsKey(t.Id)).Select(t => (t, plan[t.Id])).ToList();
        }

        // Alt and the key: the tip of that key does its work now, without the badges. False when no tip has the key.
        private bool ActivateKeyTip(string key, bool fromEditor)
        {
            if (!KeyTipsAvailable) return false;
            RevealCommandRow(false);
            CommandRow.UpdateLayout();
            var hit = PlanKeyTips().FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
            if (hit.Target == null) return false;
            hit.Target.Activate();
            return true;
        }

        // Alt pressed and released. False when the tips cannot be shown (the Settings page is up, a panel is open): the caller does what Alt did before.
        private bool ShowKeyTips(bool fromEditor)
        {
            if (keyTipsShown)
            {
                HideKeyTips(restore: true);
                return true;
            }
            if (!KeyTipsAvailable) return false;
            RevealCommandRow(false);
            CommandRow.UpdateLayout();
            keyTips = PlanKeyTips();
            if (keyTips.Count == 0) return false;

            keyTipsFromEditor = fromEditor;
            keyTipReturnFocus = fromEditor ? null : FocusManager.GetFocusedElement(Content.XamlRoot) as Control;
            var contrast = new AccessibilitySettings().HighContrast;
            KeyTipLayer.Children.Clear();
            foreach (var (target, key) in keyTips)
            {
                var badge = new Border
                {
                    Height = 18, MinWidth = 18, Padding = new Thickness(5, 0, 5, 0), CornerRadius = new CornerRadius(5),
                    // accent fill; with a contrast theme the system colours, inverted
                    Background = Resource(contrast ? "SystemColorWindowTextColorBrush" : "CaretPrimaryBrush"),
                    BorderBrush = Resource(contrast ? "SystemColorWindowColorBrush" : "CardStrokeColorDefaultBrush"), BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = key, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center, Foreground = contrast ? Resource("SystemColorWindowColorBrush") : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                    },
                };
                badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var at = target.Element.TransformToVisual(KeyTipLayer).TransformPoint(new Point(0, 0));
                var width = Math.Max(18, badge.DesiredSize.Width);
                // at the right end of a sidebar item, centred under a menu or a toolbar button
                var left = target.AtRight ? at.X + target.Element.ActualWidth - width - 10 : at.X + (target.Element.ActualWidth - width) / 2;
                var top = target.AtRight ? at.Y + (target.Element.ActualHeight - 18) / 2 : at.Y + target.Element.ActualHeight - 9;
                Canvas.SetLeft(badge, Math.Max(2, left));
                Canvas.SetTop(badge, Math.Max(2, top));
                AutomationProperties.SetAccessibilityView(badge, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
                KeyTipLayer.Children.Add(badge);
            }
            keyTipsShown = true;
            // a scroll in any area a badge sits in (the keys, a scroll bar, the wheel) puts them away: they would point at what is no longer there
            foreach (var viewer in keyTips.SelectMany(t => ScrollersAround(t.Target.Element)).Distinct())
            {
                viewer.ViewChanged += KeyTipScrollerChanged;
                keyTipScrollers.Add(viewer);
            }
            KeyTipLayer.Visibility = Visibility.Visible;
            ShowToast(Locale.GetString("KeyTipsAnnounce"), 60000);
            // a control that is not a tab stop does not take the keyboard: it is one while the tips are shown, and the only one nobody tabs to
            KeyTipSink.IsTabStop = true;
            var focused = KeyTipSink.Focus(FocusState.Programmatic);
            Log($"KeyTips: {keyTips.Count} tips, the sink took the keyboard: {focused}");
            return true;
        }

        // `restore`: the keyboard goes back to where it was (Esc, a key that is no tip); not when it is going somewhere else (a tip was used, a click).
        private void HideKeyTips(bool restore)
        {
            if (!keyTipsShown) return;
            keyTipsShown = false;
            foreach (var viewer in keyTipScrollers) viewer.ViewChanged -= KeyTipScrollerChanged;
            keyTipScrollers.Clear();
            KeyTipSink.IsTabStop = false;
            KeyTipLayer.Visibility = Visibility.Collapsed;
            KeyTipLayer.Children.Clear();
            toastVersion++;
            ToastHost.Visibility = Visibility.Collapsed;
            if (!restore) return;
            if (keyTipsFromEditor || keyTipReturnFocus == null) GiveFocusBack();
            else keyTipReturnFocus.Focus(FocusState.Programmatic);
        }

        private void KeyTipScrollerChanged(object sender, ScrollViewerViewChangedEventArgs e) => HideKeyTips(restore: true);

        private void KeyTipSink_LostFocus(object sender, RoutedEventArgs e) => HideKeyTips(restore: false);

        private void KeyTipSink_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!keyTipsShown) return;
            switch (e.Key)
            {
                // Alt, Ctrl and Shift on their own change nothing
                case VirtualKey.Menu: case VirtualKey.Control: case VirtualKey.Shift: case VirtualKey.LeftMenu: case VirtualKey.RightMenu:
                    return;
                case VirtualKey.Escape:
                    e.Handled = true;
                    HideKeyTips(restore: true);
                    return;
                // the way Alt alone worked before: the keyboard is in the menu bar
                case VirtualKey.Left: case VirtualKey.Right: case VirtualKey.Up: case VirtualKey.Down: case VirtualKey.Enter: case VirtualKey.Space: case VirtualKey.F10:
                    e.Handled = true;
                    HideKeyTips(restore: false);
                    RevealCommandRow(true);
                    return;
            }
            e.Handled = true;
            var key = KeyOfVirtualKey(e.Key);
            var hit = key == null ? default : keyTips.FirstOrDefault(t => t.Key == key);
            if (hit.Target == null)
            {
                HideKeyTips(restore: true);
                return;
            }
            HideKeyTips(restore: false);
            hit.Target.Activate();
        }
    }
}
