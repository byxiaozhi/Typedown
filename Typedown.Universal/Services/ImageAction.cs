using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Universal.ViewModels;

namespace Typedown.Universal.Services
{
    public class ImageAction
    {
        public SettingsViewModel Settings { get; }

        private IServiceProvider ServiceProvider { get; }

        public ImageAction(SettingsViewModel settingsViewModel, IServiceProvider serviceProvider)
        {
            Settings = settingsViewModel;
            ServiceProvider = serviceProvider;
        }

        public async Task<string> DoAction(InsertImageSource source, string filePath)
        {
            var action = source switch
            {
                InsertImageSource.Clipboard => Settings.InsertClipboardImageAction,
                InsertImageSource.Local => Settings.InsertLocalImageAction,
                InsertImageSource.Web => Settings.InsertWebImageAction,
                _ => throw new NotImplementedException()
            };
            var result = filePath;
            switch (action)
            {
                case Enums.InsertImageAction.CopyToPath:
                    result = await Task.Run(() => Copy(source, filePath));
                    break;
                case Enums.InsertImageAction.Upload:
                    result = await Upload(source, filePath);
                    break;
                case Enums.InsertImageAction.None:
                    break;
            }
            return result;
        }

        public string Copy(InsertImageSource source, string filePath)
        {
            var dest = source switch
            {
                InsertImageSource.Clipboard => Settings.InsertClipboardImageCopyPath,
                InsertImageSource.Local => Settings.InsertLocalImageCopyPath,
                InsertImageSource.Web => Settings.InsertWebImageCopyPath,
                _ => throw new NotImplementedException()
            };
            var fileViewModel = ServiceProvider.GetService<FileViewModel>();
            File.Copy(filePath, Path.Combine(fileViewModel.ImageBasePath, dest));
            return dest;
        }

        public Task<string> Upload(InsertImageSource source, string filePath)
        {
            return ServiceProvider.GetService<ImageUpload>().Upload(source, filePath);
        }

        public enum InsertImageSource { Clipboard, Local, Web };
    }
}
