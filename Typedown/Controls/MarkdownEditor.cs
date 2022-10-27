using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Windows;
using Typedown.Services;
using Typedown.Universal.Controls;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Utilities;
using Typedown.Windows;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml;
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

        public RemoteInvoke RemoteInvoke => this.GetService<RemoteInvoke>();

        public Transport Transport => this.GetService<Transport>();

        private readonly CompositeDisposable disposables = new();

        private readonly ResourceLoader stringResources = ResourceLoader.GetForViewIndependentUse("Resources");

        public MarkdownEditor()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Content = new Canvas() { Background = new SolidColorBrush(Colors.Transparent) };
            IsTabStop = true;
        }

        private async void OnLoaded(object sender, global::Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Add(RemoteInvoke.Handle("GetCurrentTheme", arg => Utilities.Common.GetCurrentTheme(this.GetService<IServiceProvider>())));
            disposables.Add(RemoteInvoke.Handle("GetStringResources", arg => arg["names"].ToObject<List<string>>().ToDictionary(x => x, stringResources.GetString)));
            if (WebViewController == null)
            {
                WebViewController = new();
                await WebViewController.InitializeAsync(this, AppWindow.GetWindow(AppXamlHost.GetAppXamlHost(this)).Handle);
                OnCoreWebView2Initialized();
            }
            else
            {
                CoreWebView2.Reload();
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
            CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics", "index.html");
            WebViewController.CoreWebView2.Navigate("http://localhost:3000/");
        }

        private void OnUnloaded(object sender, global::Windows.UI.Xaml.RoutedEventArgs e)
        {
            disposables.Clear();
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

        public void PostMessage(string name, object args)
        {
            var data = JsonConvert.SerializeObject(new { name, args });
            CoreWebView2?.PostWebMessageAsJson(data);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Transport.EmitWebViewMessage(this, e.WebMessageAsJson);
        }
    }
}
