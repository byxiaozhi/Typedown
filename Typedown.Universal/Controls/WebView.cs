using System;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Controls
{
    public class WebView : UserControl
    {
        public IWebViewController WebViewController { get; private set; }

        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint ParentHandle => WindowService.GetWindow(this);

        public WebView()
        {
            Loaded += WebView_Loaded;
            Unloaded += WebView_Unloaded;
            Content = new Canvas() { Background = new SolidColorBrush(Colors.Transparent) };
            IsTabStop = true;
        }

        private void WebView_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            WebViewController = null;
            GC.Collect();
            GC.Collect();
        }

        private void WebView_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            EnsureWebViewController();
        }

        private async void EnsureWebViewController()
        {
            WebViewController = this.GetService<IWebViewController>();
            await WebViewController.InitializeAsync(this, ParentHandle);
            WebViewController.Navigate("https://www.baidu.com");
        }
    }
}
