using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Typedown.Universal.Enums;
using Typedown.Universal.ViewModels;
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

        public static IntPtr OpenNewWindow(string path)
        {
            throw new NotImplementedException();
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
    }
}
