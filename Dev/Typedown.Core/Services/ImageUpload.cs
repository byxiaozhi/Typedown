using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.Core.Services
{
    public class ImageUpload
    {
        public ObservableCollection<ImageUploadConfig> ImageUploadConfigs { get; } = new();

        private IServiceProvider serviceProvider;

        public ImageUpload(IServiceProvider serviceProvider, SettingsViewModel settings)
        {
            this.serviceProvider = serviceProvider;
            settings.ResetSettingsCommand.OnExecute.Subscribe(async _ =>
            {
                try { await ResetDefaultConfigs(); }
                catch (Exception ex) { Utilities.Log.WriteLocal("ImageUploadReset", ex.ToString()); }
            });
            Ready = InitializeAsync(settings);
        }

        /// <summary>
        /// Completes once <see cref="ImageUploadConfigs"/> holds what the database has. The service is made for a window
        /// when it is first asked for, and the list is read in the background then: anything that looks a configuration
        /// up awaits this, or the first upload after a start (or in a new window) found none and said so.
        /// </summary>
        public Task Ready { get; }

        private async Task SeedAsync(SettingsViewModel settings)
        {
            await ResetDefaultConfigs();
            settings.ImageUploadDatabaseInitialized = true;
        }

        // Written once for all windows, marked once written (see FileExport).
        private static Task seeding;

        private async Task InitializeAsync(SettingsViewModel settings)
        {
            if (!settings.ImageUploadDatabaseInitialized)
            {
                seeding ??= SeedAsync(settings);
                await seeding;
            }
            else
            {
                await UpdateImageUploadConfigs();
            }
        }

        private async Task ResetDefaultConfigs()
        {
            using var ctx = await AppDbContext.Create();
            ctx.ImageUploadConfigs.RemoveRange(ctx.ImageUploadConfigs);
            await ctx.SaveChangesAsync();
            await UpdateImageUploadConfigs();
        }

        public async Task<ImageUploadConfig> AddImageUploadConfig(string name = null, ImageUploadMethod method = 0)
        {
            using var ctx = await AppDbContext.Create();
            var model = ctx.ImageUploadConfigs;
            // Enabled from the start: switched off, a new configuration was in neither Settings > Image > Upload with nor
            // the image's Upload menu, and nothing on its page said why.
            var res = new ImageUploadConfig() { Name = name ?? string.Empty, Method = method, IsEnable = true };
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
            catch (Exception ex)
            {
                Utilities.Log.WriteLocal("ImageUploadConfigSave", ex.ToString());
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

        /// <summary>The configuration uploads use (Settings > Image > Upload with), or null when none is chosen and enabled.</summary>
        public ImageUploadConfig DefaultConfig
        {
            get
            {
                var id = serviceProvider.GetService<SettingsViewModel>().DefaultImageUploadConfigId;
                return id.HasValue ? ImageUploadConfigs.FirstOrDefault(x => x.IsEnable && x.Id == id.Value) : null;
            }
        }

        /// <summary>What was uploaded with which configuration, for every window (Settings > Image > Upload history).</summary>
        public static UploadHistory History { get; } = new(System.IO.Path.Combine(Config.GetLocalFolderPath(), "ImageUploadHistory.json"));

        /// <summary><see cref="DefaultConfig"/> once the configurations have been read (<see cref="Ready"/>).</summary>
        public async Task<ImageUploadConfig> GetDefaultConfigAsync()
        {
            try { await Ready; }
            catch (Exception ex) { Log.Debug($"image upload: reading the configurations failed: {ex.Message}"); }
            return DefaultConfig;
        }

        public async Task<string> Upload(ImageAction.InsertImageSource source, string filePath)
        {
            if (await GetDefaultConfigAsync() is not ImageUploadConfig config)
                throw new InvalidOperationException(Locale.GetDialogString("UploadImages.NoConfig"));
            return (await UploadRemembered(config, filePath)).url;
        }

        /// <summary>
        /// The file's address under this configuration: the one it got before (same content, configuration unchanged),
        /// or a new upload. Test upload does not come here: it always uploads.
        /// </summary>
        public Task<(string url, bool reused)> UploadRemembered(ImageUploadConfig config, string filePath) =>
            History.UploadAsync(UploadHistory.Scope(config.Method.ToString(), config.Config), filePath,
                () => config.LoadUploadConfig().Upload(serviceProvider, filePath));

        public async Task<string> Upload(ImageUploadConfig config, string filePath)
        {
            return await config.LoadUploadConfig().Upload(serviceProvider, filePath);
        }
    }
}
