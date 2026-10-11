using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>
/// Measurements, not checks: the same cases run against two test hosts (the XAML Islands host on .NET Core 3.1 and the
/// WinUI 3 host on .NET 10, both published as a release is) give the numbers the two are compared by. Each number is a
/// note ("PERF name value unit"); a case fails only when it cannot measure.
/// </summary>
internal static partial class Program
{
    private static void Perf(List<string> notes, string name, double value, string unit) =>
        notes.Add(FormattableString.Invariant($"PERF {name} {value:0.#} {unit}"));

    private static double Median(List<double> values) { var s = values.OrderBy(v => v).ToList(); return s.Count == 0 ? double.NaN : s[s.Count / 2]; }

    private static double Percentile(List<double> values, double p) { var s = values.OrderBy(v => v).ToList(); return s.Count == 0 ? double.NaN : s[Math.Min(s.Count - 1, (int)Math.Ceiling(p * s.Count) - 1)]; }

    /// <summary>A document of about 300 KB with what the editor draws: headings, paragraphs, lists, code and tables.</summary>
    private static string LargeDocument(string marker, int size = 300_000)
    {
        var b = new StringBuilder("# Performance\n\n");
        for (var i = 1; b.Length < size; i++)
        {
            b.Append($"## Section {i}\n\nParagraph {i} with **bold**, *italic*, `code` and a [link](https://example.com/{i}). ");
            b.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n\n");
            b.Append($"- item {i}.1\n- item {i}.2\n  - nested {i}\n\n");
            if (i % 5 == 0) b.Append($"```csharp\nvar x{i} = {i};\nConsole.WriteLine(x{i});\n```\n\n");
            if (i % 7 == 0) b.Append($"| a | b | c |\n| --- | --- | --- |\n| {i} | {i * 2} | {i * 3} |\n\n");
        }
        return b.Append(marker).Append('\n').ToString();
    }

    // ---- processes: the host and what it started (its WebView2 browser and renderer processes) ----

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize, cntUsage, th32ProcessID; public IntPtr th32DefaultHeapID; public uint th32ModuleID, cntThreads, th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    private static List<int> ProcessTree(int root)
    {
        var parents = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(0x2, 0);
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
                parents[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
        }
        finally { CloseHandle(snapshot); }
        var tree = new List<int> { root };
        for (var i = 0; i < tree.Count; i++)
            tree.AddRange(parents.Where(p => p.Value == tree[i] && p.Key != tree[i] && !tree.Contains(p.Key)).Select(p => p.Key));
        return tree;
    }

    private static (double privateMb, double workingSetMb, int processes) TreeMemory(int root)
    {
        double priv = 0, ws = 0; var n = 0;
        foreach (var pid in ProcessTree(root))
            try { using var p = Process.GetProcessById(pid); priv += p.PrivateMemorySize64; ws += p.WorkingSet64; n++; } catch (ArgumentException) { } catch (InvalidOperationException) { }
        return (priv / 1048576, ws / 1048576, n);
    }

    private static async Task<bool> PageShows(Client c, string windowId, string marker)
    {
        try { return (bool?)(await c.Call("test.editor.eval", new { windowId, script = $"document.body.textContent.includes({Newtonsoft.Json.JsonConvert.ToString(marker)})" }))["result"] == true; }
        catch (Typedown.Automation.JsonRpcRemoteException) { return false; }
    }

    private static async Task<bool> PageHas(Client c, string documentId, string marker)
    {
        try { return ((string?)(await c.Call("test.editor.pageText", new { documentId }))["text"] ?? "").Contains(marker); }
        catch (Typedown.Automation.JsonRpcRemoteException) { return false; }
    }

    /// <summary>A large document opened, typed into, saved; the memory with two windows.</summary>
    private static async Task PF01(List<string> notes)
    {
        using var c = await Session("e2e PF01");
        SetProcessDpiAwarenessContext(new IntPtr(-4));

        // The editor page of the window started with the host fully loaded first: a host whose window comes up sooner
        // would otherwise have its first open timed while its page is still starting.
        var firstWindow = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]!["windowId"]!;
        for (var i = 0; i < 300; i++)
        {
            try { if ((string?)(await c.Call("test.editor.eval", new { windowId = firstWindow, script = "document.readyState" }))["result"] == "complete") break; }
            catch (Typedown.Automation.JsonRpcRemoteException) { }
            await Task.Delay(100);
        }
        await Task.Delay(5000);

        // CPU time across the opens: the host itself and its WebView2 processes apart.
        (double host, double web) Cpu()
        {
            double h = 0, w = 0;
            foreach (var pid in ProcessTree(hostPid))
                try { using var p = Process.GetProcessById(pid); var t = p.TotalProcessorTime.TotalMilliseconds; if (pid == hostPid) h += t; else w += t; } catch (ArgumentException) { } catch (InvalidOperationException) { }
            return (h, w);
        }
        // Idle: nothing asked of it for 10 s.
        var idleStart = Cpu();
        await Task.Delay(10000);
        var idleEnd = Cpu();
        Perf(notes, "idle10s.cpu.host", idleEnd.host - idleStart.host, "ms");
        Perf(notes, "idle10s.cpu.webview", idleEnd.web - idleStart.web, "ms");
        var cpuBefore = Cpu();
        Dictionary<int, (double ms, DateTime start)> Threads()
        {
            var d = new Dictionary<int, (double, DateTime)>();
            using var p = Process.GetProcessById(hostPid);
            foreach (ProcessThread t in p.Threads)
                try { d[t.Id] = (t.TotalProcessorTime.TotalMilliseconds, t.StartTime); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            return d;
        }
        var threadsBefore = Threads();

        // Opening: from document.open to the editor showing the document's end.
        var opens = new List<double>();
        string? id = null;
        for (var round = 0; round < 6; round++)
        {
            var marker = $"PF01-END-{round}";
            var path = Fixture($"pf01-{round}.md", LargeDocument(marker));
            var watch = Stopwatch.StartNew();
            id = await Open(c, path);
            // Asked of the page itself (a yes or no), not by fetching its 300 KB of text every time: that load on the
            // host's UI thread was part of what was measured. textContent, not innerText: innerText lays the page out
            // on every question, which competes with the drawing being timed.
            var windowId = await WindowIdOf(c, id);
            while (!await PageShows(c, windowId, marker))
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(60)) throw new CaseFailed("a 300 KB document was not shown within 60 s");
                await Task.Delay(20);
            }
            opens.Add(watch.Elapsed.TotalMilliseconds);
            // Was the page resized while it drew (a strip above it changing height as the tab came in)?
            var resized = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const n = window.__pfResizes || 0; window.__pfResizes = 0; if (!window.__pfResizeHooked) { window.__pfResizeHooked = true; addEventListener('resize', () => window.__pfResizes = (window.__pfResizes || 0) + 1) } return `${n} resizes, now ${innerWidth}x${innerHeight}` })()" }))["result"];
            notes.Add($"open {round}: {resized}");
            // Left open: the editor tidies a table or two of it as it loads, so a close would stop at the save question.
        }
        var cpuAfter = Cpu();
        var threadsAfter = Threads();
        var mainThread = threadsAfter.OrderBy(t => t.Value.start).First().Key;
        foreach (var t in threadsAfter.Select(t => (id: t.Key, ms: t.Value.ms - (threadsBefore.TryGetValue(t.Key, out var b) ? b.ms : 0), t.Value.start)).OrderByDescending(t => t.ms).Take(6))
            notes.Add($"thread {t.id}{(t.id == mainThread ? " (main)" : "")} started {t.start:HH:mm:ss.fff}: {t.ms:0} ms");
        Perf(notes, "open300k.cpu.host", cpuAfter.host - cpuBefore.host, "ms");
        Perf(notes, "open300k.cpu.webview", cpuAfter.web - cpuBefore.web, "ms");
        // What the page had to lay out, and what the host's log says the page took (from the host posting the document
        // to the page reporting it loaded): the part of an open that is the editor's, not the host's.
        var viewport = (await c.Call("test.editor.eval", new { windowId = await WindowIdOf(c, id!), script = "`${innerWidth}x${innerHeight} @${devicePixelRatio}`" }))["result"];
        notes.Add($"viewport {viewport}");
        try
        {
            using var reader = new StreamReader(new FileStream(Path.Combine(testRoot, "logs", "debug.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            var page = reader.ReadToEnd().Split('\n').Where(l => l.Contains("FileLoaded after")).Select(l => System.Text.RegularExpressions.Regex.Match(l, @"after (\d+) ms").Groups[1].Value).ToList();
            notes.Add("page took (host log): " + string.Join(", ", page));
            // The host's log around the second open (the first one into a window that already shows a large document).
            var lines = File.ReadAllLines(Path.Combine(testRoot, "logs", "debug.log"));
            var from = Array.FindIndex(lines, l => l.Contains("pf01-0.md"));
            var to = Array.FindIndex(lines, l => l.Contains("pf01-2.md"));
            if (from >= 0 && to > from)
                foreach (var l in lines.Skip(from).Take(Math.Min(60, to - from + 1)).Where(l => l.Length > 0 && !l.StartsWith("   at ")))
                    notes.Add("log: " + (l.Length > 200 ? l.Substring(0, 200) : l));
        }
        catch (IOException e) { notes.Add("no host log: " + e.Message); }
        // The first open of a run also starts what later ones find ready: reported apart.
        Perf(notes, "open300k.first", opens[0], "ms");
        Perf(notes, "open300k.median", Median(opens.Skip(1).ToList()), "ms");
        notes.Add("open 300 KB: " + string.Join(", ", opens.Select(v => $"{v:0}")));

        // Typing at the end of a 30 KB document: a real key to the next revision the API reports.
        async Task Keys(string documentId, string label)
        {
            await TypeInto(c, documentId, "x");
            await Task.Delay(1500);
            var keys = new List<double>();
            for (var i = 0; i < 20; i++)
            {
                var before = await Revision(c, documentId);
                var watch = Stopwatch.StartNew();
                TypeChar((char)('a' + i % 26));
                while (await Revision(c, documentId) == before)
                {
                    if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new CaseFailed($"{label}: keystroke {i} made no revision within 20 s");
                    await Task.Delay(2);
                }
                keys.Add(watch.Elapsed.TotalMilliseconds);
                await Task.Delay(150);
            }
            Perf(notes, $"keystroke{label}.median", Median(keys), "ms");
            Perf(notes, $"keystroke{label}.p90", Percentile(keys, 0.9), "ms");
        }
        var small = await Open(c, Fixture("pf01-30k.md", LargeDocument("PF01-30K", 30_000)));
        while (!await PageHas(c, small, "PF01-30K")) await Task.Delay(50);
        await Keys(small, "30k");
        // Not in the 300 KB one: there a key made no revision within 20 s on either host (the XAML Islands one too), which
        // says more about this measurement than about either host.

        // Saving the large document (shown again first: writes go to the document a window shows).
        await c.Call("document.focus", new { documentId = id });
        await Task.Delay(1000);
        var saves = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            await c.Call("document.replace", new { documentId = id, text = LargeDocument($"PF01-SAVE-{i}"), baseRevision = await Revision(c, id!), normalizationPolicy = "allowUnknown" });
            var watch = Stopwatch.StartNew();
            await c.Call("document.save", new { documentId = id });
            saves.Add(watch.Elapsed.TotalMilliseconds);
        }
        Perf(notes, "save300k.median", Median(saves), "ms");

        // Memory: this window with the three large documents and a second window, settled.
        var second = (string)(await c.Call("test.window.open"))["windowId"]!;
        await Task.Delay(8000);
        var (priv, ws, n) = TreeMemory(hostPid);
        Perf(notes, "memory2windows.private", priv, "MB");
        Perf(notes, "memory2windows.workingSet", ws, "MB");
        notes.Add($"{n} processes (the host and its WebView2 processes)");
        var secondHandle = await WindowOf(second, c);
        PostMessage(secondHandle, 0x0010, IntPtr.Zero, IntPtr.Zero);
        await Task.Delay(2000);
    }

    /// <summary>
    /// A cold start with a document on the command line, three times, each a fresh host in a fresh folder: to its window,
    /// and to the document shown in the editor. Then the window closed: until the process exits, and what of it is left.
    /// </summary>
    private static async Task PF02(List<string> notes)
    {
        var windows = new List<double>(); var shown = new List<double>(); var exits = new List<double>(); var gone = new List<double>();
        var left = 0;
        for (var round = 0; round < 3; round++)
        {
            var root = testRoot + $"-pf02-{round}";
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            var marker = $"PF02-END-{round}";
            var doc = Fixture($"pf02-{round}.md", "# Cold start\n\n" + string.Concat(Enumerable.Range(1, 200).Select(i => $"Line {i} of a small document.\n\n")) + marker + "\n");
            var watch = Stopwatch.StartNew();
            using var host = Process.Start(new ProcessStartInfo(hostExe, $"--automation-test-root \"{root}\" \"{doc}\"") { UseShellExecute = true })!;
            var name = await ReadEndpointFile(Path.Combine(root, "automation-endpoint.txt")) ?? throw new CaseFailed("the host published no endpoint");
            Client? c = null;
            for (var i = 0; c == null; i++)
            {
                try { c = new Client(name); }
                catch (Exception) when (i < 100) { await Task.Delay(50); }
            }
            using (c)
            {
                await Initialize(c, "e2e PF02");
                string? windowId = null;
                while (windowId == null)
                {
                    var list = (JArray)(await c.Call("window.list"))["windows"]!;
                    if (list.Count > 0) windowId = (string)list[0]!["windowId"]!;
                    else await Task.Delay(10);
                    if (watch.Elapsed > TimeSpan.FromSeconds(60)) throw new CaseFailed("no window within 60 s");
                }
                windows.Add(watch.Elapsed.TotalMilliseconds);
                string? id = null;
                while (true)
                {
                    id ??= ((JArray)(await c.Call("document.list", new { windowId }))["documents"]!)
                        .FirstOrDefault(d => string.Equals((string?)d["path"], doc, StringComparison.OrdinalIgnoreCase))?["documentId"]?.ToString();
                    if (id != null && await PageHas(c, id, marker)) break;
                    if (watch.Elapsed > TimeSpan.FromSeconds(60)) throw new CaseFailed("the document on the command line was not shown within 60 s");
                    await Task.Delay(10);
                }
                shown.Add(watch.Elapsed.TotalMilliseconds);
                await Task.Delay(3000); // started up fully before it is closed
                var tree = ProcessTree(host.Id);
                var handle = await WindowOf(windowId, c);
                var closing = Stopwatch.StartNew();
                PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero);
                // What the user sees: the window off the screen (the process may still be tidying up after that).
                while (IsWindow(handle) && IsWindowVisible(handle) && closing.Elapsed < TimeSpan.FromSeconds(30)) await Task.Delay(2);
                gone.Add(closing.Elapsed.TotalMilliseconds);
                if (!host.WaitForExit(30000)) { notes.Add($"round {round}: the host did not exit within 30 s"); host.Kill(); }
                else exits.Add(closing.Elapsed.TotalMilliseconds);
                await Task.Delay(3000);
                foreach (var pid in tree.Skip(1))
                    try { using var p = Process.GetProcessById(pid); if (!p.HasExited) { left++; notes.Add($"round {round}: {p.ProcessName} ({pid}) still running 3 s after the host exited"); p.Kill(); } }
                    catch (ArgumentException) { }
            }
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Perf(notes, "coldstart.window.median", Median(windows), "ms");
        Perf(notes, "coldstart.document.median", Median(shown), "ms");
        Perf(notes, "close.windowGone.median", Median(gone), "ms");
        Perf(notes, "exit.median", Median(exits), "ms");
        Perf(notes, "exit.leftoverProcesses", left, "");
        notes.Add("window: " + string.Join(", ", windows.Select(v => $"{v:0}")) + "; document: " + string.Join(", ", shown.Select(v => $"{v:0}")));
    }
}
