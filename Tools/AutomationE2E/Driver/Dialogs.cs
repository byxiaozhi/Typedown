using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// DG01: a dialog wears the custom theme the window wears (dracula: its surface and text colours), and the system's
/// colours again once the theme is switched off. Dialogs went by light and dark only and looked like another program
/// under a custom theme.
/// </summary>
internal static partial class Program
{
    private static async Task DG01(List<string> notes)
    {
        using var c = await Session("e2e DG01");
        var id = await Open(c, Fixture("dg01.md", "# DG01\n\nText\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        async Task<JToken> DialogColours()
        {
            // The editor focused (the menu's commands act on it only then), then Table: its dialog.
            JToken colours = new JObject();
            // Table once more if its dialog did not come: the menu automation now and then misses the item, and what is
            // checked here is the dialog's colours, not the menu.
            var opened = false;
            for (var attempt = 0; attempt < 2 && !opened; attempt++)
            {
                if (attempt > 0) notes.Add($"the insert-table dialog did not open ({lastMenuBarItemSkipped ?? "Table was clicked"}): Table again");
                await ClickEditorText(c, windowId, window, "Text");
                await Task.Delay(500);
                await InvokeMenuBarItem(window, "TableItem", escapeAfter: false);
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(150);
                    colours = await c.Call("test.dialog.colours", new { windowId });
                    if (opened = (bool)colours["open"]!) break;
                }
            }
            await Task.Delay(400);
            colours = await c.Call("test.dialog.colours", new { windowId });
            Send(Key(0x1B, false), Key(0x1B, true)); // Esc: the dialog closed
            await Task.Delay(800);
            return colours;
        }
        try
        {
            await c.Call("test.theme.apply", new { windowId, customTheme = "dracula" });
            await Task.Delay(1000);
            var themed = await DialogColours();
            notes.Add("dracula: " + themed.ToString(Newtonsoft.Json.Formatting.None));
            Check((bool)themed["open"]!, "the insert-table dialog opened");
            Check((string?)themed["background"] != null && (string?)themed["background"] == (string?)themed["themeSurface"], $"the dialog's background is the theme's ({themed["background"]} vs {themed["themeSurface"]})");
            Check((string?)themed["foreground"] == (string?)themed["themeForeground"], $"the dialog's text is the theme's ({themed["foreground"]} vs {themed["themeForeground"]})");

            await c.Call("test.theme.apply", new { windowId, builtIn = "Default" });
            await Task.Delay(1000);
            var plain = await DialogColours();
            notes.Add("default: " + plain.ToString(Newtonsoft.Json.Formatting.None));
            Check((string?)plain["background"] != (string?)themed["background"], "without the theme the dialog's background is not the theme's any more");
        }
        finally
        {
            try { await c.Call("test.theme.apply", new { windowId, builtIn = "Default" }); } catch { }
        }
    }
}
