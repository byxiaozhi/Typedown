using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Universal.Controls
{
    public class WebView : Canvas
    {
        public IWebViewController WebViewController { get; private set; }

        private IWindowService WindowService => this.GetService<IWindowService>();

        private nint ParentHandle => WindowService.GetWindow(this);

        public WebView()
        {
            Loaded += WebView_Loaded;
            Unloaded += WebView_Unloaded;
            Background = new SolidColorBrush(Colors.Transparent);
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
