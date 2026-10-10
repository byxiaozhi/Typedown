using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Automation;

/// <summary>
/// PM01: the Paragraph menu's checks show where the caret is, not what was last clicked. Table clicked and its dialog
/// cancelled left Table checked, with no table anywhere; with the caret in a quote or a table, Quote or Table is checked.
/// </summary>
internal static partial class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    private static async Task PM01(List<string> notes)
    {
        using var c = await Session("e2e PM01");
        var id = await Open(c, Fixture("pm01.md", "# PM01\n\nA plain paragraph.\n\n> A quoted line.\n\n| Col | Umn |\n| --- | --- |\n| cell | data |\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        await Activate(window);
        await Task.Delay(1000);
        // The caret into the plain paragraph, with a click as a person puts it there: the menu's commands act on the
        // editor only while it has the focus.
        await ClickEditorText(c, windowId, window, "plain");
        await Task.Delay(800);
        var tableBefore = await MenuToggle(window, "TableItem");
        notes.Add($"Table before: {tableBefore}");
        Check(tableBefore == ToggleState.Off, "Table is not checked with the caret in a plain paragraph");

        // Reading the check opened and closed the menus, which took the focus from the editor: back into it first.
        await ClickEditorText(c, windowId, window, "plain");
        await Task.Delay(500);
        var opened = false;
        for (var attempt = 0; attempt < 2 && !opened; attempt++)
        {
            if (attempt > 0)
            {
                notes.Add($"the insert-table dialog did not open ({lastMenuBarItemSkipped ?? "Table was clicked"}): Table again");
                await ClickEditorText(c, windowId, window, "plain");
                await Task.Delay(500);
            }
            await InvokeMenuBarItem(window, "TableItem", escapeAfter: false);
            for (var i = 0; i < 20 && !opened; i++) { await Task.Delay(150); opened = (bool)(await c.Call("test.dialog.colours", new { windowId }))["open"]!; }
        }
        Check(opened, $"Table opened the insert-table dialog (last try: {lastMenuBarItemSkipped ?? "Table was clicked"})");
        Send(Key(0x1B, false), Key(0x1B, true)); // Esc: the insert-table dialog cancelled
        await Task.Delay(1000);
        var tableAfter = await MenuToggle(window, "TableItem");
        notes.Add($"Table after its dialog was cancelled: {tableAfter}");
        Check(tableAfter == ToggleState.Off, "Table is still not checked after its dialog was cancelled");

        // The checks still show real states: the caret in the quote checks Quote, in the table checks Table.
        await ClickEditorText(c, windowId, window, "quoted");
        await Task.Delay(800);
        var caret = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const n = getSelection().anchorNode; const e = n && (n.nodeType === 1 ? n : n.parentElement); return (e && e.closest('blockquote') ? 'in the quote' : 'not in the quote') + ', focus ' + document.hasFocus() + ', in ' + (e ? e.tagName + '.' + e.className : 'nothing') })()" }))["result"];
        notes.Add($"caret after the click into the quote: {caret}");
        var quote = await MenuToggleEventually(window, "QuoteItem", ToggleState.On);
        notes.Add($"Quote with the caret in the quote: {quote}");
        Check(quote == ToggleState.On, "Quote is checked with the caret in a quote");
        await ClickEditorText(c, windowId, window, "cell");
        await Task.Delay(800);
        var table = await MenuToggleEventually(window, "TableItem", ToggleState.On);
        notes.Add($"Table with the caret in the table: {table}");
        Check(table == ToggleState.On, "Table is checked with the caret in a table");
    }

    // A click on the middle of the editor's paragraph holding the text, as a person clicks it.
    private static async Task ClickEditorText(Client c, string windowId, IntPtr window, string text)
    {
        await Activate(window);
        var at = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const p = [...document.querySelectorAll('#ag-editor-id p, #ag-editor-id h1, #ag-editor-id td, #ag-editor-id span.ag-paragraph')].find(x => x.textContent.includes(" + Newtonsoft.Json.JsonConvert.ToString(text) + ")); if (!p) return null; const r = p.getBoundingClientRect(); return { x: Math.round(r.left + Math.min(40, r.width / 2)), y: Math.round(r.top + r.height / 2) } })()" }))["result"];
        if (at == null || at.Type != Newtonsoft.Json.Linq.JTokenType.Object) throw new CaseFailed($"no paragraph with '{text}' on the page");
        var screen = await c.Call("test.editor.screenPoint", new { windowId, x = (int)at["x"]!, y = (int)at["y"]! });
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        // Moved there as a mouse moves (SendInput), not placed: a click after SetCursorPos sometimes left the caret where
        // it was.
        int x = (int)screen["x"]!, y = (int)screen["y"]!, screenW = GetSystemMetrics(0), screenH = GetSystemMetrics(1);
        foreach (var dx in new[] { -6, -3, 0 })
        {
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = (x + dx) * 65535 / (screenW - 1), dy = y * 65535 / (screenH - 1), dwFlags = 0x0001 | 0x8000 } } });
            await Task.Delay(40);
        }
        await Task.Delay(100);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } });
        await Task.Delay(60);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
    }

    // The check read until it is the one expected or three reads have passed: the editor reports where the caret went a
    // moment after the click.
    private static async Task<ToggleState?> MenuToggleEventually(IntPtr window, string automationId, ToggleState expected)
    {
        ToggleState? state = null;
        for (var i = 0; i < 3; i++)
        {
            state = await MenuToggle(window, automationId);
            if (state == expected) break;
            await Task.Delay(700);
        }
        return state;
    }

    // A menu bar item's check, read with its menu open (each menu opened until the one holding it), then closed.
    private static async Task<ToggleState?> MenuToggle(IntPtr window, string automationId)
    {
        await Activate(window);
        var root = AutomationElement.FromHandle(window);
        var bar = Deep.First(root, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuBar),
            new PropertyCondition(AutomationElement.FrameworkIdProperty, "XAML"))) ?? throw new CaseFailed("no menu bar in the window");
        foreach (AutomationElement top in bar.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)))
        {
            if (!top.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) continue;
            ((ExpandCollapsePattern)expand).Expand();
            await Task.Delay(400);
            var item = Deep.First(root, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            ToggleState? state = null;
            if (item != null && item.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)) state = ((TogglePattern)toggle).Current.ToggleState;
            try { ((ExpandCollapsePattern)expand).Collapse(); } catch (Exception) { }
            await Task.Delay(250);
            if (item != null) return state;
        }
        throw new CaseFailed($"no menu item {automationId}");
    }
}
