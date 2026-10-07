using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.WinUI.Services;
using Typedown.WinUI.Utilities;

namespace Typedown.WinUI
{
    // New since the fork: reviewing a document. A comment is written into the text in CriticMarkup, with the author
    // and the day inside it (Services/ReviewMarks.cs), and the editor draws it in colour (Typedown.Editor,
    // parser/critic). Nothing is kept outside the document. This is the right-click / Edit menu command "Add
    // comment": it asks for the note and the name, then types the markup over the selection (the page does that,
    // so Undo works as for any typing).
    public sealed partial class MainWindow
    {
        // The command from the Edit menu: in whichever pane has the focus (like Cut).
        private async void AddCommentMenuItem_Click(object sender, RoutedEventArgs e) =>
            await AddReviewComment(await RunInPage("!!(document.activeElement&&document.activeElement.closest&&document.activeElement.closest('.CodeMirror'))") == "true");

        private async Task AddReviewComment(bool inCode)
        {
            try
            {
                // What is selected, as it is in the file (the page remembers where, for after the dialog).
                var answer = await RunInPage($"window.__caretReview?window.__caretReview.capture({(inCode ? "true" : "false")}):null");
                if (string.IsNullOrEmpty(answer) || answer == "null") return;
                var captured = JObject.Parse(JsonConvert.DeserializeObject<string>(answer));
                if (captured["found"]?.ToObject<bool>() != true) return;
                // `text` is what can be marked (empty when the comment can only be placed), `quote` what the user sees selected.
                var selected = captured["text"]?.ToString() ?? "";
                var preview = ReviewMarks.Preview(captured["quote"]?.ToString() ?? selected);
                var note = new TextBox { Header = Locale.GetString("ReviewCommentNote"), TextWrapping = TextWrapping.Wrap, MinHeight = 72 };
                var name = new TextBox { Header = Locale.GetString("ReviewCommentAuthor"), Text = Environment.UserName };
                var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
                if (preview.Length > 0)
                    panel.Children.Add(new TextBlock
                    {
                        Text = "“" + preview + "”",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = 3,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Opacity = 0.75,
                    });
                panel.Children.Add(note);
                panel.Children.Add(name);
                panel.Children.Add(new TextBlock
                {
                    Text = Locale.GetString("ReviewCommentHint"),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.75,
                });
                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("ReviewCommentTitle"),
                    Content = panel,
                    PrimaryButtonText = Locale.GetString("OK"),
                    CloseButtonText = Locale.GetString("Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                    IsPrimaryButtonEnabled = false,
                };
                // A comment needs a note: without one it would only be a highlight.
                note.TextChanged += (s, e) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(note.Text);
                var focused = false;
                dialog.Opened += (s, e) => { if (!focused) { focused = true; note.Focus(FocusState.Programmatic); } };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

                var (markup, wraps) = ReviewMarks.Comment(selected, note.Text, name.Text, DateTime.Now);
                var done = await RunInPage($"window.__caretReview&&window.__caretReview.apply({JsonConvert.SerializeObject(markup)},{(wraps ? "true" : "false")})");
                if (done != "true") Log($"Review: the comment was not inserted ({done})");
                EditorView.Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                Log($"Review: add comment failed: {ex.Message}");
            }
        }

        // Right-click on a change or a comment: accept or reject the change, or delete the comment. The page found the
        // marks under the pointer and says what text they are: `raw` the whole chain (a change or highlight with the notes
        // right after it) and `clicked` the one mark that was clicked.
        private void AddReviewItems(MenuFlyout menu, string found)
        {
            JObject marks;
            try { marks = JObject.Parse(found); }
            catch (JsonException) { return; }
            var raw = marks["raw"]?.ToString() ?? "";
            var clicked = marks["clicked"]?.ToString() ?? "";
            var kind = ReviewMarks.KindOf(raw);
            if (kind == ReviewKind.None) return;
            MenuFlyoutItem Item(string key, string glyph, string text, ReviewAction action, bool onlyClicked)
            {
                var item = new MenuFlyoutItem { Text = Locale.GetString(key), Icon = new FontIcon { Glyph = glyph } };
                item.Click += (s, e) => _ = ResolveMarks(text, action, onlyClicked);
                menu.Items.Add(item);
                return item;
            }
            if (ReviewMarks.IsNote(clicked))
            {
                // A note on a change or a highlight: only that note goes.
                Item("ReviewDeleteComment", "\uE74D", clicked, ReviewAction.DeleteComments, true);
            }
            else if (kind == ReviewKind.Change)
            {
                Item("ReviewAcceptChange", "\uE73E", raw, ReviewAction.Accept, false);
                Item("ReviewRejectChange", "\uE711", raw, ReviewAction.Reject, false);
            }
            else
            {
                // A highlight: it is unwrapped and its notes go.
                Item("ReviewDeleteComment", "\uE74D", raw, ReviewAction.DeleteComments, false);
            }
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        private async Task ResolveMarks(string raw, ReviewAction action, bool onlyClicked)
        {
            try
            {
                var result = ReviewMarks.Resolve(raw, action);
                var done = await RunInPage($"window.__caretReview&&window.__caretReview.resolveApply({JsonConvert.SerializeObject(result)},{(onlyClicked ? "true" : "false")})");
                if (done != "true") Log($"Review: the marks were not replaced ({done})");
                EditorView.Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                Log($"Review: resolving marks failed: {ex.Message}");
            }
        }

        // Edit menu: every change accepted or rejected, or every comment deleted, in the whole document. The new text goes
        // in like Undo's does (SetMarkdown), and is one step in the undo history.
        private async void AcceptAllMenuItem_Click(object sender, RoutedEventArgs e) => await ResolveDocument(ReviewAction.Accept);

        private async void RejectAllMenuItem_Click(object sender, RoutedEventArgs e) => await ResolveDocument(ReviewAction.Reject);

        private async void DeleteAllCommentsMenuItem_Click(object sender, RoutedEventArgs e) => await ResolveDocument(ReviewAction.DeleteComments);

        private async Task ResolveDocument(ReviewAction action)
        {
            try
            {
                await FlushEditor();
                var text = file.Markdown ?? "";
                var result = ReviewMarks.Resolve(text, action);
                if (result == text)
                {
                    await ShowReviewMessage(Locale.GetString(action == ReviewAction.DeleteComments ? "ReviewNoComments" : "ReviewNoChanges"));
                    return;
                }
                file.ReplaceBuffer(result);
                history.ContentChange(result);
                // The cursor where it was (Muya puts it back if the text still has that place, at the start if not).
                PostMessage("SetMarkdown", new { text = result, cursor = activeDoc.Cursor, basePath = file.ImageBasePath });
                Log($"Review: {action} applied to the document");
            }
            catch (Exception ex)
            {
                Log($"Review: {action} failed: {ex.Message}");
            }
        }

        // Edit > Compare with another file: the differences between an earlier version of the document (picked here) and
        // the document on screen, written as a review into a new tab (Services/ReviewDiff.cs). The document on screen is
        // not touched. Who made the changes is asked, because it is not always the one at the keyboard: a colleague's
        // returned file is compared with the one that was sent.
        private async void CompareMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (startPageShown || SettingsPageShown || ConvertPage.Visibility == Visibility.Visible) return;
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                foreach (var extension in FileTypeHelper.Markdown) picker.FileTypeFilter.Add(extension);
                var picked = await picker.PickSingleFileAsync();
                if (picked == null) return;

                var name = new TextBox { Header = Locale.GetString("ReviewCompareAuthor"), Text = Environment.UserName };
                var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
                // With tabs the review opens in a new tab and this document stays; without them it takes this window's place.
                panel.Children.Add(new TextBlock { Text = Locale.Format(TabsEnabled ? "ReviewCompareExplain" : "ReviewCompareExplainNoTabs", picked.Name), TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(name);
                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = Locale.GetString("ReviewCompareTitle"),
                    Content = panel,
                    PrimaryButtonText = Locale.GetString("OK"),
                    CloseButtonText = Locale.GetString("Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

                await FlushEditor();
                var current = file.Markdown ?? "";
                var earlier = (await TextFileEncoding.ReadAsync(picked.Path)).Text;
                var author = name.Text;
                var codeNote = Locale.GetString("ReviewCodeChanged");
                var when = DateTime.Now;
                var result = await Task.Run(() => ReviewDiff.Mark(earlier, current, author, when, codeNote));
                if (result.Changes == 0)
                {
                    await ShowReviewMessage(result.Unmarked == 0 ? Locale.GetString("ReviewCompareSame") : Locale.Format("ReviewCompareUnmarked", result.Unmarked));
                    return;
                }
                // Without tabs this asks about unsaved changes first, and the review then replaces this document.
                if (!await MakeRoomForDocument()) return;
                file.NewFile();
                file.ApplyRecoveredBackup(result.Text);
                UpdateTitle();
                Log($"Review: compared with {picked.Path}: {result.Changes} changes, {result.Unmarked} not marked");
                if (result.Unmarked > 0) await ShowReviewMessage(Locale.Format("ReviewCompareUnmarked", result.Unmarked));
            }
            catch (Exception ex)
            {
                Log($"Review: compare failed: {ex}");
                await ShowErrorDialog(Locale.GetString("ReviewCompareTitle"), Locale.Format("ReviewCompareFailed", ex.Message));
            }
        }

        private async Task ShowReviewMessage(string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = Locale.GetString("OK"),
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }

        // Injected into the editor page. It reads the selection and types the markup over it (or after it).
        private const string ReviewScript = """
            (function () {
                var R = window.__caretReview = {};
                var saved = null;
                var chain = null;

                function elementOf(node) { return node && node.nodeType === 1 ? node : node && node.parentNode; }
                function editable(node) { var e = elementOf(node); return e && e.closest ? e.closest('[contenteditable="true"]') : null; }
                function blockOf(node) { var e = elementOf(node); return e && e.closest ? e.closest('.ag-paragraph') : null; }
                function sourcePane() { var e = document.querySelector('.CodeMirror'); return e && e.CodeMirror; }
                function answer(found, text, quote, where) { return JSON.stringify({ found: found, text: text || '', quote: quote || '', where: where || 'selection' }); }
                function clean(text) { return text.replace(/\u200b/g, ''); }

                // The editor draws its paragraphs again at times (the caret moving into a mark, a click), and what was found before is then
                // gone from the page. So a place is remembered as the paragraph's id and the offsets in its text (the marks are text in the
                // page too, only hidden, so that text is the markdown), and found again when it is needed.
                function contentOf(block) { return block.querySelector('.ag-paragraph-content') || block; }
                function offsetOf(content, node, offset) {
                    var r = document.createRange();
                    r.selectNodeContents(content);
                    r.setEnd(node, offset);
                    return r.toString().length;
                }
                function pointAt(content, offset) {
                    var walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT), seen = 0, last = null;
                    while (walker.nextNode()) {
                        last = walker.currentNode;
                        if (offset <= seen + last.length) return { node: last, offset: offset - seen };
                        seen += last.length;
                    }
                    return last ? { node: last, offset: last.length } : { node: content, offset: 0 };
                }
                function remember(range, block) {
                    var content = contentOf(block);
                    return { id: block.id, start: offsetOf(content, range.startContainer, range.startOffset), end: offsetOf(content, range.endContainer, range.endOffset) };
                }
                function recall(at) {
                    var block = at.id ? document.getElementById(at.id) : null;
                    if (!block) return null;
                    var content = contentOf(block);
                    var from = pointAt(content, at.start), to = pointAt(content, at.end);
                    var range = document.createRange();
                    range.setStart(from.node, from.offset);
                    range.setEnd(to.node, to.offset);
                    return { range: range, content: content };
                }

                // The marks of **bold**, *italic*, ~~del~~ and the like are in the page as text hidden by a class (ag-hide), so a
                // selection that looks like "very important" ends before the hidden closing **. Marking that would leave the
                // ** half inside the comment's marks, so a selection is widened over the hidden marks at its edges: the closing
                // mark right after it (it follows the element of the formatted text), the opening mark right before it.
                // Marks that are showing (the caret is inside their text) are left alone: the user can see them.
                function isMark(node) { return !!node && node.nodeType === 1 && node.classList.contains('ag-remove'); }
                function hiddenMark(node) { return isMark(node) && node.classList.contains('ag-hide'); }
                function formatted(node) { return !!node && node.nodeType === 1 && node.classList.contains('ag-inline-rule'); }
                // The node right after / before a position in the page, or null in the middle of a text.
                function after(node, at) {
                    if (node.nodeType === 3) { if (at < node.length) return null; }
                    else if (at < node.childNodes.length) return node.childNodes[at];
                    for (var n = node; n; n = n.parentNode) if (n.nextSibling) return n.nextSibling;
                    return null;
                }
                function before(node, at) {
                    if (node.nodeType === 3) { if (at > 0) return null; }
                    else if (at > 0) return node.childNodes[at - 1];
                    for (var n = node; n; n = n.parentNode) if (n.previousSibling) return n.previousSibling;
                    return null;
                }
                function widen(range) {
                    var next, prev;
                    while ((next = after(range.endContainer, range.endOffset)) && hiddenMark(next) && formatted(next.previousSibling))
                        range.setEndAfter(next);
                    while ((prev = before(range.startContainer, range.startOffset)) && hiddenMark(prev) && formatted(prev.nextSibling))
                        range.setStartBefore(prev);
                }

                function covers(range, node) {
                    var size = node.nodeType === 3 ? node.length : node.childNodes.length;
                    return range.comparePoint(node, 0) === 0 && range.comparePoint(node, size) === 0;
                }

                // Can the selection be marked `{==like this==}` without cutting any other syntax open? A link, an image, code or
                // math the selection touches can't (the comment then goes at the end of the paragraph); bold, italic and
                // strikethrough can when the selection is inside them or takes them whole with their marks.
                function markable(range, block) {
                    var list = block.querySelectorAll('.ag-inline-rule');
                    for (var i = 0; i < list.length; i++) {
                        var e = list[i];
                        if (e.classList.contains('ag-critic') || e.classList.contains('ag-speech') || !range.intersectsNode(e)) continue;
                        if (e.tagName !== 'STRONG' && e.tagName !== 'EM' && e.tagName !== 'DEL') return false;
                        if (e.contains(range.startContainer) && e.contains(range.endContainer)) continue;
                        var open = e.previousSibling, close = e.nextSibling;
                        if (!(isMark(open) && isMark(close) && covers(range, open) && covers(range, close))) return false;
                    }
                    return true;
                }

                // A selection that touches an existing change or comment is not marked again (marks can't nest): the comment
                // goes after that change, and after the comments already attached to it, so a reply follows what it answers.
                function touchedCritic(range, block) {
                    var found = null, list = block.querySelectorAll('.ag-critic');
                    for (var i = 0; i < list.length; i++)
                        if (range.intersectsNode(list[i])) {
                            var outer = list[i];
                            while (outer.parentNode && outer.parentNode.closest && outer.parentNode.closest('.ag-critic')) outer = outer.parentNode.closest('.ag-critic');
                            found = outer;
                        }
                    return found;
                }
                function closes(node) { return isMark(node) && /\}$/.test(node.textContent); }
                function opensComment(node) { return isMark(node) && node.textContent === '{>>'; }
                // The closing mark of the change or comment that starts at its opening mark (or at one of its parts).
                function endOfToken(part) {
                    var n = part;
                    while (n.nextSibling && !closes(n)) n = n.nextSibling;
                    return n;
                }
                function endOfCritic(part) {
                    var n = endOfToken(part);
                    // the notes attached to it: {>>...<<} right after, each a mark, its text and a mark
                    while (opensComment(n.nextSibling) && n.nextSibling.nextSibling && closes(n.nextSibling.nextSibling.nextSibling))
                        n = n.nextSibling.nextSibling.nextSibling;
                    return n;
                }

                // What the user selected, as it is in the file: the marks are text in the page too (only hidden), so the range's own
                // text is the markdown. `text` is what can be marked (empty when the comment can only be placed), `quote` what the
                // user sees selected. Remembers where it all is for apply, after the dialog.
                R.capture = function (inCode) {
                    saved = null;
                    if (inCode) {
                        var cm = sourcePane();
                        if (!cm) return answer(false);
                        var from = cm.getCursor('from'), to = cm.getCursor('to');
                        saved = { cm: cm, from: from, to: to };
                        var picked = cm.getRange(from, to);
                        return answer(true, picked, picked);
                    }
                    var sel = window.getSelection();
                    if (!sel || sel.rangeCount === 0) return answer(false);
                    var range = sel.getRangeAt(0);
                    if (!editable(range.startContainer) || !editable(range.endContainer)) return answer(false);
                    var seen = clean(sel.toString());
                    var first = blockOf(range.startContainer), last = blockOf(range.endContainer);
                    range = range.cloneRange();
                    // Over more than one paragraph, or nothing selected: the comment goes after the selection.
                    if (!first || first !== last || range.collapsed) {
                        if (!last) return answer(false);
                        var tail = range.cloneRange();
                        tail.collapse(false);
                        saved = remember(tail, last);
                        saved.where = 'after';
                        return answer(true, '', seen, 'after');
                    }
                    widen(range);
                    var critic = touchedCritic(range, first);
                    if (critic) {
                        var spot = document.createRange();
                        spot.setStartAfter(endOfCritic(critic));
                        spot.collapse(true);
                        saved = remember(spot, first);
                        saved.where = 'critic';
                        return answer(true, '', seen, 'critic');
                    }
                    if (!markable(range, first)) {
                        var size = contentOf(first).textContent.length;
                        saved = { id: first.id, start: size, end: size, where: 'end' };
                        return answer(true, '', seen, 'end');
                    }
                    saved = remember(range, first);
                    saved.where = 'selection';
                    return answer(true, clean(range.toString()), seen, 'selection');
                };

                // Types `markup` over the remembered selection (wraps), or where the comment goes. False when the text it was in has gone.
                R.apply = function (markup, wraps) {
                    var at = saved;
                    saved = null;
                    if (!at) return false;
                    if (at.cm) {
                        at.cm.focus();
                        if (wraps) at.cm.replaceRange(markup, at.from, at.to);
                        else at.cm.replaceRange(markup, at.to);
                        return true;
                    }
                    var found = recall(at);
                    if (!found) return false;
                    var target = found.range;
                    if (!wraps || at.where !== 'selection') target.collapse(false);
                    var root = editable(target.startContainer);
                    if (!root) return false;
                    root.focus();
                    var sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(target);
                    return document.execCommand('insertText', false, markup);
                };

                // --- One change accepted or rejected, one comment deleted (the right-click menu) ---
                var OPEN = ['{++', '{--', '{~~', '{==', '{>>'];
                function opens(node) { return isMark(node) && OPEN.indexOf(node.textContent) >= 0; }
                function outerCritic(el) {
                    var found = null;
                    for (var e = el && el.closest ? el.closest('.ag-critic') : null; e; e = e.parentNode && e.parentNode.closest ? e.parentNode.closest('.ag-critic') : null) found = e;
                    return found;
                }

                function textBetween(first, last) {
                    var raw = '';
                    for (var n = first; n; n = n.nextSibling) {
                        raw += n.textContent;
                        if (n === last) break;
                    }
                    return clean(raw);
                }

                function between(first, last, block) {
                    var range = document.createRange();
                    range.setStartBefore(first);
                    range.setEndAfter(last);
                    return remember(range, block);
                }

                // The change or comment under the pointer as the text that is in the file, with the notes right after it (a
                // comment right after another mark belongs to it), and the one mark that is under the pointer:
                // {"raw": ..., "clicked": ...}. Empty when there is none. Remembers where both are.
                R.chainAt = function (x, y) {
                    chain = null;
                    var part = outerCritic(document.elementFromPoint(x, y));
                    if (!part) return '';
                    var open = part;
                    while (open && !opens(open)) open = open.previousSibling;
                    if (!open) return '';
                    var block = blockOf(open);
                    if (!block) return '';
                    var token = { first: open, last: endOfToken(open) };
                    while (open.textContent === '{>>' && open.previousSibling && closes(open.previousSibling)) {
                        var earlier = open.previousSibling;
                        while (earlier && !opens(earlier)) earlier = earlier.previousSibling;
                        if (!earlier) break;
                        open = earlier;
                    }
                    var last = endOfCritic(open);
                    chain = { all: between(open, last, block), token: between(token.first, token.last, block) };
                    return JSON.stringify({ raw: textBetween(open, last), clicked: textBetween(token.first, token.last) });
                };

                // Types `text` over what chainAt found (nothing at all removes it): the whole chain, or only the mark that was
                // clicked. False when the text has gone.
                R.resolveApply = function (text, onlyClicked) {
                    var at = chain;
                    chain = null;
                    if (!at) return false;
                    var span = onlyClicked ? at.token : at.all;
                    var found = recall(span);
                    if (!found) return false;
                    var range = found.range;
                    // Removing a word between two spaces takes one of them too, as the whole-document command does.
                    if (text === '') {
                        var full = found.content.textContent;
                        if (span.start > 0 && full.charAt(span.start - 1) === ' ' && full.charAt(span.end) === ' ') {
                            var stop = pointAt(found.content, span.end + 1);
                            range.setEnd(stop.node, stop.offset);
                        }
                    }
                    var root = editable(range.startContainer);
                    if (!root) return false;
                    root.focus();
                    var sel = window.getSelection();
                    sel.removeAllRanges();
                    sel.addRange(range);
                    return text === '' ? document.execCommand('delete') : document.execCommand('insertText', false, text);
                };
            })();
            """;
    }
}
