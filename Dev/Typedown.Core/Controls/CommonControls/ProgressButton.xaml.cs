using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class ProgressButton : Button
    {
        public static DependencyProperty IsLoadingProperty { get; } = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(ProgressButton), new(false, (d, e) => (d as ProgressButton).OnDependencyPropertyChanged(e)));
        public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

        public static new DependencyProperty IsEnabledProperty { get; } = DependencyProperty.Register(nameof(IsEnabled), typeof(bool), typeof(ProgressButton), new(true, (d, e) => (d as ProgressButton).OnDependencyPropertyChanged(e)));
        public new bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

        public static new DependencyProperty ContentProperty { get; } = DependencyProperty.Register(nameof(Content), typeof(object), typeof(ProgressButton), new(null));
        public new object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public ProgressButton()
        {
            InitializeComponent();
        }

        private void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.IsEnabled = IsEnabled && !IsLoading;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
             Bindings?.StopTracking();
        }
    }
}
