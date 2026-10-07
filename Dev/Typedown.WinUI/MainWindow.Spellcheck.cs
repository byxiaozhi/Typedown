using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: spell checking, with red wavy underlines and suggestions on a right-click, as in Word.
    //
    // The old version only switched on the browser engine's own checker (the `spellcheck` attribute): that
    // underlined with whatever dictionaries the engine had (downloaded on demand, which a locked-down PC can't
    // do), offered no way to correct, and no choice of language. This one uses Windows' own spell checker
    // (Services/SpellChecking.cs): offline, in the languages the user has added to Windows, with their own word
    // list. The page (SpellcheckScript) finds the words in the text the user can edit, asks here which are wrong
    // and underlines them with the CSS custom highlight (no change to the editor's own DOM or content model, so
    // nothing can desync it); a right-click on a wrong word brings the suggestions to the editor's menu
    // (MainWindow.ContextMenu.cs), and choosing one types it over the word, so undo works as for any typing.
    public sealed partial class MainWindow
    {
        private SpellChecking spelling;
        private bool spellingProbed;
        private IReadOnlyList<string> spellLanguages;

        private SpellChecking Spelling()
        {
            if (!spellingProbed)
            {
                spellingProbed = true;
                spelling = SpellChecking.Create();
                Log(spelling == null ? "Spellcheck: Windows has no spell checker" : $"Spellcheck: Windows supports {spelling.SupportedLanguages.Count} languages");
            }
            return spelling;
        }

        // The language chosen in Settings, or by default each of the app's language and the user's Windows languages
        // that has a dictionary.
        private IReadOnlyList<string> SpellLanguages()
        {
            var checker = Spelling();
            if (checker == null) return Array.Empty<string>();
            if (spellLanguages != null) return spellLanguages;
            var preferred = new List<string> { Locale.CurrentLang };
            try { preferred.AddRange(Windows.System.UserProfile.GlobalizationPreferences.Languages); }
            catch (Exception ex) { Log($"Spellcheck: Windows languages unavailable: {ex.Message}"); }
            spellLanguages = checker.Languages(settings.SpellcheckLang, preferred);
            Log($"Spellcheck: checking in {string.Join(", ", spellLanguages)}");
            return spellLanguages;
        }

        // The page asks which of these words are wrong (they are sent once each, a few hundred at a time).
        private void SpellCheckRequest(JToken args)
        {
            var words = args["words"]?.ToObject<string[]>() ?? Array.Empty<string>();
            var id = args["id"]?.ToObject<int>() ?? 0;
            if (words.Length == 0) return;
            var languages = SpellLanguages();
            var checker = Spelling();
            _ = Task.Run(() =>
            {
                HashSet<string> wrong;
                try { wrong = checker == null ? new HashSet<string>() : checker.Misspelled(words, languages); }
                catch (Exception ex)
                {
                    Log($"Spellcheck: check failed: {ex.Message}");
                    wrong = new HashSet<string>();
                }
                var script = $"window.__caretSpell&&window.__caretSpell.result({JsonConvert.SerializeObject(new { id, checkedWords = words, wrong })})";
                DispatcherQueue.TryEnqueue(() => _ = RunInPage(script));
            });
        }

        // The items for the editor's right-click menu on a wrong word: suggestions, then Ignore all and Add to dictionary.
        private void AddSpellingItems(MenuFlyout menu, string word, bool inCode)
        {
            var checker = Spelling();
            var languages = SpellLanguages();
            var suggestions = checker == null ? new List<string>() : checker.Suggest(word, languages).ToList();
            foreach (var suggestion in suggestions)
            {
                var item = new MenuFlyoutItem { Text = suggestion, FontWeight = FontWeights.SemiBold };
                item.Click += (s, e) => _ = ReplaceMisspelling(suggestion);
                menu.Items.Add(item);
            }
            if (suggestions.Count == 0)
                menu.Items.Add(new MenuFlyoutItem { Text = Locale.GetString("SpellcheckNoSuggestions"), IsEnabled = false });
            menu.Items.Add(new MenuFlyoutSeparator());
            var ignore = new MenuFlyoutItem { Text = Locale.GetString("SpellcheckIgnoreAll"), Icon = new FontIcon { Glyph = "" } };
            ignore.Click += (s, e) => { checker?.Ignore(word, languages); _ = ForgetMisspelling(word); };
            menu.Items.Add(ignore);
            var add = new MenuFlyoutItem { Text = Locale.GetString("SpellcheckAddToDictionary"), Icon = new FontIcon { Glyph = "" } };
            add.Click += (s, e) => { checker?.Add(word, languages); _ = ForgetMisspelling(word); };
            menu.Items.Add(add);
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        // Types the suggestion over the word the page selected when the menu opened.
        private async Task ReplaceMisspelling(string text)
        {
            try
            {
                EditorView.Focus(FocusState.Programmatic);
                await RunInPage($"window.__caretSpell&&window.__caretSpell.replace({JsonConvert.SerializeObject(text)})");
            }
            catch (Exception ex)
            {
                Log($"Spellcheck: replace failed: {ex.Message}");
            }
        }

        private async Task ForgetMisspelling(string word)
        {
            try { await RunInPage($"window.__caretSpell&&window.__caretSpell.forget({JsonConvert.SerializeObject(word)})"); }
            catch (Exception ex) { Log($"Spellcheck: forget failed: {ex.Message}"); }
        }

        // Settings: on or off, and the language.
        private void ApplySpellcheckSetting() =>
            _ = RunInPage($"window.__caretSpell&&window.__caretSpell.enable({(settings.SpellcheckEnabled ? "true" : "false")})");

        private void ResetSpellcheckInPage()
        {
            spellLanguages = null;
            _ = RunInPage("window.__caretSpell&&window.__caretSpell.reset()");
        }

        // The language list in Settings: Automatic, then every language Windows has a dictionary for.
        private void LoadSpellcheckLanguageSettings()
        {
            var checker = Spelling();
            SpellcheckLanguageBox.Items.Clear();
            SpellcheckLanguageBox.Items.Add(new ComboBoxItem { Content = Locale.GetString("SpellcheckLanguageAutomatic"), Tag = "" });
            if (checker != null)
            {
                foreach (var tag in checker.SupportedLanguages.OrderBy(t => LanguageName(t), StringComparer.CurrentCultureIgnoreCase))
                    SpellcheckLanguageBox.Items.Add(new ComboBoxItem { Content = LanguageName(tag), Tag = tag });
            }
            var chosen = checker != null && checker.Supports(settings.SpellcheckLang) ? settings.SpellcheckLang : "";
            SpellcheckLanguageBox.SelectedItem = SpellcheckLanguageBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == chosen) ?? SpellcheckLanguageBox.Items[0];
            UpdateSpellcheckLanguageText();
        }

        private static string LanguageName(string tag)
        {
            try { return CultureInfo.GetCultureInfo(tag).DisplayName; }
            catch (CultureNotFoundException) { return tag; }
        }

        // Says what is being checked, or how to get a dictionary when there is none.
        private void UpdateSpellcheckLanguageText()
        {
            var languages = SpellLanguages();
            SpellcheckLanguageCard.Description = languages.Count > 0
                ? Locale.Format("SpellcheckLanguageUsing", string.Join(", ", languages.Select(LanguageName)))
                : Locale.GetString("SpellcheckNoDictionary");
        }

        private void SpellcheckLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSettingsEvents || SpellcheckLanguageBox.SelectedItem is not ComboBoxItem item) return;
            settings.SpellcheckLang = (string)item.Tag ?? "";
            ResetSpellcheckInPage();
            UpdateSpellcheckLanguageText();
        }

        // Runs in the editor page. Words come from the text nodes of whatever the user can edit (the editor's
        // contenteditable and the source pane's CodeMirror), left out where they are not prose (code, math, link
        // addresses, syntax marks, addresses and e-mails written in the text), once each to the host, and the wrong
        // ones are underlined with the CSS custom highlight. The word being typed is not underlined until the caret
        // leaves it.
        private static string BuildSpellcheckScript(bool enabled) => $"window.__caretSpellInitial = {(enabled ? "true" : "false")};\n" + SpellcheckScript;

        private const string SpellcheckScript = """
            (function () {
                if (window.__caretSpell) return;
                var S = window.__caretSpell = { enabled: false };
                var wrong = new Map();       // word -> true when wrong, false when right
                var asked = new Set();       // words sent to the host and not answered yet
                var entries = [];            // the words found by the last scan: { node, start, end, word }
                var hit = null;              // the word a right-click selected
                var seq = 0, invalidThrough = 0, timer = 0;   // answers to requests up to invalidThrough are out of date
                var wordRe = /[\p{L}\p{M}]+(?:['’][\p{L}\p{M}]+)*/gu;
                var skipRe = /(?:https?:\/\/|www\.)\S+|[^\s@]+@[^\s@]+\.[^\s@]+|[A-Za-z]:\\\S+/g;
                var skipTags = { CODE: 1, PRE: 1, KBD: 1, SVG: 1, SCRIPT: 1, STYLE: 1, BUTTON: 1, INPUT: 1, TEXTAREA: 1 };
                var skipClass = /(^|\s)(ag-gray|ag-hide|ag-html-tag|ag-math|ag-math-text|ag-math-render|ag-link-in-bracket|ag-image-src|ag-image-marked-text|ag-emoji-marked-text|ag-reference-label|ag-reference-title|ag-front-matter|cm-formatting|cm-url|cm-comment|cm-tag|cm-atom|cm-attribute|cm-builtin|cm-keyword|cm-def|cm-qualifier|cm-property|cm-string|cm-string-2)(\s|$)/;

                function skipped(node, root) {
                    for (var el = node.parentNode; el && el !== root; el = el.parentNode) {
                        if (el.nodeType !== 1) continue;
                        if (skipTags[el.tagName] && !(el.tagName === 'PRE' && /CodeMirror-line/.test(el.getAttribute('class') || ''))) return true;
                        if (el.getAttribute('spellcheck') === 'false' && !el.hasAttribute('data-caret-spell')) return true;
                        if (el.getAttribute('contenteditable') === 'false') return true;
                        var cls = el.getAttribute('class');
                        if (cls && skipClass.test(cls)) return true;
                    }
                    return false;
                }

                function roots() {
                    return document.querySelectorAll('[contenteditable="true"], .CodeMirror-lines');
                }

                // The engine of the page underlines too (with its own dictionaries): turn that off, ours replaces it.
                function quietEngine() {
                    document.querySelectorAll('[contenteditable="true"], .CodeMirror textarea').forEach(function (el) {
                        if (el.getAttribute('spellcheck') !== 'false') {
                            el.setAttribute('spellcheck', 'false');
                            el.setAttribute('data-caret-spell', '1');
                        }
                    });
                }

                function collect() {
                    entries = [];
                    var budget = 60000;
                    roots().forEach(function (root) {
                        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                        for (var node = walker.nextNode(); node && budget > 0; node = walker.nextNode()) {
                            var text = node.nodeValue;
                            if (!text || text.length < 2 || skipped(node, root)) continue;
                            var skip = [];
                            var m;
                            skipRe.lastIndex = 0;
                            while ((m = skipRe.exec(text))) skip.push([m.index, m.index + m[0].length]);
                            wordRe.lastIndex = 0;
                            while ((m = wordRe.exec(text)) && budget-- > 0) {
                                var start = m.index, end = start + m[0].length;
                                if (m[0].length < 2 || m[0].length > 40) continue;
                                if (/[\d_]/.test(text.charAt(start - 1) || '') || /[\d_]/.test(text.charAt(end) || '')) continue;
                                if (skip.some(function (r) { return start >= r[0] && start < r[1]; })) continue;
                                entries.push({ node: node, start: start, end: end, word: m[0] });
                            }
                        }
                    });
                }

                function typing(entry) {
                    var sel = window.getSelection();
                    if (!sel || !sel.isCollapsed || !sel.anchorNode || sel.anchorNode !== entry.node) return false;
                    return sel.anchorOffset >= entry.start && sel.anchorOffset <= entry.end;
                }

                function paint() {
                    if (!window.CSS || !CSS.highlights || typeof Highlight === 'undefined') return;
                    if (!S.enabled) { CSS.highlights.delete('caret-misspelled'); return; }
                    var ranges = [];
                    entries.forEach(function (e) {
                        if (wrong.get(e.word) !== true || typing(e) || !e.node.isConnected) return;
                        var r = document.createRange();
                        try { r.setStart(e.node, e.start); r.setEnd(e.node, e.end); ranges.push(r); } catch (x) { }
                    });
                    CSS.highlights.set('caret-misspelled', new Highlight(...ranges));
                }

                function scan() {
                    timer = 0;
                    if (!S.enabled) return;
                    quietEngine();
                    collect();
                    var toAsk = [];
                    entries.forEach(function (e) {
                        if (!wrong.has(e.word) && !asked.has(e.word) && toAsk.indexOf(e.word) < 0) toAsk.push(e.word);
                    });
                    for (var i = 0; i < toAsk.length; i += 300) {
                        var chunk = toAsk.slice(i, i + 300);
                        chunk.forEach(function (w) { asked.add(w); });
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'message', name: 'SpellCheck', args: { id: ++seq, words: chunk } }));
                    }
                    paint();
                }

                function schedule() {
                    if (!S.enabled) return;
                    clearTimeout(timer);
                    timer = setTimeout(scan, 250);
                }

                S.result = function (r) {
                    if (r.id <= invalidThrough) return;   // asked before a language change or an Ignore/Add: the answer may be stale
                    (r.checkedWords || []).forEach(function (w) { asked.delete(w); wrong.set(w, false); });
                    (r.wrong || []).forEach(function (w) { wrong.set(w, true); });
                    paint();
                };
                S.enable = function (on) {
                    S.enabled = !!on;
                    if (on) scan(); else paint();
                };
                S.reset = function () {
                    invalidThrough = seq;
                    wrong.clear();
                    asked.clear();
                    scan();
                };
                // Ignore all / Add to dictionary: the word is right now, and an older answer must not say otherwise; the other
                // words still waiting for an answer are asked again.
                S.forget = function (word) {
                    invalidThrough = seq;
                    asked.clear();
                    wrong.set(word, false);
                    scan();
                    paint();
                };

                // The wrong word under a right-click, selected so that a suggestion can be typed over it; null otherwise.
                S.atPoint = function (x, y) {
                    hit = null;
                    if (!S.enabled || !document.caretRangeFromPoint) return null;
                    var at = document.caretRangeFromPoint(x, y);
                    if (!at) return null;
                    collect();
                    var found = null;
                    for (var i = 0; i < entries.length && !found; i++) {
                        var e = entries[i];
                        if (e.node === at.startContainer && at.startOffset >= e.start && at.startOffset <= e.end && wrong.get(e.word) === true) found = e;
                    }
                    if (!found) return null;
                    var box = found.node.parentNode && found.node.parentNode.closest ? found.node.parentNode.closest('.CodeMirror') : null;
                    var cm = box && box.CodeMirror;
                    if (cm) {
                        var pos = cm.coordsChar({ left: x, top: y }, 'window');
                        var span = cm.findWordAt(pos);
                        var text = cm.getRange(span.anchor, span.head);
                        if (text !== found.word) return null;
                        cm.setSelection(span.anchor, span.head);
                        hit = { cm: cm, anchor: span.anchor, head: span.head, word: found.word };
                        return found.word;
                    }
                    var range = document.createRange();
                    range.setStart(found.node, found.start);
                    range.setEnd(found.node, found.end);
                    var sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(range);
                    hit = { node: found.node, start: found.start, end: found.end, word: found.word };
                    return found.word;
                };

                // Types the suggestion over the selected word: as the editor sees typing, so undo works.
                S.replace = function (text) {
                    if (!hit) return;
                    if (hit.cm) {
                        hit.cm.focus();
                        hit.cm.replaceRange(text, hit.anchor, hit.head);
                        hit = null;
                        return;
                    }
                    var node = hit.node;
                    if (!node.isConnected) { hit = null; return; }
                    var root = node.parentNode && node.parentNode.closest ? node.parentNode.closest('[contenteditable="true"]') : null;
                    if (root) root.focus();
                    var range = document.createRange();
                    range.setStart(node, hit.start);
                    range.setEnd(node, hit.end);
                    var sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(range);
                    document.execCommand('insertText', false, text);
                    hit = null;
                };

                document.addEventListener('DOMContentLoaded', function () {
                    var style = document.createElement('style');
                    style.textContent = '::highlight(caret-misspelled){text-decoration:underline wavy #d13438;text-decoration-thickness:1px;text-underline-offset:2px;}';
                    document.head.appendChild(style);
                    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
                    document.addEventListener('selectionchange', schedule);
                    S.enable(window.__caretSpellInitial);
                });
            })();
            """;
    }
}
