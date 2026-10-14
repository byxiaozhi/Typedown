using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// SF01: a window opening under the dark theme shows no light frame on its way: from the moment it is on screen, its
/// client area is sampled as often as the screen can be read for two seconds, and no sample may be light. The old host kept a new window hidden
/// until its content had loaded; the WinUI 3 window was shown at once, before its content.
/// </summary>
internal static partial class Program
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);

    private static async Task SF01(List<string> notes)
    {
        using var c = await Session("e2e SF01");
        var theme = (await c.Call("settings.get", new { keys = new[] { "appearance.theme" } }))["values"]!["appearance.theme"]!;
        await SetSetting(c, "appearance.theme", JObject.FromObject(new { kind = "builtIn", id = "dark" }));
        await Task.Delay(1500);
        List<IntPtr> HostWindows()
        {
            var found = new List<IntPtr>();
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid == hostPid && IsWindowVisible(window) && GetWindowRect(window, out var r) && r.Right - r.Left > 300 && r.Bottom - r.Top > 200)
                    found.Add(window);
                return true;
            }, IntPtr.Zero);
            return found;
        }
        var before = HostWindows();
        string? windowId = null;
        IntPtr shown = IntPtr.Zero;
        try
        {
            var opening = c.Call("test.window.open");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 8000 && shown == IntPtr.Zero)
            {
                shown = HostWindows().FirstOrDefault(w => !before.Contains(w));
                if (shown == IntPtr.Zero) await Task.Delay(5);
            }
            if (shown == IntPtr.Zero) throw new CaseFailed("no new window came on screen");
            var appeared = clock.ElapsedMilliseconds;
            // Five points of the client area (the middle and around it), physical pixels on the screen.
            SetProcessDpiAwarenessContext(new IntPtr(-4));
            var samples = new List<(long At, double Light)>();
            var sampling = System.Diagnostics.Stopwatch.StartNew();
            while (sampling.ElapsedMilliseconds < 2000)
            {
                if (GetWindowRect(shown, out var r))
                {
                    int w = r.Right - r.Left, h = r.Bottom - r.Top;
                    var lightest = 0.0;
                    // The window's area in one read of the screen (a read per point took 30 ms each), the points from it.
                    using (var shot = new System.Drawing.Bitmap(w, h))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(shot)) g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(w, h));
                        foreach (var (fx, fy) in new[] { (0.5, 0.5), (0.3, 0.4), (0.7, 0.4), (0.3, 0.7), (0.7, 0.7) })
                        {
                            var k = shot.GetPixel((int)(w * fx), (int)(h * fy));
                            lightest = Math.Max(lightest, 0.2126 * k.R + 0.7152 * k.G + 0.0722 * k.B);
                        }
                    }
                    samples.Add((sampling.ElapsedMilliseconds, lightest));
                }
                await Task.Delay(10);
            }
            windowId = (string?)(await opening)["windowId"];
            var light = samples.Where(s => s.Light > 150).ToList();
            notes.Add($"on screen {appeared} ms after the call; {samples.Count} samples, lightest {samples.Max(s => s.Light):0}; " +
                string.Join(" ", samples.Where((_, i) => i % 6 == 0).Take(20).Select(s => $"{s.At}:{s.Light:0}")));
            Check(light.Count == 0, $"the dark window showed a light frame {light.Count} times in its first two seconds (first at {light.FirstOrDefault().At} ms, {light.FirstOrDefault().Light:0})");
        }
        finally
        {
            if (shown != IntPtr.Zero) { PostMessage(shown, 0x0010, IntPtr.Zero, IntPtr.Zero); await Task.Delay(1000); }
            await SetSetting(c, "appearance.theme", theme);
        }
    }
}
