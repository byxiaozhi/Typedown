using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class FileConverter : IFileConverter
    {
        public async Task<MemoryStream> HtmlToPdf(string html, CoreWebView2PrintSettings settings = null)
        {
            var tmpWindow = PInvoke.CreateWindowEx(0, "Static", null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var tmpFile = Core.Utilities.Common.GetTempFileName(".html");
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
            // The browser's own PDF printer can write a document outline from the headings, which is the
            // bookmark pane in a PDF reader — "the exported PDF has no table of contents" (Store reviews).
            // WebView2's PrintToPdf does not ask it to; the protocol call does. Anything wrong falls back.
            try
            {
                var outlined = await HtmlToPdfWithOutline(html, settings);
                if (outlined != null) return outlined;
            }
            catch (Exception ex)
            {
                Log.Debug($"pdf: outline export failed, falling back: {ex.Message}");
            }
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

        private static string Esc(string text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);

        private async Task<MemoryStream> HtmlToPdfWithOutline(string html, PdfPrintSettings settings)
        {
            var tmpWindow = PInvoke.CreateWindowEx(0, "Static", null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            var tmpFile = Core.Utilities.Common.GetTempFileName(".html");
            try
            {
                var environment = await WebViewController.EnsureCreateEnvironment();
                var compositionController = await environment.CreateCoreWebView2CompositionControllerAsync(tmpWindow);
                var controller = WebViewController.CreateCoreWebView2Controller(compositionController);
                try
                {
                    var coreWebView2 = controller.CoreWebView2;
                    var loaded = new TaskCompletionSource<bool>();
                    coreWebView2.NavigationCompleted += (s, e) => loaded.TrySetResult(true);
                    File.WriteAllText(tmpFile, html);
                    coreWebView2.Navigate(tmpFile);
                    await loaded.Task;
                    var landscape = settings.Orientation == Core.Enums.PrintOrientation.Landscape;
                    var style = "font-size:8px;font-family:sans-serif;color:#666;width:100%;padding:0 0.4in;box-sizing:border-box;";
                    var parameters = new Newtonsoft.Json.Linq.JObject
                    {
                        ["landscape"] = landscape,
                        ["printBackground"] = true,
                        ["preferCSSPageSize"] = false,
                        ["generateDocumentOutline"] = true,
                        ["generateTaggedPDF"] = true,
                        ["displayHeaderFooter"] = settings.ShouldPrintHeaderAndFooter,
                        ["headerTemplate"] = $"<div style=\"{style}text-align:center\">{Esc(settings.Header)}</div>",
                        ["footerTemplate"] = $"<div style=\"{style}display:flex;justify-content:space-between\"><span>{Esc(settings.Footer)}</span><span><span class=\"pageNumber\"></span> / <span class=\"totalPages\"></span></span></div>",
                    };
                    if (settings.ScaleFactor.HasValue) parameters["scale"] = settings.ScaleFactor.Value;
                    if (settings.PageSize.HasValue)
                    {
                        parameters["paperWidth"] = settings.PageSize.Value.Width;
                        parameters["paperHeight"] = settings.PageSize.Value.Height;
                    }
                    if (settings.Margin.HasValue)
                    {
                        parameters["marginTop"] = settings.Margin.Value.Top;
                        parameters["marginBottom"] = settings.Margin.Value.Bottom;
                        parameters["marginLeft"] = settings.Margin.Value.Left;
                        parameters["marginRight"] = settings.Margin.Value.Right;
                    }
                    var result = await coreWebView2.CallDevToolsProtocolMethodAsync("Page.printToPDF", parameters.ToString(Newtonsoft.Json.Formatting.None));
                    var data = Newtonsoft.Json.Linq.JObject.Parse(result)["data"]?.ToString();
                    if (string.IsNullOrEmpty(data)) return null;
                    return new MemoryStream(Convert.FromBase64String(data));
                }
                finally
                {
                    controller.Close();
                }
            }
            finally
            {
                PInvoke.DestroyWindow(tmpWindow);
                try { File.Delete(tmpFile); } catch { }
            }
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
