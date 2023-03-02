using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Typedown.Core.Interfaces;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;

namespace Typedown.Core.Services
{
    public class ImageAction
    {
        public SettingsViewModel Settings { get; }

        public AppViewModel AppViewModel => ServiceProvider.GetService<AppViewModel>();

        public FileViewModel FileViewModel => ServiceProvider.GetService<FileViewModel>();

        private IServiceProvider ServiceProvider { get; }

        public ImageAction(SettingsViewModel settingsViewModel, IServiceProvider serviceProvider)
        {
            Settings = settingsViewModel;
            ServiceProvider = serviceProvider;
        }

        public async Task<string> DoLocalFileAction(string src)
        {
            try
            {
                var result = src;
                if (!UriHelper.TryGetLocalPath(src, out var filePath))
                    return result;
                switch (Settings.InsertLocalImageAction)
                {
                    case Enums.InsertImageAction.CopyToPath:
                        result = await Task.Run(() => CopyImage(InsertImageSource.Local, filePath));
                        break;
                    case Enums.InsertImageAction.Upload:
                        result = await Upload(InsertImageSource.Local, filePath);
                        break;
                    default:
                        if (UriHelper.IsAbsolutePath(filePath) && Settings.PreferRelativeImagePaths)
                        {
                            result = Path.GetRelativePath(FileViewModel.ImageBasePath, filePath);
                            if (Settings.AddSymbolBeforeRelativePath)
                                result = "./" + result;
                        }
                        break;
                }
                if (UriHelper.IsAbsolutePath(result))
                    return new Uri(result).AbsoluteUri;
                return result;
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(AppViewModel.XamlRoot);
                return src;
            }
        }

        public async Task<string> DoWebFileAction(string src)
        {
            try
            {
                var result = src;
                switch (Settings.InsertWebImageAction)
                {
                    case Enums.InsertImageAction.CopyToPath:
                        result = await SaveImage(InsertImageSource.Web, await GetWebImage(new(src)));
                        break;
                    case Enums.InsertImageAction.Upload:
                        result = await Upload(InsertImageSource.Web, await GetWebImage(new(src)));
                        break;
                    default:
                        return src;
                }
                if (UriHelper.IsAbsolutePath(result))
                    return new Uri(result).AbsoluteUri;
                return result;
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(AppViewModel.XamlRoot);
                return src;
            }
        }

        public async Task<string> DoClipboardAction(IClipboardImage image)
        {
            try
            {
                string result;
                switch (Settings.InsertClipboardImageAction)
                {
                    case Enums.InsertImageAction.Upload:
                        result = await Upload(InsertImageSource.Clipboard, image.GetBytes());
                        break;
                    default:
                        result = await Task.Run(() => SaveImage(InsertImageSource.Clipboard, image));
                        break;
                }
                if (UriHelper.IsAbsolutePath(result))
                    return new Uri(result).AbsoluteUri;
                return result;
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetString("Error"), ex.Message, Locale.GetString("Ok")).ShowAsync(AppViewModel.XamlRoot);
                return string.Empty;
            }
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
            return Path.Combine(GetDestFolder(source), fileName);
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
            return Path.Combine(GetDestFolder(source), fileName);
        }

        public string SaveImage(InsertImageSource source, IClipboardImage image, string fileName = null)
        {
            fileName ??= $"{Guid.NewGuid()}.png";
            var destFilePath = Path.Combine(GetAbsoluteDestFolder(source), fileName);
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, image.GetBytes()); i++)
                destFilePath = Path.Combine(GetAbsoluteDestFolder(source), $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}");
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                image.SaveAsPng(destFilePath);
            }
            return Path.Combine(GetDestFolder(source), fileName);
        }

        public async Task<byte[]> GetWebImage(Uri uri)
        {
            return await Task.Run(() => new WebClient().DownloadData(uri));
        }

        public string GetDestFolder(InsertImageSource source)
        {
            return source switch
            {
                InsertImageSource.Clipboard => Settings.InsertClipboardImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertClipboardImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Local => Settings.InsertLocalImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertLocalImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Web => Settings.InsertWebImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertWebImageCopyPath : Settings.DefaultImageBasePath,
                _ => throw new NotImplementedException()
            };
        }

        public string GetAbsoluteDestFolder(InsertImageSource source)
        {
            return Path.GetFullPath(Path.Combine(FileViewModel.ImageBasePath, GetDestFolder(source)));
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
