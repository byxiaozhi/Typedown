using System;
using System.Collections.ObjectModel;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace Typedown.Core.Controls
{
    [Content(Name = nameof(Action))]
    public sealed partial class ButtonSettingItem : UserControl
    {
        public static readonly StyledProperty TitleProperty = AvaloniaProperty.Register<ButtonSettingItem, object>(nameof(Title), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly StyledProperty DescriptionProperty = AvaloniaProperty.Register<ButtonSettingItem, object>(nameof(Description), null);
        public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly StyledProperty ActionProperty = AvaloniaProperty.Register<ButtonSettingItem, object>(nameof(Action), null);
        public object Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

        public static readonly StyledProperty IconProperty = AvaloniaProperty.Register<ButtonSettingItem, IconElement>(nameof(Icon), null);
        public IconElement Icon { get => (IconElement)GetValue(IconProperty); set => SetValue(IconProperty, value); }

        public static readonly StyledProperty HorizontalActionAlignmentProperty = AvaloniaProperty.Register<ButtonSettingItem, HorizontalAlignment>(nameof(HorizontalActionAlignment), new(HorizontalAlignment.Right));
        public HorizontalAlignment HorizontalActionAlignment { get => (HorizontalAlignment)GetValue(HorizontalActionAlignmentProperty); set => SetValue(HorizontalActionAlignmentProperty, value); }

        public static readonly StyledProperty CommandProperty = AvaloniaProperty.Register<ButtonSettingItem, ICommand>(nameof(Command), new(null));
        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

        public static readonly StyledProperty CommandParameterProperty = AvaloniaProperty.Register<ButtonSettingItem, object>(nameof(CommandParameter), new(null));
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
