using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// DG02: every diagram is drawn once: the showcase document (every kind of block, a flowchart, a sequence diagram,
/// Vega-Lite and Mermaid among them) written into a window just opened, whose page has not loaded the diagram renderers
/// or the sequence diagram's font yet, holds one picture in each diagram block a few seconds later. The sequence
/// library waits for its font the first time and then adds its picture to the block, so the block drawn again in that
/// wait - while the other renderers load - got two: a reader saw the showcase's sequence diagram twice now and then.
/// </summary>
internal static partial class Program
{
    private static async Task DG02(List<string> notes)
    {
        using var c = await Session("e2e DG02");
        string text;
        using (var stream = typeof(Program).Assembly.GetManifestResourceStream("showcase.md")!)
        using (var reader = new System.IO.StreamReader(stream)) text = reader.ReadToEnd();
        var failures = new List<string>();
        for (var round = 1; round <= 3; round++)
        {
            var windowId = (string)(await c.Call("test.window.open"))["windowId"]!;
            var window = await WindowOf(windowId, c);
            try
            {
                // Opened as a file into that window, as a reader opens it (written in one go through the API, the
                // document is refused: the editor rewrites some of its text).
                await Task.Delay(1500);
                await c.Call("document.open", new { path = Fixture($"dg02-{round}.md", text), windowId, reveal = "document" });
                await Task.Delay(6000);
                var counts = (string?)(await c.Call("test.editor.eval", new { windowId, script = "[...document.querySelectorAll('#ag-editor-id .ag-container-preview')].map(p => (p.closest('[data-role]')?.dataset.role || p.parentElement?.className || '?').toString().slice(0, 20) + ':' + [...p.querySelectorAll('svg, img')].filter(e => !e.parentElement.closest('svg')).length).join(',')" }))["result"] ?? "";
                notes.Add($"round {round}: pictures in each diagram block: [{counts}]");
                // The sequence block's pictures, each with its size and how many shapes it holds: ten whole drawings, or
                // one drawing and helpers?
                notes.Add($"round {round}: sequence pictures: " + (string?)(await c.Call("test.editor.eval", new { windowId, script = "(() => { const p = [...document.querySelectorAll('#ag-editor-id .ag-container-preview')].find(p => (p.closest('[data-role]')?.dataset.role || '') === 'SEQUENCE'); if (!p) return 'none'; return [...p.querySelectorAll('svg')].filter(e => !e.parentElement.closest('svg')).map(e => e.tagName + ' ' + Math.round(e.getBoundingClientRect().width) + 'x' + Math.round(e.getBoundingClientRect().height) + ' shapes ' + e.querySelectorAll('rect, path, line, text').length + (getComputedStyle(e).display === 'none' ? ' hidden' : '')).join('; ') })()" }))["result"]);
                if (counts.Length == 0) failures.Add($"round {round}: no diagram block on the page");
                else if (counts.Split(',').Any(n => new[] { "FLOWCHART", "SEQUENCE", "MERMAID", "VEGA-LITE", "PLANTUML" }.Contains(n.Split(':')[0]) && int.TryParse(n.Split(':').Last(), out var k) && k > 1)) failures.Add($"round {round}: a diagram block holds [{counts}] pictures");
            }
            finally
            {
                PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                await Task.Delay(1200);
                if (IsWindow(window)) notes.Add($"round {round}: the window did not close");
            }
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }
}
