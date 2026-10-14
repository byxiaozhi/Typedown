using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using Newtonsoft.Json.Linq;

/// <summary>
/// RD02: a menu entry disabled while its menu is closed is drawn disabled in the window's theme: in reading mode the View
/// menu's Focus mode and Typewriter mode, Edit > Undo once undone and the editor's Cut once nothing is selected, their text fainter than the entries that work, in
/// Dark and in Light, whether the menu had been opened before or not. A reader saw them in white on a light menu.
/// </summary>
internal static partial class Program
{
    private static async Task RD02(List<string> notes)
    {
        using var c = await Session("e2e RD02");
        var theme = (await c.Call("settings.get", new { keys = new[] { "appearance.theme" } }))["values"]!["appearance.theme"]!;
        var failures = new List<string>();
        try
        {
            foreach (var themeId in new[] { "dark", "light" })
            {
                await SetSetting(c, "appearance.theme", JObject.FromObject(new { kind = "builtIn", id = themeId }));
                // Reading mode put on by the automation API before the View menu was ever opened and after, and by the View
                // menu's own entry.
                foreach (var way in new[] { "api before the menu was first opened", "api after the menu was opened", "View > Reading mode" })
                {
                    var step = $"{themeId}, reading mode by {way}";
                    // A window of its own: its View menu not opened yet.
                    var windowId = (string)(await c.Call("test.window.open"))["windowId"]!;
                    var window = await WindowOf(windowId, c);
                    try
                    {
                        await Task.Delay(1500);
                        if (way.StartsWith("api after")) await ViewMenuContrast(window);
                        if (way.StartsWith("api")) await c.Call("window.setView", new { windowId, mode = "reading" });
                        else await ClickViewMenuEntry(window, "ReadOnlyModeItem");
                        await Task.Delay(1000);
                        var mode = (string?)(await c.Call("window.getView", new { windowId }))["mode"];
                        if (mode != "reading") failures.Add($"{step}: the window is in {mode} mode, not reading");
                        var (focus, typewriter, reading, enabled) = await ViewMenuContrast(window);
                        notes.Add($"{step}: text contrast - Focus mode {focus:0} (enabled {enabled["FocusModeItem"]}), Typewriter mode {typewriter:0} (enabled {enabled["TypewriterModeItem"]}), Reading mode {reading:0}");
                        if (enabled["FocusModeItem"] || enabled["TypewriterModeItem"]) failures.Add($"{step}: Focus or Typewriter mode is not disabled");
                        // Faint, but there: a disabled entry's text is lighter than the menu where an entry that works is
                        // lighter (Dark), darker where it is darker (Light), and stands out a quarter to three quarters
                        // as much. Its colour taken from the other theme drew it white on a light menu, or next to
                        // invisible on a dark one.
                        foreach (var (name, value) in new[] { ("Focus mode", focus), ("Typewriter mode", typewriter) })
                            if (Math.Sign(value) != Math.Sign(reading) || Math.Abs(value) > Math.Abs(reading) * 0.75 || Math.Abs(value) < Math.Abs(reading) * 0.25)
                                failures.Add($"{step}: {name} is drawn {value:0} from the menu's background, where an entry that works is {reading:0}");
                    }
                    finally
                    {
                        await c.Call("window.setView", new { windowId, mode = "visual" });
                        PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        await Task.Delay(1000);
                    }
                }
                // The editor's context menu as well: Cut and Copy disabled once the selection is gone, after the menu
                // was shown with them enabled.
                {
                    var step = $"{themeId}, Cut disabled with the context menu closed";
                    var docId = await Open(c, Fixture("rd02.md", "# RD02\n\nSome text to select.\n"));
                    var windowId = await WindowIdOf(c, docId);
                    var window = await WindowOf(windowId, c);
                    await ClickEditorText(c, windowId, window, "Some text");
                    Keys(("ctrl", (ushort)0x41));
                    await Task.Delay(600);
                    var before = await ContextMenuAt(c, windowId, window, "#ag-editor-id p");
                    var wasEnabled = before.TryGetValue("CutItem", out var cutBefore) && cutBefore.Current.IsEnabled;
                    Send(Key(0x1B, false), Key(0x1B, true));
                    await Task.Delay(600);
                    await ClickEditorText(c, windowId, window, "Some text");
                    await Task.Delay(600);
                    var after = await ContextMenuAt(c, windowId, window, "#ag-editor-id p");
                    try
                    {
                        if (!after.TryGetValue("CutItem", out var cut) || !after.TryGetValue("SelectAllItem", out var all)) failures.Add($"{step}: no Cut or Select all in the context menu");
                        else
                        {
                            double cutText = TextContrast(cut.Current.BoundingRectangle), working = TextContrast(all.Current.BoundingRectangle);
                            notes.Add($"{step}: Cut {(wasEnabled ? "enabled" : "disabled")} with the text selected, {(cut.Current.IsEnabled ? "enabled" : "disabled")} {cutText:0} without; Select all {working:0}");
                            if (!wasEnabled || cut.Current.IsEnabled) failures.Add($"{step}: Cut was not enabled with a selection and disabled without");
                            else if (Math.Sign(cutText) != Math.Sign(working) || Math.Abs(cutText) > Math.Abs(working) * 0.75 || Math.Abs(cutText) < Math.Abs(working) * 0.25)
                                failures.Add($"{step}: Cut is drawn {cutText:0} from the menu's background, where an entry that works is {working:0}");
                        }
                    }
                    finally
                    {
                        Send(Key(0x1B, false), Key(0x1B, true));
                        await Task.Delay(600);
                    }
                }
                // Not only those two: Edit > Undo, enabled through a binding, undone with its menu closed after the
                // menu was opened.
                {
                    var step = $"{themeId}, Undo disabled with the Edit menu closed";
                    var windowId = (string)(await c.Call("test.window.open"))["windowId"]!;
                    var window = await WindowOf(windowId, c);
                    try
                    {
                        await Task.Delay(1500);
                        await Activate(window);
                        TypeChar('Q');
                        await Task.Delay(800);
                        var (before, wasEnabled) = await MenuContrast(window, "UndoItem", "SelectAllItem");
                        // Undone through the API: a key could find the focus still on the menu bar the menu was closed into.
                        var doc = (string)((JArray)(await c.Call("document.list", new { windowId }))["documents"]!)[0]["documentId"]!;
                        for (var i = 0; i < 5 && ((string?)(await Get(c, doc))["text"] ?? "").Contains('Q'); i++)
                            await c.Call("document.undo", new { documentId = doc, baseRevision = await Revision(c, doc), reveal = "document" });
                        await Task.Delay(1000);
                        var (after, enabled) = await MenuContrast(window, "UndoItem", "SelectAllItem");
                        double undo = after["UndoItem"], working = after["SelectAllItem"];
                        notes.Add($"{step}: Undo {(wasEnabled["UndoItem"] ? "enabled" : "disabled")} {before["UndoItem"]:0} after typing, {(enabled["UndoItem"] ? "enabled" : "disabled")} {undo:0} after undoing; Select all {working:0}");
                        if (!wasEnabled["UndoItem"] || enabled["UndoItem"]) failures.Add($"{step}: Undo was not enabled after typing and disabled once undone");
                        else if (Math.Sign(undo) != Math.Sign(working) || Math.Abs(undo) > Math.Abs(working) * 0.75 || Math.Abs(undo) < Math.Abs(working) * 0.25)
                            failures.Add($"{step}: Undo is drawn {undo:0} from the menu's background, where an entry that works is {working:0}");
                    }
                    finally
                    {
                        PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        await Task.Delay(1000);
                    }
                }
            }
        }
        finally
        {
            await SetSetting(c, "appearance.theme", theme);
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }

    // The View menu opened, and how far each entry's text stands out from the menu's background (the largest difference
    // in brightness over the left of the entry, where its text is, signed: lighter is positive), with whether UI Automation reports it enabled.
    private static async Task<(double Focus, double Typewriter, double Reading, Dictionary<string, bool> Enabled)> ViewMenuContrast(IntPtr window)
    {
        var (contrast, enabled) = await MenuContrast(window, "FocusModeItem", "TypewriterModeItem", "ReadOnlyModeItem");
        return (contrast["FocusModeItem"], contrast["TypewriterModeItem"], contrast["ReadOnlyModeItem"], enabled);
    }

    // The menu holding these entries opened, and how far each one's text stands out from the menu's background, with
    // whether UI Automation reports it enabled.
    private static async Task<(Dictionary<string, double> Contrast, Dictionary<string, bool> Enabled)> MenuContrast(IntPtr window, params string[] ids)
    {
        await Activate(window);
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        var root = AutomationElement.FromHandle(window);
        var bar = Deep.First(root, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuBar),
            new PropertyCondition(AutomationElement.FrameworkIdProperty, "XAML"))) ?? throw new CaseFailed("no menu bar in the window");
        foreach (AutomationElement top in bar.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)))
        {
            if (!top.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) continue;
            ((ExpandCollapsePattern)expand).Expand();
            await Task.Delay(600);
            try
            {
                var items = ids.Select(id => (id, element: Deep.First(root, new PropertyCondition(AutomationElement.AutomationIdProperty, id)))).ToList();
                if (items.Any(i => i.element == null)) continue;
                return (items.ToDictionary(i => i.id, i => TextContrast(i.element!.Current.BoundingRectangle)),
                    items.ToDictionary(i => i.id, i => i.element!.Current.IsEnabled));
            }
            finally
            {
                try { ((ExpandCollapsePattern)expand).Collapse(); } catch (Exception) { }
                await Task.Delay(300);
            }
        }
        throw new CaseFailed("no menu with " + string.Join(", ", ids));
    }

    // A real click on an entry of the View menu, as a person picks it.
    private static async Task ClickViewMenuEntry(IntPtr window, string automationId)
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
            await Task.Delay(600);
            var item = Deep.First(root, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            if (item == null) { try { ((ExpandCollapsePattern)expand).Collapse(); } catch (Exception) { } await Task.Delay(300); continue; }
            await ClickInPopup(window, item);
            return;
        }
        throw new CaseFailed($"no menu entry {automationId}");
    }

    private static double TextContrast(System.Windows.Rect rect)
    {
        // The left half of the entry: its check mark and text, not the shortcut at the right (always drawn faint).
        int x = (int)rect.Left, y = (int)rect.Top, w = Math.Max(1, (int)(rect.Width / 2)), h = Math.Max(1, (int)rect.Height);
        using var b = new System.Drawing.Bitmap(w, h);
        using (var g = System.Drawing.Graphics.FromImage(b)) g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
        var lum = new List<double>(w * h);
        for (var i = 0; i < w; i++)
            for (var j = 0; j < h; j++)
            {
                var k = b.GetPixel(i, j);
                lum.Add(0.2126 * k.R + 0.7152 * k.G + 0.0722 * k.B);
            }
        lum.Sort();
        var background = lum[lum.Count / 2];
        // Signed: the text lighter than the background (positive) or darker (negative), whichever stands out more.
        double lighter = lum[lum.Count - 1] - background, darker = lum[0] - background;
        return lighter >= -darker ? lighter : darker;
    }
}
