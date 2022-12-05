using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Core.Controls.DialogControls
{
    public sealed partial class AddUploadConfigDialog : AppContentDialog
    {
        public static readonly DependencyProperty ConfigNameProperty = DependencyProperty.Register(nameof(ConfigName), typeof(string), typeof(AddUploadConfigDialog), new(""));
        public string ConfigName { get => (string)GetValue(ConfigNameProperty); set => SetValue(ConfigNameProperty, value); }

        public static readonly DependencyProperty UploadMethodProperty = DependencyProperty.Register(nameof(UploadMethod), typeof(ImageUploadMethod), typeof(AddUploadConfigDialog), new(Enums.Enumerable.AvailableImageUploadMethods.First()));
        public ImageUploadMethod UploadMethod { get => (ImageUploadMethod)GetValue(UploadMethodProperty); set => SetValue(UploadMethodProperty, value); }

        public static readonly DependencyProperty ErrMsgProperty = DependencyProperty.Register(nameof(ErrMsg), typeof(string), typeof(AddUploadConfigDialog), new(""));
        public string ErrMsg { get => (string)GetValue(ErrMsgProperty); set => SetValue(ErrMsgProperty, value); }

        public AddUploadConfigDialog()
        {
            this.InitializeComponent();
        }

        public class Result
        {
            public string ConfigName { get; set; }

            public ImageUploadMethod UploadMethod { get; set; }
        }

        public static async Task<Result> OpenAddUploadConfigDialog(XamlRoot xamlRoot)
        {
            var dialog = new AddUploadConfigDialog() { XamlRoot = xamlRoot, };
            dialog.PrimaryButtonClick += (s, e) =>
            {
                if (string.IsNullOrEmpty(dialog.ConfigName))
                {
                    e.Cancel = true;
                    dialog.ErrMsg = "名称不能为空";
                }
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return new Result() { ConfigName = dialog.ConfigName, UploadMethod = dialog.UploadMethod };
            return null;
        }
    }
}
