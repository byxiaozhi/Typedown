using System;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: help with the punctuation of Spanish, and the marks that are not on every keyboard.
    //
    // In Spanish a question or exclamation opens with a mark as well as closing with one (¿Cómo estás? ¡Qué bien!). The page
    // (Typedown.Editor/src/services/punctuation.ts, the rules in punctuationCore.js) underlines a sentence that closes with ? or !
    // where nothing opened, as a hint like the spell checker's underline. A right-click on it offers to add the mark, at the start
    // of the sentence or after its last comma; nothing is ever typed unasked, and nothing is guessed (a sentence may well start
    // after a vocative or an "if" clause). Edit > Insert punctuation types ¿ ¡ « » and the dialogue dash at the caret.
    public sealed partial class MainWindow
    {
        // Set before the page's own scripts run, so that the first scan already knows the setting.
        private static string BuildPunctuationScript(bool enabled) => $"window.__caretPunctInitial = {(enabled ? "true" : "false")};";

        private void ApplyPunctuationSetting() =>
            _ = RunInPage($"window.__caretPunct&&window.__caretPunct.enable({(settings.PunctuationHints ? "true" : "false")})");

        // The items for the right-click menu on a question or exclamation without its opening mark. "punct" is "q" or "e", with
        // ",c" when the sentence has a comma where the mark could go as well.
        private void AddPunctuationItems(MenuFlyout menu, string punct)
        {
            var question = punct.StartsWith("q", StringComparison.Ordinal);
            var start = new MenuFlyoutItem { Text = Locale.GetString(question ? "PunctuationAddQuestionStart" : "PunctuationAddExclamationStart"), FontWeight = FontWeights.SemiBold };
            start.Click += (s, e) => _ = ApplyPunctuation("start");
            menu.Items.Add(start);
            if (punct.EndsWith(",c", StringComparison.Ordinal))
            {
                var comma = new MenuFlyoutItem { Text = Locale.GetString(question ? "PunctuationAddQuestionComma" : "PunctuationAddExclamationComma"), FontWeight = FontWeights.SemiBold };
                comma.Click += (s, e) => _ = ApplyPunctuation("comma");
                menu.Items.Add(comma);
            }
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        private async Task ApplyPunctuation(string where)
        {
            try
            {
                EditorView.Focus(FocusState.Programmatic);
                await RunInPage($"window.__caretPunct&&window.__caretPunct.apply({JsonConvert.SerializeObject(where)})");
            }
            catch (Exception ex)
            {
                Log($"Punctuation: add failed: {ex.Message}");
            }
        }

        // Edit > Insert punctuation: the mark of the item (its Tag) goes in at the caret, in the editor or the source pane.
        private void InsertPunctuationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: string mark } && !string.IsNullOrEmpty(mark)) _ = InsertPunctuation(mark);
        }

        private async Task InsertPunctuation(string mark)
        {
            try
            {
                EditorView.Focus(FocusState.Programmatic);
                await RunInPage($"window.__caretPunct&&window.__caretPunct.insert({JsonConvert.SerializeObject(mark)})");
            }
            catch (Exception ex)
            {
                Log($"Punctuation: insert failed: {ex.Message}");
            }
        }
    }
}
