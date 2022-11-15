using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Universal.Enums;
using Typedown.Universal.Models;
using Typedown.Universal.Utilities;

namespace Typedown.Universal.Services
{
    public class ImageUpload
    {
        public ObservableCollection<ImageUploadConfig> ImageUploadConfigs { get; } = new();

        public ImageUpload()
        {
            _ = UpdateImageUploadConfigs();
        }

        public async Task<ImageUploadConfig> AddImageUploadConfig(string name = null, ImageUploadMethod method = 0)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ImageUploadConfigs;
            var res = new ImageUploadConfig() { Name = name ?? string.Empty, Method = method };
            await model.AddAsync(res);
            await ctx.SaveChangesAsync();
            await UpdateImageUploadConfigs();
            return res;
        }

        public async Task RemoveImageUploadConfig(int id)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ImageUploadConfigs;
            model.RemoveRange(model.Where(x => x.Id == id));
            await ctx.SaveChangesAsync();
            await UpdateImageUploadConfigs();
        }

        public async Task<bool> SaveImageUploadConfig(ImageUploadConfig config)
        {
            try
            {
                using var ctx = await AppDbContext.Create();
                var model = ctx.ImageUploadConfigs;
                model.Update(config);
                await ctx.SaveChangesAsync();
                await UpdateImageUploadConfigs();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<ImageUploadConfig> GetImageUploadConfig(int id)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ImageUploadConfigs;
            return await model.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task UpdateImageUploadConfigs()
        {
            using var ctx = await AppDbContext.Create();
            var newItems = await ctx.ImageUploadConfigs.ToListAsync();
            ImageUploadConfigs.UpdateCollection(newItems, (a, b) => a.Id == b.Id);
        }
    }
}
