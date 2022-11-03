﻿using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Input;
using Typedown.Universal.ViewModels;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Web;
using System.IO;

namespace Typedown.Controls
{
    public class MarkdownEditor : UserControl, IMarkdownEditor
    {
        // static readonly string proxyPrefix = "http://local-file-access/";

        public WebViewController WebViewController { get; private set; }

        public CoreWebView2 CoreWebView2 => WebViewController?.CoreWebView2;

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public Transport Transport => ServiceProvider.GetService<Transport>();

        public IServiceProvider ServiceProvider { get; }

        private readonly Rectangle dummyRectangle = new();

        private readonly ResourceLoader stringResources = ResourceLoader.GetForViewIndependentUse("Resources");

        public MarkdownEditor(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Loaded += OnLoaded;
            Content = new Canvas() { Background = new SolidColorBrush(Colors.Transparent), Children = { dummyRectangle } };
            IsTabStop = true;
            RemoteInvoke.Handle("ContentLoaded", OnContentLoaded);
            RemoteInvoke.Handle("GetCurrentTheme", () => ServiceProvider.GetCurrentTheme());
            RemoteInvoke.Handle<JToken, object>("GetStringResources", arg => arg["names"].ToObject<List<string>>().ToDictionary(x => x, stringResources.GetString));
            ActualThemeChanged += OnThemeChanged;
            AppViewModel.FileViewModel.WhenPropertyChanged(nameof(FileViewModel.FilePath)).Cast<string>().Subscribe(OnFilePathChanged);
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            PostMessage("ThemeChanged", ServiceProvider.GetCurrentTheme());
        }

        private void OnContentLoaded()
        {

        }

        private void OnFilePathChanged(string path)
        {
            //if (CoreWebView2 != null)
            //{
            //    var script = $"window.DIRNAME={JsonConvert.SerializeObject(System.IO.Path.GetDirectoryName(path).Replace("\\", "/") + "/")}";
            //    CoreWebView2.ExecuteScriptAsync(script);
            //}
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus(FocusState.Pointer);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
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
            // CoreWebView2.AddWebResourceRequestedFilter(proxyPrefix + "*", CoreWebView2WebResourceContext.All);
            // CoreWebView2.WebResourceRequested += OnWebResourceRequested;
#if DEBUG
            var path = "http://localhost:3000";
#else
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics", "index.html");
#endif
            WebViewController.CoreWebView2.Navigate(path);
        }

        //private async void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs args)
        //{
        //    try
        //    {
        //        var path = args.Request.Uri[proxyPrefix.Length..];
        //        var pos = path.IndexOf('?');
        //        path = HttpUtility.UrlDecode(pos > 0 ? path[..pos] : path);
        //        using var stream = new MemoryStream();
        //        await stream.WriteAsync(File.ReadAllBytes(path));
        //        args.Response = CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", null);
        //    }
        //    catch (Exception)
        //    {
        //        args.Response = CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", null);
        //    }
        //}

        private async void OnScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        {
            await AppContentDialog.Create("Message", args.Message, Localize.DialogMessages.GetString("Ok")).ShowAsync();
        }

        public void PostMessage(string name, object args)
        {
            CoreWebView2?.PostWebMessageAsJson(JsonConvert.SerializeObject(new { name, args }, Universal.Config.EditorJsonSerializerSettings));
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Transport.EmitWebViewMessage(this, e.WebMessageAsJson);
        }

        public void Dispose()
        {
            WebViewController?.CoreWebView2Controller?.Close();
        }

        public Rectangle GetDummyRectangle(Rect rect)
        {
            dummyRectangle.Width = rect.Width;
            dummyRectangle.Height = rect.Height;
            Canvas.SetLeft(dummyRectangle, rect.Left);
            Canvas.SetTop(dummyRectangle, rect.Top);
            return dummyRectangle;
        }
    }
}
