using System;
using System.Linq;
using Typedown.Core.Interfaces;
using Typedown.Core.Models;
using Typedown.Core.Pages.SettingPages;
using Typedown.Core.Utilities;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Typedown.Core.Controls.SettingControls.SettingItems.UploadConfigItems
{
    [ContentProperty(Name = nameof(Detail))]
    public sealed partial class CommonConfig : UserControl
    {
        public static DependencyProperty ImageUploadConfigProperty { get; } = DependencyProperty.Register(nameof(ImageUploadConfig), typeof(ImageUploadConfig), typeof(CommonConfig), null);
        public ImageUploadConfig ImageUploadConfig { get => (ImageUploadConfig)GetValue(ImageUploadConfigProperty); set => SetValue(ImageUploadConfigProperty, value); }

        public static DependencyProperty DetailProperty { get; } = DependencyProperty.Register(nameof(Detail), typeof(UIElement), typeof(CommonConfig), null);
        public UIElement Detail { get => (UIElement)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

        public CommonConfig()
        {
            InitializeComponent();
        }

        private void OnDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            this.GetAncestor<UploadConfigPage>()?.DeleteConfigAsync();
        }

        private async void OnTestUploadButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            try
            {
                button.IsEnabled = false;
                var filePicker = new FileOpenPicker();
                FileTypeHelper.Image.ToList().ForEach(filePicker.FileTypeFilter.Add);
                filePicker.SetOwnerWindow(this.GetService<IWindowService>().GetWindow(this));
                var file = await filePicker.PickSingleFileAsync();
                if (file == null)
                    return;
                var res = await ImageUploadConfig.LoadUploadConfig().Upload(this.GetService<IServiceProvider>(), file.Path);
                await AppContentDialog.Create(Locale.GetDialogString("UploadSuccessfulTitle"), res, "Ok").ShowAsync(XamlRoot);
            }
            catch (Exception ex)
            {
                await AppContentDialog.Create(Locale.GetDialogString("UploadFailedTitle"), ex.Message, "Ok").ShowAsync(XamlRoot);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }
    }
}
