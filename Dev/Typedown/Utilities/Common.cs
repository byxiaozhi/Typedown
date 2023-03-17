using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reactive.Linq;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Typedown.Windows;
using Typedown.XamlUI;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Typedown.Utilities
{
    public static class Common
    {
        public static Point MakePoint(this IntPtr p) => new(GetLowWord(p), GetHighWord(p));

        public static nint PackPoint(this Point point) => ((int)point.X) | (((int)point.Y) << 16);

        public static int GetHighWord(IntPtr p) => (int)(p.ToInt64() >> 16);

        public static int GetLowWord(IntPtr p) => (int)(p.ToInt64() & 0xFFFF);

        public static object GetCurrentTheme(this IServiceProvider provider)
        {
            var ui = provider.GetService<UIViewModel>();
            var isDarkMode = ui.ActualTheme == ElementTheme.Dark;
            var accentColor = new UISettings().GetColorValue(UIColorType.Accent);
            var background = Colors.Transparent;
            return new { theme = isDarkMode ? "Dark" : "Light", accentColor, background };
        }

        public static bool GetUseLightTheme()
        {
            return Application.Current.RequestedTheme == global::Windows.UI.Xaml.ApplicationTheme.Light;
        }

        public static void TrySaveWindowPlacement(this MainWindow window, Point offset = default)
        {
            if (window.Handle == IntPtr.Zero)
                return;
            PInvoke.GetWindowPlacement(window.Handle, out var placement);
            var rawX = (int)(offset.X * window.ScalingFactor);
            var rawY = (int)(offset.Y * window.ScalingFactor);
            placement.rcNormalPosition.left += rawX;
            placement.rcNormalPosition.top += rawY;
            placement.rcNormalPosition.right += rawX;
            placement.rcNormalPosition.bottom += rawY;
            window.AppViewModel.SettingsViewModel.StartupPlacement = placement;
        }

        public static bool ShowWindowWithSavedPlacement(this MainWindow window)
        {
            var placement = window.AppViewModel.SettingsViewModel.StartupPlacement;
            if (!placement.HasValue)
            {
                window.Show(ShowWindowCommand.SW_NORMAL);
                return false;
            }
            var value = placement.Value;
            if (value.showCmd != PInvoke.ShowWindowCommand.ShowMaximized)
                value.showCmd = PInvoke.ShowWindowCommand.Normal;
            PInvoke.SetWindowPlacement(window.Handle, ref value);
            return true;
        }

        public static IntPtr OpenNewWindow(string[] args)
        {
            var filePath = CommandLine.GetOpenFilePath(args);
            if (string.IsNullOrEmpty(filePath) || !FileViewModel.TryGetOpenedWindow(filePath, out var windowHWnd))
            {
                var newWindow = new MainWindow();
                newWindow.Show(ShowWindowCommand.SW_HIDE);
                newWindow.AppViewModel.CommandLineArgs = args;
                return newWindow.Handle;
            }
            else
            {
                var appWindows = XamlWindow.AllWindows;
                var window = appWindows.Where(x => x.Handle == windowHWnd).FirstOrDefault();
                if (window != null && PInvoke.IsIconic(window.Handle))
                    PInvoke.ShowWindow(window.Handle, PInvoke.ShowWindowCommand.Restore);
                return windowHWnd;
            }
        }
    }
}
