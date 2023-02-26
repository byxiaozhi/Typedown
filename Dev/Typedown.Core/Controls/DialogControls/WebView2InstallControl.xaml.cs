using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class WebView2InstallControl : UserControl
    {
        public EventHandler CloseButtonClick;

        public EventHandler InstallButtonClick;

        public WebView2InstallControl()
        {
            this.InitializeComponent();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            CloseButtonClick?.Invoke(this, EventArgs.Empty);
        }

        private void OnInstallButtonClick(object sender, RoutedEventArgs e)
        {
            InstallButtonClick?.Invoke(this, EventArgs.Empty);
        }
    }
}
