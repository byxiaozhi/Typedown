using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Utilities;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Typedown.Core.Controls.DialogControls
{
    public sealed partial class AddUploadConfigDialog : AppContentDialog
    {
        public static readonly StyledProperty ConfigNameProperty = AvaloniaProperty.Register<AddUploadConfigDialog, string>(nameof(ConfigName), new(""));
        public string ConfigName { get => GetValue(ConfigNameProperty); set => SetValue(ConfigNameProperty, value); }

        public static readonly StyledProperty UploadMethodProperty = AvaloniaProperty.Register<AddUploadConfigDialog, ImageUploadMethod>(nameof(UploadMethod), new(Enums.Enumerable.AvailableImageUploadMethods.First()));
        public ImageUploadMethod UploadMethod { get => (ImageUploadMethod)GetValue(UploadMethodProperty); set => SetValue(UploadMethodProperty, value); }

        public static readonly StyledProperty ErrMsgProperty = AvaloniaProperty.Register<AddUploadConfigDialog, string>(nameof(ErrMsg), new(""));
        public string ErrMsg { get => GetValue(ErrMsgProperty); set => SetValue(ErrMsgProperty, value); }

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
                    dialog.ErrMsg = Locale.GetString("NameCannotBeEmpty");
                }
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return new Result() { ConfigName = dialog.ConfigName, UploadMethod = dialog.UploadMethod };
            return null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings?.StopTracking();
        }
    }
}
