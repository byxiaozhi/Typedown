using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Typedown.Universal;
using Typedown.Universal.Enums;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;
using Typedown.Utilities;
using Windows.Storage;

namespace Typedown.Services
{
    public class FileExport : IFileExport
    {
        public ObservableCollection<ExportConfig> ExportConfigs { get; } = new();

        public AppViewModel ViewModel { get; }

        public IFileConverter FileConverter { get; }

        public FileExport(AppViewModel viewModel, SettingsViewModel settings, IFileConverter fileConverter)
        {
            ViewModel = viewModel;
            FileConverter = fileConverter;
            settings.ResetSettingsCommand.OnExecute.Subscribe(async _ => await ResetDefaultConfigs());
            Initialize();
        }

        private async void Initialize()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (settings["FileExportDatabaseInitialized"] == null)
            {
                settings["FileExportDatabaseInitialized"] = true;
                await ResetDefaultConfigs();
            }
            else
            {
                await UpdateExportConfigs();
            }
        }

        private async Task ResetDefaultConfigs()
        {
            using var ctx = await AppDbContext.Create();
            ctx.ExportConfigs.RemoveRange(ctx.ExportConfigs);
            await ctx.SaveChangesAsync();
            await AddExportConfig("PDF", ExportType.PDF);
            await AddExportConfig("HTML", ExportType.HTML);
            await UpdateExportConfigs();
        }

        public async Task<ExportConfig> AddExportConfig(string name = null, ExportType type = 0)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ExportConfigs;
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

        public async Task Print(string basePath, string html, string documentName = null)
        {
            using var stream = await FileConverter.HtmlToPdf(html);
            await PrintHelper.PrintPDF(ViewModel.MainWindow, stream, documentName);
        }
    }
}
