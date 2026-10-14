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

        /// <summary>The table's own size to start from: the dialog showed 4 x 3 whatever the table was.</summary>
        public static async Task<Result> OpenResizeTableDialog(XamlRoot xamlRoot, int rows, int columns)
        {
            var (dialog, content) = CreateContentDialog(Locale.GetDialogString("ResizeTableTitle"));
            if (rows > 0) content.rows.Value = rows;
            if (columns > 0) content.columns.Value = columns;
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
