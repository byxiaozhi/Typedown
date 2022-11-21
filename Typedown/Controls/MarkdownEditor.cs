using Microsoft.Web.WebView2.Core;
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
using Windows.UI.Xaml.Input;
using Typedown.Universal.ViewModels;
using System.Reactive.Linq;
using Newtonsoft.Json.Linq;
using System.Web;
using System.IO;
using Windows.UI.Xaml.Shapes;
using System.Threading.Tasks;

namespace Typedown.Controls
{
    public class MarkdownEditor : UserControl, IMarkdownEditor, IDisposable
    {
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
        }

        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            PostMessage("ThemeChanged", ServiceProvider.GetCurrentTheme());
        }

        private void OnContentLoaded()
        {
            Opacity = 1;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus(FocusState.Pointer);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            if (WebViewController == null)
            {
                WebViewController = new();
                await WebViewController.InitializeAsync(this, AppWindow.GetWindow(this).XamlSourceHandle);
                SetChromeWidgetWindowTransparent();
                OnCoreWebView2Initialized();
            }
            LoadStaticResources();
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
#if DEBUG
            CoreWebView2.OpenDevToolsWindow();
#endif
        }

        private void LoadStaticResources()
        {
# if DEBUG
            // var staticsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics");
            // WebViewController.CoreWebView2.Navigate($"file:///{staticsFolder}/index.html");
            WebViewController.CoreWebView2.Navigate("http://localhost:3000");
#else
            var staticsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics");
            WebViewController.CoreWebView2.Navigate($"file:///{staticsFolder}/index.html");
#endif
        }

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
            WebViewController.Dispose();
        }

        public Rectangle GetDummyRectangle(Rect rect)
        {
            dummyRectangle.Width = rect.Width;
            dummyRectangle.Height = rect.Height;
            Canvas.SetLeft(dummyRectangle, rect.Left);
            Canvas.SetTop(dummyRectangle, rect.Top);
            return dummyRectangle;
        }

        public Rectangle MoveDummyRectangle(Point offset)
        {
            Canvas.SetLeft(dummyRectangle, Canvas.GetLeft(dummyRectangle) + offset.X);
            Canvas.SetTop(dummyRectangle, Canvas.GetTop(dummyRectangle) + offset.Y);
            return dummyRectangle;
        }

        private void SetChromeWidgetWindowTransparent()
        {
            var processId = (int)WebViewController.CoreWebView2.BrowserProcessId;
            Task.Run(() =>
            {
                foreach (var window in PInvoke.EnumProcessWindow(processId))
                {
                    var str = "Chrome_WidgetWin_1";
                    if (PInvoke.GetClassName(window, str.Length + 1) == str)
                    {
                        var styleEx = PInvoke.GetWindowLong(window, PInvoke.WindowLongFlags.GWL_EXSTYLE);
                        PInvoke.SetWindowLong(window, PInvoke.WindowLongFlags.GWL_EXSTYLE, styleEx | (int)PInvoke.WindowStylesEx.WS_EX_TRANSPARENT);
                    }
                }
            });
        }
    }
}
