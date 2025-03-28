﻿using System;
using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Typedown.Core.Controls
{
    [ContentProperty(Name = nameof(Action))]
    public sealed partial class ButtonSettingItem : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(object), typeof(ButtonSettingItem), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(ButtonSettingItem), null);
        public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(ButtonSettingItem), null);
        public object Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(ButtonSettingItem), null);
        public IconElement Icon { get => (IconElement)GetValue(IconProperty); set => SetValue(IconProperty, value); }

        public static readonly DependencyProperty HorizontalActionAlignmentProperty = DependencyProperty.Register(nameof(HorizontalActionAlignment), typeof(HorizontalAlignment), typeof(ButtonSettingItem), new(HorizontalAlignment.Right));
        public HorizontalAlignment HorizontalActionAlignment { get => (HorizontalAlignment)GetValue(HorizontalActionAlignmentProperty); set => SetValue(HorizontalActionAlignmentProperty, value); }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command), typeof(ICommand), typeof(ButtonSettingItem), new(null));
        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(ButtonSettingItem), new(null));
        public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

        public event EventHandler Click;

        public ButtonSettingItem()
        {
            InitializeComponent();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
