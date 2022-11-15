using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Models;
using Typedown.Universal.Services;
using Typedown.Universal.Utilities;

namespace Typedown.Services
{
    public class FileExport : IFileExport
    {
        public ObservableCollection<ExportConfig> ExportConfigs { get; } = new();

        public FileExport()
        {
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

        public void HtmlToPdf(string basePath, string htmlString, string sourcePath, string savePath)
        {
            throw new NotImplementedException();
        }

        public void Print(string basePath, string htmlString)
        {
            throw new NotImplementedException();
        }
    }
}
