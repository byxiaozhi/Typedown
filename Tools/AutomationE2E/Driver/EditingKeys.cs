using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// ED01: the keys that select and move in the visual editor reach it through the app's keyboard hook as Windows sends
/// them. Store reviews (1.x): "Ctrl+arrow keys cannot select a paragraph, only the mouse can", "Ctrl+F often does
/// nothing", "the mouse wheel does not scroll". The page side passes headless (Chrome on Linux); what only Windows
/// can show is whether the host lets these keys through, or takes them, and whether the wheel reaches the page.
/// </summary>
internal static partial class Program
{
    private static async Task ED01(List<string> notes)
    {
        using var c = await Session("e2e ED01");
        var filler = string.Join("\n\n", Enumerable.Range(1, 80).Select(i => $"filler paragraph {i}"));
        var id = await Open(c, Fixture("ed01.md", $"# ED01\n\nfirst para has several words\n\nsecond para\n\n{filler}\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        async Task<string> Selected() => (string?)(await c.Call("test.editor.eval", new { windowId, script = "getSelection().toString()" }))["result"] ?? "";
        async Task<double> ScrollY() => (double?)(await c.Call("test.editor.eval", new { windowId, script = "window.scrollY" }))["result"] ?? -1;

        await ClickEditorTextOnce(c, windowId, window, "first para");
        await Task.Delay(300);
        Chord("Home");
        await Task.Delay(200);

        // Word by word: Ctrl+Shift+Right twice selects "first para " (Windows takes the space after a word with it).
        Chord("Ctrl+Shift+Right");
        await Task.Delay(150);
        Chord("Ctrl+Shift+Right");
        await Task.Delay(300);
        var words = await Selected();
        notes.Add($"after Ctrl+Shift+Right twice: '{words}'");
        Check(words.Trim() == "first para", $"Ctrl+Shift+Right selects word by word ('{words}')");

        // To the end of the paragraph: Ctrl+Shift+Down.
        Chord("Ctrl+Shift+Down");
        await Task.Delay(300);
        var paragraph = await Selected();
        notes.Add($"after Ctrl+Shift+Down: '{paragraph.Replace("\n", "\\n")}'");
        Check(paragraph.StartsWith("first para has several words"), $"Ctrl+Shift+Down extends the selection over the paragraph ('{paragraph.Replace("\n", "\\n")}')");

        // Ctrl+Left / Ctrl+Right move the caret by words without selecting.
        Chord("Ctrl+Left");
        await Task.Delay(150);
        Chord("Ctrl+Right");
        await Task.Delay(300);
        var afterMove = await Selected();
        Check(afterMove == "", $"Ctrl+Left and Ctrl+Right move without selecting ('{afterMove}')");

        // Ctrl+F opens the find bar with the keyboard in its search box; Esc closes it.
        Chord("Ctrl+F");
        var found = false;
        for (var i = 0; i < 40 && !found; i++)
        {
            found = System.Windows.Automation.AutomationElement.FocusedElement?.Current.AutomationId == "TextBoxSearch";
            if (!found) await Task.Delay(50);
        }
        Check(found, "Ctrl+F in the editor opens the find bar with the keyboard in its search box");
        Chord("Esc");
        await Task.Delay(600);

        // The wheel over the page scrolls it, down and back up.
        var at = await c.Call("test.editor.screenPoint", new { windowId, x = 400, y = 300 });
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        var top = await ScrollY();
        for (var i = 0; i < 3; i++) { Wheel((int)at["x"]!, (int)at["y"]!, -120); await Task.Delay(120); }
        await Task.Delay(600);
        var down = await ScrollY();
        for (var i = 0; i < 3; i++) { Wheel((int)at["x"]!, (int)at["y"]!, 120); await Task.Delay(120); }
        await Task.Delay(600);
        var back = await ScrollY();
        notes.Add($"wheel: scrollY {top} -> {down} -> {back}");
        Check(down > top + 50, $"three notches down scroll the page ({top} -> {down})");
        Check(back < down - 50, $"three notches up scroll it back ({down} -> {back})");
    }
}
