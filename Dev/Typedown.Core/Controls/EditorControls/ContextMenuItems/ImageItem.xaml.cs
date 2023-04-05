using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Models.RuntimeModels;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Typedown.Core.ViewModels;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.EditorControls.ContextMenuItems
{
    public sealed partial class ImageItem : MenuItemCollection, INotifyPropertyChanged
    {
        public AppViewModel ViewModel { get; private set; }

        public ImageUpload ImageUpload => ViewModel.ServiceProvider.GetService<ImageUpload>();

        public IFileOperation FileOperation => ViewModel.ServiceProvider.GetService<IFileOperation>();

        public ImageAction ImageAction => ViewModel.ServiceProvider.GetService<ImageAction>();

        public JToken SelectedImage { get; private set; }

        public string ImageSrc => SelectedImage?["token"]?["src"]?.ToString() ?? "";

        public string ImageAlt => SelectedImage?["token"]?["alt"]?.ToString() ?? "";

        public string ImageTitle => SelectedImage?["token"]?["title"]?.ToString() ?? "";

        public ImageItem()
        {
            this.InitializeComponent();
        }

        private void OnOpenImageLocationItemLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel = (sender as FrameworkElement).GetService<AppViewModel>();
            SelectedImage = ViewModel.EditorViewModel.Selection["selectedImage"];
            UpdateMenuItemState();
            UpdateImageUploadConfigItem();
        }

        private void UpdateMenuItemState()
        {
            var isLocalImage = UriHelper.TryGetLocalPath(ImageSrc, out _);
            OpenImageLocationItem.IsEnabled = isLocalImage;
            MoveImageItem.IsEnabled = isLocalImage;
            DeleteImageItem.IsEnabled = isLocalImage;
        }

        private void UpdateImageUploadConfigItem()
        {
            var configs = ImageUpload.ImageUploadConfigs.Where(x => x.IsEnable).ToList();
            while (UploadSubMenu.Items[1] is not MenuFlyoutSeparator)
                UploadSubMenu.Items.RemoveAt(1);
            foreach (var config in configs.Reverse<ImageUploadConfig>())
            {
                var item = new MenuFlyoutItem() { Text = config.Name, Tag = config };
                item.Click += (s, e) => OnUploadImageClick(config);
                UploadSubMenu.Items.Insert(1, item);
            }
            NoUploadConfigItem.Visibility = configs.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnOpenImageLocationClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UriHelper.TryGetLocalPath(ImageSrc, out var path);
                var fullPath = Path.GetFullPath(Path.Combine(ViewModel.FileViewModel.ImageBasePath, path));
                Common.OpenFileLocation(fullPath);
            }
            catch
            {

            }
        }

        private async void OnCopyImageToClick(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] bytes = await GetImageBytes();
                if (bytes == null)
                    return;
                var file = await PickImageSavePath(bytes);
                if (file == null)
                    return;
                await File.WriteAllBytesAsync(file, bytes);
                file = ImageAction.ConvertImagePath(file);
                ReplaceImage(new(file, ImageAlt, ImageTitle));
            }
            catch
            {

            }
        }

        private async void OnMoveImageToClick(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] bytes = await GetImageBytes();
                if (bytes == null)
                    return;
                var file = await PickImageSavePath(bytes);
                if (file == null)
                    return;
                await File.WriteAllBytesAsync(file, bytes);
                file = ImageAction.ConvertImagePath(file);
                ReplaceImage(new(file, ImageAlt, ImageTitle));
                if (UriHelper.TryGetLocalPath(ImageSrc, out var path))
                {
                    path = ViewModel.GetImageAbsolutePath(path);
                    File.Delete(path);
                }
            }
            catch
            {

            }
        }

        private async void OnUploadImageClick(ImageUploadConfig config)
        {
            try
            {
                if (UriHelper.TryGetLocalPath(ImageSrc, out var path))
                {
                    path = ViewModel.GetImageAbsolutePath(path);
                    var uri = await ImageUpload.Upload(config, path);
                    ReplaceImage(new(uri, ImageAlt, ImageTitle));
                }
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetDialogString("UploadFailedTitle"), ex.Message, "Ok").ShowAsync(ViewModel.XamlRoot);
            }
        }

        private void OnImageUploadSettingsClick(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigateCommand.Execute("Settings/Image");
        }

        private async void OnSaveImageClick(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] bytes = await GetImageBytes();
                if (bytes == null)
                    return;
                var file = await PickImageSavePath(bytes);
                if (file == null)
                    return;
                await File.WriteAllBytesAsync(file, bytes);
            }
            catch
            {
                // Ignore
            }
        }

        private void OnDeleteImageFileClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UriHelper.TryGetLocalPath(ImageSrc, out var path))
                {
                    path = ViewModel.GetImageAbsolutePath(path);
                    if (FileOperation.Delete(new StringCollection() { path }))
                    {
                        ViewModel.EditorViewModel.DeleteSelectionCommand.Execute(null);
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        private void ReplaceImage(HtmlImgTag htmlImgTag)
        {
            ViewModel.MarkdownEditor.PostMessage("ReplaceImage", new
            {
                htmlImgTag.Src,
                htmlImgTag.Alt,
                htmlImgTag.Title,
                IsReplaceSelected = true
            });
        }

        private async Task<string> PickImageSavePath(byte[] bytes)
        {
            var filePicker = new FileSavePicker();
            var type = ImageAction.GetImageType(bytes, "png");
            filePicker.FileTypeChoices.Add(type, new List<string>() { $".{type}" });
            filePicker.SuggestedFileName = ImageAlt;
            filePicker.SetOwnerWindow(ViewModel.MainWindow);
            var file = await filePicker.PickSaveFileAsync();
            return file?.Path;
        }

        private async Task<byte[]> GetImageBytes()
        {
            if (UriHelper.IsWebUrl(ImageSrc))
            {
                return await ImageAction.GetWebImage(new(ImageSrc));
            }
            else if (UriHelper.TryGetLocalPath(ImageSrc, out var path))
            {
                path = ViewModel.GetImageAbsolutePath(path);
                return await File.ReadAllBytesAsync(path);
            }
            return null;
        }
    }
}
