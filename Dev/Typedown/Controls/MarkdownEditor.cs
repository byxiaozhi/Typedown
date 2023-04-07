using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using PropertyChanged;
using System;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Web;
using Typedown.Core.Controls;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Services;
using Typedown.Utilities;
using Typedown.XamlUI;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Typedown.Controls
{
    public partial class MarkdownEditor : UserControl, IMarkdownEditor, IDisposable, INotifyPropertyChanged
    {
        public WebViewController WebViewController { get; private set; }

        public CoreWebView2 CoreWebView2 => WebViewController?.CoreWebView2;

        public EventCenter EventCenter => ServiceProvider.GetService<EventCenter>();

        public RemoteInvoke RemoteInvoke => ServiceProvider.GetService<RemoteInvoke>();

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public Transport Transport => ServiceProvider.GetService<Transport>();

        public bool IsEditorLoadFailed { get; set; }

        public bool IsEditorLoaded { get; set; }

        public IServiceProvider ServiceProvider { get; }

        private readonly Rectangle dummyRectangle = new();

        private readonly Canvas canvas = new() { Background = new SolidColorBrush(Colors.Transparent) };

        private readonly UISettings uiSettings = new();

        private readonly CompositeDisposable disposables = new();

        public MarkdownEditor(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Loaded += OnLoaded;
            canvas.Children.Add(dummyRectangle);
            Content = canvas;
            IsTabStop = true;
            disposables.Add(RemoteInvoke.Handle("ContentLoaded", OnContentLoaded));
            disposables.Add(RemoteInvoke.Handle<string>("UnhandledException", OnUnhandledException));
            disposables.Add(RemoteInvoke.Handle("GetCurrentTheme", () => ServiceProvider.GetCurrentTheme()));
            disposables.Add(RemoteInvoke.Handle<string>("OpenNewWindow", OpenNewWindow));
            disposables.Add(AppViewModel.UIViewModel.WhenPropertyChanged(nameof(UIViewModel.ActualTheme))
                .Merge(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseEditorMicaEffect)))
                .Merge(AppViewModel.SettingsViewModel.WhenPropertyChanged(nameof(SettingsViewModel.UseMicaEffect)))
                .Merge(Observable.FromEventPattern(uiSettings, nameof(uiSettings.ColorValuesChanged)))
                .Subscribe(_ => OnThemeChanged()));
        }

        private void OnUnhandledException(string error)
        {
            CoreWebView2.Reload();
            Log.Report("WebViewUnhandledException", error);
        }

        [SuppressPropertyChangedWarnings]
        private void OnThemeChanged()
        {
            _ = Dispatcher.RunIdleAsync(() => PostMessage("ThemeChanged", ServiceProvider.GetCurrentTheme()));
        }

        private void OnContentLoaded()
        {
            Opacity = 1;
            IsEditorLoaded = true;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Focus(FocusState.Pointer);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Opacity = 0;
            IsEditorLoaded = false;
            IsEditorLoadFailed = false;
            if (WebViewController == null)
            {
                var webViewController = new WebViewController();
                if (!await webViewController.InitializeAsync(this, XamlWindow.GetWindow(this).XamlSourceHandle))
                {
                    webViewController.Dispose();
                    IsEditorLoadFailed = true;
                    return;
                }
                if (disposables.IsDisposed)
                {
                    webViewController.Dispose();
                    IsEditorLoadFailed = true;
                    return;
                }
                try
                {
                    WebViewController = webViewController;
                    OnCoreWebView2Initialized();
                }
                catch
                {
                    webViewController.Dispose();
                    WebViewController = null;
                    IsEditorLoadFailed = true;
                    return;
                }
                _ = TrySetChromeWidgetWindowTransparent(webViewController);
            }
            try
            {
                LoadStaticResources();
            }
            catch
            {
                IsEditorLoadFailed = true;
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
            CoreWebView2.NewWindowRequested += OnNewWindowRequested;
#if DEBUG
            CoreWebView2.AddWebResourceRequestedFilter("http://local-file-access/*", CoreWebView2WebResourceContext.All);
            CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            CoreWebView2.OpenDevToolsWindow();
#endif
        }

        private void LoadStaticResources()
        {
# if DEBUG
            WebViewController.CoreWebView2.Navigate("http://localhost:3000");
            // var staticsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics");
            // WebViewController.CoreWebView2.Navigate($"file:///{staticsFolder}/index.html");
#else
            var staticsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Statics");
            WebViewController.CoreWebView2.Navigate($"file:///{staticsFolder}/index.html");
#endif
        }

        private async void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            try
            {
                var uri = new Uri(args.Request.Uri);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var src = HttpUtility.UrlDecode(query["src"]);
                UriHelper.TryGetLocalPath(src, out var path);
                path = System.IO.Path.Combine(AppViewModel.FileViewModel.ImageBasePath, path);
                var stream = await Task.Run(() => new MemoryStream(File.ReadAllBytes(path)));
                args.Response = CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", null);
            }
            catch (Exception)
            {
                args.Response = CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not found", null);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void OnScriptDialogOpening(object sender, CoreWebView2ScriptDialogOpeningEventArgs args)
        {
            await AppContentDialog.Create("Message", args.Message, Locale.GetString("Ok")).ShowAsync();
        }

        public bool PostMessage(string name, object args)
        {
            try
            {
                CoreWebView2?.PostWebMessageAsString(JsonConvert.SerializeObject(new { name, args }, Core.Config.EditorJsonSerializerSettings));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Transport.EmitWebViewMessage(this, e.TryGetWebMessageAsString());
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            if (PInvoke.GetIsKeyDown(VirtualKey.Control))
                Core.Utilities.Common.OpenUrl(args.Uri);
        }

        private async void OpenNewWindow(string uri)
        {
            try
            {
                if (!UriHelper.IsWebUrl(uri) && UriHelper.TryGetLocalPath(uri, out var path))
                {
                    var filePath = AppViewModel.FileViewModel.FilePath;
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath), path));
                        if (File.Exists(fullPath))
                        {
                            if (FileTypeHelper.IsMarkdownFile(fullPath))
                                AppViewModel.FileViewModel.NewWindowCommand.Execute(fullPath);
                            else
                                Core.Utilities.Common.OpenUrl(fullPath);
                            return;
                        }
                    }
                }
                Core.Utilities.Common.OpenUrl(uri);
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetDialogString("Ok")).ShowAsync(XamlRoot);
            }
        }

        public void Dispose()
        {
            WebViewController?.Dispose();
            canvas?.Children.Clear();
            disposables.Dispose();
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

        private async static Task<bool> TrySetChromeWidgetWindowTransparent(WebViewController webViewController)
        {
            try
            {
                var processId = (int)webViewController.CoreWebView2.BrowserProcessId;
                await Task.Run(() =>
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
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
