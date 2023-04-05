using System.Threading.Tasks;
using Typedown.Core.Utilities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls
{
    public sealed partial class InsertTableDialog : UserControl
    {
        public InsertTableDialog()
        {
            InitializeComponent();
        }

        public class Result
        {
            public int Rows { get; set; }

            public int Columns { get; set; }
        }

        public static async Task<Result> OpenInsertTableDialog(XamlRoot xamlRoot)
        {
            var (dialog, content) = CreateContentDialog(Locale.GetDialogString("InsertTableTitle"));
            var result = await dialog.ShowAsync(xamlRoot);
            if (result == ContentDialogResult.Primary)
                return new() { Rows = (int)content.rows.Value, Columns = (int)content.columns.Value };
            return null;
        }

        public static async Task<Result> OpenResizeTableDialog(XamlRoot xamlRoot)
        {
            var (dialog, content) = CreateContentDialog(Locale.GetDialogString("ResizeTableTitle"));
            var result = await dialog.ShowAsync(xamlRoot);
            if (result == ContentDialogResult.Primary)
                return new() { Rows = (int)content.rows.Value, Columns = (int)content.columns.Value };
            return null;
        }

        private static (AppContentDialog, InsertTableDialog) CreateContentDialog(string title)
        {
            var content = new InsertTableDialog();
            var dialog = AppContentDialog.Create(title, content, Locale.GetDialogString("Cancel"), Locale.GetDialogString("Ok"));
            return (dialog, content);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
