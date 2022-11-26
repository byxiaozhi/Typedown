using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Windows.Devices.Bluetooth.Advertisement;

namespace Typedown.Utilities
{
    public static class FileConverter
    {
        public static async Task<Stream> HtmlToPdf(string basePath, string html, CoreWebView2PrintSettings settings = null)
        {
            var tmpWindow = PInvoke.CreateWindowEx(0, "Static", null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var tmpFile = Universal.Utilities.Common.GetTempFileName(".html");
            try
            {
                var environment = await WebViewController.EnsureCreateEnvironment();
                var compositionController = await environment.CreateCoreWebView2CompositionControllerAsync(tmpWindow);
                var controller = WebViewController.CreateCoreWebView2Controller(compositionController);
                try
                {
                    var coreWebView2 = controller.CoreWebView2;
                    var loadedTaskSource = new TaskCompletionSource<bool>();
                    coreWebView2.NavigationCompleted += (s, e) => loadedTaskSource.SetResult(true);
                    File.WriteAllText(tmpFile, html);
                    coreWebView2.Navigate(tmpFile);
                    await loadedTaskSource.Task;
                    return await coreWebView2.PrintToPdfStreamAsync(settings);
                }
                finally
                {
                    controller.Close();
                }
            }
            finally
            {
                PInvoke.DestroyWindow(tmpWindow);
                File.Delete(tmpFile);
            }
        }

        private static async Task<Stream> PrintToPdfStreamAsync(this CoreWebView2 coreWebView2, CoreWebView2PrintSettings settings = null)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                await coreWebView2.PrintToPdfAsync(tmpFile, settings);
                return new MemoryStream(await File.ReadAllBytesAsync(tmpFile));
            }
            finally
            {
                File.Delete(tmpFile);
            }
        }
    }
}
