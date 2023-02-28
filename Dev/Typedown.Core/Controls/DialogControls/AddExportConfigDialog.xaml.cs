using System.Linq;
using System.Threading.Tasks;
using Typedown.Core.Enums;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.DialogControls
{
    public sealed partial class AddExportConfigDialog : AppContentDialog
    {
        public static readonly DependencyProperty ConfigNameProperty = DependencyProperty.Register(nameof(ConfigName), typeof(string), typeof(AddExportConfigDialog), new(""));
        public string ConfigName { get => (string)GetValue(ConfigNameProperty); set => SetValue(ConfigNameProperty, value); }

        public static readonly DependencyProperty ExportTypeProperty = DependencyProperty.Register(nameof(ExportType), typeof(ExportType), typeof(AddExportConfigDialog), new(Enums.Enumerable.AvailableExportTypes.First()));
        public ExportType ExportType { get => (ExportType)GetValue(ExportTypeProperty); set => SetValue(ExportTypeProperty, value); }

        public static readonly DependencyProperty ErrMsgProperty = DependencyProperty.Register(nameof(ErrMsg), typeof(string), typeof(AddExportConfigDialog), new(""));
        public string ErrMsg { get => (string)GetValue(ErrMsgProperty); set => SetValue(ErrMsgProperty, value); }

        public AddExportConfigDialog()
        {
            this.InitializeComponent();
        }

        public class Result
        {
            public string ConfigName { get; set; }

            public ExportType ExportType { get; set; }
        }

        public static async Task<Result> OpenAddExportConfigDialog(XamlRoot xamlRoot)
        {
            var dialog = new AddExportConfigDialog() { XamlRoot = xamlRoot, };
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
                return new Result() { ConfigName = dialog.ConfigName, ExportType = dialog.ExportType };
            return null;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }
    }
}
