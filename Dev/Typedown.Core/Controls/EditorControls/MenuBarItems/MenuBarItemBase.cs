using System;
using System.Reactive.Disposables;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Core.Controls.EditorControls.MenuBarItems
{
    public abstract class MenuBarItemBase : muxc.MenuBarItem
    {
        public AppViewModel ViewModel => DataContext as AppViewModel;

        public SettingsViewModel Settings => ViewModel?.SettingsViewModel;

        private readonly CompositeDisposable disposables = new();

        public MenuBarItemBase()
        {
            Unloaded += OnUnloaded;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // An exception here runs on the dispatcher with nothing above it to catch it, which on XAML islands
            // takes the process down without a report.
            _ = Dispatcher.RunIdleAsync(() =>
            {
                if (!IsLoaded) return;
                try
                {
                    OnRegisterShortcut();
                    WatchCommands(Items);
                }
                catch (Exception ex)
                {
                    Log.WriteLocal("MenuShortcutRegister", $"{GetType().Name}\n{ex}");
                }
            });
        }

        protected abstract void OnRegisterShortcut();

        private bool watching;

        // A command chosen in a menu left the focus on the menu bar, or on nothing: the letters typed next went nowhere
        // until a click in the text. Once the command has run, the keyboard goes to the editor - unless the command put
        // it somewhere on purpose (the find box, a dialog, the settings page) or a menu is open again. Only a chosen
        // command does this: opening the menus, going from one to the next and Esc are left as they are (handing the
        // keys over on every closing menu closed the next one opened).
        private void WatchCommands(System.Collections.Generic.IList<MenuFlyoutItemBase> items)
        {
            if (watching && items == Items) return;
            if (items == Items) watching = true;
            DisabledMenuLook.Watch(items);
            foreach (var item in items)
            {
                if (item is MenuFlyoutSubItem sub) WatchCommands(sub.Items);
                else if (item is MenuFlyoutItem command) command.Click += OnCommandClick;
            }
        }

        private void OnCommandClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var root = XamlRoot;
            if (root == null) return;
            var dispatcher = Dispatcher;
            _ = System.Threading.Tasks.Task.Delay(150).ContinueWith(t => _ = dispatcher.RunIdleAsync(_ =>
            {
                if (Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Count > 0) return;
                var focused = FocusManager.GetFocusedElement(root);
                if (focused != null && !(focused is muxc.MenuBarItem)) return;
                ViewModel?.MarkdownEditor?.FocusEditor();
            }));
        }

        protected void RegisterWindowShortcut(ShortcutKey key, MenuFlyoutItem item)
        {
            RegisterMenuItemShortcut(OnWindowShortcutEvent, key, item);
        }

        /// <summary>
        /// A window shortcut with no menu entry behind it — the tab numbers, which are fixed rather than
        /// bindable and would only clutter the menu.
        /// </summary>
        protected void RegisterWindowShortcut(ShortcutKey key, Action action)
        {
            var acc = this.GetService<IKeyboardAccelerator>();
            disposables.Add(acc.Register(key, (s, e) =>
            {
                if (PInvoke.GetForegroundWindow() != ViewModel.MainWindow) return;
                if (VimWants(e)) return;
                e.Handled = true;
                _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => action());
            }));
        }

        protected void RegisterEditorShortcut(ShortcutKey key, MenuFlyoutItem item)
        {
            RegisterMenuItemShortcut(OnEditorShortcutEvent, key, item);
        }

        private void RegisterMenuItemShortcut(Func<MenuFlyoutItem, bool> handler, ShortcutKey key, MenuFlyoutItem item)
        {
            var acc = this.GetService<IKeyboardAccelerator>();
            item.KeyboardAcceleratorTextOverride = acc.GetShortcutKeyText(key);
            disposables.Add(acc.Register(key, (s, e) =>
            {
                if (VimWants(e)) return;
                if (handler(item))
                    e.Handled = true;
            }));
        }

        /// <summary>A key Vim needs right now in the focused editor (Settings > Editor > Vim keys): left to the page.</summary>
        protected bool VimWants(KeyEventArgs e)
        {
            if (ViewModel?.SettingsViewModel?.VimMode != true) return false;
            if (!VimKeys.EditorWants(ViewModel.EditorViewModel.VimState, e.Modifiers, e.Key)) return false;
            return FocusManager.GetFocusedElement(XamlRoot) == this.GetService<IMarkdownEditor>();
        }

        private bool OnWindowShortcutEvent(MenuFlyoutItem item)
        {
            var focused = PInvoke.GetForegroundWindow();
            if (focused != ViewModel.MainWindow)
                return false;
            _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => TriggerMenuFlyoutItem(item));
            return true;
        }

        private bool OnEditorShortcutEvent(MenuFlyoutItem item)
        {
            var editor = this.GetService<IMarkdownEditor>();
            var focused = FocusManager.GetFocusedElement(XamlRoot);
            if (focused != editor)
                return false;
            _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => TriggerMenuFlyoutItem(item));
            return true;
        }

        private void TriggerMenuFlyoutItem(MenuFlyoutItem item)
        {
            // Same reasoning as OnLoaded: this runs from a dispatcher callback started by a keyboard accelerator.
            try
            {
                item.Command?.Execute(item.CommandParameter);
                if (item is ToggleMenuFlyoutItem toggle)
                    toggle.IsChecked = !toggle.IsChecked;
            }
            catch (Exception ex)
            {
                Log.WriteLocal("MenuShortcutTrigger", $"{GetType().Name} / {item?.Name}\n{ex}");
            }
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
