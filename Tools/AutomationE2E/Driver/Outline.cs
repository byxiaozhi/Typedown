using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// OL02: in reading mode, two long documents, each scrolled to its middle (the caret left at the top, as a reader scrolling
/// with the wheel leaves it), switched between: the outline marks the shown document's heading, and while a tab is being left it does
/// not mark another heading of the document being left first. A reader saw the outline jump to some other section of
/// the previous outline for a moment on every switch.
/// </summary>
internal static partial class Program
{
    private static async Task OL02(List<string> notes)
    {
        using var c = await Session("e2e OL02");
        string Long(string prefix)
        {
            var text = new System.Text.StringBuilder();
            for (var a = 1; a <= 40; a++)
            {
                text.Append($"## {prefix} {a}\n\n");
                for (var p = 0; p < 3; p++) text.Append($"Paragraph {p} of {prefix} {a}, long enough to take a line or two of the page, so that forty sections make a document several screens tall.\n\n");
            }
            return Fixture($"ol02-{prefix.ToLowerInvariant()}.md", text.ToString());
        }
        var prefixes = new[] { "Kilo", "Lima" };
        var paths = prefixes.Select(Long).ToArray();
        var ids = new List<string>();
        foreach (var path in paths) ids.Add(await Open(c, path));
        var windowId = await WindowIdOf(c, ids[0]);
        // Reading mode: the outline follows the page as it is scrolled (in the other modes it follows the caret).
        await c.Call("window.setView", new { windowId, mode = "reading", sidePane = new { open = true, page = "outline" } });
        // Each document scrolled to its middle, the caret where it was opened (the top).
        foreach (var i in new[] { 0, 1 })
        {
            await Open(c, paths[i]);
            await Task.Delay(1500);
            await c.Call("test.editor.eval", new { windowId, script = "window.scrollTo(0, document.documentElement.scrollHeight / 2); true" });
            await Task.Delay(1500);
        }
        List<string> Marks() => LogLines("outline: marked ");
        var bad = new List<string>();
        var current = 1;
        for (var round = 0; round < 8; round++)
        {
            await Task.Delay(1500);
            var before = Marks();
            var lastMark = before.LastOrDefault() ?? "";
            var next = 1 - current;
            await Open(c, paths[next]);
            await Task.Delay(2500);
            var during = Marks().Skip(before.Count).ToList();
            notes.Add($"round {round}, {prefixes[current]} -> {prefixes[next]}: last mark '{Tail(lastMark)}', then {string.Join(" / ", during.Select(Tail))}");
            // A mark of the document being left, other than the one it had, is the jump.
            foreach (var m in during.TakeWhile(m => !Tail(m).StartsWith(prefixes[next])))
                if (Tail(m).StartsWith(prefixes[current]) && Tail(m) != Tail(lastMark)) bad.Add($"round {round}: {Tail(m)}");
            Check(during.Count == 0 || Tail(during.Last()).StartsWith(prefixes[next]), $"round {round}: the outline ends on a heading of {prefixes[next]}");
            // The document shown is marked once, at the place being read - not first at its caret's heading.
            var shownMarks = during.Where(m => Tail(m).StartsWith(prefixes[next])).Select(Tail).ToList();
            if (round > 0 && shownMarks.Count > 1) bad.Add($"round {round}: {prefixes[next]} marked {string.Join(", then ", shownMarks)}");
            current = next;
        }
        Check(bad.Count == 0, $"no other heading of the document being left is marked on a switch ({string.Join("; ", bad)})");
        await c.Call("window.setView", new { windowId, mode = "visual" });
    }

    // "12:00:00.000 outline: marked Kilo 21 (load 9)" -> "Kilo 21"
    private static string Tail(string line)
    {
        var i = line.IndexOf("outline: marked ");
        if (i < 0) return line;
        var s = line.Substring(i + "outline: marked ".Length);
        var j = s.LastIndexOf(" (load ");
        return j < 0 ? s : s.Substring(0, j);
    }
}
