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
            var settings = provider.GetService<SettingsViewModel>();
            var theme = settings.EffectiveTheme;
            var isDarkMode = ui.ActualTheme == ElementTheme.Dark;
            var isBlack = theme == Typedown.Core.Enums.AppTheme.Black;
            // A custom theme may name its own accent; without one the editor keeps the system colour.
            var custom = Typedown.Core.Utilities.ThemeFiles.Find(settings.CustomTheme);
            // global:: — inside Typedown.* a plain "Windows." binds to Typedown.Windows, the app's own namespace.
            var themeAccent = Typedown.Core.Utilities.ThemeFiles.Brush(custom?.Accent) as global::Windows.UI.Xaml.Media.SolidColorBrush;
            var accentColor = themeAccent?.Color ?? new UISettings().GetColorValue(UIColorType.Accent);
            var solidBackground = isBlack ? Colors.Black : isDarkMode ? Color.FromArgb(0xFF, 0x28, 0x28, 0x28) : Color.FromArgb(0xFF, 0xF9, 0xF9, 0xF9);
            var background = settings.UseMicaEffect && settings.UseEditorMicaEffect && !isBlack ? Colors.Transparent : solidBackground;
            return new { theme = isBlack ? "Black" : isDarkMode ? "Dark" : "Light", accentColor, background };
        }

        public static bool GetUseLightTheme()
        {
            return Application.Current.RequestedTheme == ApplicationTheme.Light;
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

        public static IntPtr OpenNewWindow(string[] args, bool forceNewWindow = false)
        {
            var filePath = CommandLine.GetOpenFilePath(args);
            if (!string.IsNullOrEmpty(filePath) && FileViewModel.TryGetOpenedWindow(filePath, out var windowHWnd))
            {
                // Already open somewhere: bring that window (and tab) to the front.
                var window = XamlWindow.AllWindows.OfType<MainWindow>().FirstOrDefault(x => x.Handle == windowHWnd);
                if (window != null)
                {
                    if (PInvoke.IsIconic(window.Handle))
                        PInvoke.ShowWindow(window.Handle, PInvoke.ShowWindowCommand.Restore);
                    var tab = window.AppViewModel.TabsViewModel?.FindByPath(filePath);
                    if (tab != null) _ = window.AppViewModel.TabsViewModel.SwitchTo(tab);
                }
                return windowHWnd;
            }
            // A file opened from the shell goes into the last active window as a new tab (upstream #73) unless the
            // user prefers separate windows; a plain launch without a file always creates a window.
            var target = MainWindow.LastActive ?? XamlWindow.AllWindows.OfType<MainWindow>().FirstOrDefault();
            if (!forceNewWindow && !string.IsNullOrEmpty(filePath) && target != null && target.AppViewModel != null && target.AppViewModel.SettingsViewModel.OpenFilesInNewTab)
            {
                if (PInvoke.IsIconic(target.Handle))
                    PInvoke.ShowWindow(target.Handle, PInvoke.ShowWindowCommand.Restore);
                target.AppViewModel.FileViewModel.OpenFileCommand.Execute(filePath);
                return target.Handle;
            }
            var newWindow = new MainWindow();
            newWindow.Show(ShowWindowCommand.SW_HIDE);
            newWindow.AppViewModel.CommandLineArgs = args;
            return newWindow.Handle;
        }
    }
}
