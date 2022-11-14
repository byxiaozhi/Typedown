﻿using System;
using System.Linq;
using System.Reactive.Linq;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls.CommonControls
{
    public sealed partial class ShortcutPickerButton : Button
    {
        public static DependencyProperty ShortcutKeyProperty = DependencyProperty.Register(nameof(ShortcutKey), typeof(ShortcutKey), typeof(ShortcutPickerButton), new(null));
        public ShortcutKey ShortcutKey { get => (ShortcutKey)GetValue(ShortcutKeyProperty); set => SetValue(ShortcutKeyProperty, value); }

        public ShortcutPickerButton()
        {
            InitializeComponent();
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            var picker = new ShortcutPicker(ShortcutKey);
            var dialog = new AppContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "设置快捷键",
                Content = picker,
                PrimaryButtonText = "保存",
                SecondaryButtonText = "清除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };
            picker.Binding(new(nameof(picker.ShortcutKey))).Cast<ShortcutKey>().Subscribe(_ => OnPickerShortcutKeyChanged(dialog));
            dialog.PrimaryButtonClick += OnDialogPrimaryButtonClick;
            dialog.SecondaryButtonClick += OnDialogSecondaryButtonClick;
            await dialog.ShowAsync();
        }

        private void OnDialogPrimaryButtonClick(AppContentDialog sender, AppContentDialogButtonClickEventArgs args)
        {
            var picker = sender.Content as ShortcutPicker;
            if (picker.Verified || picker.ShortcutKey == new ShortcutKey(0, 0))
                ShortcutKey = picker.ShortcutKey;
            else
                args.Cancel = true;
        }

        private void OnDialogSecondaryButtonClick(AppContentDialog sender, AppContentDialogButtonClickEventArgs args)
        {
            var picker = sender.Content as ShortcutPicker;
            picker.ResetShortcutKey();
            args.Cancel = true;
        }

        private void OnPickerShortcutKeyChanged(AppContentDialog dialog)
        {
            var picker = dialog.Content as ShortcutPicker;
            dialog.PrimaryButton.IsEnabled = picker.Verified;
        }

        public static bool HasShortcutKey(ShortcutKey key)
        {
            return key != null && key.Key != Windows.System.VirtualKey.None;
        }

        public static bool HasShortcutKeyReverse(ShortcutKey key)
        {
            return !HasShortcutKey(key);
        }
    }
}