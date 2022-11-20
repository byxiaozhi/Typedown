using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Typedown.Universal.ViewModels;

namespace Typedown.Universal.Services
{
    public class ImageAction
    {
        public SettingsViewModel Settings { get; }

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        private IServiceProvider ServiceProvider { get; }

        public ImageAction(SettingsViewModel settingsViewModel, IServiceProvider serviceProvider)
        {
            Settings = settingsViewModel;
            ServiceProvider = serviceProvider;
        }

        public async Task<string> DoLocalFileAction(string filePath)
        {
            var result = filePath;
            if (filePath.ToLower().StartsWith("file://"))
                filePath = new Uri(filePath).LocalPath;
            if(filePath.StartsWith("./"))
                filePath = filePath.Substring("./".Length);
            switch (Settings.InsertLocalImageAction)
            {
                case Enums.InsertImageAction.CopyToPath:
                    result = await Task.Run(() => CopyImage(InsertImageSource.Local, filePath));
                    break;
                case Enums.InsertImageAction.Upload:
                    result = await Upload(InsertImageSource.Local, filePath);
                    break;
                case Enums.InsertImageAction.None:
                    break;
            }
            return result;
        }

        public async Task<string> DoWebFileAction(string url)
        {
            var result = url;
            switch (Settings.InsertLocalImageAction)
            {
                case Enums.InsertImageAction.CopyToPath:
                    result = await SaveImage(InsertImageSource.Web, await GetWebImage(new(url)));
                    break;
                case Enums.InsertImageAction.Upload:
                    result = await Upload(InsertImageSource.Web, await GetWebImage(new(url)));
                    break;
            }
            return result;
        }

        public async Task<string> DoClipboardAction(byte[] image)
        {
            string result;
            switch (Settings.InsertClipboardImageAction)
            {
                case Enums.InsertImageAction.Upload:
                    result = await Upload(InsertImageSource.Clipboard, image);
                    break;
                default:
                    result = await Task.Run(() => SaveImage(InsertImageSource.Clipboard, image));
                    break;
            }
            return result;
        }

        public string CopyImage(InsertImageSource source, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var destFilePath = Path.Combine(GetAbsoluteDestFolder(source), fileName);
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, filePath); i++)
                destFilePath = Path.Combine(GetAbsoluteDestFolder(source), $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}");
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                File.Copy(filePath, destFilePath);
            }
            return Common.CombinePath(GetDestFolder(source), fileName);
        }

        public async Task<string> SaveImage(InsertImageSource source, byte[] bytes, string fileName = null)
        {
            fileName ??= $"{Guid.NewGuid()}.png";
            var destFilePath = Path.Combine(GetAbsoluteDestFolder(source), fileName);
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, bytes); i++)
                destFilePath = Path.Combine(GetAbsoluteDestFolder(source), $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}");
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                await File.WriteAllBytesAsync(destFilePath, bytes);
            }
            return Common.CombinePath(GetDestFolder(source), fileName);
        }

        public async Task<byte[]> GetWebImage(Uri uri)
        {
            return await Task.Run(() => new WebClient().DownloadData(uri));
        }

        public string GetDestFolder(InsertImageSource source)
        {
            return Common.CombinePath(source switch
            {
                InsertImageSource.Clipboard => Settings.InsertClipboardImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertClipboardImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Local => Settings.InsertLocalImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertLocalImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Web => Settings.InsertWebImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertWebImageCopyPath : Settings.DefaultImageBasePath,
                _ => throw new NotImplementedException()
            });
        }

        public string GetAbsoluteDestFolder(InsertImageSource source)
        {
            var dest = GetDestFolder(source);
            if (dest.StartsWith("./")) dest = dest.Substring("./".Length);
            return Common.CombinePath(FileViewModel.ImageBasePath, dest);
        }

        public async Task<string> Upload(InsertImageSource source, string filePath)
        {
            return await ServiceProvider.GetService<ImageUpload>().Upload(source, filePath);
        }

        public async Task<string> Upload(InsertImageSource source, byte[] bytes)
        {
            string tmpFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tmpFile, bytes);
                return await Upload(source, tmpFile);
            }
            finally
            {
                File.Delete(tmpFile);
            }
        }

        public enum InsertImageSource { Clipboard, Local, Web };
    }
}
