using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// KY01: Ctrl+Z and Ctrl+Y are the host's undo and redo, however close Ctrl and the letter come. The keyboard hook read
/// Ctrl as the web view's thread had it: a chord sent at once (a fast hand, a busy page, a macro key) was taken for a
/// plain Z, and the page made its own undo (the browser's in the visual editor, CodeMirror's in source mode), apart from
/// the host's history. (The modifiers are now the system's, so a Ctrl let go in another window cannot be still down for
/// the host either; a step pressing Ctrl here and letting it go in a second window left that window with the keyboard and
/// took the next cases' keys with it, so it is not part of this case.)
/// </summary>
internal static partial class Program
{

    private static async Task KY01(List<string> notes)
    {
        using var c = await Session("e2e KY01");
        var id = await Open(c, Fixture("ky01.md", "# KY01\n\nSome text.\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        // What the page's own keydown listeners and its history saw: a key the host took never reaches it.
        const string install = "(() => { window.__ky = []; if (!window.__kyOn) { window.__kyOn = 1; " +
            "document.addEventListener('keydown', e => window.__ky.push('kd:' + e.key), true); " +
            "document.addEventListener('beforeinput', e => { if (e.inputType.startsWith('history')) window.__ky.push('bi:' + e.inputType) }, true); } return 'ok' })()";
        async Task<string> Text() => (string?)(await Get(c, id))["text"] ?? "";
        async Task<string> PageSaw() => (string?)(await c.Call("test.editor.eval", new { windowId, script = "JSON.stringify(window.__ky)" }))["result"] ?? "";
        async Task Ready()
        {
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await Task.Delay(200);
            await c.Call("test.editor.eval", new { windowId, script = install });
        }
        try
        {
            foreach (var (mode, letter) in new[] { ("visual", 'Q'), ("source", 'W') })
            {
                await c.Call("window.setView", new { windowId, mode });
                await Task.Delay(1000);
                await TypeInto(c, id, letter.ToString());
                await WaitForPage(c, id, t => t.Contains(letter), "the keystroke");
                await Task.Delay(500);
                // Ctrl and Z in one go, as a tool sends them.
                await Ready();
                Keys(("ctrl", (ushort)0x5A));
                await Task.Delay(1000);
                var undoSaw = await PageSaw();
                var undone = await Text();
                notes.Add($"{mode}: Ctrl+Z at once - page saw {undoSaw}; text '{undone.Replace("\n", "\\n")}'");
                Check(!undoSaw.Contains("kd:z"), $"{mode}: the host takes Ctrl+Z sent at once (the page saw {undoSaw})");
                Check(!undone.Contains(letter), $"{mode}: and undoes the letter");
                await Ready();
                Keys(("ctrl", (ushort)0x59));
                await Task.Delay(1000);
                var redoSaw = await PageSaw();
                var redone = await Text();
                notes.Add($"{mode}: Ctrl+Y at once - page saw {redoSaw}; text '{redone.Replace("\n", "\\n")}'");
                Check(!redoSaw.Contains("kd:y"), $"{mode}: the host takes Ctrl+Y sent at once (the page saw {redoSaw})");
                Check(redone.Contains(letter), $"{mode}: and its redo brings the letter back, from the same history");
            }
        }
        finally
        {
            try { await c.Call("window.setView", new { windowId, mode = "visual" }); } catch { }
        }
    }
}
