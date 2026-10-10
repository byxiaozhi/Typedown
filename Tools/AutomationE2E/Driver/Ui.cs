using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using Newtonsoft.Json.Linq;
using Typedown.Automation;

// What a case does on the screen as a person would, in one place: finding and waiting for what is shown, clicks, drags
// and the wheel, key chords and text, a dialog's buttons, menus by their path, the popups open, the log, and what to
// keep when a case fails. A case that needs a real click (a popup opened by focus through UI Automation did not open,
// one opened by a click did) uses these rather than its own.
internal static partial class Program
{
    // ---- finding and waiting ----

    private static AutomationElement? FindIn(IntPtr window, string automationId) =>
        AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));

    private static AutomationElement? FindNamed(IntPtr window, string name, ControlType? type = null)
    {
        Condition condition = new PropertyCondition(AutomationElement.NameProperty, name);
        if (type != null) condition = new AndCondition(condition, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        return AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants, condition);
    }

    /// <summary>The element once it is there; a failure that says what was waited for when it is not.</summary>
    private static async Task<AutomationElement> WaitFor(IntPtr window, string automationId, int timeoutMs = 5000)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            var found = FindIn(window, automationId);
            if (found != null) return found;
            if (clock.ElapsedMilliseconds > timeoutMs) throw new CaseFailed($"'{automationId}' did not appear within {timeoutMs} ms ({FocusInfo(window)})");
            await Task.Delay(100);
        }
    }

    private static async Task WaitGone(IntPtr window, string automationId, int timeoutMs = 5000)
    {
        var clock = Stopwatch.StartNew();
        while (FindIn(window, automationId) != null)
        {
            if (clock.ElapsedMilliseconds > timeoutMs) throw new CaseFailed($"'{automationId}' was still there after {timeoutMs} ms");
            await Task.Delay(100);
        }
    }

    /// <summary>A condition polled until it holds or the time is up; whether it held.</summary>
    private static async Task<bool> Eventually(Func<Task<bool>> condition, int timeoutMs = 5000, int everyMs = 200)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            if (await condition()) return true;
            if (clock.ElapsedMilliseconds > timeoutMs) return false;
            await Task.Delay(everyMs);
        }
    }

    // ---- the mouse ----

    private enum Mouse { Left, Right, Middle }

    private static (int X, int Y) CenterOf(AutomationElement element)
    {
        var r = element.Current.BoundingRectangle;
        if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) throw new CaseFailed($"'{element.Current.AutomationId}{element.Current.Name}' is not on screen");
        return ((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2));
    }

    /// <summary>A real click on the element's middle, the window brought to the front first.</summary>
    private static Task Click(IntPtr window, AutomationElement element, Mouse button = Mouse.Left, int clicks = 1)
    {
        var (x, y) = CenterOf(element);
        return Click(window, x, y, button, clicks);
    }

    /// <summary>
    /// A real click at a point on the screen. The point must be in the window: what lies under it otherwise (another
    /// window over it) is named in the failure, as it is the click's first suspect.
    /// </summary>
    private static async Task Click(IntPtr window, int x, int y, Mouse button = Mouse.Left, int clicks = 1)
    {
        await Activate(window);
        var under = WindowFromPoint(new POINT { X = x, Y = y });
        if (under != window && GetAncestor(under, 2) != window)
        {
            var name = new System.Text.StringBuilder(256);
            GetClassName(under, name, 256);
            throw new CaseFailed($"the point ({x}, {y}) is not in the test window: {name} is there");
        }
        SetCursorPos(x, y);
        var (down, up) = button switch { Mouse.Right => (0x0008u, 0x0010u), Mouse.Middle => (0x0020u, 0x0040u), _ => (0x0002u, 0x0004u) };
        for (var i = 0; i < clicks; i++)
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = down } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = up } } });
    }

    /// <summary>
    /// A real click on an item of an open menu. WinUI 3 shows a menu in a popup window of its own, not in the test
    /// window: the point must be in a window of the test window's process. The window is not activated first - that
    /// would close the menu.
    /// </summary>
    private static async Task ClickInPopup(IntPtr window, AutomationElement element)
    {
        var (x, y) = CenterOf(element);
        var under = WindowFromPoint(new POINT { X = x, Y = y });
        GetWindowThreadProcessId(window, out var windowProcess);
        GetWindowThreadProcessId(under, out var underProcess);
        if (windowProcess != underProcess)
        {
            var name = new System.Text.StringBuilder(256);
            GetClassName(under, name, 256);
            throw new CaseFailed($"the menu item at ({x}, {y}) is not in the test window's process: {name} is there");
        }
        // Moved there as a mouse moves (SendInput), not placed (SetCursorPos sends no pointer move): a menu item that
        // had not seen the pointer come in took the press now and then as nothing, and the command did not run.
        int screenW = GetSystemMetrics(0), screenH = GetSystemMetrics(1);
        foreach (var dx in new[] { -6, -3, 0 })
        {
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = (x + dx) * 65535 / (screenW - 1), dy = y * 65535 / (screenH - 1), dwFlags = 0x0001 | 0x8000 } } });
            await Task.Delay(40);
        }
        await Task.Delay(100);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
    }

    /// <summary>A press at one point, a move to another in steps, and the release there.</summary>
    private static async Task Drag(IntPtr window, (int X, int Y) from, (int X, int Y) to, int steps = 12)
    {
        await Activate(window);
        SetCursorPos(from.X, from.Y);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } });
        for (var i = 1; i <= steps; i++)
        {
            SetCursorPos(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps);
            await Task.Delay(15);
        }
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
    }

    /// <summary>The wheel turned over a point: one notch is 120, positive away from the reader (up).</summary>
    private static void Wheel(int x, int y, int delta)
    {
        SetCursorPos(x, y);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0800, mouseData = unchecked((uint)delta) } } });
    }

    // ---- the keyboard ----

    private static readonly Dictionary<string, ushort> KeyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = 0x11, ["Shift"] = 0x10, ["Alt"] = 0x12, ["Win"] = 0x5B, ["Enter"] = 0x0D, ["Esc"] = 0x1B, ["Tab"] = 0x09,
        ["Space"] = 0x20, ["Back"] = 0x08, ["Delete"] = 0x2E, ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28, ["Insert"] = 0x2D,
    };

    /// <summary>
    /// A chord as a person presses it ("Ctrl+Shift+V", "Alt+F4", "Esc"): the keys down in order, up in reverse. Sent
    /// with their scan codes too: some of the app's shortcuts (Ctrl+N, Ctrl+F) did not see a virtual key alone.
    /// </summary>
    private static void Chord(string chord)
    {
        var keys = chord.Split('+').Select(k => k.Trim()).Select(k =>
        {
            if (KeyCodes.TryGetValue(k, out var vk)) return vk;
            if (k.Length > 1 && (k[0] == 'F' || k[0] == 'f') && int.TryParse(k.Substring(1), out var f) && f is >= 1 and <= 24) return (ushort)(0x70 + f - 1);
            if (k.Length == 1 && char.IsLetterOrDigit(k[0])) return (ushort)char.ToUpperInvariant(k[0]);
            throw new CaseFailed($"no key '{k}' in the chord '{chord}'");
        }).ToList();
        INPUT Press(ushort vk, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)MapVirtualKey(vk, 0), dwFlags = up ? 0x0002u : 0u } } };
        Send(keys.Select(k => Press(k, false)).Concat(Enumerable.Reverse(keys).Select(k => Press(k, true))).ToArray());
    }

    /// <summary>Text typed as characters (any language, not through the keyboard layout).</summary>
    private static void TypeText(string text) =>
        Send(text.SelectMany(ch => new[]
        {
            new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0004 } } },
            new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0004 | 0x0002 } } },
        }).ToArray());

    // ---- dialogs, menus, popups ----

    private enum DialogButton { Primary, Secondary, Close }

    /// <summary>
    /// Presses a button of the dialog open in the window, as a click would (test.dialog.press: the app finds its own
    /// dialog - looked for through UI Automation, the window's title bar close button was found first and the window
    /// closed). Waits for a dialog to be there. The dialog's title and message back, when they are text.
    /// </summary>
    private static async Task<(string? Title, string? Content)> PressDialogButton(Client c, string windowId, DialogButton which, int timeoutMs = 3000)
    {
        var name = which.ToString().ToLowerInvariant();
        string? error = null;
        JToken? pressed = null;
        if (!await Eventually(async () =>
        {
            try { pressed = await c.Call("test.dialog.press", new { windowId, button = name }); return true; }
            catch (JsonRpcRemoteException e) { error = e.Message; return false; }
        }, timeoutMs, 150))
            throw new CaseFailed($"no {name} button of a dialog to press: {error}");
        await Task.Delay(300);
        return ((string?)pressed?["title"], (string?)pressed?["content"]);
    }

    /// <summary>
    /// A menu by the names along its path ("文件", "导出", "PDF"): each opened in turn, the last one invoked. The names
    /// are the ones shown, in the interface's language.
    /// </summary>
    private static async Task OpenMenu(IntPtr window, params string[] path)
    {
        await Activate(window);
        for (var i = 0; i < path.Length; i++)
        {
            AutomationElement? item = null;
            if (!await Eventually(() => Task.FromResult((item = FindNamed(window, path[i])) != null), 3000, 100))
                throw new CaseFailed($"no menu entry '{path[i]}' (after {string.Join(" > ", path.Take(i))})");
            if (i < path.Length - 1 && item!.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand))
                ((ExpandCollapsePattern)expand).Expand();
            else if (item!.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
                ((InvokePattern)invoke).Invoke();
            else
                await Click(window, item);
            await Task.Delay(250);
        }
    }

    /// <summary>The popups open in the window (test.window.popups), by the type of what they show.</summary>
    private static async Task<List<string>> Popups(Client c, string windowId) =>
        ((JArray)(await c.Call("test.window.popups", new { windowId }))["popups"]!).Select(x => (string)x!).ToList();

    // ---- the log ----

    /// <summary>The test host's debug log lines containing the text (the log is written in batches: a moment late).</summary>
    private static List<string> LogLines(string contains)
    {
        try
        {
            using var reader = new StreamReader(new FileStream(Path.Combine(testRoot, "logs", "debug.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            return reader.ReadToEnd().Split('\n').Where(l => l.Contains(contains)).Select(l => l.TrimEnd('\r')).ToList();
        }
        catch (IOException) { return new List<string>(); }
    }

    // ---- the desktop ----

    /// <summary>
    /// Waits for the desktop to be left alone before a case drives it: a person's mouse or keys in the last seconds
    /// (a moved mouse once failed a click). Our own input counts too, so this is asked before a case, not inside one.
    /// </summary>
    private static async Task<string?> WaitForQuietDesktop(int quietMs = 2000, int maxWaitMs = 20000)
    {
        var clock = Stopwatch.StartNew();
        while (IdleMs() < quietMs)
        {
            if (clock.ElapsedMilliseconds > maxWaitMs) return $"the desktop was not left alone for {quietMs} ms in {maxWaitMs / 1000} s; went on";
            await Task.Delay(250);
        }
        return clock.ElapsedMilliseconds > 300 ? $"waited {clock.ElapsedMilliseconds / 1000.0:0.0} s for the desktop to be left alone" : null;
    }

    private static long IdleMs()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref info) ? (uint)Environment.TickCount - info.dwTime : long.MaxValue;
    }

    /// <summary>What a failure keeps: the screen, and where the keyboard was.</summary>
    private static string FailureEvidence(string caseName)
    {
        var shot = Screenshot(caseName + "-failed-" + DateTime.Now.ToString("HHmmss"));
        string focus;
        try { focus = FocusInfo(GetForegroundWindow()); } catch (Exception e) { focus = e.Message; }
        return $"screen {shot}; {focus}";
    }

    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    // ---- pictures of a window ----

    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT rect, int size);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

    /// <summary>
    /// The window alone as a PNG, its visible frame without the invisible resize borders, drawn by the window itself
    /// (PrintWindow with PW_RENDERFULLCONTENT: the web view and the XAML content too), so windows in front do not matter
    /// - as Tools/Store/make-screenshots.ps1 takes the Store's pictures. Returns the picture's size.
    /// </summary>
    private static (int Width, int Height) WindowPicture(IntPtr window, string path)
    {
        GetWindowRect(window, out var outer);
        if (DwmGetWindowAttribute(window, 9, out var frame, Marshal.SizeOf<RECT>()) != 0) frame = outer;
        using var bitmap = new System.Drawing.Bitmap(outer.Right - outer.Left, outer.Bottom - outer.Top);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            var dc = g.GetHdc();
            try { PrintWindow(window, dc, 2); } finally { g.ReleaseHdc(dc); }
        }
        var crop = new System.Drawing.Rectangle(frame.Left - outer.Left, frame.Top - outer.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
        using var picture = bitmap.Clone(crop, bitmap.PixelFormat);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        picture.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return (crop.Width, crop.Height);
    }

    /// <summary>The window's visible frame made <paramref name="width"/> x <paramref name="height"/> (window.setView's bounds include the invisible borders).</summary>
    private static async Task SetVisibleSize(Client c, string windowId, IntPtr window, int width, int height)
    {
        int w = width, h = height;
        for (var i = 0; i < 2; i++)
        {
            await c.Call("window.setView", new { windowId, bounds = new { x = 80, y = 40, width = w, height = h } });
            await Task.Delay(300);
            if (DwmGetWindowAttribute(window, 9, out var frame, Marshal.SizeOf<RECT>()) != 0) return;
            w += width - (frame.Right - frame.Left);
            h += height - (frame.Bottom - frame.Top);
        }
    }

    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
}
