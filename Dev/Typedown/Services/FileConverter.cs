using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class FileConverter : IFileConverter
    {
        public async Task<MemoryStream> HtmlToPdf(string html, CoreWebView2PrintSettings settings = null)
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
                    return await PrintToPdfStreamAsync(coreWebView2, settings);
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

        public async Task<MemoryStream> HtmlToPdf(string html, PdfPrintSettings settings = null)
        {
            settings ??= new PdfPrintSettings();
            var environment = await WebViewController.EnsureCreateEnvironment();
            var webView2settings = environment.CreatePrintSettings();
            webView2settings.Orientation = (CoreWebView2PrintOrientation)settings.Orientation;
            if (settings.PageSize.HasValue)
            {
                webView2settings.PageWidth = settings.PageSize.Value.Width;
                webView2settings.PageHeight = settings.PageSize.Value.Height;
            }
            if (settings.Margin.HasValue)
            {
                webView2settings.MarginLeft = settings.Margin.Value.Left;
                webView2settings.MarginTop = settings.Margin.Value.Top;
                webView2settings.MarginRight = settings.Margin.Value.Right;
                webView2settings.MarginBottom = settings.Margin.Value.Bottom;
            }
            webView2settings.ShouldPrintHeaderAndFooter = settings.ShouldPrintHeaderAndFooter;
            webView2settings.HeaderTitle = settings.Header;
            webView2settings.FooterUri = settings.Footer;
            return await HtmlToPdf(html, webView2settings);
        }

        private async Task<MemoryStream> PrintToPdfStreamAsync(CoreWebView2 coreWebView2, CoreWebView2PrintSettings settings = null)
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
