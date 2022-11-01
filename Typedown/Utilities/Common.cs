using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Typedown.Universal.Enums;
using Typedown.Universal.ViewModels;
using Typedown.Windows;
using Windows.UI.Xaml;

namespace Typedown.Utilities
{
    public static class Common
    {
        public static System.Windows.Point MakePoint(this IntPtr p) => new(p.ToInt32() & 0xFFFF, p.ToInt32() >> 16);

        public static nint PackPoint(this System.Drawing.Point point) => point.X | (point.Y << 16);

        public static string GetMimeType(string fileName)
        {
            string ext = Path.GetExtension(fileName).ToLower();
            Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
            var mime = regKey?.GetValue("Content Type")?.ToString();
            return mime ?? "application/unknown";
        }

        static public void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
        }

        public static object GetCurrentTheme(this IServiceProvider provider)
        {
            var settings = provider.GetService<SettingsViewModel>();
            var themeSetting = settings.AppTheme;
            var isDarkMode = themeSetting switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => Universal.App.Current.RequestedTheme == ApplicationTheme.Dark ? true : false
            };
            var color = Universal.App.Current.Resources["SystemAccentColor"];
            var background = isDarkMode ? Color.FromArgb(0xFF, 0x28, 0x28, 0x28) : Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9);
            return new { theme = isDarkMode ? "Dark" : "Light", color, background };
        }

        public static Universal.Models.WindowPlacement GetWindowPlacement(this AppWindow window)
        {
            PInvoke.WINDOWPLACEMENT placement = new();
            PInvoke.GetWindowPlacement(window.Handle, ref placement);
            var scale = PInvoke.GetDpiForWindow(window.Handle) / 96.0;
            var max = placement.showCmd == PInvoke.ShowWindowCommand.Maximize;
            var x = placement.rcNormalPosition.left / scale;
            var y = placement.rcNormalPosition.top / scale;
            var w = (placement.rcNormalPosition.right - placement.rcNormalPosition.left) / scale;
            var h = (placement.rcNormalPosition.bottom - placement.rcNormalPosition.top) / scale;
            return new(max, new(x, y, w, h));
        }

        public static void RestoreWindowPlacement(this AppWindow window, Universal.Models.WindowPlacement placement)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = placement.NormalPosition.Left;
            window.Top = placement.NormalPosition.Top;
            window.Width = placement.NormalPosition.Width;
            window.Height = placement.NormalPosition.Height;
            window.WindowState = placement.IsMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        public static IntPtr OpenNewWindow(string path)
        {
            var oldWindow = AppViewModel.GetInstances().Where(x => x.FileViewModel.FilePath == path);
            if (string.IsNullOrEmpty(path) || !oldWindow.Any())
            {
                var promise = new TaskCompletionSource<IntPtr>();
                var newWindow = new MainWindow();
                newWindow.AppViewModel.FileViewModel.FilePath = path;
                newWindow.ShowActivated = true;
                newWindow.Loaded += (s, e) => promise.SetResult((s as MainWindow).Handle);
                newWindow.Show();
                return newWindow.Handle;
            }
            else
            {
                return oldWindow.FirstOrDefault().MainWindow;
            }
        }
    }
}
