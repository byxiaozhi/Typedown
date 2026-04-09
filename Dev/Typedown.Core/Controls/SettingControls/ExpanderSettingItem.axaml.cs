using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Typedown.Core.Controls
{
    [Content(Name = nameof(Action))]
    public sealed partial class ExpanderSettingItem : UserControl
    {
        public static readonly StyledProperty TitleProperty = AvaloniaProperty.Register<ExpanderSettingItem, object>(nameof(Title), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly StyledProperty DescriptionProperty = AvaloniaProperty.Register<ExpanderSettingItem, object>(nameof(Description), null);
        public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly StyledProperty StateProperty = AvaloniaProperty.Register<ExpanderSettingItem, object>(nameof(Action), null);
        public object State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

        public static readonly StyledProperty ActionProperty = AvaloniaProperty.Register<ExpanderSettingItem, object>(nameof(Action), null);
        public object Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

        public static readonly StyledProperty IconProperty = AvaloniaProperty.Register<ExpanderSettingItem, IconElement>(nameof(Icon), null);
        public IconElement Icon { get => (IconElement)GetValue(IconProperty); set => SetValue(IconProperty, value); }

        public ExpanderSettingItem()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (VisualTreeHelper.GetParent(ContentPresenter_Expander) is FrameworkElement parent)
            {
                ContentPresenter_Expander.Margin = new Thickness(-parent.ActualOffset.X, -parent.ActualOffset.Y, -parent.ActualOffset.X, -parent.ActualOffset.Y);
                ContentPresenter_Expander.Width = (sender as Expander).ActualWidth;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
