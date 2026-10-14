using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// IM01: the showcase's checkerboard (./ path and a title), after View > Source mode and back, is still drawn and not
/// failed when it is clicked. The same file is also a picture inside a link further down; loaded again together after the
/// mode switch, the linked one threw putting itself on the page, which cached the file as failed, and the checkerboard
/// clicked was redrawn failed - a reader saw it after Ctrl+/ there and back.
/// </summary>
internal static partial class Program
{
    private static async Task IM01(List<string> notes)
    {
        using var c = await Session("e2e IM01");
        // The showcase document itself, where the reader saw it (its other pictures are not there: only the checkerboard).
        string showcase;
        using (var stream = typeof(Program).Assembly.GetManifestResourceStream("showcase.md")!)
        using (var reader = new StreamReader(stream)) showcase = reader.ReadToEnd();
        var path = Fixture("im01.md", showcase);
        // Its pictures beside it, as in the showcase's folder.
        var folder = Path.Combine(Path.GetDirectoryName(path)!, "images");
        Directory.CreateDirectory(folder);
        foreach (var name in typeof(Program).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("showcase-images/")))
        {
            using var from = typeof(Program).Assembly.GetManifestResourceStream(name)!;
            using var to = File.Create(Path.Combine(folder, name.Substring("showcase-images/".Length)));
            from.CopyTo(to);
        }
        var id = await Open(c, path);
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        // The checkerboard's own state: its picture, and the source text Muya keeps beside it (shown with the caret in
        // the paragraph; marked ag-image-fail when the picture is taken as failed to load).
        const string probe = "(() => { const box = [...document.querySelectorAll('#ag-editor-id .ag-inline-image')].find(e => /checker\\.jpg/.test(e.outerHTML) && !/checker as a link/.test(e.outerHTML)); if (!box) return 'no picture box'; const img = box.querySelector('img'); const text = box.previousElementSibling && box.previousElementSibling.classList.contains('ag-image-marked-text') ? box.previousElementSibling : document.querySelector('#ag-editor-id .ag-image-marked-text[data-raw*=checker]'); const mark = [...document.querySelectorAll('#ag-editor-id .ag-image-marked-text')].find(e => /checker\\.jpg/.test(e.textContent) && !/checker as a link/.test(e.textContent)); return 'img ' + (img ? (img.complete && img.naturalWidth > 0 ? 'drawn' : 'not drawn') : 'none') + ' | its text ' + (mark ? (mark.classList.contains('ag-image-fail') ? 'MARKED FAILED' : 'ok') + ' ' + JSON.stringify(mark.textContent.slice(0, 40)) : 'none') + ' | box classes ' + box.className })()";
        async Task<string> State(string step)
        {
            var s = (string?)(await c.Call("test.editor.eval", new { windowId, script = probe }))["result"] ?? "";
            notes.Add($"{step}: {s}");
            return s;
        }
        async Task ClickPicture()
        {
            var at = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const img = [...document.querySelectorAll('#ag-editor-id img')].find(i => /checker\\.jpg$/.test(i.getAttribute('src') || '')); if (!img) return null; img.scrollIntoView({ block: 'center' }); const r = img.getBoundingClientRect(); return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) } })()" }))["result"];
            if (at == null || at.Type != JTokenType.Object) throw new CaseFailed("no picture on the page to click");
            await Task.Delay(500);
            at = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const img = [...document.querySelectorAll('#ag-editor-id img')].find(i => /checker\\.jpg$/.test(i.getAttribute('src') || '')); const r = img.getBoundingClientRect(); return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) } })()" }))["result"]!;
            var screen = await c.Call("test.editor.screenPoint", new { windowId, x = (int)at["x"]!, y = (int)at["y"]! });
            await Click(window, (int)screen["x"]!, (int)screen["y"]!);
            await Task.Delay(1000);
        }
        await Task.Delay(2000);
        await State("opened");
        await ClickPicture();
        var first = await State("clicked");
        // Source mode and back with Ctrl+/, as the reader did it (the API's setView did not show it).
        async Task<string> PageMode() => (string?)(await c.Call("test.editor.eval", new { windowId, script = "document.querySelector('.CodeMirror') ? 'source' : 'visual'" }))["result"] ?? "?";
        // View > Source mode and back, clicked as a person does (Ctrl+/ toggles the same setting; sent from here it did
        // not reach the window).
        foreach (var expected in new[] { "source", "visual" })
        {
            await ClickViewMenuEntry(window, "SourceCodeModeItem");
            for (var i = 0; i < 30 && await PageMode() != expected; i++) await Task.Delay(150);
            notes.Add($"View > Source mode: the page shows {await PageMode()} (expected {expected})");
            await Task.Delay(1500);
        }
        await Task.Delay(1000);
        await State("after source mode and back");
        await ClickPicture();
        var after = await State("clicked after source mode");
        // Its box carries the state: ag-image-fail is the "failed to load" the reader saw.
        Check(!first.Contains("MARKED FAILED") && !first.Contains("ag-image-fail") && first.Contains("img drawn"), $"clicked before source mode: drawn, not failed ({first})");
        Check(!after.Contains("MARKED FAILED") && !after.Contains("ag-image-fail") && after.Contains("img drawn"), $"clicked after source mode and back: drawn, not failed ({after})");
    }
}
