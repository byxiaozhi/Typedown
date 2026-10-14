using System.Threading.Tasks;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.Controls.DialogControls
{
    /// <summary>Shows the links of a note just shared to HedgeDoc with copy / open-in-browser actions.</summary>
    public static class HedgeDocShareDialog
    {
        public static async Task ShowAsync(XamlRoot xamlRoot, HedgeDocShareResult result, string note)
        {
            var panel = new StackPanel { Spacing = 8, MinWidth = 420 };
            if (!string.IsNullOrEmpty(note))
                panel.Children.Add(new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap });
            if (result.PublishedUrl != null)
                AddLink(panel, Locale.GetDialogString("HedgeDocShare.ReadOnlyLink"), result.PublishedUrl);
            AddLink(panel, Locale.GetDialogString("HedgeDocShare.EditLink"), result.NoteUrl);
            if (result.PublishedUrl == null)
                panel.Children.Add(new TextBlock { Text = Locale.GetDialogString("HedgeDocShare.EditableWarning"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });

            var dialog = AppContentDialog.Create(
                Locale.GetDialogString("HedgeDocShare.ResultTitle"),
                panel,
                Locale.GetString("Close"),
                Locale.GetDialogString("HedgeDocShare.CopyLink"),
                Locale.GetDialogString("HedgeDocShare.OpenInBrowser"));
            var choice = await dialog.ShowAsync(xamlRoot);
            if (choice == ContentDialogResult.Primary)
            {
                var package = new DataPackage();
                package.SetText(result.ShareUrl);
                Clipboard.SetContent(package);
            }
            else if (choice == ContentDialogResult.Secondary)
            {
                Common.OpenUrl(result.ShareUrl);
            }
        }

        private static void AddLink(StackPanel panel, string label, string url)
        {
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(new TextBox { Text = url, IsReadOnly = true, TextWrapping = TextWrapping.Wrap });
        }
    }
}
