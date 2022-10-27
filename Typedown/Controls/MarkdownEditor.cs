using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Windows;
using Typedown.Universal.Interfaces;
using Typedown.Utilities;
using Typedown.Windows;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Controls
{
    public class MarkdownEditor : UserControl, IMarkdownEditor
    {
        public WebViewController WebViewController { get; } = new();

        public MarkdownEditor()
        {
            Loaded += OnLoaded;
            Content = new Canvas() { Background = new SolidColorBrush(Colors.Transparent) };
            IsTabStop = true;
        }

        private async void OnLoaded(object sender, global::Windows.UI.Xaml.RoutedEventArgs e)
        {
            await WebViewController.InitializeAsync(this, (Window.GetWindow(AppXamlHost.GetAppXamlHost(this)) as AppWindow).Handle);
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics", "index.html");
            WebViewController.CoreWebView2.Navigate(path);
        }
    }
}
