using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Controls
{
    [ContentProperty(Name = nameof(Action))]
    public sealed partial class ExpanderSettingItem : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(object), typeof(ExpanderSettingItem), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(object), typeof(ExpanderSettingItem), null);
        public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(ExpanderSettingItem), null);
        public object State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

        public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(object), typeof(ExpanderSettingItem), null);
        public object Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(IconElement), typeof(ExpanderSettingItem), null);
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
    }
}
