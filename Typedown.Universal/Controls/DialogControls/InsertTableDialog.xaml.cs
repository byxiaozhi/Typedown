using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Typedown.Universal.Controls
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
            var (dialog, content) = CreateContentDialog(Localize.GetDialogString("InsertTableTitle"));
            var result = await dialog.ShowAsync(xamlRoot);
            if (result == ContentDialogResult.Primary)
                return new() { Rows = (int)content.rows.Value, Columns = (int)content.columns.Value };
            return null;
        }

        public static async Task<Result> OpenResizeTableDialog(XamlRoot xamlRoot)
        {
            var (dialog, content) = CreateContentDialog(Localize.GetDialogString("ResizeTableTitle"));
            var result = await dialog.ShowAsync(xamlRoot);
            if (result == ContentDialogResult.Primary)
                return new() { Rows = (int)content.rows.Value, Columns = (int)content.columns.Value };
            return null;
        }

        private static (AppContentDialog, InsertTableDialog) CreateContentDialog(string title)
        {
            var content = new InsertTableDialog();
            var dialog = AppContentDialog.Create(title, content, Localize.GetDialogString("Cancel"), Localize.GetDialogString("Confirm"));
            return (dialog, content);
        }
    }
}
