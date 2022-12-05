using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using Typedown.Core.Controls.SettingControls.SettingItems;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class ShortcutPicker : UserControl
    {
        public static DependencyProperty ShortcutKeyProperty = DependencyProperty.Register(nameof(ShortcutKey), typeof(ShortcutKey), typeof(ShortcutPicker), new(null));
        public ShortcutKey ShortcutKey { get => (ShortcutKey)GetValue(ShortcutKeyProperty); set => SetValue(ShortcutKeyProperty, value); }

        public static DependencyProperty VerifiedProperty = DependencyProperty.Register(nameof(Verified), typeof(bool), typeof(ShortcutPicker), new(true));
        public bool Verified { get => (bool)GetValue(VerifiedProperty); set => SetValue(VerifiedProperty, value); }

        private readonly CompositeDisposable disposables = new();

        private ShortcutKey currentShortcutKey;

        private Dictionary<ShortcutKey, PropertyInfo> existShortcutKeys;

        private HashSet<VirtualKey> modifiers;

        private SettingsViewModel settings;

        public ShortcutPicker(ShortcutKey currentShortcutKey)
        {
            this.currentShortcutKey = currentShortcutKey;
            ShortcutKey = currentShortcutKey;
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            settings = this.GetService<SettingsViewModel>();
            existShortcutKeys = new();
            typeof(SettingsViewModel)
                .GetProperties()
                .Where(x => x.PropertyType == typeof(ShortcutKey))
                .Select(x => (PropertyInfo: x, ShortcutKey: x.GetValue(settings) as ShortcutKey))
                .Where(x => x.ShortcutKey != null && x.ShortcutKey != new ShortcutKey(0, 0))
                .ToList()
                .ForEach(x => existShortcutKeys[x.ShortcutKey] = x.PropertyInfo);

            modifiers = new HashSet<VirtualKey>() {
                VirtualKey.LeftControl,
                VirtualKey.RightControl,
                VirtualKey.LeftShift,
                VirtualKey.RightShift,
                VirtualKey.LeftMenu,
                VirtualKey.RightMenu,
                VirtualKey.LeftWindows,
                VirtualKey.RightWindows };
            var acc = this.GetService<IKeyboardAccelerator>();
            disposables.Add(acc.RegisterGlobal(OnKeyEvent));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void OnKeyEvent(object sender, KeyEventArgs args)
        {
            ErrorMsgPanel.Visibility = Visibility.Collapsed;
            var shortcutKey = new ShortcutKey(args.Modifiers, args.Key);
            var displayText = Common.GetShortcutKeyTextList(shortcutKey);
            if (!modifiers.Contains(args.Key) && (args.Modifiers != 0 || (args.Key >= VirtualKey.F1 && args.Key <= VirtualKey.F12) || args.Key == VirtualKey.Delete))
            {
                if (existShortcutKeys.ContainsKey(shortcutKey) && shortcutKey != currentShortcutKey)
                {
                    Verified = false;
                    ErrorMsgPanel.Visibility = Visibility.Visible;
                    ExistOwnerTextBlock.Text = new ShortcutSettingItemModel(settings, existShortcutKeys[shortcutKey]).Description;
                }
                else
                {
                    Verified = true;
                }
                ShortcutKey = shortcutKey;
                args.Handled = true;
            }
            else if (displayText.ToHashSet().Count == displayText.Count)
            {
                Verified = false;
                ShortcutKey = shortcutKey;
            }
        }

        public void ResetShortcutKey()
        {
            Verified = true;
            ErrorMsgPanel.Visibility = Visibility.Collapsed;
            ShortcutKey = new(0, 0);
        }
    }
}
