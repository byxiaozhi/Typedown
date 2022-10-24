using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Universal.Controls
{
    public sealed partial class Caption : UserControl
    {
        public static DependencyProperty IsBackButtonVisibleProperty = DependencyProperty.Register("IsBackButtonVisible", typeof(Visibility), typeof(Caption), null);

        public Visibility IsBackButtonVisible { get => (Visibility)GetValue(IsBackButtonVisibleProperty); set => SetValue(IsBackButtonVisibleProperty, value); }

        public EventHandler BackButtonClick;

        public Caption()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackButtonClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
