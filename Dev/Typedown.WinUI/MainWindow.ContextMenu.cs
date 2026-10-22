using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace Typedown.WinUI
{
    // New since the fork: the right-click menu of the editor (Cut, Copy, Paste, Select all).
    //
    // The editor page suppresses the browser's own context menu (Typedown.Editor/src/App.tsx), and the
    // original app's replacement was never ported, so a right-click did nothing. The page (HostShortcutScript)
    // now reports the click and what is selected; this shows a menu at that spot whose items do exactly what
    // the Edit menu and the keyboard do: Copy and Paste go through the editor's own clipboard handling
    // (markdown text + HTML out, markdown-aware paste in), Cut is the browser's own cut (the editor's Cut
    // leaves a removed paragraph on screen, see CopyMenuItem_Click). In the Code and Split source pane
    // (CodeMirror) the same four commands work on its plain text.
    public sealed partial class MainWindow
    {
        /// <summary>
        /// Shows the editor menu at the reported pointer position, using the selection, spelling,
        /// review and source-pane state to choose commands, including access to the Speech ring.
        /// </summary>
        private void ShowEditorContextMenu(JToken args)
        {
            if (startPageShown || SettingsPageShown || ConvertPage.Visibility == Visibility.Visible) return;
            var x = args["x"]?.ToObject<double>() ?? 0;
            var y = args["y"]?.ToObject<double>() ?? 0;
            var hasSelection = args["hasSelection"]?.ToObject<bool>() ?? false;
            var inCode = args["code"]?.ToObject<bool>() ?? false;

            var menu = new MenuFlyout();
            // On a misspelled word: suggestions, Ignore all and Add to dictionary come first, as in Word.
            var misspelled = args["spell"]?.ToString();
            if (!string.IsNullOrEmpty(misspelled)) AddSpellingItems(menu, misspelled, inCode);
            // On a question or an exclamation that closes without opening (Spanish): where the mark could go (MainWindow.Punctuation.cs).
            var punctuation = args["punct"]?.ToString();
            if (!string.IsNullOrEmpty(punctuation)) AddPunctuationItems(menu, punctuation);
            // On a change or a comment: accept, reject or delete it (MainWindow.Review.cs).
            var marks = args["review"]?.ToString();
            if (!string.IsNullOrEmpty(marks)) AddReviewItems(menu, marks);
            MenuFlyoutItem Item(string text, string glyph, string accelerator, bool enabled, Action action)
            {
                var item = new MenuFlyoutItem
                {
                    Text = text,
                    Icon = new FontIcon { Glyph = glyph },
                    KeyboardAcceleratorTextOverride = accelerator,
                    IsEnabled = enabled,
                };
                item.Click += (s, e) => action();
                menu.Items.Add(item);
                return item;
            }
            Item(Locale.GetString("Cut"), "", "Ctrl+X", hasSelection, () => _ = EditorCut(inCode));
            Item(Locale.GetString("CopyMenuItem_Text"), "", "Ctrl+C", hasSelection, () => _ = EditorCopy(inCode));
            Item(Locale.GetString("CopyAsWhatsApp"), "", "", true, () => _ = CopyAsWhatsApp());
            Item(Locale.GetString("PasteMenuItem_Text"), "", "Ctrl+V", true, () => _ = EditorPaste(inCode));
            menu.Items.Add(new MenuFlyoutSeparator());
            Item(Locale.GetString("SelectAllMenuItem_Text"), "", "Ctrl+A", true, () => _ = EditorSelectAll(inCode));
            menu.Items.Add(new MenuFlyoutSeparator());
            // A comment on the selection (or at the caret): see MainWindow.Review.cs.
            Item(Locale.GetString("ReviewAddComment"), "\uE90A", "", true, () => _ = AddReviewComment(inCode));
            // Where the ring did not open by itself (a misspelled word, a change of the review, a link, an image, a table):
            // the way to it. Not when the ring's own middle button asked for this menu.
            if (settings.SpeechMode && !inCode && args["fromRing"]?.ToObject<bool>() != true)
                Item(Locale.GetString("SpeechRingMenuItem"), "\uE8BD", "", true, () => _ = OpenSpeechRing(x, y));
            menu.ShowAt(EditorView, new FlyoutShowOptions { Position = new Windows.Foundation.Point(x, y) });
        }

        // The source pane's CodeMirror, as the page's script sees it.
        private const string CodePane = "(function(){var e=document.querySelector('.CodeMirror');return e&&e.CodeMirror;})()";

        private async Task<string> RunInPage(string script)
        {
            var view = EditorView.CoreWebView2;
            return view == null ? null : await view.ExecuteScriptAsync(script);
        }

        // Edit > Cut: the same cut as the right-click menu, in whichever pane has the focus.
        private async void CutMenuItem_Click(object sender, RoutedEventArgs e) =>
            await EditorCut(await RunInPage("!!(document.activeElement&&document.activeElement.closest&&document.activeElement.closest('.CodeMirror'))") == "true");

        // False when nothing reached the clipboard (Cut in the source pane must then leave the text alone).
        private async Task<bool> EditorCopy(bool inCode)
        {
            try
            {
                if (!inCode)
                {
                    EditorView.Focus(FocusState.Programmatic);
                    PostMessage("Copy", new { type = "normal" });
                    return true;
                }
                var selected = JsonConvert.DeserializeObject<string>(await RunInPage($"(function(){{var c={CodePane};return c?c.getSelection():'';}})()"));
                if (string.IsNullOrEmpty(selected)) return false;
                var package = new DataPackage();
                package.SetText(selected);
                Clipboard.SetContent(package);
                // Stays on the clipboard after Caret closes (as SetClipboardFromEditor does); the text is
                // already there if this can't be done.
                try { Clipboard.Flush(); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Log($"ContextMenu: copy failed: {ex.Message}");
                return false;
            }
        }

        private async Task EditorCut(bool inCode)
        {
            try
            {
                if (inCode)
                {
                    // Only once the text is on the clipboard: a failed copy must not lose it.
                    if (await EditorCopy(true))
                        await RunInPage($"(function(){{var c={CodePane};if(c)c.replaceSelection('');}})()");
                    return;
                }
                // The browser's own cut, as Ctrl+X does, sent as the editing command it stands for: a script's
                // document.execCommand('cut') needs a user gesture, a menu click in the host isn't one.
                EditorView.Focus(FocusState.Programmatic);
                await EditorView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                    "{\"type\":\"rawKeyDown\",\"modifiers\":2,\"windowsVirtualKeyCode\":88,\"key\":\"x\",\"code\":\"KeyX\",\"commands\":[\"cut\"]}");
                await EditorView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                    "{\"type\":\"keyUp\",\"modifiers\":2,\"windowsVirtualKeyCode\":88,\"key\":\"x\",\"code\":\"KeyX\"}");
            }
            catch (Exception ex)
            {
                Log($"ContextMenu: cut failed: {ex.Message}");
            }
        }

        private async Task EditorPaste(bool inCode)
        {
            try
            {
                if (!inCode)
                {
                    EditorView.Focus(FocusState.Programmatic);
                    PasteFromClipboard();
                    return;
                }
                var view = await GetClipboardContent();
                if (!Offers(view, StandardDataFormats.Text)) return;
                var text = await view.GetTextAsync();
                await RunInPage($"(function(){{var c={CodePane};if(c)c.replaceSelection({JsonConvert.SerializeObject(text)});}})()");
            }
            catch (Exception ex)
            {
                Log($"ContextMenu: paste failed: {ex.Message}");
            }
        }

        private async Task EditorSelectAll(bool inCode)
        {
            try
            {
                EditorView.Focus(FocusState.Programmatic);
                if (inCode) await RunInPage($"(function(){{var c={CodePane};if(c){{c.focus();c.execCommand('selectAll');}}}})()");
                else PostMessage("SelectAll", null);
            }
            catch (Exception ex)
            {
                Log($"ContextMenu: select all failed: {ex.Message}");
            }
        }
    }
}
