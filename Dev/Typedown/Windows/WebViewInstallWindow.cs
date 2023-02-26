using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Typedown.Core.Utilities;
using Typedown.XamlUI;

namespace Typedown.Windows
{
    public class WebViewInstallWindow : XamlWindow
    {
        private readonly WebView2InstallControl webView2InstallControl = new();

        public WebViewInstallWindow()
        {
            Width = 500;
            Height = 300;
            ResizeMode = WindowResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Frame = false;
            Title = Locale.GetDialogString("WebView2RuntimeNotInstalledTitle");
            Content = webView2InstallControl;
            webView2InstallControl.CloseButtonClick += (s, e) => Close();
            webView2InstallControl.InstallButtonClick += (s, e) => Install();
        }

        public static Task<bool> TryInstallWebView2()
        {
            var window = new WebViewInstallWindow();
            var task = new TaskCompletionSource<bool>();
            window.Closed += (s, e) =>
            {
                task.SetResult(EnvCheck.IsWebView2Installed());
            };
            window.Show();
            return task.Task;
        }

        public async void Install()
        {
            Show(ShowWindowCommand.SW_HIDE);
            var runDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var process = Process.Start(Path.Combine(runDir, "MicrosoftEdgeWebview2Setup.exe"));
            await Task.Run(() => process.WaitForExit());
            Close();
        }
    }
}
