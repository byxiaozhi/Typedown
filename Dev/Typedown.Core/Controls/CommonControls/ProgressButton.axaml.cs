using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class ProgressButton : Button
    {
        public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<ProgressButton, bool>(nameof(IsLoading), false);
        public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

        public static new readonly StyledProperty<bool> IsEnabledProperty = AvaloniaProperty.Register<ProgressButton, bool>(nameof(IsEnabled), true);
        public new bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

        public static new readonly StyledProperty<object> ContentProperty = AvaloniaProperty.Register<ProgressButton, object>(nameof(Content), null);
        public new object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public ProgressButton()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsLoadingProperty || change.Property == IsEnabledProperty)
            {
                base.IsEnabled = IsEnabled && !IsLoading;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Bindings?.StopTracking();
        }
    }
}

