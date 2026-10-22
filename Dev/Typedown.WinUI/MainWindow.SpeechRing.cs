using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: the host's half of the Speech ring (docs/speech-marks-design.md, 5.4), the round menu that a
    // right-click opens in Speech mode. The ring is drawn by the page (Typedown.Editor/src/services/speechRing.ts), over
    // the text, so the selection and the focus are not disturbed; this sends it what to show (the same list as the Speech
    // card, in the same order, from SpeechGroups) and the words in the language of the interface, and answers its two
    // requests: the ordinary menu (the round button in the middle: ContextMenu, see MainWindow.ContextMenu.cs) and "More...",
    // which shows a group of the Speech card in full.
    public sealed partial class MainWindow
    {
        // The petals, clockwise from the top, as the page lays them out.
        private static readonly string[] SpeechRingOrder = { "time", "pace", "volume", "tone", "cue", "mine" };

        /// <summary>
        /// Builds the page's ring catalog with localized labels and shared Speech entries in petal order.
        /// </summary>
        private object SpeechRingCatalog()
        {
            // Looks up a ring label or status message in the current interface language.
            string T(string key) => Locale.GetString(key);
            var groups = SpeechGroups().ToDictionary(g => g.Id, g => g.Entries);
            return new
            {
                labels = new
                {
                    ring = T("SpeechRingLabel"),
                    center = T("SpeechRingCenter"),
                    more = T("SpeechRingMore"),
                    applied = T("SpeechRingApplied"),
                    notApplied = T("SpeechRingNotApplied"),
                    empty = T("SpeechRingEmpty"),
                    hint = T("SpeechRingHint"),
                    time = T("SpeechRingTime"),
                    timeAdds = T("SpeechRingTimeAdds"),
                    timeFromTo = T("SpeechRingTimeFromTo"),
                    nothing = T("SpeechMsgNothing"),
                    unsafeText = T("SpeechMsgUnsafe"),
                    several = T("SpeechMsgSeveral"),
                    unknown = T("SpeechMsgUnknown"),
                    noFocus = T("SpeechMsgNoFocus"),
                },
                groups = SpeechRingOrder.Select(id => new
                {
                    id,
                    label = T(SpeechGroupKey(id)),
                    items = groups[id].Select(e => new
                    {
                        label = e.Label,
                        spec = e.Spec,
                        written = e.Written,
                        meaning = e.Meaning,
                        role = e.Role,
                        kind = e.Kind,
                        style = e.Style,
                        speed = e.Speed,
                        perWord = e.PerWord,
                        color = e.Color,
                        mine = e.Mine,
                        match = e.Match,
                    }),
                }),
            };
        }

        // The list changes with the library, the recipes and the language: the page is given it again each time the card is built.
        private void PushSpeechRing()
        {
            try
            {
                if (EditorView?.CoreWebView2 == null) return;
                PostMessage("SettingsChanged", new Dictionary<string, object> { { "speechRing", SpeechRingCatalog() } });
            }
            catch (Exception ex)
            {
                Log($"Speech: the ring was not sent to the page: {ex.Message}");
            }
        }

        // "Speech marks..." in the ordinary menu (shown where the ring does not open itself: on a misspelled word, a change or a
        // comment of the review, a link, an image, a table): opens the ring at the same place.
        private async Task OpenSpeechRing(double x, double y)
        {
            try
            {
                var at = System.FormattableString.Invariant($"{x},{y}");
                await RunInPage($"window.__caretSpeech&&window.__caretSpeech.openRing?window.__caretSpeech.openRing({at}):null");
            }
            catch (Exception ex)
            {
                Log($"Speech: the ring did not open: {ex.Message}");
            }
        }

        // "More..." of an arc: the group in full, in the Speech card.
        private void ShowSpeechGroup(JToken args)
        {
            try
            {
                var id = args?["group"]?.ToString();
                speechCardCollapsed = false;
                UpdateSpeechCard();
                var header = SpeechBody.Children.OfType<Microsoft.UI.Xaml.Controls.TextBlock>()
                    .FirstOrDefault(b => b.Text == Locale.GetString(SpeechGroupKey(id ?? "")));
                header?.StartBringIntoView();
            }
            catch (Exception ex)
            {
                Log($"Speech: the group was not shown: {ex.Message}");
            }
        }
    }
}
