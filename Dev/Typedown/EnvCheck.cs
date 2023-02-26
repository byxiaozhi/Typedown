using Microsoft.Web.WebView2.Core;
using System;
using System.Threading.Tasks;
using Typedown.Windows;

namespace Typedown
{
    public static class EnvCheck
    {
        public static bool IsWebView2Installed()
        {
            try
            {
                _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> EnsureWebView2Installed()
        {
            if (IsWebView2Installed())
            {
                return true;
            }
            return await WebViewInstallWindow.TryInstallWebView2();
        }
    }
}
