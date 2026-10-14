using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// BL01: blank lines are kept through a visual edit and a save. Extra blank lines show as empty paragraphs, fewer of
/// them than there are lines, and an even run lost a line as soon as the reader typed anything: 2 came back as 1, 4 as
/// 3 (todo S8, a review from 2022). The page side is checked headless (Tools/EditorBench/blanklines-check.js); this is
/// the same through the app: the keystroke, the text the host holds, and the file a save writes.
/// </summary>
internal static partial class Program
{
    private static async Task BL01(List<string> notes)
    {
        using var c = await Session("e2e BL01");
        const string source = "\n# BL01\n\n\nTwo blank lines above.\n\n\n\n\nFour above.\n\n\n- a list\n\n\n\n> a quote\n\n\n\n\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\n\nLast\n\n";
        var path = Fixture("bl01.md", source);
        var id = await Open(c, path);
        var windowId = await WindowIdOf(c, id);
        try
        {
            await WaitUntilDrawn(c, windowId, id, "Four above.");
            // Ctrl+End and a key: the caret goes to the end of "Last" (one blank line after it is no empty paragraph).
            await TypeIntoWindow(c, windowId, "X");
            var expected = source.Replace("Last\n", "LastX\n");
            string text = "";
            for (var i = 0; i < 50; i++)
            {
                text = (string?)(await Get(c, id))["text"] ?? "";
                if (text != source) break;
                await Task.Delay(200);
            }
            notes.Add("after the key: " + text.Replace("\n", "\\n"));
            Check(text != source, "the key reached the document");
            Check(text == expected, "the edit added the key and nothing else: one blank line at the start, then 2, 4, 2, 3, 5 and 2 between the blocks and one at the end, as in the file");
            await c.Call("document.save", new { documentId = id });
            var saved = Disk(path);
            Check(saved == text, "the file a save writes is that text");
        }
        finally
        {
            try { await c.Call("document.close", new { documentId = id }); } catch { }
            try { File.Delete(path); } catch { }
        }
    }
}
