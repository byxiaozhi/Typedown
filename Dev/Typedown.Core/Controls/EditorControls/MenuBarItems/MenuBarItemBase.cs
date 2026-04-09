using System;
using System.Collections.ObjectModel;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Reactive.Disposables;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
            _ = Dispatcher.RunIdleAsync(() => { if (IsLoaded) OnRegisterShortcut(); });
        }

        protected abstract void OnRegisterShortcut();

        protected void RegisterWindowShortcut(ShortcutKey key, MenuItem item)
        {
            RegisterMenuItemShortcut(OnWindowShortcutEvent, key, item);
        }

        protected void RegisterEditorShortcut(ShortcutKey key, MenuItem item)
        {
            RegisterMenuItemShortcut(OnEditorShortcutEvent, key, item);
        }

        private void RegisterMenuItemShortcut(Func<MenuItem, bool> handler, ShortcutKey key, MenuItem item)
        {
            var acc = this.GetService<IKeyboardAccelerator>();
            item.KeyboardAcceleratorTextOverride = acc.GetShortcutKeyText(key);
            disposables.Add(acc.Register(key, (s, e) =>
            {
                if (handler(item))
                    e.Handled = true;
            }));
        }

        private bool OnWindowShortcutEvent(MenuItem item)
        {
            var focused = PInvoke.GetForegroundWindow();
            if (focused != ViewModel.MainWindow)
                return false;
            _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => TriggerMenuItem(item));
            return true;
        }

        private bool OnEditorShortcutEvent(MenuItem item)
        {
            var editor = this.GetService<IMarkdownEditor>();
            var focused = FocusManager.GetFocusedElement(XamlRoot);
            if (focused != editor)
                return false;
            _ = Dispatcher.TryRunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => TriggerMenuItem(item));
            return true;
        }

        private void TriggerMenuItem(MenuItem item)
        {
            item.Command?.Execute(item.CommandParameter);
            if (item is ToggleMenuItem toggle)
                toggle.IsChecked = !toggle.IsChecked;
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
        }
    }
}
