using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;

using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class ShortcutPicker : UserControl
    {
        public static AvaloniaProperty ShortcutKeyProperty = AvaloniaProperty.Register<ShortcutPicker, ShortcutKey>(nameof(ShortcutKey), null);
        public ShortcutKey ShortcutKey { get => (ShortcutKey)GetValue(ShortcutKeyProperty); set => SetValue(ShortcutKeyProperty, value); }

        public static AvaloniaProperty VerifiedProperty = AvaloniaProperty.Register<ShortcutPicker, bool>(nameof(Verified), true);
        public bool Verified { get => (bool)GetValue(VerifiedProperty); set => SetValue(VerifiedProperty, value); }

        private readonly CompositeDisposable disposables = new();

        private ShortcutKey currentShortcutKey;

        private Dictionary<ShortcutKey, PropertyInfo> existShortcutKeys;

        private HashSet<Typedown.Core.Enums.VirtualKey> modifiers;

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

            modifiers = new HashSet<Typedown.Core.Enums.VirtualKey>() {
                Typedown.Core.Enums.VirtualKey.Control,
                Typedown.Core.Enums.VirtualKey.Shift,
                Typedown.Core.Enums.VirtualKey.Menu };
            var acc = this.GetService<IKeyboardAccelerator>();
            disposables.Add(acc.RegisterGlobal(OnKeyEvent));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            disposables.Clear();
        }

        private void OnKeyEvent(object? sender, Typedown.Core.Models.KeyEventArgs args)
        {
            ErrorMsgPanel.IsVisible = false;
            var shortcutKey = new ShortcutKey(args.Modifiers, args.Key);
            var displayText = Common.GetShortcutKeyTextList(shortcutKey);
            if (!modifiers.Contains(args.Key) && (args.Modifiers != Typedown.Core.Enums.VirtualKeyModifiers.None || (args.Key >= Typedown.Core.Enums.VirtualKey.F1 && args.Key <= Typedown.Core.Enums.VirtualKey.F12) || args.Key == Typedown.Core.Enums.VirtualKey.Delete))
            {
                if (existShortcutKeys.ContainsKey(shortcutKey) && shortcutKey != currentShortcutKey)
                {
                    Verified = false;
                    ErrorMsgPanel.IsVisible = true;
                    // ExistOwnerTextBlock.Text = new ShortcutSettingItemModel(settings, existShortcutKeys[shortcutKey]).Description;
                    ExistOwnerTextBlock.Text = existShortcutKeys[shortcutKey].Name;
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
            ErrorMsgPanel.IsVisible = false;
            ShortcutKey = new(0, 0);
        }
    }
}

