using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Windows;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Utilities;
using Typedown.Windows;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Controls
{
    public class MarkdownEditor : UserControl, IMarkdownEditor
    {
        public WebViewController WebViewController { get; private set; }

        public CoreWebView2 CoreWebView2 => WebViewController.CoreWebView2;

        public EventCenter EventCenter => this.GetService<EventCenter>();

        public MarkdownEditor()
        {
            Loaded += OnLoaded;
            Content = new Canvas() { Background = new SolidColorBrush(Colors.Transparent) };
            IsTabStop = true;
        }

        private async void OnLoaded(object sender, global::Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (WebViewController == null)
            {
                WebViewController = new();
                await WebViewController.InitializeAsync(this, (Window.GetWindow(AppXamlHost.GetAppXamlHost(this)) as AppWindow).Handle);
                OnCoreWebView2Initialized();
            }
        }

        private void OnCoreWebView2Initialized()
        {
            CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            CoreWebView2.Settings.AreDevToolsEnabled = false;
            CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;
            CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            CoreWebView2.Settings.IsPinchZoomEnabled = true;
            CoreWebView2.Settings.IsStatusBarEnabled = false;
            CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
            CoreWebView2.Settings.IsZoomControlEnabled = false;
            CoreWebView2.ScriptDialogOpening += OnScriptDialogOpening;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics", "index.html");
            WebViewController.CoreWebView2.Navigate(path);
        }

        private void OnScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        {
            AppContentDialog dialog = new()
            {
                Title = "Message",
                Content = args.Message,
                CloseButtonText = "Ok",
                XamlRoot = XamlRoot
            };
            _ = dialog.ShowAsync();
        }
    }
}
