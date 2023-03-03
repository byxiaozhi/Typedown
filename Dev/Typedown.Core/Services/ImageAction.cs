using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
                        result = await Task.Run(() => CopyImage(InsertImageSource.Local, AppViewModel.GetImageAbsolutePath(filePath)));
                        break;
                    case Enums.InsertImageAction.Upload:
                        result = await Upload(InsertImageSource.Local, AppViewModel.GetImageAbsolutePath(filePath));
                        break;
                    default:
                        result = ConvertImagePath(filePath);
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

        public string CopyImage(InsertImageSource source, string sourceFile, string destFolder = null)
        {
            var fileName = Path.GetFileName(sourceFile);
            destFolder ??= GetDefaultDestFolder(source);
            var destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, fileName));
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, sourceFile); i++)
                destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}"));
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                File.Copy(sourceFile, destFilePath);
            }
            return Path.Combine(destFolder, fileName);
        }

        public async Task<string> SaveImage(InsertImageSource source, byte[] bytes, string fileName = null, string destFolder = null)
        {
            fileName ??= $"{Guid.NewGuid()}.{GetImageType(bytes, "png")}";
            destFolder ??= GetDefaultDestFolder(source);
            var destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, fileName));
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, bytes); i++)
                destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}"));
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                await File.WriteAllBytesAsync(destFilePath, bytes);
            }
            return Path.Combine(destFolder, fileName);
        }

        public string SaveImage(InsertImageSource source, IClipboardImage image, string fileName = null, string destFolder = null)
        {
            fileName ??= $"{Guid.NewGuid()}.png";
            destFolder ??= GetDefaultDestFolder(source);
            var destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, fileName));
            for (int i = 2; File.Exists(destFilePath) && !Common.FileContentEqual(destFilePath, image.GetBytes()); i++)
                destFilePath = AppViewModel.GetImageAbsolutePath(Path.Combine(destFolder, $"{Path.GetFileNameWithoutExtension(destFilePath)} ({i}){Path.GetExtension(destFilePath)}"));
            if (!File.Exists(destFilePath))
            {
                new FileInfo(destFilePath).Directory?.Create();
                image.SaveAsPng(destFilePath);
            }
            return Path.Combine(destFolder, fileName);
        }

        public async Task<byte[]> GetWebImage(Uri uri)
        {
            return await Task.Run(() => new WebClient().DownloadData(uri));
        }

        public string GetDefaultDestFolder(InsertImageSource source)
        {
            return source switch
            {
                InsertImageSource.Clipboard => Settings.InsertClipboardImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertClipboardImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Local => Settings.InsertLocalImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertLocalImageCopyPath : Settings.DefaultImageBasePath,
                InsertImageSource.Web => Settings.InsertWebImageAction == Enums.InsertImageAction.CopyToPath ? Settings.InsertWebImageCopyPath : Settings.DefaultImageBasePath,
                _ => throw new NotImplementedException()
            };
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

        public static string GetImageType(byte[] bytes, string defaultType)
        {
            string headerCode = GetHeaderInfo(bytes).ToUpper();

            if (headerCode.StartsWith("FFD8FFE0"))
            {
                return "jpg";
            }
            else if (headerCode.StartsWith("49492A"))
            {
                return "tiff";
            }
            else if (headerCode.StartsWith("424D"))
            {
                return "bmp";
            }
            else if (headerCode.StartsWith("474946"))
            {
                return "gif";
            }
            else if (headerCode.StartsWith("89504E470D0A1A0A"))
            {
                return "png";
            }
            else
            {
                return defaultType; //UnKnown
            }
        }

        public static string GetHeaderInfo(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes.Take(8))
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        public string ConvertImagePath(string filePath)
        {
            if (UriHelper.IsAbsolutePath(filePath) && Settings.PreferRelativeImagePaths)
            {
                filePath = Path.GetRelativePath(FileViewModel.ImageBasePath, filePath);
                if (Settings.AddSymbolBeforeRelativePath)
                    filePath = "./" + filePath;
            }
            return filePath.Replace('\\', '/');
        }
    }
}
