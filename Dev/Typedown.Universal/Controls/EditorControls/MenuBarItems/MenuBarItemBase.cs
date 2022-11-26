using System;
using System.Reactive.Disposables;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using muxc = Microsoft.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.EditorControls.MenuBarItems
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

        private async void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await Dispatcher.RunIdleAsync(_ => { if (IsLoaded) OnRegisterShortcut(); });
        }

        protected abstract void OnRegisterShortcut();

        protected void RegisterWindowShortcut(ShortcutKey key, MenuFlyoutItem item)
        {
            RegisterMenuItemShortcut(OnWindowShortcutEvent, key, item);
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
                if (handler(item))
                    e.Handled = true;
            }));
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
            item.Command?.Execute(item.CommandParameter);
            if (item is ToggleMenuFlyoutItem toggle)
                toggle.IsChecked = !toggle.IsChecked;
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
