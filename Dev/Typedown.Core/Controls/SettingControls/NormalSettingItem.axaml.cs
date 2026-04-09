using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace Typedown.Core.Controls
{
    [Content(Name = nameof(Action))]
    public sealed partial class NormalSettingItem : UserControl
    {
        public static readonly StyledProperty TitleProperty = AvaloniaProperty.Register<NormalSettingItem, object>(nameof(Title), null);
        public object Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        public static readonly StyledProperty DescriptionProperty = AvaloniaProperty.Register<NormalSettingItem, object>(nameof(Description), null);
        public object Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

        public static readonly StyledProperty ActionProperty = AvaloniaProperty.Register<NormalSettingItem, object>(nameof(Action), null);
        public object Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

        public static readonly StyledProperty IconProperty = AvaloniaProperty.Register<NormalSettingItem, IconElement>(nameof(Icon), null);
        public IconElement Icon { get => (IconElement)GetValue(IconProperty); set => SetValue(IconProperty, value); }

        public static readonly StyledProperty HorizontalActionAlignmentProperty = AvaloniaProperty.Register<NormalSettingItem, HorizontalAlignment>(nameof(HorizontalActionAlignment), new(HorizontalAlignment.Right));
        public HorizontalAlignment HorizontalActionAlignment { get => (HorizontalAlignment)GetValue(HorizontalActionAlignmentProperty); set => SetValue(HorizontalActionAlignmentProperty, value); }

        public NormalSettingItem()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
