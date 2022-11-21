using Microsoft.EntityFrameworkCore;
using Microsoft.Web.WebView2.Core;
using PdfiumViewer;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Typedown.Universal.Enums;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Typedown.Utilities;

namespace Typedown.Services
{
    public class FileExport : IFileExport
    {
        public ObservableCollection<ExportConfig> ExportConfigs { get; } = new();

        public AppViewModel ViewModel { get; }

        public FileExport(AppViewModel viewModel)
        {
            ViewModel = viewModel;
            _ = UpdateExportConfigs();
        }

        public async Task<ExportConfig> AddExportConfig(string name = null, ExportType type = 0)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx;
            var res = new ExportConfig() { Name = name ?? string.Empty, Type = type };
            await model.AddAsync(res);
            await ctx.SaveChangesAsync();
            await UpdateExportConfigs();
            return res;
        }

        public async Task RemoveExportConfig(int id)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ExportConfigs;
            model.RemoveRange(model.Where(x => x.Id == id));
            await ctx.SaveChangesAsync();
            await UpdateExportConfigs();
        }

        public async Task<bool> SaveExportConfig(ExportConfig config)
        {
            try
            {
                using var ctx = await AppDbContext.Create();
                var model = ctx.ExportConfigs;
                model.Update(config);
                await ctx.SaveChangesAsync();
                await UpdateExportConfigs();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<ExportConfig> GetExportConfig(int id)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ExportConfigs;
            return await model.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateExportConfigs()
        {
            using var ctx = await AppDbContext.Create();
            var newItems = await ctx.ExportConfigs.ToListAsync();
            ExportConfigs.UpdateCollection(newItems, (a, b) => a.Id == b.Id);
        }

        public async Task HtmlToPdf(string basePath, string htmlString, string savePath)
        {
            var tmpWindow = PInvoke.CreateWindowEx(0, "Static", null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            using var destroyWindow = Disposable.Create(() => PInvoke.DestroyWindow(tmpWindow));
            var environment = await WebViewController.EnsureCreateEnvironment();
            var compositionController = await environment.CreateCoreWebView2CompositionControllerAsync(tmpWindow);
            var raw = typeof(CoreWebView2CompositionController).GetField("_rawNative", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(compositionController);
            var controller = typeof(CoreWebView2Controller).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(object) }, null).Invoke(new object[] { raw }) as CoreWebView2Controller;
            using var closeController = Disposable.Create(() => controller.Close());
            var coreWebView2 = controller.CoreWebView2;
            var loadedTaskSource = new TaskCompletionSource<bool>();
            coreWebView2.DOMContentLoaded += (s, e) => loadedTaskSource.SetResult(true);
            controller.CoreWebView2.NavigateToString(htmlString);
            await loadedTaskSource.Task;
            await controller.CoreWebView2.PrintToPdfAsync(savePath);
            controller.Close();
        }

        public async Task Print(string basePath, string htmlString, string documentName = null)
        {
            var tmpFile = Universal.Utilities.Common.GetTempFileName(".pdf");
            using var deleteTmpFile = Disposable.Create(() => File.Delete(tmpFile));
            await HtmlToPdf(basePath, htmlString, tmpFile);
            using var stream = new MemoryStream();
            await stream.WriteAsync(File.ReadAllBytes(tmpFile));
            using var pdf = PdfDocument.Load(stream);
            using var print = pdf.CreatePrintDocument();
            print.DocumentName = documentName;
            var dialog = new PrintDialog() { Document = print };
            var result = dialog.ShowDialog(new Win32Window(ViewModel.MainWindow));
            if (result == DialogResult.OK)
                print.Print();
        }

        private record Win32Window(nint Handle) : IWin32Window;
    }
}
