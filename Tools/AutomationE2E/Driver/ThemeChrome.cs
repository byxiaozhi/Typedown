using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// TH06: built-in and custom themes switched back and forth: switched back to a built-in theme, the window is drawn as it
/// was before - the editor page's background, the line between the side pane and the editor, the side pane, the status
/// bar - and the editor is dark under a dark theme. A reader saw the editor stay light (#F7F7F7) in a dark window, and
/// the line beside the side pane gone, after using a custom theme.
/// </summary>
internal static partial class Program
{
    private static async Task TH06(List<string> notes)
    {
        using var c = await Session("e2e TH06");
        var id = await Open(c, Fixture("th06.md", "# TH06\n\nSome text in the editor.\n"));
        var windowId = await WindowIdOf(c, id);
        await c.Call("window.setView", new { windowId, sidePane = new { open = true, page = "outline" }, statusBar = true });
        var theme = (await c.Call("settings.get", new { keys = new[] { "appearance.theme" } }))["values"]!["appearance.theme"]!;
        async Task<JObject> State(string step)
        {
            await Task.Delay(1500);
            var chrome = (JObject)await c.Call("test.theme.chrome", new { windowId });
            var page = (string?)(await c.Call("test.editor.eval", new { windowId, script = "(() => { const l = document.getElementById('link_style_editor'); return getComputedStyle(document.body).backgroundColor + ' | style ' + (document.body.style.backgroundColor || '-') + ' | css ' + (l ? l.getAttribute('href') : '-') + ' | page theme ' + window.actualTheme })()" }))["result"];
            chrome["page"] = page;
            notes.Add($"{step}: {chrome.ToString(Newtonsoft.Json.Formatting.None)}");
            return chrome;
        }
        // "rgb(40, 40, 40) | ..." or "rgba(0, 0, 0, 0) | ..." -> the page's own colour, or null when it lets the window show.
        static int? Luminance(string? page)
        {
            var colour = (page ?? "").Split('|')[0].Trim();
            var start = colour.IndexOf('('); var end = colour.IndexOf(')');
            if (start < 0 || end < start) return null;
            var parts = colour.Substring(start + 1, end - start - 1).Split(',');
            if (parts.Length == 4 && parts[3].Trim() == "0") return null;
            return (int.Parse(parts[0]) * 299 + int.Parse(parts[1]) * 587 + int.Parse(parts[2]) * 114) / 1000;
        }
        var failures = new List<string>();
        try
        {
            await c.Call("test.theme.apply", new { windowId, builtIn = "Light" });
            var light = await State("light");
            await c.Call("test.theme.apply", new { windowId, customTheme = "dracula" });
            await State("dracula");
            await c.Call("test.theme.apply", new { windowId, builtIn = "Light" });
            var lightAgain = await State("light after dracula");
            foreach (var key in new[] { "splitter", "sidePane", "statusBar", "menuBar" })
                if (!JToken.DeepEquals(light[key], lightAgain[key])) failures.Add($"{key}: {light[key]} at first, {lightAgain[key]} after dracula");
            if (Luminance((string?)light["page"]) != Luminance((string?)lightAgain["page"]))
                failures.Add($"editor page: '{light["page"]}' at first, '{lightAgain["page"]}' after dracula");
            await c.Call("test.theme.apply", new { windowId, builtIn = "Dark" });
            var dark = await State("dark");
            if (Luminance((string?)dark["page"]) is int l1 && l1 > 128) failures.Add($"dark: the editor page is light ({dark["page"]})");
            // Dark draws its own colours, not Light's kept on (a colour fixed when the window was built would compare equal
            // after a switch back and pass the checks above).
            foreach (var key in new[] { "splitter", "statusBar", "sidePaneText" })
                if (light[key] is JToken was && (string?)was != "none" && JToken.DeepEquals(was, dark[key])) failures.Add($"dark, {key}: still Light's {was}");
            await c.Call("test.theme.apply", new { windowId, customTheme = "dracula" });
            await State("dracula again");
            await c.Call("test.theme.apply", new { windowId, builtIn = "Dark" });
            var darkAgain = await State("dark after dracula");
            if (Luminance((string?)darkAgain["page"]) is int l2 && l2 > 128) failures.Add($"dark after dracula: the editor page is light ({darkAgain["page"]})");
            foreach (var key in new[] { "splitter", "sidePane", "statusBar", "menuBar" })
                if (!JToken.DeepEquals(dark[key], darkAgain[key])) failures.Add($"dark, {key}: {dark[key]} at first, {darkAgain[key]} after dracula");
            // The custom theme entered from Dark and left for Light: what the built-in theme draws changed while the theme
            // was on.
            await c.Call("test.theme.apply", new { windowId, customTheme = "dracula" });
            await State("dracula from dark");
            await c.Call("test.theme.apply", new { windowId, builtIn = "Light" });
            var lightLast = await State("light after dark and dracula");
            foreach (var key in new[] { "splitter", "sidePane", "statusBar", "menuBar" })
                if (!JToken.DeepEquals(light[key], lightLast[key])) failures.Add($"light after dark and dracula, {key}: {light[key]} at first, {lightLast[key]} now");
            if (Luminance((string?)light["page"]) != Luminance((string?)lightLast["page"]))
                failures.Add($"light after dark and dracula, editor page: '{light["page"]}' at first, '{lightLast["page"]}' now");
        }
        finally
        {
            await SetSetting(c, "appearance.theme", theme);
        }
        Check(failures.Count == 0, string.Join("; ", failures));
    }
}
