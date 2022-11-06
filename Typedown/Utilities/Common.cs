using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using Typedown.Universal.Utilities;
using Typedown.Universal.Enums;
using Typedown.Universal.ViewModels;
using Typedown.Windows;
using Microsoft.Win32;
using Windows.UI.ViewManagement;
using Typedown.Properties;
using System.Reactive.Linq;

namespace Typedown.Utilities
{
    public static class Common
    {
        public static System.Windows.Point MakePoint(this IntPtr p) => new(p.ToInt32() & 0xFFFF, p.ToInt32() >> 16);

        public static nint PackPoint(this System.Drawing.Point point) => point.X | (point.Y << 16);

        public static string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            var regKey = Registry.ClassesRoot.OpenSubKey(ext);
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
                _ => !GetUseLightTheme()
            };
            var color = new UISettings().GetColorValue(UIColorType.Accent);
            var background = isDarkMode ? Color.FromArgb(0xFF, 0x28, 0x28, 0x28) : Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9);
            return new { theme = isDarkMode ? "Dark" : "Light", color, background };
        }

        public static bool GetUseLightTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var registryValueObject = key?.GetValue("AppsUseLightTheme");
            return registryValueObject == null || (int)registryValueObject > 0;
        }

        public static void SaveWindowPlacement(this AppWindow window, System.Windows.Point offset = default)
        {
            PInvoke.WINDOWPLACEMENT placement = new();
            PInvoke.GetWindowPlacement(window.Handle, ref placement);
            var settings = Settings.Default;
            var scale = PInvoke.GetDpiForWindow(window.Handle) / 96.0;
            settings.StartupIsMaximized = placement.showCmd == PInvoke.ShowWindowCommand.Maximize;
            settings.StartupLeft = placement.rcNormalPosition.left / scale + offset.X;
            settings.StartupTop = placement.rcNormalPosition.top / scale + offset.Y;
            settings.StartupWidth = (placement.rcNormalPosition.right - placement.rcNormalPosition.left) / scale;
            settings.StartupHeight = (placement.rcNormalPosition.bottom - placement.rcNormalPosition.top) / scale;
            settings.Save();
        }

        public static void RestoreWindowPlacement(this AppWindow window)
        {
            var settings = Settings.Default;
            if (settings.StartupLeft >= 0 && settings.StartupTop >= 0)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = settings.StartupLeft;
                window.Top = settings.StartupTop;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            window.Width = settings.StartupWidth;
            window.Height = settings.StartupHeight;
            window.WindowState = settings.StartupIsMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        public static IntPtr OpenNewWindow(string[] args)
        {
            var filePath = CommandLine.GetOpenFilePath(args);
            if (string.IsNullOrEmpty(filePath) || !FileViewModel.TryGetOpenedWindow(filePath, out var windowHWnd))
            {
                var newWindow = new MainWindow();
                newWindow.Show();
                newWindow.InitializeComponent();
                newWindow.AppViewModel.CommandLineArgs = args;
                return newWindow.Handle;
            }
            else
            {
                var appWindows = Application.Current.Windows.OfType<AppWindow>();
                var window = appWindows.Where(x => x.Handle == windowHWnd).FirstOrDefault();
                if (window?.WindowState == WindowState.Minimized)
                    SystemCommands.RestoreWindow(window);
                return windowHWnd;
            }
        }
    }
}
