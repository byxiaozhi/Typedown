using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace Typedown.WinUI
{
    // Edit > Copy as WhatsApp text (and the same item in the right-click menu): the selection, or the whole note when
    // nothing is selected, put on the clipboard as a message WhatsApp draws as it should (Services/WhatsAppFormat.cs).
    public sealed partial class MainWindow
    {
        private int toastVersion;

        // A short message at the bottom of the window that goes by itself.
        private async void ShowToast(string message, int milliseconds = 5000)
        {
            ToastText.Text = message;
            ToastHost.Visibility = Visibility.Visible;
            var version = ++toastVersion;
            await Task.Delay(milliseconds);
            if (version == toastVersion) ToastHost.Visibility = Visibility.Collapsed;
        }

        private async void CopyAsWhatsAppMenuItem_Click(object sender, RoutedEventArgs e) => await CopyAsWhatsApp();

        private async Task CopyAsWhatsApp()
        {
            if (startPageShown || SettingsPageShown || ConvertPage.Visibility == Visibility.Visible)
            {
                ShowToast(Locale.GetString("WhatsAppCopyFailed"));
                return;
            }
            try
            {
                // The page answers with the Markdown of the selection, or of the document, and which of the two it is.
                EditorView.Focus(FocusState.Programmatic);
                var answer = await RunInPage("window.__caretCopy?window.__caretCopy.markdown():null");
                var data = string.IsNullOrEmpty(answer) || answer == "null" ? null : JObject.Parse(answer);
                var message = WhatsAppFormat.Convert(data?["text"]?.ToString());
                if (message.Length == 0)
                {
                    ShowToast(Locale.GetString("WhatsAppCopyFailed"));
                    return;
                }
                var package = new DataPackage();
                package.SetText(message);
                Clipboard.SetContent(package);
                // Stays on the clipboard after Caret closes; the text is already there if this can't be done.
                try { Clipboard.Flush(); } catch { }
                var count = message.Length.ToString("N0", CultureInfo.CurrentCulture);
                var done = Locale.Format(data["selected"]?.ToObject<bool>() == true ? "WhatsAppCopiedSelection" : "WhatsAppCopiedDocument", count);
                if (message.Length > WhatsAppFormat.MessageLimit)
                    done += " " + Locale.Format("WhatsAppTooLong", WhatsAppFormat.MessageLimit.ToString("N0", CultureInfo.CurrentCulture));
                ShowToast(done, message.Length > WhatsAppFormat.MessageLimit ? 9000 : 5000);
            }
            catch (Exception ex)
            {
                Log($"WhatsApp: the text was not copied: {ex.Message}");
                ShowToast(Locale.GetString("WhatsAppCopyFailed"));
            }
        }
    }
}