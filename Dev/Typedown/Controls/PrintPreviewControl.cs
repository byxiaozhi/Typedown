using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Utilities;
using Typedown.XamlUI;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Typedown.Controls
{
    public class PrintPreviewControl : UserControl, IDisposable
    {
        private readonly string html;

        private readonly string documentName;

        private WebViewController webViewController;

        private string tempFile;

        private bool disposed;

        /// <summary>
        /// The print dialog has been closed, printed or not. The preview used to stay on screen after it,
        /// showing the document as a plain page until its own close button was found; the page is told by
        /// the browser when the dialog goes (afterprint), and that is passed on here.
        /// </summary>
        public event Action PrintDismissed;

        /// <summary>Printing could not start, or the web view behind the preview died; the message says which.</summary>
        public event Action<string> PrintFailed;

        public PrintPreviewControl(string html, string documentName)
        {
            this.html = html;
            this.documentName = documentName;
            Content = new Grid { Background = new SolidColorBrush(Colors.White) };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (webViewController != null)
                    return;
                var controller = new WebViewController();
                var parentHWnd = XamlWindow.GetWindow(this).XamlSourceHandle;
                if (!await controller.InitializeAsync(this, parentHWnd))
                {
                    controller.Dispose();
                    return;
                }
                if (disposed)
                {
                    controller.Dispose();
                    return;
                }
                webViewController = controller;
                var coreWebView2 = webViewController.CoreWebView2;
                coreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                coreWebView2.Settings.AreDevToolsEnabled = false;
                coreWebView2.Settings.IsStatusBarEnabled = false;
                var navigationCompletedTaskSource = new TaskCompletionSource<bool>();
                coreWebView2.NavigationCompleted += (s, args) => navigationCompletedTaskSource.TrySetResult(args.IsSuccess);
                await coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.addEventListener('afterprint', () => window.chrome.webview.postMessage('afterprint'))");
                coreWebView2.WebMessageReceived += (s, args) =>
                {
                    string message = null;
                    try { message = args.TryGetWebMessageAsString(); } catch { }
                    // Not from inside the browser's own callback: closing the preview disposes this web view.
                    if (message == "afterprint") _ = Dispatcher.RunIdleAsync(_ => PrintDismissed?.Invoke());
                };
                tempFile = Core.Utilities.Common.GetTempFileName(".html");
                File.WriteAllText(tempFile, html);
                // The browser process behind the preview can die (a driver, a renderer bug): say so instead of
                // leaving a blank preview on screen — "the app crashes when I click print" in the Store reviews
                // may well have been this, with nothing to see but the blank.
                coreWebView2.ProcessFailed += (s, args) =>
                {
                    Core.Utilities.Log.Debug($"print preview: web view process failed: {args.ProcessFailedKind} {args.Reason} exit={args.ExitCode}");
                    _ = Dispatcher.RunIdleAsync(_ => PrintFailed?.Invoke($"{args.ProcessFailedKind}: {args.Reason}"));
                };
                coreWebView2.Navigate(tempFile);
                await navigationCompletedTaskSource.Task;
                if (disposed)
                    return;
                try
                {
                    coreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
                }
                catch (Exception ex)
                {
                    // The browser's own dialog failed to open; the system one is a separate code path.
                    Core.Utilities.Log.Debug($"print: browser print dialog failed, trying the system one: {ex.Message}");
                    coreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.System);
                }
            }
            catch (Exception ex)
            {
                Core.Utilities.Log.Debug($"print: could not start: {ex}");
                if (!disposed) PrintFailed?.Invoke(ex.Message);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            webViewController?.Dispose();
            webViewController = null;
            try
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // Ignore
            }
        }
    }
}
