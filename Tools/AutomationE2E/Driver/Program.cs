using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.Automation;

// End-to-end cases against a running automation test host. Every case checks the API's answer, the revision the
// window holds and the bytes on disk; interleavings are forced with test.barrier.* (arm -> wait until hit -> act ->
// release), never with sleeps; a person's keystroke is real input (SendInput) into the window.
//
//   Typedown.AutomationE2E --root <test data root> --pid <host process id> --fixtures <dir> --out <result.json>

internal static partial class Program
{
    private sealed class NoHandler : IJsonRpcHandler
    {
        public Task<JToken?> HandleRequestAsync(JsonRpcRequest r, CancellationToken ct) => throw new AutomationException(AutomationErrorKind.method_not_found, "no");
        public Task HandleNotificationAsync(JsonRpcRequest n, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Client : IDisposable
    {
        private readonly NamedPipeClientStream pipe;
        public readonly JsonRpcConnection Connection;
        private readonly Task running;

        public Client(string endpoint)
        {
            pipe = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect(1000);
            Connection = new JsonRpcConnection(new MessageFraming(pipe, 64L << 20), new NoHandler());
            running = Connection.RunAsync();
        }

        public async Task<JToken> Call(string method, object? parameters = null)
        {
            var p = parameters == null ? null : JToken.FromObject(parameters);
            return (await Connection.SendRequestAsync(method, p).WaitAsync(TimeSpan.FromSeconds(60)))!;
        }

        public async Task<int> ErrorCode(string method, object? parameters = null)
        {
            try { await Call(method, parameters); return 0; }
            catch (JsonRpcRemoteException e) { return e.Code; }
        }

        public async Task<string?> ErrorKind(string method, object? parameters = null)
        {
            try { await Call(method, parameters); return null; }
            catch (JsonRpcRemoteException e) { return (string?)e.ErrorData?["kind"]; }
        }

        public void Dispose() { Connection.Dispose(); pipe.Dispose(); }
    }

    private static readonly List<JObject> results = new();
    private static string outputDir = ".";
    private static string[]? only;

    /// <summary>A picture of the screen next to result.json, for a failure that depends on what is shown.</summary>
    private static string Screenshot(string name)
    {
        try
        {
            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            using var bitmap = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var g = System.Drawing.Graphics.FromImage(bitmap)) g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
            var path = Path.Combine(outputDir, name + ".png");
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return path;
        }
        catch (Exception e) { return "no screenshot: " + e.Message; }
    }
    private static string hostExe = "";
    private static string testRoot = "";
    private static string endpoint = "";
    private static int hostPid;
    private static string fixtures = "";

    /// <summary>The cases an edition adds, run before the last one (Q01 ends the test host).</summary>
    static partial void EditionCases(List<(string Name, Func<List<string>, Task> Run)> cases);

    private static async Task<int> Main(string[] args)
    {
        string Arg(string name) => args[Array.IndexOf(args, name) + 1];
        var root = Arg("--root");
        hostPid = int.Parse(Arg("--pid"));
        hostExe = Arg("--exe");
        testRoot = root;
        fixtures = Arg("--fixtures");
        var output = Arg("--out");
        outputDir = Path.GetDirectoryName(Path.GetFullPath(output))!;
        only = args.Contains("--only") ? CaseCatalog.Expand(Arg("--only").Split(',')) : null;
        var started = DateTime.UtcNow;
        var environmentError = (string?)null;
        try
        {
            var endpointFile = Path.Combine(root, "automation-endpoint.txt");
            endpoint = await ReadEndpointFile(endpointFile) ?? throw new InvalidOperationException("the test host never published its endpoint");

            using (var probe = await Connect())
            {
                var init = await Initialize(probe, "e2e-probe");
                if ((string?)init["server"]?["buildType"] != BuildTypes.AutomationTestHost)
                    throw new InvalidOperationException($"connected to a '{init["server"]?["buildType"]}' build, not the automation test host");
                await WaitForWindow(probe);
            }

            await Case("S0 open, write, read back exactly, save bytes, stale write refused", S0);
            await Case("S1 a real keystroke is an edit of the reader: revision advances, the text is in", S1);
            await Case("R01 a write held after its flush while the reader switches tabs never writes the other document", R01);
            await Case("R02 undo and redo stay in their own document", R02);
            await Case("R03 a keystroke while a saving write is held: the saved file is the written revision, the keystroke is kept", R03);
            await Case("B01 settings: one window's change reaches the other window; window modes stay with their window", B01);
            await Case("S2 settings.set reaches every window and the settings file; a stale revision is refused", S2);
            await Case("S3 the newer settings: language, tab size, compact mode and word count reach the window and go back", S3);
            await Case("V01 window.setView: source mode with the outline in a restored 1100x720 window, then reading; a mode switch waits for a held write", V01);
            await Case("N01 back from the settings page (the editor page loads again), a file opened at once is read and written without a timeout", N01);
            await Case("K01 a font size changed through the API while the reader types: the keys still reach the page", K01);
            await Case("B02 an open settings page shows a font size changed from elsewhere", B02);
            await Case("F01 a written document with protected payloads keeps every one of them through a real first keystroke", F01);
            await Case("F02 the first-edit warning for unsafe HTML stays briefly, then closes by itself", F02);
            await Case("M01 a written text survives visual, reading and source mode switches byte for byte, then saves exactly", M01);
            await Case("P01 awaitPresentation: a revealed write in a background tab of a minimized window reports it drawn; it needs reveal", P01);
            await Case("W01 the editor page reloads after it applied a write and before the commit: nothing is committed, host and page agree", W01);
            await Case("W02 a mode switch while a write is held after the page applied it: the write commits and every mode shows it", W02);
            await Case("W03 the tab switched away and back while a write is held: refused and restored; switched away only: committed into the tab", W03);
            await Case("MC01 typedownctl mcp as its own process: read, replace text with reveal, a stale revision is a conflict that says to read again", MC01);
            await Case("EX01 the PowerShell and Python client examples edit through the real pipe", EX01);
            await Case("EQ01 the equivalence scenario (compared with the Uno edition's answers offline)", EQ01);
            await Case("R04 untitled and background documents come back from their backups after a kill, each its own", R04);
            await Case("C01 document.close: a saved document closes, an unsaved one (a fresh keystroke too) is refused, the last one leaves an empty document", C01);
            await Case("Q02 a window closed at once after it opened (its web view still being created): the process lives on", Q02);
            await Case("D01 the window's UI thread runs every callback posted to it, from many threads at once", D01);
            await Case("K02 Ctrl+, twice opens and closes the settings: the caret and the keyboard are where they were", K02);
            await Case("K04 a window just opened takes the keys at once: a letter typed without a click reaches the document", K04);
            await Case("K05 a new tab (the + button, Ctrl+N) and a tab clicked in the strip take the keys at once, without a click in the text", K05);
            await Case("K06 after closing a tab, a menu command, a mode switch, an outline jump, closing the find bar or a dialog, and Ctrl+Tab, the keys go to the document without a click", K06);
            await Case("K03 the same with an untitled document (no per-file caret memory)", K03);
            await Case("FS01 in full screen the main page starts at the top edge of the screen", FS01);
            await Case("FS02 out of full screen the window is dragged by its title bar again at once (with the title row, and in compact mode by the menu row)", FS02);
            await Case("PU01 PlantUML is not drawn by default (nothing goes to plantuml.com, the block says so); turned on it is, by the server set if one is, off again it is not", PU01);
            await Case("RV01 reveal: \"change\" scrolls a change off screen into view, the caret where it was; \"document\" leaves the page", RV01);
            await Case("IU01 File > Upload local images (PowerShell): each file uploaded once, every use replaced in one undo step, web, missing and code left alone", IU01);
            await Case("IU03 a window that has not used the upload service yet finds the configuration at once (it said none until the database was read)", IU03);
            await Case("IU02 File > Upload local images to an S3 bucket (rclone serve s3): signed PUT, the object reads back, a wrong secret changes nothing, the secret is not stored in plain text", IU02);
            await Case("IU04 pictures saved and uploaded under the right names: a pasted screenshot goes up as image.png, a second different picture of the same name is copied as \"name (2)\" and the document says so, a new upload configuration starts enabled", IU04);
            await Case("IN01 several image files dropped at once: each in a paragraph of its own, in the drop's order, one undo step", IN01);
            await Case("VI01 Vim keys in source mode: real keys edit (dd, A, Esc), Ctrl+V reaches Vim as block visual, u undoes, :w saves", VI01);
            await Case("VI02 Vim keys in reading mode: G, gg, ]] and Ctrl+D move the page", VI02);
            await Case("TC01 a tab closed with its x button: the outline shows the headings of the tab shown next", TC01);
            await Case("TC02 a tab closed while its neighbour is not the tab used last: the outline is the shown tab's, with long documents", TC02);
            await Case("SE01 the startup action chosen on the settings page is in the settings file", SE01);
            await Case("WP02 pasted web pictures: one behind hotlink protection is fetched with the page as referrer, a page that is not a picture is kept on the web and said, and an untitled document saved into a folder takes its pictures along", WP02);
            await Case("EX02 an export says where it was written, with buttons to open it, and goes when closed; with no action after export, nothing shows", EX02);
            await Case("TB01 the table toolbar's Resize table opens with the table's own size; Cancel leaves the table as it was; OK resizes it", TB01);
            await Case("TH01 a custom theme colours the page in visual, reading and source mode, and a change shows at once in each", TH01);
            await Case("RD01 reading mode: the context menu offers copying and selecting only, Copy works on a selection, copy as plain text leaves the Markdown out, Ctrl+Z changes nothing", RD01);
            await Case("RD02 reading mode: Focus mode and Typewriter mode in the View menu are disabled and drawn fainter than the entries that work, in Dark and Light, whether the menu was opened before or not", RD02);
            await Case("SF01 a window opening under the dark theme shows no light frame in its first two seconds on screen", SF01);
            await Case("DG02 every diagram of the showcase document, written into a window just opened, is drawn once", DG02);
            await Case("IM01 the showcase's checkerboard, after View > Source mode and back, is drawn and not failed when clicked", IM01);
            await Case("CP01 Copy pasted into Word: pictures at absolute file:/// addresses, a name and an alt text with brackets, an SVG sized in pt, a JPEG", CP01);
            await Case("TH03 the side pane marks what is chosen (the bar under Files/Outline, the outline's and the folder tree's row pill) in a custom theme's accent, and in the system accent again without one", TH03);
            await Case("TH06 a custom theme switched back to Light or Dark: the editor page, the line beside the side pane, the side pane and the status bar are drawn as before, and the editor is dark under Dark", TH06);
            await Case("KY01 Ctrl+Z and Ctrl+Y are the host's undo and redo however close Ctrl and the letter come, in the visual editor and in source mode", KY01);
            await Case("ED01 in the visual editor Ctrl+Shift+Right selects by words and Ctrl+Shift+Down to the end of the paragraph, Ctrl+Left/Right move without selecting, Ctrl+F opens the find bar, and the wheel scrolls the page", ED01);
            await Case("ED02 Shift+Enter and Alt+Enter in a table cell put a line break in that cell", ED02);
            await Case("TH02 View > Theme > Reload themes finds a new theme file and a renamed one; the window draws in the custom theme's base whatever the built-in setting says", TH02);
            await Case("WP01 a web page pasted: headings with #, code fenced with its language and without line numbers, Google Docs bold and italic, late-loaded and relative pictures by their real addresses; with Insert web image copying, its pictures are copied beside the document and their addresses replaced", WP01);
            await Case("ER01 a page message handler that throws stays subscribed and the message still reaches the other handlers; an exception out of an event handler XAML calls is logged and the app goes on", ER01);
            await Case("DG01 a dialog wears the custom theme (its surface and text colours), and the system's colours again without it", DG01);
            await Case("PM01 the Paragraph menu's checks follow the caret: Table stays unchecked after its dialog is cancelled, Quote and Table are checked with the caret in a quote and a table", PM01);
            await Case("OL02 two long documents scrolled to their middle, switched between: the outline does not mark another heading of the document being left", OL02);
            await Case("LD01 a load the editor gives back rewritten is loaded once more and keeps the file's text; one rewritten again is taken after that one retry, not retried without end", LD01);
            // An edition's own cases (Edition.<name>.cs beside this file, in the edition's repository); none here.
            var editionCases = new List<(string Name, Func<List<string>, Task> Run)>();
            EditionCases(editionCases);
            foreach (var (name, run) in editionCases) await Case(name, run);
            // Measured, and last: they start and end test hosts of their own (only when named, as in the WinUI 3 build).
            await Case("PF01 measured: a 300 KB document opened, typed into and saved; the memory with two windows", PF01);
            await Case("PF02 measured: a cold start with a document on the command line, then the window closed", PF02);
            // Last: it ends the test host.
            await Case("Q01 two windows closed one after the other: the process exits (it stayed, headless)", Q01);
        }
        catch (Exception e)
        {
            environmentError = e.Message;
        }
        var failed = results.Count(r => !(bool)r["passed"]!);
        var summary = new JObject
        {
            ["started"] = started.ToString("O"),
            ["seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 1),
            ["environmentError"] = environmentError,
            ["passed"] = results.Count(r => (bool)r["passed"]!),
            ["failed"] = failed,
            ["cases"] = new JArray(results),
        };
        File.WriteAllText(output, summary.ToString(Formatting.Indented));
        Console.WriteLine(summary.ToString(Formatting.Indented));
        return environmentError != null ? 3 : failed > 0 ? 1 : 0;
    }

    /// <summary>Connects, retrying while the endpoint is not there yet (a host that has just started), up to ~30 s.</summary>
    private static async Task<Client> Connect()
    {
        for (var attempt = 0; ; attempt++)
        {
            await Task.Yield();
            try { return new Client(endpoint); }
            catch (TimeoutException) when (attempt < 30) { }
        }
    }

    private static readonly string[] AllScopes = { Scopes.AppRead, Scopes.DocumentRead, Scopes.DocumentWrite, Scopes.DocumentSave, Scopes.WindowFocus, Scopes.SettingsRead, Scopes.SettingsWrite, Scopes.WindowView };

    private static Task<JToken> Initialize(Client c, string name) => c.Call("system.initialize", new
    {
        apiVersion = 1,
        client = new { id = "0f0f0f0f-0000-4000-8000-00000000e2e0", name, version = "1" },
        requestedScopes = AllScopes,
    });

    private static async Task<Client> Session(string name)
    {
        var c = await Connect();
        await Initialize(c, name);
        return c;
    }

    private static async Task WaitForWindow(Client c)
    {
        for (var i = 0; i < 300; i++)
        {
            var windows = (JArray)(await c.Call("window.list"))["windows"]!;
            if (windows.Count > 0) return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("the test host opened no window");
    }

    private sealed class CaseFailed : Exception { public CaseFailed(string m) : base(m) { } }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new CaseFailed(what);
    }

    private static async Task Case(string name, Func<List<string>, Task> body)
    {
        if (only != null && !only.Any(o => name.StartsWith(o + " "))) return;
        // Pictures (SHOT...) are taken only when asked for by name: never part of a run of every case.
        if ((name.StartsWith("SHOT") || name.StartsWith("PF")) && only == null) return;
        var notes = new List<string>();
        // A person's mouse or keys a moment ago would be in the case's way: it waits for the desktop to be left alone.
        var quiet = await WaitForQuietDesktop(1000);
        if (quiet != null) notes.Add(quiet);
        // Dialogs left open closed, and the case and its purpose in the test window's title for the whole case.
        try
        {
            using var labeller = await Session("e2e label").WaitAsync(TimeSpan.FromSeconds(5));
            // A dialog an earlier case left open holds every click back: closed first, and noted.
            var closed = (JArray?)(await labeller.Call("test.dialogs.dismiss", new { }).WaitAsync(TimeSpan.FromSeconds(10)))["closed"];
            if (closed != null && closed.Count > 0) notes.Add("closed a dialog an earlier case left open: " + string.Join(", ", closed));
            await labeller.Call("test.app.caseLabel", new { label = CaseCatalog.Label(name.Split(' ')[0]) }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception) { /* no test host yet, or one without the hooks */ }
        var watch = Stopwatch.StartNew();
        try
        {
            // Every case is bounded: a hang is a failure with what it got to, never a run that never ends.
            var run = body(notes);
            // Pictures in several languages take their time; any other case that runs past two minutes is stuck.
            var limit = name.StartsWith("SHOT") ? 900 : 120;
            if (await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(limit))) != run) throw new CaseFailed($"the case did not finish within {limit} s");
            await run;
            results.Add(new JObject { ["name"] = name, ["passed"] = true, ["ms"] = watch.ElapsedMilliseconds, ["notes"] = new JArray(notes) });
        }
        catch (Exception e)
        {
            // A call that got no answer: what the screen showed (a dialog holding the window, say) is the first clue.
            if (e is TimeoutException || e is CaseFailed && e.Message.Contains("did not finish"))
            {
                notes.Add("screen at the timeout: " + Screenshot(name.Split(' ')[0] + "-timeout-" + DateTime.Now.ToString("HHmmss")));
                // And the host's threads at that moment: a window's UI thread stuck shows nothing in the log.
                notes.Add("host dump at the timeout: " + HangDump(name.Split(' ')[0] + "-timeout-" + DateTime.Now.ToString("HHmmss")));
            }
            else
            {
                // Any other failure: the screen and where the keyboard was, for what the case could not say itself.
                notes.Add("at the failure: " + FailureEvidence(name.Split(' ')[0]));
            }
            results.Add(new JObject { ["name"] = name, ["passed"] = false, ["ms"] = watch.ElapsedMilliseconds, ["error"] = e is CaseFailed ? e.Message : e.ToString(), ["notes"] = new JArray(notes) });
        }
    }

    // What one failure used to take with it: a page message handler that threw was detached for good (the outline or
    // the selection stopped following the page), and an exception out of a click handler ended the process.
    private static async Task ER01(List<string> notes)
    {
        using var c = await Session("e2e ER01");
        var id = await Open(c, Fixture("er01.md", "# ER01\n"));
        var windowId = await WindowIdOf(c, id);
        var got = await c.Call("test.events.throwingHandler", new { windowId });
        notes.Add("two messages, a handler throwing on the first: " + got.ToString(Newtonsoft.Json.Formatting.None));
        Check((int)got["throwing"]! == 2, "a handler that threw on the first message still gets the second");
        Check((int)got["other"]! == 2, "the other handler gets both messages");

        var logs = Path.Combine(testRoot, "logs");
        var before = Directory.Exists(logs) ? Directory.GetFiles(logs, "*XamlUnhandledException*").Length : 0;
        await c.Call("test.app.throwUnhandled", new { windowId });
        // A process the exception ends takes a few seconds to go (Windows Error Reporting first).
        await Task.Delay(4000);
        var alive = !Process.GetProcessById(hostPid).HasExited;
        notes.Add($"after an exception out of an event handler: process alive={alive}");
        Check(alive, "an exception out of an event handler does not end the app");
        await c.Call("system.ping");
        var after = Directory.Exists(logs) ? Directory.GetFiles(logs, "*XamlUnhandledException*").Length : 0;
        Check(after > before, "the exception is in the log");
    }

    // A web page pasted as Windows hands it over (CF_HTML with the page's address), its pictures on a small server here.
    private static async Task WP01(List<string> notes)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAIAAAB/FOjAAAAAE0lEQVR4nGPQqDhBEmIY1UALDQCJj7QBup2ubwAAAABJRU5ErkJggg==");
        var server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        server.Start();
        var port = ((System.Net.IPEndPoint)server.LocalEndpoint).Port;
        var served = new List<string>();
        var serving = Task.Run(async () =>
        {
            while (true)
            {
                System.Net.Sockets.TcpClient client;
                try { client = await server.AcceptTcpClientAsync(); } catch { return; }
                using (client)
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream);
                    var request = await reader.ReadLineAsync() ?? "";
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                    lock (served) served.Add(request);
                    var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: {png.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(header);
                    await stream.WriteAsync(png);
                }
            }
        });
        using var c = await Session("e2e WP01");
        var file = Fixture("wp01.md", "# WP01\n\n");
        var folder = Path.Combine(Path.GetDirectoryName(file)!, "wp01-images");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var id = await Open(c, file);
        var windowId = await WindowIdOf(c, id);
        var site = $"http://127.0.0.1:{port}";
        var fragment = "<h2>Pasted</h2><p>Some <span style=\"font-weight:700\">bold</span> text.</p>"
            + "<pre class=\"prettyprint\"><code class=\"language-python\">print(1)\nprint(2)\n</code><ul class=\"pre-numbering\"><li>1</li><li>2</li></ul></pre>"
            + $"<p><img src=\"{site}/direct.png\" alt=\"direct\"></p>"
            + $"<p><img src=\"data:image/gif;base64,R0lGODlhAQABAAAAACw=\" data-original=\"{site}/lazy.png\" alt=\"lazy\"></p>"
            + "<p><img src=\"img/relative.png\" alt=\"relative\"></p>";
        var html = $"Version:0.9\r\nStartHTML:0\r\nEndHTML:0\r\nStartFragment:0\r\nEndFragment:0\r\nSourceURL:{site}/article/page.html\r\n<html><body><!--StartFragment-->{fragment}<!--EndFragment--></body></html>";
        try
        {
            // The page showing this document before anything is pasted: pasted at once after the open, it landed in the
            // document the page still held, and the load that followed put "# WP01" back over it (seen on Windows 10).
            await WaitForPage(c, id, t => t.Contains("# WP01"), "the opened document");
            // Kept on the web: the conversion alone.
            await c.Call("test.images.webAction", new { windowId, action = "none" });
            await c.Call("test.editor.focus", new { windowId });
            await c.Call("test.paste.html", new { windowId, html });
            string text = "";
            await Eventually(async () => (text = (string)(await Get(c, id))["text"]!).Contains("relative"), 5000);
            notes.Add("kept on the web: " + text.Replace("\n", "\\n"));
            Check(text.Contains("## Pasted"), "a heading is written with #");
            Check(text.Contains("**bold**"), "bold given by style is bold");
            Check(text.Contains("```python\nprint(1)\nprint(2)\n```") && !text.Contains("- 1"), "code is fenced with its language, without its line numbers");
            Check(text.Contains($"]({site}/direct.png)") && text.Contains($"]({site}/lazy.png)") && text.Contains($"]({site}/article/img/relative.png)"), "pictures by their real addresses: the late-loaded one's and the relative one's made absolute");

            // Copied beside the document: the three pictures fetched and their addresses replaced.
            await c.Call("document.replace", new { documentId = id, baseRevision = await Revision(c, id), text = "# WP01\n\n", reveal = "document", normalizationPolicy = "allowUnknown" });
            await c.Call("test.images.webAction", new { windowId, action = "copy", path = "./wp01-images" });
            await c.Call("test.editor.focus", new { windowId });
            await c.Call("test.paste.html", new { windowId, html });
            await Eventually(async () => !(text = (string)(await Get(c, id))["text"]!).Contains(site) && text.Contains("relative"), 8000);
            var copied = Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;
            notes.Add($"copied: {copied} file(s); served {string.Join(", ", served)}; text: {text.Replace("\n", "\\n")}");
            Check(!text.Contains(site), "with Insert web image copying, no picture is left on the web");
            Check(copied == 3 && text.Contains("](./wp01-images/") && !text.Contains("wp01-images\\"), "the three pictures are copied beside the document and the document points at them, with /");
            Check(text.StartsWith("# WP01\n\n## Pasted"), "the pasted heading is a block of its own after the document's");
        }
        finally
        {
            try { await c.Call("test.images.webAction", new { windowId, action = "none" }); } catch { }
            try
            {
                await c.Call("document.replace", new { documentId = id, baseRevision = await Revision(c, id), text = "# WP01\n\n", reveal = "document", normalizationPolicy = "allowUnknown" });
                await c.Call("document.save", new { documentId = id, baseRevision = await Revision(c, id) });
            }
            catch { }
            server.Stop();
        }
    }

    // A restored tab once came back from the editor with a long document rewritten (its tables laid out again, its raw
    // HTML changed) and the host took that as the file's text. The test host has the next loads come back rewritten.
    private static async Task LD01(List<string> notes)
    {
        using var c = await Session("e2e LD01");
        var other = await Open(c, Fixture("ld01-other.md", "# other\n"));
        var windowId = await WindowIdOf(c, other);
        const string text = "# LD01\n\n|a|b|\n|-|-|\n|1|22|\n\n<div align=\"center\">raw</div>\n";
        var retries = LogLines("loading the text once more").Count;

        await c.Call("test.editor.rewriteLoads", new { windowId, count = 1 });
        var id = await Open(c, Fixture("ld01.md", text));
        string Body(JToken doc) => ((string)doc["text"]!).Replace("\r\n", "\n");
        JToken doc = await Get(c, id);
        await Eventually(async () => LogLines("loading the text once more").Count > retries, 5000);
        await Task.Delay(800);
        doc = await Get(c, id);
        notes.Add($"rewritten once: saved={doc["saved"]}, {Body(doc).Length} chars (file {text.Length}); log: {LogLines("the editor rewrote the text it was given").LastOrDefault()?.Trim()}");
        Check(Body(doc) == text, "rewritten once: the document holds the file's text, not the rewrite");
        Check((bool)doc["saved"]!, "rewritten once: the document is still saved");
        Check(LogLines("loading the text once more").Count == retries + 1, "rewritten once: loaded once more");
        Check(LogLines("the editor rewrote the text it was given").Any(l => l.Contains("table 1,")), "the log says what kind of lines were rewritten (one table row)");

        // Rewritten every time: one retry, then taken as it is (the editor's own normalization, as before). A document's
        // first load is the one checked (a tab switched back to keeps what its first load gave): a new one.
        retries = LogLines("loading the text once more").Count;
        await c.Call("test.editor.rewriteLoads", new { windowId, count = 2 });
        var again = await Open(c, Fixture("ld01-again.md", text));
        await Eventually(async () => LogLines("rewritten again; taken").Count > 0, 6000);
        await Task.Delay(800);
        doc = await Get(c, again);
        notes.Add($"rewritten twice: {Body(doc).Length} chars, retries {LogLines("loading the text once more").Count - retries}");
        Check(LogLines("loading the text once more").Count == retries + 1, "rewritten twice: one retry only");
        Check(Body(doc).Contains("rewritten"), "rewritten again: taken as the editor gave it");
        await c.Call("test.editor.rewriteLoads", new { windowId, count = 0 });
    }

    // ---- helpers over the API ----

    private static string Fixture(string name, string text)
    {
        var path = Path.Combine(fixtures, name);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        return path;
    }

    private static string Disk(string path) => new UTF8Encoding(false).GetString(File.ReadAllBytes(path));

    private static async Task<string> Open(Client c, string path) => (string)(await c.Call("document.open", new { path, reveal = "document" }))["documentId"]!;

    private static async Task<JToken> Get(Client c, string id) => await c.Call("document.get", new { documentId = id, consistency = "latest", include = new[] { "text" } });

    private static async Task<long> Revision(Client c, string id) => (long)(await c.Call("document.get", new { documentId = id, consistency = "latest" }))["revision"]!;

    // ---- cases ----

    private static async Task S0(List<string> notes)
    {
        using var c = await Session("e2e S0");
        var path = Fixture("s0.md", "# S0\n\nalpha\n");
        var id = await Open(c, path);
        var doc = await Get(c, id);
        Check((string)doc["text"]! == "# S0\n\nalpha\n", "the opened text is the file's text");
        var r = (long)doc["revision"]!;
        var written = await c.Call("document.replace", new { documentId = id, baseRevision = r, text = "# S0\n\nalpha written\n", reveal = "document", normalizationPolicy = "allowUnknown" });
        Check((long)written["revision"]! == r + 1, $"the write advanced the revision ({written["revision"]} after {r})");
        notes.Add($"normalization {written["normalization"]?["pendingNormalization"]}");
        Check((string)(await Get(c, id))["text"]! == "# S0\n\nalpha written\n", "latest read returns exactly what was written");
        Check(await c.ErrorCode("document.replace", new { documentId = id, baseRevision = r, text = "stale\n" }) == -32012, "a stale base revision is a revision_conflict");
        await c.Call("document.save", new { documentId = id, baseRevision = r + 1 });
        Check(Disk(path) == "# S0\n\nalpha written\n", "the file holds exactly the saved revision");
        // Saving goes through a hidden, temporary-marked temp file renamed over the target: the target must not keep
        // those marks (1.2.27-1.2.30 left every saved file hidden, and sync clients skip such files).
        var attributes = File.GetAttributes(path);
        Check((attributes & (FileAttributes.Hidden | FileAttributes.Temporary)) == 0, $"the saved file is neither hidden nor temporary ({attributes})");
        Check(WindowTitle().Contains("AUTOMATION TEST HOST"), $"the title marks the test host ({WindowTitle()})");
    }

    private static async Task S1(List<string> notes)
    {
        using var c = await Session("e2e S1");
        var id = await Open(c, Fixture("s1.md", "# S1\n\nbefore\n"));
        await c.Call("document.focus", new { documentId = id });
        var before = await Get(c, id);
        await TypeInto(c, id, "Q");
        await WaitForPage(c, id, t => t.Contains('Q'), "the keystroke");
        var after = await Get(c, id);
        var text = (string)after["text"]!;
        Check(text.Contains('Q') && text.StartsWith("# S1\n\nbefore"), $"the keystroke is in the text, the rest intact ({JsonConvert.SerializeObject(text)})");
        Check(!(bool)after["saved"]!, "the keystroke leaves the document unsaved");
        Check((long)after["revision"]! > (long)before["revision"]!, "the keystroke advanced the revision");
    }

    private static async Task R01(List<string> notes)
    {
        using var writer = await Session("e2e R01 writer");
        using var driver = await Session("e2e R01 driver");
        var pathA = Fixture("r01-a.md", "# A\n\nalpha\n");
        var pathB = Fixture("r01-b.md", "# B\n\nbeta\n");
        var b = await Open(driver, pathB);
        var a = await Open(driver, pathA);
        var rA = await Revision(driver, a);
        var barrier = (string)(await driver.Call("test.barrier.arm", new { point = EditBarrierPoints.BeforeFlushReply, documentId = a }))["barrierId"]!;
        var write = writer.Call("document.replace", new { documentId = a, baseRevision = rA, text = "# A\n\nalpha by API\n", save = true, normalizationPolicy = "allowUnknown" });
        var hit = await driver.Call("test.barrier.waitHit", new { barrierId = barrier, timeoutMs = 20000 });
        Check((bool)hit["hit"]!, "the write reached the barrier after its flush");
        await driver.Call("document.focus", new { documentId = b }); // the reader switches to B meanwhile
        await driver.Call("test.barrier.release", new { barrierId = barrier });
        string outcome;
        try { var result = await write; outcome = $"committed r{result["revision"]} saved={result["saved"]}"; }
        catch (JsonRpcRemoteException e) { outcome = $"refused {e.ErrorData?["kind"]}"; }
        notes.Add(outcome);
        Check(Disk(pathB) == "# B\n\nbeta\n", "B's file is untouched");
        var diskA = Disk(pathA);
        Check(diskA == "# A\n\nalpha\n" || diskA == "# A\n\nalpha by API\n", $"A's file is either the old text or the written revision ({JsonConvert.SerializeObject(diskA)})");
        Check((string)(await Get(driver, b))["text"]! == "# B\n\nbeta\n", "B's text in the window is untouched");
    }

    private static async Task R02(List<string> notes)
    {
        using var c = await Session("e2e R02");
        var a = await Open(c, Fixture("r02-a.md", "# A\n\none\n"));
        var b = await Open(c, Fixture("r02-b.md", "# B\n\ntwo\n"));
        await c.Call("document.replace", new { documentId = a, baseRevision = await Revision(c, a), text = "# A\n\none edited\n", reveal = "document", normalizationPolicy = "allowUnknown" });
        await c.Call("document.replace", new { documentId = b, baseRevision = await Revision(c, b), text = "# B\n\ntwo edited\n", reveal = "document", normalizationPolicy = "allowUnknown" });
        await c.Call("document.undo", new { documentId = a, baseRevision = await Revision(c, a), reveal = "document" });
        Check((string)(await Get(c, a))["text"]! == "# A\n\none\n", "undo in A restores A");
        Check((string)(await Get(c, b))["text"]! == "# B\n\ntwo edited\n", "B is not touched by A's undo");
        await c.Call("document.undo", new { documentId = b, baseRevision = await Revision(c, b), reveal = "document" });
        Check((string)(await Get(c, b))["text"]! == "# B\n\ntwo\n", "undo in B restores B");
        await c.Call("document.redo", new { documentId = a, baseRevision = await Revision(c, a), reveal = "document" });
        Check((string)(await Get(c, a))["text"]! == "# A\n\none edited\n", "redo in A brings A's edit back");
        Check((string)(await Get(c, b))["text"]! == "# B\n\ntwo\n", "B is not touched by A's redo");
    }

    private static async Task R03(List<string> notes)
    {
        using var writer = await Session("e2e R03 writer");
        using var driver = await Session("e2e R03 driver");
        var path = Fixture("r03.md", "# R03\n\nstart\n");
        var id = await Open(driver, path);
        await driver.Call("document.focus", new { documentId = id });
        var r = await Revision(driver, id);
        var barrier = (string)(await driver.Call("test.barrier.arm", new { point = EditBarrierPoints.AfterEditorMutationBeforeReport, documentId = id }))["barrierId"]!;
        var write = writer.Call("document.replace", new { documentId = id, baseRevision = r, text = "# R03\n\nwritten\n", save = true, normalizationPolicy = "allowUnknown" });
        Check((bool)(await driver.Call("test.barrier.waitHit", new { barrierId = barrier, timeoutMs = 20000 }))["hit"]!, "the write reached the barrier with the page already changed");
        await TypeIntoWindow(driver, (string)(await driver.Call("document.get", new { documentId = id, consistency = "snapshot" }))["windowId"]!, "Z"); // the reader's keystroke on top of the write, while the host has not committed it
        await WaitForPage(driver, id, t => t.Contains('Z'), "the keystroke");
        await driver.Call("test.barrier.release", new { barrierId = barrier });
        var result = await write;
        var saved = Disk(path);
        notes.Add($"write r{result["revision"]} saved={result["saved"]}");
        Check(saved == "# R03\n\nwritten\n", $"the file holds exactly the written revision ({JsonConvert.SerializeObject(saved)})");
        var now = await Get(driver, id);
        Check(((string)now["text"]!).Contains('Z'), "the keystroke is not lost");
        Check((long)now["revision"]! > (long)result["revision"]!, "the keystroke is a later revision than the write");
        Check(!(bool)now["saved"]!, "the keystroke leaves the document unsaved");
    }

    /// <summary>
    /// settings.set as a client should do it: against the revision just read, again after a conflict. The revision also
    /// advances when the app stores a window's placement (a moved or resized window), so a conflict can come from that.
    /// </summary>
    private static async Task<long> SetSetting(Client c, string key, JToken value)
    {
        for (var attempt = 0; ; attempt++)
        {
            var revision = (long)(await c.Call("settings.get", new { keys = new[] { key } }))["settingsRevision"]!;
            try { return (long)(await c.Call("settings.set", new { key, value, baseSettingsRevision = revision }))["settingsRevision"]!; }
            catch (JsonRpcRemoteException e) when ((string?)e.ErrorData?["kind"] == "revision_conflict" && attempt < 3) { }
        }
    }

    private static async Task S3(List<string> notes)
    {
        using var c = await Session("e2e S3");
        var windowId = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        var keys = new[] { "ui.language", "editor.tabSize", "appearance.compactMode", "status.wordCount" };
        var got = await c.Call("settings.get", new { keys });
        var original = (JObject)got["values"]!;
        var revision = (long)got["settingsRevision"]!;
        async Task<JToken> Setting(string name) => (await c.Call("test.settings.get", new { windowId, name }))["value"]!;
        async Task Set(string key, JToken value) => revision = await SetSetting(c, key, value);
        async Task<string> Menu() => string.Join(" ", ((JArray)(await c.Call("test.window.menuTitles", new { windowId }))["titles"]!).Select(t => (string)t!));
        string? menuBefore = null;
        try
        {
            var language = (string)original["ui.language"]! == "ja" ? "en" : "ja";
            menuBefore = await Menu();
            await Set("ui.language", language);
            Check((string)(await Setting("Language"))! == language, $"the window's language is {language}");
            // The menu bar follows without a restart (it kept the old language when the change came through the API).
            string menuAfter = menuBefore;
            for (var i = 0; i < 40 && (menuAfter == menuBefore || menuAfter == ""); i++) { await Task.Delay(250); try { menuAfter = await Menu(); } catch (JsonRpcRemoteException) { } }
            notes.Add($"menu: {menuBefore} -> {menuAfter}");
            Check(menuAfter != menuBefore && menuAfter != "", $"the menu bar follows the language ({menuBefore} -> {menuAfter})");
            // Menus made again in source mode still follow it: the language is changed back while the window is in source
            // mode, and Paragraph and Format stay hidden; back in visual mode all five are there.
            async Task<int> MenuCount() => ((JArray)(await c.Call("test.window.menuTitles", new { windowId }))["titles"]!).Count;
            await c.Call("window.setView", new { windowId, mode = "source" });
            await Set("ui.language", original["ui.language"]!);
            var menuBack = "";
            // In source mode the bar shows three of the five titles: "back" is no longer the other language's menu.
            for (var i = 0; i < 40 && (menuBack == menuAfter || menuBack == ""); i++) { await Task.Delay(250); try { menuBack = await Menu(); } catch (JsonRpcRemoteException) { } }
            var inSource = await MenuCount();
            await c.Call("window.setView", new { windowId, mode = "visual" });
            var inVisual = 0;
            for (var i = 0; i < 20 && inVisual != 5; i++) { await Task.Delay(150); inVisual = await MenuCount(); }
            Check(menuBack != menuAfter && menuBack != "" && inSource == 3 && inVisual == 5,
                $"menus made again in source mode: {inSource} shown ({menuBack}); in visual mode {inVisual}");
            var tab = (int)original["editor.tabSize"]! == 2 ? 4 : 2;
            await Set("editor.tabSize", tab);
            Check((int)await Setting("TabSize") == tab, $"tab size {tab}");
            var compact = !(bool)original["appearance.compactMode"]!;
            await Set("appearance.compactMode", compact);
            Check((bool)await Setting("AppCompactMode") == compact, $"compact mode {compact}");
            await Set("status.wordCount", "characters");
            Check((int)await Setting("WordCountMethod") == 0, "word count by characters");
            // Valid in the catalog (the Uno edition has them), refused by this edition after the revision check.
            async Task<long> Current() => (long)(await c.Call("settings.get", new { keys = new[] { "ui.language" } }))["settingsRevision"]!;
            Check(await c.ErrorKind("settings.set", new { key = "status.wordCount", value = "paragraphs", baseSettingsRevision = await Current() }) == "setting_invalid",
                "paragraphs is not a word count this edition has");
            Check(await c.ErrorKind("settings.set", new { key = "markdown.listIndentation", value = "tab", baseSettingsRevision = await Current() }) == "setting_invalid",
                "tab is not a list indentation this edition has");
            var described = (JArray)(await c.Call("settings.describe"))["settings"]!;
            notes.Add($"{described.Count} settings described; language {language}, tab {tab}, compact {compact}");
        }
        finally
        {
            foreach (var key in keys) await Set(key, original[key]!);
            // The language put back rebuilds the main page and reloads the editor page once more: the case ends only when
            // that is done, or the next case starts on a page about to go (a write to it was refused, editor_not_ready).
            if (menuBefore != null)
            {
                var menu = "";
                for (var i = 0; i < 40 && menu != menuBefore; i++) { await Task.Delay(250); try { menu = await Menu(); } catch (JsonRpcRemoteException) { } }
                var active = (string?)((JArray)(await c.Call("window.list"))["windows"]!).First(w => (string)w["windowId"]! == windowId)["activeDocumentId"];
                if (active != null) await c.Call("document.get", new { documentId = active, consistency = "latest" });
            }
        }
        var back = (JObject)(await c.Call("settings.get", new { keys }))["values"]!;
        Check(JToken.DeepEquals(back, original), $"every setting is back ({back.ToString(Formatting.None)})");
    }

    private static async Task V01(List<string> notes)
    {
        using var c = await Session("e2e V01");
        using var writer = await Session("e2e V01 writer");
        using var viewer = await Session("e2e V01 viewer");
        const string text = "# V01\n\n## One\n\nfirst\n\n## Two\n\nsecond\n";
        var id = await Open(c, Fixture("v01.md", text));
        await c.Call("document.focus", new { documentId = id });
        var windowId = await WindowIdOf(c, id);
        // Mode, side pane, status bar and bounds are the window's own state, not settings a client can set: changing them
        // (and the placement the window then saves) leaves settingsRevision as it was.
        var settingsBefore = (long)(await c.Call("settings.get", new { keys = new[] { "editor.fontSize" } }))["settingsRevision"]!;
        var window = await WindowOf(windowId, c);
        var before = await c.Call("window.getView", new { windowId });
        var revision = await Revision(c, id);
        async Task<JToken> Setting(string name) => (await c.Call("test.settings.get", new { windowId, name }))["value"]!;
        try
        {
            ShowWindow(window, 3); // SW_MAXIMIZE: bounds restore the window first
            await Task.Delay(300);
            var v = await c.Call("window.setView", new { windowId, mode = "source", sidePane = new { open = true, page = "outline" }, bounds = new { x = 60, y = 50, width = 1100, height = 720 } });
            Check((string)v["mode"]! == "source" && (bool)v["sidePane"]!["open"]! && (string)v["sidePane"]!["page"]! == "outline", $"the reply is the new view ({v.ToString(Formatting.None)})");
            Check(!(bool)v["maximized"]!, "the maximized window was restored");
            GetWindowRect(window, out var rect);
            Check(rect.Left == 60 && rect.Top == 50 && rect.Right - rect.Left == 1100 && rect.Bottom - rect.Top == 720,
                $"the window is at 60,50 1100x720 ({rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top})");
            Check((bool)await Setting("SourceCode") && (bool)await Setting("SidePaneOpen") && (int)await Setting("SidePaneIndex") == 1, "the window's settings say source mode with the outline");
            Check(await WaitForPage(c, id, t => t == text, "source mode") == text, "source mode shows the text");

            v = await c.Call("window.setView", new { windowId, mode = "reading", sidePane = new { open = false } });
            Check((string)v["mode"]! == "reading" && (bool)await Setting("ReadOnly") && !(bool)await Setting("SourceCode"), "reading mode, source mode off");
            Check(!(bool)v["sidePane"]!["open"]!, "the side pane is closed");
            Check(await Revision(c, id) == revision, "no revision from a view change");

            // A mode switch waits for an automation write that is under way in the window's active document.
            const string written = "# V01\n\nwritten while the view waits\n";
            await c.Call("window.setView", new { windowId, mode = "visual" });
            var (write, barrier) = await HoldAfterApply(writer, c, id, written);
            var view = viewer.Call("window.setView", new { windowId, mode = "source" });
            await Task.Delay(700);
            Check(!view.IsCompleted, "the mode switch waits while the write is held");
            await c.Call("test.barrier.release", new { barrierId = barrier });
            var outcome = await Outcome(write);
            Check(outcome.StartsWith("committed"), $"the write commits ({outcome})");
            Check((string)(await view)["mode"]! == "source", "then the mode switches");
            Check(await WaitForPage(c, id, t => t == written, "source mode after the write") == written, "source mode shows the write");
            Check(await c.ErrorKind("window.setView", new { windowId, bounds = new { width = 100 } }) == "invalid_params", "a 100 pixel wide window is refused");
            notes.Add(outcome);
        }
        finally
        {
            var b = before["bounds"]!;
            await c.Call("window.setView", new
            {
                windowId, mode = (string)before["mode"]!, sidePane = new { open = (bool)before["sidePane"]!["open"]!, page = (string)before["sidePane"]!["page"]! },
                statusBar = (bool)before["statusBar"]!, bounds = new { x = (int)b["x"]!, y = (int)b["y"]!, width = (int)b["width"]!, height = (int)b["height"]! },
            });
        }
        // The window saves its placement a moment after a move or resize.
        await Task.Delay(3000);
        var settingsAfter = (long)(await c.Call("settings.get", new { keys = new[] { "editor.fontSize" } }))["settingsRevision"]!;
        notes.Add($"settings revision {settingsBefore} -> {settingsAfter} across the view changes");
        Check(settingsAfter == settingsBefore, "view changes and the saved window placement leave settingsRevision as it was");
    }

    private static async Task B01(List<string> notes)
    {
        using var c = await Session("e2e B01");
        var first = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        var second = (string)(await c.Call("test.window.open"))["windowId"]!;
        async Task<JToken> Value(string window, string name) => (await c.Call("test.settings.get", new { windowId = window, name }))["value"]!;
        async Task Until(string window, string name, Func<JToken, bool> ok, string what)
        {
            for (var i = 0; i < 100; i++) { if (ok(await Value(window, name))) return; await Task.Delay(50); }
            throw new CaseFailed($"{what} (still {await Value(window, name)})");
        }
        var font = (double)await Value(first, "FontSize") == 21 ? 19.0 : 21.0;
        await c.Call("test.settings.set", new { windowId = first, name = "FontSize", value = font });
        await Until(second, "FontSize", v => (double)v == font, $"the second window follows the first window's font size {font}");
        await c.Call("test.settings.set", new { windowId = second, name = "TextDirection", value = "rtl" });
        await Until(first, "TextDirection", v => (string)v! == "rtl", "the first window follows the second window's text direction");
        await c.Call("test.settings.set", new { windowId = second, name = "TextDirection", value = "auto" });
        await Until(first, "TextDirection", v => (string)v! == "auto", "and back");
        // Window-local: switching one window to source mode must not switch the other.
        var sourceBefore = (bool)await Value(second, "SourceCode");
        await c.Call("test.settings.set", new { windowId = first, name = "SourceCode", value = !sourceBefore });
        await Until(first, "SourceCode", v => (bool)v == !sourceBefore, "the first window's own mode changes");
        await c.Call("window.list"); // a round trip through both windows' queues after the change
        Check((bool)await Value(second, "SourceCode") == sourceBefore, "the second window keeps its mode");
        await c.Call("test.settings.set", new { windowId = first, name = "SourceCode", value = sourceBefore });
        notes.Add($"font {font}, text direction rtl/auto, source mode stayed per window");
    }

    private static async Task S2(List<string> notes)
    {
        using var c = await Session("e2e S2");
        var windows = ((JArray)(await c.Call("window.list"))["windows"]!).Select(w => (string)w["windowId"]!).ToList();
        var got = await c.Call("settings.get", new { keys = new[] { "editor.fontSize" } });
        var revision = (long)got["settingsRevision"]!;
        var font = (int)got["values"]!["editor.fontSize"]! == 23 ? 24 : 23;
        var set = await c.Call("settings.set", new { key = "editor.fontSize", value = font, baseSettingsRevision = revision });
        Check((long)set["settingsRevision"]! > revision, "the settings revision advanced");
        foreach (var window in windows)
            Check((double)(await c.Call("test.settings.get", new { windowId = window, name = "FontSize" }))["value"]! == font, $"window {window[..7]} has font size {font}");
        foreach (var window in windows)
        {
            // The editor page itself is drawn with it, not only the host's setting.
            string? drawn = null;
            for (var i = 0; i < 100 && drawn != $"{font}px"; i++)
            {
                try { drawn = (string?)(await c.Call("test.editor.style", new { windowId = window }))["fontSize"]; }
                catch (JsonRpcRemoteException e) { drawn = "error: " + e.Message; }
                if (drawn != $"{font}px") await Task.Delay(50);
            }
            Check(drawn == $"{font}px", $"window {window[..7]} ({windows.IndexOf(window) + 1} of {windows.Count})'s editor page is drawn at {font}px ({drawn})");
        }
        // The other exposed keys reach the pages too: line height, text direction; the theme reaches every window.
        var rev = (long)(await c.Call("settings.get"))["settingsRevision"]!;
        rev = (long)(await c.Call("settings.set", new { key = "editor.lineHeight", value = 1.83, baseSettingsRevision = rev }))["settingsRevision"]!;
        rev = (long)(await c.Call("settings.set", new { key = "editor.textDirection", value = "rtl", baseSettingsRevision = rev }))["settingsRevision"]!;
        foreach (var window in windows)
        {
            JToken? style = null;
            for (var i = 0; i < 100; i++)
            {
                try { style = await c.Call("test.editor.style", new { windowId = window }); } catch (JsonRpcRemoteException) { }
                if ((string?)style?["direction"] == "rtl" && Math.Abs(double.Parse(((string)style!["lineHeight"]!).Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture) - font * 1.8) < 0.6) break;
                await Task.Delay(50);
            }
            Check((string?)style?["direction"] == "rtl", $"window {window[..7]}'s page is right-to-left ({style})");
            Check(style != null && Math.Abs(double.Parse(((string)style["lineHeight"]!).Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture) - font * 1.8) < 0.6, $"window {window[..7]}'s page line height is {font}*1.8 (1.83 rounded) ({style})");
        }
        rev = (long)(await c.Call("settings.set", new { key = "editor.textDirection", value = "auto", baseSettingsRevision = rev }))["settingsRevision"]!;
        rev = (long)(await c.Call("settings.set", new { key = "editor.lineHeight", value = 1.6, baseSettingsRevision = rev }))["settingsRevision"]!;
        rev = (long)(await c.Call("settings.set", new { key = "appearance.theme", value = new { kind = "builtIn", id = "dark" }, baseSettingsRevision = rev }))["settingsRevision"]!;
        foreach (var window in windows)
            Check((int)(await c.Call("test.settings.get", new { windowId = window, name = "AppTheme" }))["value"]! == 2, $"window {window[..7]} uses the dark theme");
        Check((string)(await c.Call("settings.get", new { keys = new[] { "appearance.theme" } }))["values"]!["appearance.theme"]!["id"]! == "dark", "settings.get reads the theme back");
        await c.Call("settings.set", new { key = "appearance.theme", value = new { kind = "builtIn", id = "system" }, baseSettingsRevision = rev });
        var file = JObject.Parse(File.ReadAllText(Path.Combine(testRoot, "data", "Settings.json")));
        Check((double)file["FontSize"]! == font, $"Settings.json holds {font} ({file["FontSize"]})");
        Check((int)(await c.Call("settings.get", new { keys = new[] { "editor.fontSize" } }))["values"]!["editor.fontSize"]! == font, "settings.get reads it back");
        Check(await c.ErrorCode("settings.set", new { key = "editor.fontSize", value = 12, baseSettingsRevision = revision }) == -32012, "a stale settings revision is refused");
        Check(await c.ErrorCode("settings.set", new { key = "SourceCode", value = true, baseSettingsRevision = (long)set["settingsRevision"]! }) == -32020, "a window mode is not an external setting");
        // Leave the windows as they were: a later case that clicks and types into a page (F01) found it re-laid out
        // under a font size nobody put back, and its keystroke went elsewhere.
        var original = (int)got["values"]!["editor.fontSize"]!;
        await SetSetting(c, "editor.fontSize", original);
        foreach (var window in windows)
        {
            string? drawn = null;
            for (var i = 0; i < 100 && drawn != $"{original}px"; i++)
            {
                try { drawn = (string?)(await c.Call("test.editor.style", new { windowId = window }))["fontSize"]; } catch (JsonRpcRemoteException) { }
                if (drawn != $"{original}px") await Task.Delay(50);
            }
        }
        notes.Add($"{windows.Count} window(s), font {font}, back to {original}");
    }

    private static async Task B02(List<string> notes)
    {
        using var c = await Session("e2e B02");
        var windows = ((JArray)(await c.Call("window.list"))["windows"]!).Select(w => (string)w["windowId"]!).ToList();
        if (windows.Count < 2) windows.Add((string)(await c.Call("test.window.open"))["windowId"]!);
        var settingsWindow = windows[1];
        await c.Call("test.window.navigate", new { windowId = settingsWindow, route = "Settings/Editor" });
        try
        {
            var got = await c.Call("settings.get", new { keys = new[] { "editor.fontSize" } });
            var before = (int)got["values"]!["editor.fontSize"]!;
            var windowHandle = WindowOf(settingsWindow, c);
            await WaitForNumberBox(await windowHandle, before, notes, "the settings page shows the current size");
            var font = before == 25 ? 26 : 25;
            await SetSetting(c, "editor.fontSize", font);
            await WaitForNumberBox(await windowHandle, font, notes, "the open settings page follows a change made elsewhere");
            await SetSetting(c, "editor.fontSize", before);
        }
        finally
        {
            await c.Call("test.window.navigate", new { windowId = settingsWindow, route = "Main" });
        }
    }

    private static async Task<IntPtr> WindowOf(string windowId, Client c) =>
        new IntPtr((long)(await c.Call("test.window.handle", new { windowId }))["hwnd"]!);

    /// <summary>
    /// The entries View > Theme shows on screen, read the way a screen reader reads them: each menu of the menu bar is
    /// opened until the one holding the theme submenu (automation id ThemeSubMenu), which is opened too; then every
    /// menu is closed again.
    /// </summary>
    private static async Task<List<string>> ShownThemeMenu(IntPtr window)
    {
        var root = System.Windows.Automation.AutomationElement.FromHandle(window);
        var menuItem = new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuItem);
        var bar = root.FindFirst(System.Windows.Automation.TreeScope.Descendants,
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuBar))
            ?? throw new CaseFailed("no menu bar in the window");
        var seen = new List<string>();
        try
        {
            foreach (System.Windows.Automation.AutomationElement top in bar.FindAll(System.Windows.Automation.TreeScope.Children, menuItem))
            {
                if (!top.TryGetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern, out var topPattern)) { seen.Add(top.Current.Name + " (no expand)"); continue; }
                ((System.Windows.Automation.ExpandCollapsePattern)topPattern).Expand();
                await Task.Delay(400);
                var sub = root.FindFirst(System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, "ThemeSubMenu"));
                seen.Add($"{top.Current.Name}: {((System.Windows.Automation.ExpandCollapsePattern)topPattern).Current.ExpandCollapseState}, {root.FindAll(System.Windows.Automation.TreeScope.Descendants, menuItem).Count} items");
                if (sub == null)
                {
                    ((System.Windows.Automation.ExpandCollapsePattern)topPattern).Collapse();
                    // A closed menu hands the keyboard back to the editor a moment later (MenuFocus): opened before
                    // that, the next menu was closed again by it.
                    await Task.Delay(700);
                    continue;
                }
                ((System.Windows.Automation.ExpandCollapsePattern)sub.GetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern)).Expand();
                await Task.Delay(500);
                var names = new List<string>();
                foreach (System.Windows.Automation.AutomationElement item in root.FindAll(System.Windows.Automation.TreeScope.Descendants, menuItem))
                    names.Add(item.Current.Name);
                return names;
            }
            throw new CaseFailed("no menu holds the theme submenu; opened " + string.Join(" | ", seen));
        }
        finally
        {
            for (var i = 0; i < 3; i++) { Send(Key(0x1B, false), Key(0x1B, true)); await Task.Delay(150); }
        }
    }

    private static async Task WaitForNumberBox(IntPtr window, int expected, List<string> notes, string what)
    {
        string seen = "";
        for (var i = 0; i < 80; i++)
        {
            try
            {
                var root = System.Windows.Automation.AutomationElement.FromHandle(window);
                var edits = root.FindAll(System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Edit));
                var values = new List<string>();
                foreach (System.Windows.Automation.AutomationElement edit in edits)
                    if (edit.TryGetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern, out var pattern))
                        values.Add(((System.Windows.Automation.ValuePattern)pattern).Current.Value);
                seen = string.Join(" | ", values);
                if (values.Any(v => new string(v.TakeWhile(char.IsDigit).ToArray()) == expected.ToString())) { notes.Add($"{what}: {seen}"); return; }
            }
            catch (Exception e) { seen = e.Message; }
            await Task.Delay(100);
        }
        throw new CaseFailed($"{what}: expected {expected}, the page shows [{seen}]");
    }

    // What the editor page holds - focus, the caret's block - for failure messages.
    private const string PageStateScript = @"(() => {
        const s = getSelection(); const n = s && s.anchorNode; const e = n && (n.nodeType === 1 ? n : n.parentElement);
        const blk = e && e.closest('[id^=""ag-""]');
        return { hasFocus: document.hasFocus(), active: document.activeElement && (document.activeElement.id || document.activeElement.tagName), caret: blk && blk.id, editor: !!window.__typedownMuya };
    })()";

    /// <summary>A full dump of the test host (dotnet-dump, where installed) next to the screenshots; why not, otherwise.</summary>
    private static string HangDump(string label)
    {
        try
        {
            var tool = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "dotnet-dump.exe");
            if (!File.Exists(tool)) return "no dotnet-dump";
            var file = Path.Combine(outputDir, label + ".dmp");
            using var dump = Process.Start(new ProcessStartInfo(tool, $"collect -p {hostPid} --type Full -o \"{file}\"") { UseShellExecute = false, CreateNoWindow = true })!;
            return dump.WaitForExit(180000) && File.Exists(file) ? file : "dump failed";
        }
        catch (Exception e)
        {
            return "dump failed: " + e.Message;
        }
    }

    private static async Task D01(List<string> notes)
    {
        using var c = await Session("e2e D01");
        var windowId = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        var result = await c.Call("test.dispatcher.stress", new { windowId, threads = 8, posts = 1000 });
        notes.Add(result.ToString(Formatting.None));
        Check((long)result["ran"]! == (long)result["posted"]!, $"every posted callback ran ({result["ran"]} of {result["posted"]})");
    }

    private static async Task<string> PageState(Client c, string windowId)
    {
        try { return (await c.Call("test.editor.eval", new { windowId, script = PageStateScript }))["result"]!.ToString(Formatting.None); }
        catch (Exception e) { return "no state: " + e.Message; }
    }

    private static async Task N01(List<string> notes)
    {
        using var c = await Session("e2e N01");
        const string text = "---\ntitle: kept\n---\n\n# N01\n\nbody\n";
        var windowId = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        for (var round = 0; round < 4; round++)
        {
            await c.Call("test.window.navigate", new { windowId, route = "Settings/Editor" });
            await Task.Delay(800);
            await c.Call("test.window.navigate", new { windowId, route = "Main" });
            var id = await Open(c, Fixture($"n01-{round}.md", "# N01\n"));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var kind = await c.ErrorKind("document.get", new { documentId = id, consistency = "latest" });
            Check(kind == null, $"round {round}: the first latest read after returning from the settings page succeeds ({kind} after {clock.ElapsedMilliseconds} ms; page {await PageState(c, windowId)})");
            await c.Call("document.replace", new { documentId = id, baseRevision = await Revision(c, id), text, normalizationPolicy = "allowUnknown" });
            Check((string)(await Get(c, id))["text"]! == text, $"round {round}: the write is the text");
            notes.Add($"round {round}: read in {clock.ElapsedMilliseconds} ms");
        }
    }

    private static async Task K01(List<string> notes)
    {
        using var c = await Session("e2e K01");
        var id = await Open(c, Fixture("k01.md", "# K01\n\nThe end.\n"));
        var windowId = await WindowIdOf(c, id);
        await TypeInto(c, id, "A");
        await WaitForPage(c, id, t => t.Contains('A'), "the first keystroke");
        // Another program changes the font size and the line height while the reader has the keyboard.
        using (var other = await Session("e2e K01 settings"))
        {
            var font = (int)(await other.Call("settings.get", new { keys = new[] { "editor.fontSize" } }))["values"]!["editor.fontSize"]!;
            await SetSetting(other, "editor.fontSize", font + 1);
            await Task.Delay(500);
            await SetSetting(other, "editor.fontSize", font);
            await Task.Delay(500);
        }
        var state = await PageState(c, windowId);
        // No new focus from the host: the keys go wherever the page's own focus is, as they do for the reader.
        Send(Key(0x11, false), Key(0x23, false), Key(0x23, true), Key(0x11, true));
        var inputs = new[] { new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = 'B', dwFlags = 0x0004 } } }, new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = 'B', dwFlags = 0x0006 } } } };
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        string? text = null;
        for (var i = 0; i < 40 && text?.Contains('B') != true; i++) { await Task.Delay(100); text = (string?)(await c.Call("test.editor.pageText", new { documentId = id }))["text"]; }
        Check(text?.Contains('B') == true, $"the keystroke after the font change reached the page (page after the change: {state}; text {JsonConvert.SerializeObject(text)})");
        notes.Add("page after the change: " + state);
    }

    // Ctrl+, opens the settings and Ctrl+, closes them: the reader is back where they were, caret and keyboard. Closing
    // the settings builds the main page again and reloads the editor page; the caret came back from the per-file memory
    // only (or not at all), and the keyboard was not given back.
    private static Task K02(List<string> notes) => SettingsRoundTrip(notes, untitled: false);

    // A window just opened (the app started, a new window) gave the keyboard to nothing: the first letters went nowhere
    // until a click in the text. Only coming back from the settings handed it to the editor.
    private static async Task K04(List<string> notes)
    {
        using var c = await Session("e2e K04");
        var windowId = (string)(await c.Call("test.window.open"))["windowId"]!;
        var window = await WindowOf(windowId, c);
        var id = (string)((JArray)(await c.Call("document.list", new { windowId }))["documents"]!)[0]["documentId"]!;
        try
        {
            await c.Call("document.get", new { documentId = id, consistency = "latest" });
            await Task.Delay(1500);
            // Brought to the front as the system does for a window that opens - no click inside it, no focus call.
            await Activate(window);
            Check(GetForegroundWindow() == window, "the new window is in front");
            TypeChar('Q');
            string? text = null;
            // The window's document asked for anew each time: its untitled document can be replaced as the window settles.
            async Task<string> Current() => (string)((JArray)(await c.Call("document.list", new { windowId }))["documents"]!)[0]["documentId"]!;
            for (var i = 0; i < 30 && text?.Contains('Q') != true; i++)
            {
                await Task.Delay(100);
                try { id = await Current(); text = (string?)(await c.Call("test.editor.pageText", new { documentId = id }))["text"]; } catch (Exception ex) { notes.Add("read: " + ex.Message.Split('\n')[0]); }
            }
            notes.Add("page text: " + JsonConvert.SerializeObject(text) + (text?.Contains('Q') == true ? "" : "; screen: " + Screenshot("k04-no-key")));
            Check(text?.Contains('Q') == true, "a letter typed without a click reaches the new window's document");
        }
        finally
        {
            // The letter undone first: closed with it, the untitled document asked to be saved, and that question stayed on
            // screen over the window the following cases opened their documents in.
            try
            {
                var doc = (string)((JArray)(await c.Call("document.list", new { windowId }))["documents"]!)[0]["documentId"]!;
                for (var i = 0; i < 5 && ((string?)(await Get(c, doc))["text"] ?? "").Contains('Q'); i++)
                    await c.Call("document.undo", new { documentId = doc, baseRevision = await Revision(c, doc), reveal = "document" });
                notes.Add("left with: " + JsonConvert.SerializeObject((string?)(await Get(c, doc))["text"]));
            }
            catch (Exception ex) { notes.Add("undo: " + ex.Message.Split('\n')[0]); }
            PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(1000);
            Check(!IsWindow(window), "the new window closes (no question about saving left on screen)");
        }
    }

    // A new tab, from the + button or Ctrl+N, and a tab clicked in the strip left the keyboard on the button or the
    // tab: the letters typed next went nowhere until a click in the text.
    private static async Task K05(List<string> notes)
    {
        using var c = await Session("e2e K05");
        var first = await Open(c, Fixture("k05.md", "# K05\n\nText\n"));
        var windowId = await WindowIdOf(c, first);
        var window = await WindowOf(windowId, c);
        async Task<string> Active() => (string)((JArray)(await c.Call("window.list"))["windows"]!).First(w => (string)w["windowId"]! == windowId)["activeDocumentId"]!;
        async Task<string?> PageHas(string documentId, char ch)
        {
            string? text = null;
            for (var i = 0; i < 30 && text?.Contains(ch) != true; i++) { await Task.Delay(100); text = (string?)(await c.Call("test.editor.pageText", new { documentId }))["text"]; }
            return text;
        }
        void Click(int x, int y) { SetCursorPos(x, y); Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } }); }
        System.Windows.Automation.AutomationElement? Find(string automationId) =>
            System.Windows.Automation.AutomationElement.FromHandle(window).FindFirst(System.Windows.Automation.TreeScope.Descendants,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, automationId));
        var created = new List<string>();
        try
        {
            await TypeInto(c, first, "A");
            await WaitForPage(c, first, t => t.Contains('A'), "the first letter");

            // Ctrl+N with the caret in the document, as the reader presses it while writing.
            // N with its scan code, Ctrl down first: the page (Chromium) reads the key from the scan code.
            Send(Key(0x11, false));
            await Task.Delay(80);
            Send(new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0x4E, wScan = 0x31 } } }, new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0x4E, wScan = 0x31, dwFlags = 0x0002 } } });
            await Task.Delay(80);
            Send(Key(0x11, true));
            await Task.Delay(1500);
            var ctrlN = await Active();
            created.Add(ctrlN);
            TypeChar('N');
            var text = await PageHas(ctrlN, 'N');
            notes.Add("after Ctrl+N: " + JsonConvert.SerializeObject(text));
            Check(ctrlN != first && text?.Contains('N') == true, "a letter typed after Ctrl+N reaches the new tab");

            // The + button, clicked.
            var add = Find("AddButton");
            if (add == null)
            {
                var buttons = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button));
                throw new CaseFailed("no + button in the tab strip; buttons: " + string.Join(", ", buttons.Cast<System.Windows.Automation.AutomationElement>().Select(x => $"'{x.Current.Name}'/{x.Current.AutomationId}")));
            }
            var r = add.Current.BoundingRectangle;
            Click((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2));
            await Task.Delay(1500);
            var plus = await Active();
            created.Add(plus);
            TypeChar('P');
            text = await PageHas(plus, 'P');
            notes.Add("after +: " + JsonConvert.SerializeObject(text));
            Check(plus != ctrlN && text?.Contains('P') == true, "a letter typed after the + button reaches the new tab");

            // The first tab, clicked in the strip.
            var tabs = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.TabItem));
            var tab = tabs.Cast<System.Windows.Automation.AutomationElement>().FirstOrDefault(t => t.Current.Name.Contains("k05")) ?? throw new CaseFailed("no k05 tab in the strip");
            r = tab.Current.BoundingRectangle;
            Click((int)(r.Left + 20), (int)(r.Top + r.Height / 2));
            await Task.Delay(1500);
            Check(await Active() == first, "the click switched to the first tab");
            // A letter the document does not hold already ("Text" has a T).
            TypeChar('J');
            text = await PageHas(first, 'J');
            notes.Add("after the tab click: " + JsonConvert.SerializeObject(text));
            Check(text?.Contains('J') == true, "a letter typed after a tab is clicked reaches its document");
        }
        finally
        {
            // The new tabs' letters undone and the tabs closed, so nothing asks to be saved later.
            foreach (var doc in created.Distinct())
            {
                try
                {
                    for (var i = 0; i < 5 && ((string?)(await Get(c, doc))["text"] ?? "").Trim().Length > 0; i++)
                        await c.Call("document.undo", new { documentId = doc, baseRevision = await Revision(c, doc), reveal = "document" });
                    await c.Call("document.close", new { documentId = doc });
                }
                catch (Exception ex) { notes.Add("cleanup: " + ex.Message.Split('\n')[0]); }
            }
        }
    }

    // Everywhere the reader leaves the text for a moment - a tab's close button, a menu, the outline, the find bar, a
    // dialog - and comes back to write: the letter typed next must reach the document without a click in it. Each
    // step is tried and recorded; the case fails at the end with the list of those where the letter went nowhere.
    private static async Task K06(List<string> notes)
    {
        using var c = await Session("e2e K06");
        var main = await Open(c, Fixture("k06.md", "# K06\n\n## Two\n\nText\n"));
        var windowId = await WindowIdOf(c, main);
        var window = await WindowOf(windowId, c);
        var failed = new List<string>();
        async Task<string> Active() => (string)((JArray)(await c.Call("window.list"))["windows"]!).First(w => (string)w["windowId"]! == windowId)["activeDocumentId"]!;
        async Task<string> TextOf(string doc) => (string?)(await Get(c, doc))["text"] ?? "";
        void Click(int x, int y, bool twice = false)
        {
            SetCursorPos(x, y);
            for (var i = 0; i < (twice ? 2 : 1); i++)
                Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
        }
        void ClickElement(System.Windows.Automation.AutomationElement e, bool twice = false)
        {
            var r = e.Current.BoundingRectangle;
            Click((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2), twice);
        }
        System.Windows.Automation.AutomationElement? FindIn(System.Windows.Automation.AutomationElement root, Func<System.Windows.Automation.AutomationElement, bool> match)
        {
            foreach (System.Windows.Automation.AutomationElement e in root.FindAll(System.Windows.Automation.TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
                if (match(e)) return e;
            return null;
        }
        // Anywhere in the test host's windows (menus and dialogs are popups of their own).
        System.Windows.Automation.AutomationElement? FindAnywhere(Func<System.Windows.Automation.AutomationElement, bool> match)
        {
            foreach (System.Windows.Automation.AutomationElement top in System.Windows.Automation.AutomationElement.RootElement.FindAll(System.Windows.Automation.TreeScope.Children,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ProcessIdProperty, hostPid)))
                if (FindIn(top, match) is { } found) return found;
            return null;
        }
        INPUT Scan(ushort vk, ushort scan, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = up ? 0x0002u : 0u } } };
        // A top menu of the menu bar (0 File ... 4 View), opened with the mouse, and one of its items clicked.
        async Task Menu(int index, string itemId)
        {
            var file = FindIn(System.Windows.Automation.AutomationElement.FromHandle(window), e => e.Current.AutomationId == "MenuBarFileItem") ?? throw new CaseFailed("no File menu");
            var bar = System.Windows.Automation.TreeWalker.ControlViewWalker.GetParent(file);
            var menus = bar.FindAll(System.Windows.Automation.TreeScope.Children, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuItem));
            // View is the last menu: in source mode Paragraph and Format are hidden and the others move up.
            ClickElement(menus[index < 0 ? menus.Count + index : index]);
            System.Windows.Automation.AutomationElement? item = null;
            for (var i = 0; i < 20 && item == null; i++) { await Task.Delay(150); item = FindAnywhere(e => e.Current.AutomationId == itemId && e.Current.ControlType == System.Windows.Automation.ControlType.MenuItem && !e.Current.IsOffscreen); }
            if (item == null) throw new CaseFailed($"no menu item {itemId}");
            await Task.Delay(600); // the menu's opening animation over: a click during it lands on nothing
            var ir = item.Current.BoundingRectangle;
            // The pointer moved onto the item and resting there first, as a hand does: a click with no move before it
            // did not take in the menu's popup.
            int ix = (int)(ir.Left + ir.Width / 2), iy = (int)(ir.Top + ir.Height / 2);
            SetCursorPos(ix - 30, iy);
            for (var i = 1; i <= 6; i++) { await Task.Delay(30); SetCursorPos(ix - 30 + 5 * i, iy); }
            await Task.Delay(300);
            Click(ix, iy);
            await Task.Delay(800);
            if (FindAnywhere(e => e.Current.AutomationId == itemId && e.Current.ControlType == System.Windows.Automation.ControlType.MenuItem && !e.Current.IsOffscreen) is { } still)
            {
                notes.Add($"     {itemId}: the click did not close the menu; toggled instead");
                if (still.TryGetCurrentPattern(System.Windows.Automation.TogglePattern.Pattern, out var toggle)) ((System.Windows.Automation.TogglePattern)toggle).Toggle();
                else ((System.Windows.Automation.InvokePattern)still.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
                // A checkable item toggled that way leaves its menu open: closed with Esc, as a person closes it.
                await Task.Delay(300);
                if (FindAnywhere(e => e.Current.AutomationId == itemId && e.Current.ControlType == System.Windows.Automation.ControlType.MenuItem && !e.Current.IsOffscreen) != null)
                    Send(Scan(0x1B, 0x01, false), Scan(0x1B, 0x01, true));
            }
            await Task.Delay(1200);
        }
        // A menu or popup a failed step left open closed, so it does not take the next step's clicks.
        async Task CloseMenus()
        {
            for (var i = 0; i < 2; i++)
            {
                Send(Scan(0x1B, 0x01, false), Scan(0x1B, 0x01, true));
                await Task.Delay(300);
            }
        }
        async Task Chord(ushort vk, ushort scan)
        {
            Send(Key(0x11, false));
            await Task.Delay(80);
            Send(Scan(vk, scan, false), Scan(vk, scan, true));
            await Task.Delay(80);
            Send(Key(0x11, true));
            await Task.Delay(1200);
        }
        // The letter typed with no click, and whether it reached the document that should have it.
        async Task Probe(string step, char letter, Func<Task<string>> expectedDocument)
        {
            try
            {
                var doc = await expectedDocument();
                TypeChar(letter);
                string text = "";
                for (var i = 0; i < 30 && !text.Contains(letter); i++) { await Task.Delay(100); text = await TextOf(doc); }
                var ok = text.Contains(letter);
                notes.Add($"{(ok ? "ok  " : "FAIL")} {step}");
                if (!ok)
                {
                    failed.Add(step);
                    string page;
                    try { page = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const a = document.activeElement; const s = getSelection(); return { hasFocus: document.hasFocus(), active: a ? a.tagName + '.' + (a.className || '').toString().slice(0, 40) + (a.isContentEditable ? ' editable' : '') : null, ranges: s.rangeCount, anchor: s.anchorNode ? (s.anchorNode.nodeName + ':' + (s.anchorNode.textContent || '').slice(0, 20)) : null } })()" }))["result"]!.ToString(Formatting.None); }
                    catch (Exception ex) { page = ex.Message.Split('\n')[0]; }
                    notes.Add($"     keys went to: {FocusInfo(window)}; page: {page}; screen: {Screenshot("k06-" + letter)}");
                }
            }
            catch (Exception ex)
            {
                failed.Add(step);
                notes.Add($"FAIL {step}: {ex.Message.Split('\n')[0]}");
            }
            await Task.Delay(300);
        }
        async Task Try(string step, Func<Task> action, char letter, Func<Task<string>> expectedDocument)
        {
            await CloseMenus();
            // Each step from the reader writing in the document: the caret in the text, so one step's outcome is not
            // the next one's start.
            try { await c.Call("test.editor.focus", new { windowId }); await Task.Delay(300); } catch { }
            notes.Add($"     before '{step}': {FocusInfo(window)}");
            try { await action(); }
            catch (Exception ex) { failed.Add(step); notes.Add($"FAIL {step} (could not be done): {ex.Message.Split('\n')[0]}"); return; }
            await Probe(step, letter, expectedDocument);
        }

        var second = "";
        try
        {
            await TypeInto(c, main, "A");
            await WaitForPage(c, main, t => t.Contains('A'), "the first letter");

            await Try("a tab closed with its x button", async () =>
            {
                second = await Open(c, Fixture("k06b.md", "# K06b\n"));
                await Task.Delay(1200);
                var tab = FindIn(System.Windows.Automation.AutomationElement.FromHandle(window), e => e.Current.ControlType == System.Windows.Automation.ControlType.TabItem && e.Current.Name.Contains("k06b")) ?? throw new CaseFailed("no k06b tab");
                var close = FindIn(tab, e => e.Current.ControlType == System.Windows.Automation.ControlType.Button) ?? throw new CaseFailed("no close button on the tab");
                ClickElement(close);
                await Task.Delay(1500);
                second = "";
            }, 'Q', () => Task.FromResult(main));

            await Try("Format > Strong from the menu", () => Menu(3, "StrongItem"), 'Z', () => Task.FromResult(main));

            await Try("View > Source code mode from the menu", () => Menu(-1, "SourceCodeModeItem"), 'V', () => Task.FromResult(main));
            await Try("View > Source code mode again (back to the visual editor)", () => Menu(-1, "SourceCodeModeItem"), 'Y', () => Task.FromResult(main));

            await Try("a heading clicked in the outline", async () =>
            {
                await c.Call("window.setView", new { windowId, sidePane = new { open = true, page = "outline" } });
                await Task.Delay(1500);
                System.Windows.Automation.AutomationElement? heading = null;
                for (var i = 0; i < 20 && heading == null; i++) { await Task.Delay(150); heading = FindIn(System.Windows.Automation.AutomationElement.FromHandle(window), e => e.Current.Name == "Two" && e.Current.ControlType != System.Windows.Automation.ControlType.Document); }
                ClickElement(heading ?? throw new CaseFailed("no Two in the outline"));
                await Task.Delay(1200);
            }, 'U', () => Task.FromResult(main));

            await Try("the find bar opened with Ctrl+F and closed with Esc", async () =>
            {
                await Chord(0x46, 0x21);
                // Esc once the bar has the keys, as a person presses it: sent at once, it could reach the text before
                // the bar took the focus, and the bar stayed open with the next letter typed into its search box.
                for (var i = 0; i < 40; i++)
                {
                    if (System.Windows.Automation.AutomationElement.FocusedElement?.Current.AutomationId == "TextBoxSearch") break;
                    await Task.Delay(50);
                }
                Send(Scan(0x1B, 0x01, false), Scan(0x1B, 0x01, true));
                await Task.Delay(1200);
            }, 'L', () => Task.FromResult(main));

            await Try("the find bar closed with its close button", async () =>
            {
                await Chord(0x46, 0x21);
                System.Windows.Automation.AutomationElement? input = null;
                for (var i = 0; i < 20 && input == null; i++) { await Task.Delay(150); input = FindIn(System.Windows.Automation.AutomationElement.FromHandle(window), e => e.Current.ControlType == System.Windows.Automation.ControlType.Edit); }
                // The close button: the rightmost button level with the search box and right of it (the window's own
                // close button is higher up, in the title row).
                var box = (input ?? throw new CaseFailed("no search box")).Current.BoundingRectangle;
                var close = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button))
                    .Cast<System.Windows.Automation.AutomationElement>()
                    .Where(b => { var r = b.Current.BoundingRectangle; return r.Left >= box.Right && Math.Abs((r.Top + r.Height / 2) - (box.Top + box.Height / 2)) < box.Height; })
                    .OrderBy(b => b.Current.BoundingRectangle.Left).LastOrDefault() ?? throw new CaseFailed("no close button in the find bar");
                ClickElement(close);
                await Task.Delay(1200);
            }, 'H', () => Task.FromResult(main));

            await Try("the save question (Ctrl+W on a changed document) answered Cancel", async () =>
            {
                await Chord(0x57, 0x11);
                System.Windows.Automation.AutomationElement? cancel = null;
                for (var i = 0; i < 20 && cancel == null; i++) { await Task.Delay(150); cancel = FindAnywhere(e => e.Current.ControlType == System.Windows.Automation.ControlType.Button && (e.Current.Name == "取消" || e.Current.Name == "Cancel")); }
                ClickElement(cancel ?? throw new CaseFailed("no Cancel in the save question"));
                await Task.Delay(1200);
            }, 'G', () => Task.FromResult(main));

            await Try("Ctrl+Tab to the other tab", async () =>
            {
                second = await Open(c, Fixture("k06b.md", "# K06b\n"));
                await Task.Delay(1000);
                await c.Call("document.focus", new { documentId = second });
                await TypeInto(c, second, "B");
                await Task.Delay(500);
                Send(Key(0x11, false));
                await Task.Delay(80);
                Send(Scan(0x09, 0x0F, false), Scan(0x09, 0x0F, true));
                await Task.Delay(80);
                Send(Key(0x11, true));
                await Task.Delay(1500);
            }, 'M', Active);

            notes.Add("text: " + JsonConvert.SerializeObject(await TextOf(main)));
            Check(failed.Count == 0, "the letter typed next went nowhere after: " + string.Join("; ", failed));
        }
        finally
        {
            // Saved, so nothing asks to be saved later.
            foreach (var doc in new[] { main, second }.Where(d => d.Length > 0))
                try { await c.Call("document.save", new { documentId = doc }); } catch { }
            try { await c.Call("window.setView", new { windowId, sidePane = new { open = false } }); } catch { }
            try { await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = false }); } catch { }
        }
    }

    // The same with an untitled document: no file, so no per-file caret memory to come back from.
    private static Task K03(List<string> notes) => SettingsRoundTrip(notes, untitled: true);

    private static async Task SettingsRoundTrip(List<string> notes, bool untitled)
    {
        using var c = await Session(untitled ? "e2e K03" : "e2e K02");
        const string text0 = "# K02\n\nfirst line\n\nsecond line\n\nthird line\n";
        string id;
        if (untitled)
        {
            id = (string)(await c.Call("document.create", new { text = text0, reveal = "document" }))["documentId"]!;
            await c.Call("document.get", new { documentId = id, consistency = "latest" });
        }
        else id = await Open(c, Fixture("k02.md", text0));
        var windowId = await WindowIdOf(c, id);
        await TypeInto(c, id, "A");
        await WaitForPage(c, id, t => t.Contains("third lineA"), "the first keystroke");
        // Up to the second paragraph (one Up: the blank lines between paragraphs are no lines here), its end, a letter.
        Send(Key(0x26, false), Key(0x26, true));
        await Task.Delay(150);
        Send(Key(0x23, false), Key(0x23, true));
        await Task.Delay(150);
        TypeChar('B');
        await WaitForPage(c, id, t => t.Contains("second lineB"), "B at the end of the second paragraph");
        var before = await PageState(c, windowId);
        // Ctrl+, twice, as the reader presses it: open the settings, close them.
        async Task<int> Menus() => ((JArray)(await c.Call("test.window.menuTitles", new { windowId }))["titles"]!).Count;
        await CtrlComma();
        await Task.Delay(2000);
        var inSettings = await Menus();
        if (inSettings != 0) notes.Add("screen after the first Ctrl+,: " + Screenshot("k02-settings"));
        await CtrlComma();
        await Task.Delay(1500);
        var back = await Menus();
        notes.Add($"menus: {inSettings} with the settings open, {back} after");
        Check(inSettings == 0 && back > 0, $"Ctrl+, opened the settings and Ctrl+, closed them (menus {inSettings}, then {back})");
        await c.Call("document.get", new { documentId = id, consistency = "latest" });
        await Task.Delay(500);
        var after = await PageState(c, windowId);
        notes.Add($"before {before}; after {after}");
        TypeChar('C');
        string? text = null;
        for (var i = 0; i < 30 && text?.Contains('C') != true; i++) { await Task.Delay(100); text = (string?)(await c.Call("test.editor.pageText", new { documentId = id }))["text"]; }
        Check(text?.Contains("second lineBC") == true, $"a letter typed after the settings closed goes where the caret was ({JsonConvert.SerializeObject(text)})");
    }

    // Ctrl+, as a person types it: the settings shortcut is a low-level keyboard hook reading the key state, and Ctrl
    // sent in the same batch as the comma was not down yet when the hook looked.
    private static async Task CtrlComma()
    {
        Send(Key(0x11, false));
        await Task.Delay(80);
        Send(Comma(false), Comma(true));
        await Task.Delay(80);
        Send(Key(0x11, true));
    }

    // The comma key with its scan code: the editor page (Chromium) reads the key from it, and a bare VK_OEM_COMMA
    // was not Ctrl+, there - the settings never opened.
    private static INPUT Comma(bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = 0xBC, wScan = 0x33, dwFlags = up ? 0x0002u : 0u } } };

    // F11 puts the window in full screen: the main page then starts at the top edge of the screen. A 4 px row kept for
    // compact mode's top border stayed above it, a strip of window background along the top.
    private static async Task FS01(List<string> notes)
    {
        using var c = await Session("e2e FS01");
        var id = await Open(c, Fixture("fs01.md", "# FS01\n\nText\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        await Activate(window);
        async Task<JToken> Layout() => await c.Call("test.window.layout", new { windowId });
        var before = await Layout();
        Send(Key(0x7A, false), Key(0x7A, true));
        JToken full = before;
        for (var i = 0; i < 30 && !(bool)full["fullScreen"]!; i++) { await Task.Delay(100); full = await Layout(); }
        await Task.Delay(800);
        full = await Layout();
        notes.Add($"before {before.ToString(Formatting.None)}; full screen {full.ToString(Formatting.None)}");
        try
        {
            Check((bool)full["fullScreen"]!, "F11 put the window in full screen");
            Check((double)full["mainPageTop"]! == 0, $"in full screen the main page starts at the top edge ({full["mainPageTop"]} px below it)");
        }
        finally
        {
            await Activate(window);
            Send(Key(0x7A, false), Key(0x7A, true));
            JToken after = full;
            for (var i = 0; i < 30 && (bool)after["fullScreen"]!; i++) { await Task.Delay(100); after = await Layout(); }
            await Task.Delay(500);
            after = await Layout();
            notes.Add("after " + after.ToString(Formatting.None));
            Check(!(bool)after["fullScreen"]! && (double)after["mainPageTop"]! == (double)before["mainPageTop"]!, "F11 again leaves full screen, the layout as it was");
        }
    }

    // After F11 twice the window could not be dragged by its title bar until a menu was opened or the settings were
    // visited. The drag is tried before full screen too, so a point that is no drag area does not pass for the bug.
    private static async Task FS02(List<string> notes)
    {
        using var c = await Session("e2e FS02");
        var id = await Open(c, Fixture("fs02.md", "# FS02\n\nText\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        async Task<JToken> Layout() => await c.Call("test.window.layout", new { windowId });
        // Pressed in the title area, moved 80 px right and 50 down, released: how far the window went.
        async Task<(int dx, int dy)> Drag(bool compact)
        {
            await Activate(window);
            GetWindowRect(window, out var r);
            // The title row: its middle. Compact (menus in the title row): right of the menus, left of the window buttons.
            int x = compact ? r.Right - 260 : (r.Left + r.Right) / 2, y = r.Top + (compact ? 16 : 12);
            SetCursorPos(x, y);
            await Task.Delay(150);
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } });
            for (var i = 1; i <= 10; i++) { await Task.Delay(30); SetCursorPos(x + 8 * i, y + 5 * i); }
            await Task.Delay(150);
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
            await Task.Delay(400);
            GetWindowRect(window, out var after);
            return (after.Left - r.Left, after.Top - r.Top);
        }
        var compactBefore = (bool)(await c.Call("test.settings.get", new { windowId, name = "AppCompactMode" }))["value"]!;
        try
        {
            foreach (var compact in new[] { false, true })
            {
                await c.Call("test.settings.set", new { windowId, name = "AppCompactMode", value = compact });
                // Changed where a person changes it, on the settings page: coming back builds the main page anew, and
                // the compact menu row's drag areas are laid out when it is built.
                await c.Call("test.window.navigate", new { windowId, route = "Settings/General" });
                await Task.Delay(1200);
                await c.Call("test.window.navigate", new { windowId, route = "Main" });
                await Task.Delay(1500);
                var first = await Drag(compact);
                Check(Math.Abs(first.dx - 80) <= 4 && Math.Abs(first.dy - 50) <= 4, $"{(compact ? "compact" : "title row")}: before full screen the drag moves the window ({first})");
                await Activate(window);
                Send(Key(0x7A, false), Key(0x7A, true));
                JToken state = await Layout();
                for (var i = 0; i < 30 && !(bool)state["fullScreen"]!; i++) { await Task.Delay(100); state = await Layout(); }
                await Task.Delay(800);
                Send(Key(0x7A, false), Key(0x7A, true));
                for (var i = 0; i < 30 && (bool)state["fullScreen"]!; i++) { await Task.Delay(100); state = await Layout(); }
                await Task.Delay(800);
                var second = await Drag(compact);
                notes.Add($"{(compact ? "compact" : "title row")}: drag before {first}, after F11 twice {second}");
                if (second.dx == 0 && second.dy == 0)
                {
                    notes.Add("screen: " + Screenshot("fs02-" + (compact ? "compact" : "title")));
                    // What lies under the point pressed, and where the window's child windows are.
                    GetWindowRect(window, out var w);
                    int px = compact ? w.Right - 260 : (w.Left + w.Right) / 2, py = w.Top + (compact ? 16 : 12);
                    var under = WindowFromPoint(new POINT { X = px, Y = py });
                    var cls = new System.Text.StringBuilder(256);
                    GetClassName(under, cls, 256);
                    var children = new List<string>();
                    EnumChildWindows(window, (h, _) =>
                    {
                        var n = new System.Text.StringBuilder(256);
                        GetClassName(h, n, 256);
                        GetWindowRect(h, out var r);
                        children.Add($"{n} {h}:{r.Left},{r.Top},{r.Right},{r.Bottom}{(IsWindowVisible(h) ? "" : " hidden")}");
                        return true;
                    }, IntPtr.Zero);
                    notes.Add($"pressed at {px},{py} (window {w.Left},{w.Top},{w.Right},{w.Bottom}): under it {cls} {under}; children: {string.Join(" | ", children)}");
                }
                Check(Math.Abs(second.dx - 80) <= 4 && Math.Abs(second.dy - 50) <= 4, $"{(compact ? "compact" : "title row")}: out of full screen the same drag moves the window ({second})");
            }
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "AppCompactMode", value = compactBefore });
        }
    }

    // PlantUML blocks are drawn by plantuml.com from their source. Off by default (Settings > Editor): no image
    // pointing there, and the notice in its place; turned on, the image; off again, the notice.
    private static async Task PU01(List<string> notes)
    {
        using var c = await Session("e2e PU01");
        var id = await Open(c, Fixture("pu01.md", "# PU01\n\n```plantuml\nAlice -> Bob: hello\n```\n\nText\n"));
        var windowId = await WindowIdOf(c, id);
        const string script = "(() => { const off = document.querySelector('.ag-plantuml-off'); " +
            "const img = [...document.querySelectorAll('img')].find(i => (i.getAttribute('src') || '').includes('/svg/~h')); " +
            "return { remote: [...document.querySelectorAll('img')].filter(i => (i.src || '').includes('plantuml.com')).length, src: img ? img.getAttribute('src') : null, " +
            "off: !!off, notice: off ? getComputedStyle(off, '::before').content : null, " +
            // The page's strings under both names: the Windows host answers camel-cased, the stylesheet reads both.
            "strings: ['FirstEditWarning', 'firstEditWarning', 'InputMathFormula', 'inputMathFormula'].map(k => document.documentElement.style.getPropertyValue('--' + k).length) } })()";
        async Task<JToken> Page() { await Task.Delay(1200); return (await c.Call("test.editor.eval", new { windowId, script }))["result"]!; }
        var before = await Page();
        notes.Add("default: " + before.ToString(Formatting.None));
        Check((int)before["remote"]! == 0, "by default no image points at plantuml.com");
        Check((bool)before["off"]! && ((string?)before["notice"] ?? "").Contains("PlantUML"), "by default the block shows the notice");
        Check(before["strings"]!.All(n => (int)n! > 0), "the page's strings are there under both names (the first-edit notice read one the host never sent)");
        try
        {
            await c.Call("test.settings.set", new { windowId, name = "RenderPlantUml", value = true });
            var on = await Page();
            notes.Add("on: " + on.ToString(Formatting.None));
            Check((int)on["remote"]! == 1 && !(bool)on["off"]!, "turned on, the diagram is drawn by plantuml.com");
            // "Alice -> Bob: hello" in PlantUML's hex encoding: the address used to end in "undefined".
            Check((string?)on["src"] == "https://www.plantuml.com/plantuml/svg/~h416c696365202d3e20426f623a2068656c6c6f", $"the address carries the diagram's text ({on["src"]})");
            // A server of the person's own (Settings > Editor > PlantUML server) draws it instead, at once.
            await c.Call("test.settings.set", new { windowId, name = "PlantUmlServer", value = "http://127.0.0.1:9/plantuml/" });
            var own = await Page();
            notes.Add("own server: " + own.ToString(Formatting.None));
            Check((string?)own["src"] == "http://127.0.0.1:9/plantuml/svg/~h416c696365202d3e20426f623a2068656c6c6f" && (int)own["remote"]! == 0, $"the address is the server set, nothing goes to plantuml.com ({own["src"]})");
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "PlantUmlServer", value = "" });
            await c.Call("test.settings.set", new { windowId, name = "RenderPlantUml", value = false });
        }
        var after = await Page();
        notes.Add("off again: " + after.ToString(Formatting.None));
        Check((int)after["remote"]! == 0 && (bool)after["off"]!, "off again, the notice and no image");
    }

    // A PNG of one pixel; the byte after the signature makes each one different.
    private static byte[] Png(byte variant) => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, variant, 1, 2, 3 };

    private const string IU01Text = "# IU01\n\n![a](iu01-images/a.png)\n\n![a again](iu01-images/a.png \"title\")\n\n" +
        "![space](<iu01-images/b b.png>)\n\n![encoded](iu01-images/b%20b.png)\n\n![web](https://example.com/x.png)\n\n" +
        "![missing](iu01-images/missing.png)\n\n![copy](iu01-images/a-copy.png)\n\n<img src=\"iu01-images/a.png\" width=\"10\">\n\n`![code](iu01-images/a.png)`\n\n" +
        "```md\n![fence](iu01-images/a.png)\n```\n";

    private static async Task IU01(List<string> notes)
    {
        using var c = await Session("e2e IU01");
        var images = Path.Combine(Path.GetDirectoryName(Fixture("iu01.md", IU01Text))!, "iu01-images");
        Directory.CreateDirectory(images);
        File.WriteAllBytes(Path.Combine(images, "a.png"), Png(1));
        File.WriteAllBytes(Path.Combine(images, "b b.png"), Png(2));
        // a.png's picture under another name: the upload history gives it a.png's address.
        File.WriteAllBytes(Path.Combine(images, "a-copy.png"), Png(1));
        var log = Path.Combine(images, "uploads.log");
        File.Delete(log);
        var id = await Open(c, Path.Combine(Path.GetDirectoryName(images)!, "iu01.md"));
        var windowId = await WindowIdOf(c, id);
        var script = "function Upload-Image([string]$path) {\n" +
            $"  Add-Content -LiteralPath '{log}' -Value $path\n" +
            "  'https://img.test/' + [IO.Path]::GetFileName($path).Replace(' ', '-')\n}\n";
        var configured = await c.Call("test.images.configure", new { windowId, method = "powershell", config = new { script } });
        notes.Add("configured: " + configured["id"]);
        Check((int?)configured["default"] == (int)configured["id"]!, "the new configuration is the one uploads use");

        var before = await Revision(c, id);
        var result = await c.Call("test.images.uploadAll", new { windowId });
        notes.Add("result: " + result.ToString(Formatting.None));
        var uploads = File.Exists(log) ? File.ReadAllLines(log) : new string[0];
        notes.Add("script calls: " + string.Join(" | ", uploads));
        Check((int)result["Files"]! == 4 && (int)result["Uploaded"]! == 3 && (int)result["Reused"]! == 1, "four files are named (one missing), three get addresses, one of them a.png's");
        Check(uploads.Length == 2, $"each picture is uploaded once, however often and under whatever name it is used ({uploads.Length} calls)");
        Check(result["Failures"]!.Count() == 1 && (string?)result["Failures"]![0]!["Address"] == "iu01-images/missing.png", "the missing file is the one failure");
        var text = (string)(await Get(c, id))["text"]!;
        notes.Add("text: " + JsonConvert.SerializeObject(text));
        var expected = IU01Text
            .Replace("](iu01-images/a.png", "](https://img.test/a.png")
            .Replace("<iu01-images/b b.png>", "<https://img.test/b-b.png>")
            .Replace("(iu01-images/b%20b.png)", "(https://img.test/b-b.png)")
            .Replace("(iu01-images/a-copy.png)", "(https://img.test/a.png)")
            .Replace("src=\"iu01-images/a.png\"", "src=\"https://img.test/a.png\"")
            .Replace("`![code](https://img.test/a.png)`", "`![code](iu01-images/a.png)`")
            .Replace("![fence](https://img.test/a.png)", "![fence](iu01-images/a.png)");
        Check(text == expected, "every use of an uploaded file has its new address; the web image, the missing file and the code are as they were");
        Check(await Revision(c, id) == before + 1, "the addresses changed in one edit");

        await c.Call("document.undo", new { documentId = id, baseRevision = await Revision(c, id), reveal = "document" });
        Check((string)(await Get(c, id))["text"]! == IU01Text, "one undo puts every local path back");

        var again = await c.Call("test.images.uploadAll", new { windowId });
        notes.Add("again: " + again.ToString(Formatting.None));
        Check((int)again["Uploaded"]! == 3 && (int)again["Reused"]! == 3, "run again on the restored text, every address comes from the history");
        Check(File.ReadAllLines(log).Length == 2, "and the upload script is not called again");
        await c.Call("document.undo", new { documentId = id, baseRevision = await Revision(c, id), reveal = "document" });
    }

    // A screenshot uploaded went up as "tmpXXXX.tmp" (an S3 object a browser downloads rather than shows); copying a
    // second, different picture of an existing name saved it as "name (2)" but wrote the first one's name into the
    // document; and a new upload configuration started switched off, in no menu.
    private static async Task IU04(List<string> notes)
    {
        using var c = await Session("e2e IU04");
        var doc = Fixture("iu04.md", "# IU04\n");
        var folder = Path.Combine(Path.GetDirectoryName(doc)!, "iu04");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        var id = await Open(c, doc);
        var windowId = await WindowIdOf(c, id);
        try
        {
            // The screenshot: a real PNG, which the clipboard image decodes and encodes again.
            var shot = Path.Combine(folder, "shot-source.png");
            using (var bitmap = new System.Drawing.Bitmap(3, 2))
            {
                bitmap.SetPixel(1, 1, System.Drawing.Color.Red);
                bitmap.Save(shot, System.Drawing.Imaging.ImageFormat.Png);
            }
            var received = Path.Combine(folder, "received");
            var log = Path.Combine(folder, "uploads.log");
            var script = "function Upload-Image([string]$path) {\n" +
                $"  Add-Content -LiteralPath '{log}' -Value $path\n" +
                $"  Copy-Item -LiteralPath $path -Destination '{received}'\n" +
                "  'https://img.test/' + [IO.Path]::GetFileName($path)\n}\n";
            var configured = await c.Call("test.images.configure", new { windowId, method = "powershell", config = new { script } });
            notes.Add("configured: " + configured.ToString(Formatting.None));

            await c.Call("test.settings.set", new { windowId, name = "InsertClipboardImageAction", value = "Upload" });
            var pasted = await c.Call("test.images.paste", new { windowId, path = shot });
            var called = File.Exists(log) ? File.ReadAllLines(log) : new string[0];
            notes.Add($"pasted: {pasted.ToString(Formatting.None)}; the script got: {string.Join(" | ", called)}");
            Check(called.Length == 1 && Path.GetFileName(called[0]) == "image.png", "the pasted screenshot reaches the uploader as image.png");
            var bytes = File.Exists(received) ? File.ReadAllBytes(received) : new byte[0];
            Check(bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47, "and the file is a PNG");
            Check((string?)pasted["address"] == "https://img.test/image.png", "the address the script returned is the one inserted");

            // Two different pictures both called pic.png, copied into one folder.
            await c.Call("test.settings.set", new { windowId, name = "InsertLocalImageAction", value = "CopyToPath" });
            await c.Call("test.settings.set", new { windowId, name = "InsertLocalImageCopyPath", value = "./iu04/copies" });
            var first = Path.Combine(folder, "one", "pic.png");
            var second = Path.Combine(folder, "two", "pic.png");
            Directory.CreateDirectory(Path.GetDirectoryName(first)!);
            Directory.CreateDirectory(Path.GetDirectoryName(second)!);
            File.WriteAllBytes(first, Png(1));
            File.WriteAllBytes(second, Png(2));
            // In one insertion, each in a paragraph of its own (one at a time, each replaced the one before, still selected).
            await c.Call("test.images.insert", new { windowId, paths = new[] { first, second, first } });
            await Task.Delay(1000);
            var text = (string)(await Get(c, id))["text"]!;
            notes.Add("text: " + JsonConvert.SerializeObject(text));
            var copies = Path.Combine(folder, "copies");
            notes.Add("copies: " + string.Join(", ", Directory.Exists(copies) ? Directory.GetFiles(copies).Select(Path.GetFileName) : new string[0]));
            Check(File.Exists(Path.Combine(copies, "pic (2).png")) && File.ReadAllBytes(Path.Combine(copies, "pic (2).png")).SequenceEqual(Png(2)), "the second picture is saved as pic (2).png");
            Check(text.Contains("pic%20(2).png") || text.Contains("pic (2).png"), "and the document points at pic (2).png");
            Check(System.Text.RegularExpressions.Regex.Matches(text, @"copies/pic\.png").Count == 2, "the first picture, inserted twice, is pic.png both times");
            Check(Directory.GetFiles(copies).Length == 2, "and is not copied a second time");
            Check((bool?)configured["enabledOnCreate"] == true, "a new upload configuration starts enabled");
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "InsertClipboardImageAction", value = "None" });
            await c.Call("test.settings.set", new { windowId, name = "InsertLocalImageAction", value = "None" });
            await c.Call("test.settings.set", new { windowId, name = "InsertLocalImageCopyPath", value = "./images" });
        }
    }

    private static async Task IN01(List<string> notes)
    {
        using var c = await Session("e2e IN01");
        const string original = "# IN01\n\nEnd\n";
        var doc = Fixture("in01.md", original);
        var folder = Path.Combine(Path.GetDirectoryName(doc)!, "in01-images");
        Directory.CreateDirectory(folder);
        // Not in alphabetical order, and one name with a space: the order is the drop's.
        var names = new[] { "z first.png", "a-second.png", "m-third.png" };
        var paths = names.Select((n, i) => { var p = Path.Combine(folder, n); File.WriteAllBytes(p, Png((byte)(20 + i))); return p; }).ToArray();
        var id = await Open(c, doc);
        var windowId = await WindowIdOf(c, id);
        await c.Call("test.editor.focus", new { windowId });
        await Task.Delay(500);
        var before = await Revision(c, id);
        await c.Call("test.images.insert", new { windowId, paths });
        string text = "";
        for (var i = 0; i < 30 && !(text = (string)(await Get(c, id))["text"]!).Contains("m-third"); i++) await Task.Delay(200);
        notes.Add("text: " + JsonConvert.SerializeObject(text));
        var paragraphs = text.TrimEnd('\n').Split("\n\n");
        var images = paragraphs.Select((p, i) => (p, i)).Where(x => x.p.StartsWith("![")).ToList();
        Check(images.Count == 3 && images.All(x => System.Text.RegularExpressions.Regex.IsMatch(x.p, @"^!\[[^\]]*\]\([^)\s]+\)$")), "three images, each a paragraph of its own and nothing else");
        Check(images[0].p.StartsWith("![z first]") && images[1].p.StartsWith("![a-second]") && images[2].p.StartsWith("![m-third]"), "in the drop's order, named after their files");
        Check(images[1].i == images[0].i + 1 && images[2].i == images[1].i + 1, "next to each other");
        Check(string.Join("\n\n", paragraphs.Where(p => !p.StartsWith("![")).ToArray()) + "\n" == original, "the rest of the document is as it was");
        notes.Add($"revision {before} -> {await Revision(c, id)}");
        await c.Call("document.undo", new { documentId = id, baseRevision = await Revision(c, id), reveal = "document" });
        await Task.Delay(300);
        Check((string)(await Get(c, id))["text"]! == original, "one undo takes all three out");
    }

    /// <summary>Characters sent as Unicode input: what an editor reads as text arrives whatever the keyboard layout or IME.</summary>
    private sealed record Typed(string Text);

    // Real key presses into the focused page, as a keyboard sends them: each character by its virtual key (with Shift
    // when the layout needs it), the rest by virtual key, Ctrl+key as a chord. Characters sent as Unicode input
    // (TypeChar) reach the page's keydown with the wrong key, which reading mode's keys read.
    private static void Keys(params object[] keys)
    {
        foreach (var k in keys)
        {
            if (k is Typed typed) foreach (var ch in typed.Text) TypeChar(ch);
            else if (k is string text)
                foreach (var ch in text)
                {
                    var scan = VkKeyScanW(ch);
                    var vk = (ushort)(scan & 0xff);
                    if ((scan & 0x100) != 0) Send(Key(0x10, false), Key(vk, false), Key(vk, true), Key(0x10, true));
                    else Send(Key(vk, false), Key(vk, true));
                    Thread.Sleep(30);
                }
            else if (k is ushort vk) Send(Key(vk, false), Key(vk, true));
            else if (k is ValueTuple<string, ushort> chord && chord.Item1 == "ctrl") Send(Key(0x11, false), Key(chord.Item2, false), Key(chord.Item2, true), Key(0x11, true));
            Thread.Sleep(60);
        }
    }

    private static async Task VI01(List<string> notes)
    {
        using var c = await Session("e2e VI01");
        const string original = "# VI01\n\none\ntwo\nthree\n";
        var path = Fixture("vi01.md", original);
        var id = await Open(c, path);
        var windowId = await WindowIdOf(c, id);
        try
        {
            await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = true });
            await c.Call("test.settings.set", new { windowId, name = "VimMode", value = true });
            await Task.Delay(800);
            var window = await WindowOf(windowId, c);
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await c.Call("test.editor.eval", new { windowId, script = "document.querySelector('.CodeMirror').CodeMirror.focus(), 0" });
            await Task.Delay(300);
            async Task<string> Text() { await Task.Delay(500); return (string)(await Get(c, id))["text"]!; }
            async Task<string?> Badge() => (string?)(await c.Call("test.editor.eval", new { windowId, script = "(document.querySelector('.vim-mode-badge') || {}).textContent || null" }))["result"];

            Check(await Badge() == "-- NORMAL --", $"the badge says normal mode ({await Badge()})");
            await c.Call("test.editor.eval", new { windowId, script = "(window.__keys = [], window.addEventListener('keydown', e => window.__keys.push(e.key + (e.ctrlKey ? '^' : '')), true), 0)" });
            Keys(new Typed("gg2jdd"), new Typed("A!"), (ushort)0x1B);
            notes.Add("page saw: " + (await c.Call("test.editor.eval", new { windowId, script = "JSON.stringify({ keys: window.__keys, active: document.activeElement && (document.activeElement.tagName + '.' + document.activeElement.className), state: window.__typedownVimWants ? 'loaded' : 'none' })" }))["result"]);
            var edited = await Text();
            notes.Add("after dd, A!, Esc: " + JsonConvert.SerializeObject(edited));
            Check(edited == "# VI01\n\ntwo!\nthree\n", "dd deleted the line, A appended, Esc left insert mode");

            // Ctrl+V is the application's paste; with Vim keys in normal mode it is Vim's block visual.
            Keys(new Typed("gg"), ("ctrl", (ushort)0x56), new Typed("jd"));
            var block = await Text();
            notes.Add("after Ctrl+V j d: " + JsonConvert.SerializeObject(block));
            Check(block == " VI01\n\ntwo!\nthree\n", "Ctrl+V j d deleted the first column of two lines (block visual)");
            Keys(new Typed("u"));
            Check(await Text() == "# VI01\n\ntwo!\nthree\n", "u undid it");

            Keys(new Typed(":w"), (ushort)0x0D);
            await Task.Delay(800);
            Check(Disk(path) == "# VI01\n\ntwo!\nthree\n", $"the file holds the text after :w ({JsonConvert.SerializeObject(Disk(path))})");
            Check((bool)(await Get(c, id))["saved"]!, ":w left the document saved");
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "VimMode", value = false });
            await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = false });
        }
    }

    private static async Task VI02(List<string> notes)
    {
        using var c = await Session("e2e VI02");
        var text = new StringBuilder("# VI02\n\n");
        for (var i = 1; i <= 6; i++)
        {
            text.Append($"## Part {i}\n\n");
            for (var j = 1; j <= 12; j++) text.Append($"Paragraph {i}.{j} of the long document.\n\n");
        }
        var id = await Open(c, Fixture("vi02.md", text.ToString()));
        var windowId = await WindowIdOf(c, id);
        try
        {
            await c.Call("test.settings.set", new { windowId, name = "VimMode", value = true });
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = true });
            await Task.Delay(800);
            var window = await WindowOf(windowId, c);
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await Task.Delay(300);
            async Task<JToken> Page() { await Task.Delay(300); return (await c.Call("test.editor.eval", new { windowId, script = "(() => { const h = [...document.querySelectorAll('#editor h2')].find(e => Math.abs(e.getBoundingClientRect().top) < 40); return { y: Math.round(scrollY), h: innerHeight, heading: h ? h.textContent : null } })()" }))["result"]!; }

            Keys("G");
            var end = await Page();
            notes.Add("G: " + end.ToString(Formatting.None));
            Check((int)end["y"]! > 1000, "G goes to the end");
            Keys("gg");
            Check((int)(await Page())["y"]! == 0, "gg goes back to the top");
            // The document's title is the first heading; Part 1 the second.
            Keys("2]]");
            var heading = await Page();
            notes.Add("2]]: " + heading.ToString(Formatting.None));
            Check(((string?)heading["heading"]) is string h1 && h1.EndsWith("Part 1"), "2]] brings the second heading below the top to the top");
            Keys("2]]");
            Check(((string?)(await Page())["heading"]) is string h3 && h3.EndsWith("Part 3"), "2]] again: two headings on");
            Keys("[[");
            Check(((string?)(await Page())["heading"]) is string h2 && h2.EndsWith("Part 2"), "[[ the previous heading");
            var before = (int)(await Page())["y"]!;
            Keys(("ctrl", (ushort)0x44));
            var half = await Page();
            notes.Add("Ctrl+D: " + half.ToString(Formatting.None));
            Check((int)half["y"]! - before > (int)half["h"]! / 3, "Ctrl+D scrolls half a page");
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = false });
            await c.Call("test.settings.set", new { windowId, name = "VimMode", value = false });
        }
    }

    private static async Task TH01(List<string> notes)
    {
        using var c = await Session("e2e TH01");
        var id = await Open(c, Fixture("th01.md", "# TH01\n\nText\n"));
        var windowId = await WindowIdOf(c, id);
        // The page's background variable, and what the source editor is painted with when it is shown.
        async Task<JToken> Colours()
        {
            await Task.Delay(1200);
            return (await c.Call("test.editor.eval", new { windowId, script = "(() => { const cm = document.querySelector('.CodeMirror'); return { bg: getComputedStyle(document.documentElement).getPropertyValue('--editorBgColor').trim(), cm: cm ? getComputedStyle(cm).backgroundColor : null, order: [...document.head.querySelectorAll('link[id^=link_style], style[id^=typedown-]')].map(e => e.id) } })()" }))["result"]!;
        }
        try
        {
            // As the View menu picks a theme: a custom one brings its base (light for both used here), and the window -
            // its menus included - draws light. Set alone, the setting left the window dark under a light theme.
            async Task Apply(string theme)
            {
                await c.Call("test.theme.apply", new { windowId, customTheme = theme });
                await Task.Delay(800);
                var applied = await c.Call("test.theme.apply", new { windowId, customTheme = theme });
                notes.Add($"{theme}: " + applied.ToString(Formatting.None));
                Check((string?)applied["appTheme"] == "Light" && (string?)applied["actualTheme"] == "Light", $"the window draws light under {theme} ({applied.ToString(Formatting.None)})");
            }
            await Apply("sepia");
            var visual = await Colours();
            notes.Add("visual, sepia: " + visual.ToString(Formatting.None));
            Check((string?)visual["bg"] == "#f4ecd8", $"visual mode has the theme's background ({visual["bg"]})");
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = true });
            var reading = await Colours();
            notes.Add("reading: " + reading.ToString(Formatting.None));
            Check((string?)reading["bg"] == "#f4ecd8", $"reading mode has it ({reading["bg"]})");
            await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = true });
            var source = await Colours();
            notes.Add("source: " + source.ToString(Formatting.None));
            Check((string?)source["cm"] == "rgb(244, 236, 216)", $"source mode paints the editor with it ({source["cm"]})");
            // Changed while in source mode: shown at once, not after the next mode switch.
            await Apply("solarized-light");
            var changed = await Colours();
            notes.Add("source, solarized-light: " + changed.ToString(Formatting.None));
            Check((string?)changed["cm"] != "rgb(244, 236, 216)" && (string?)changed["bg"] != "#f4ecd8", "a theme changed in source mode shows at once");
            await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = false });
            var back = await Colours();
            notes.Add("visual again: " + back.ToString(Formatting.None));
            Check((string?)back["bg"] == (string?)changed["bg"], "back in visual mode the same theme");
        }
        finally
        {
            await c.Call("test.settings.set", new { windowId, name = "SourceCode", value = false });
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = false });
            await c.Call("test.theme.apply", new { windowId, builtIn = "Default" });
        }
    }

    // The marks of what is chosen in the side pane stayed the system blue under every custom theme: WinUI draws them in
    // the accent colour it takes from Windows, and only the page was given the theme's accent.
    private static async Task TH03(List<string> notes)
    {
        using var c = await Session("e2e TH03");
        var id = await Open(c, Fixture("th03.md", "# TH03\n\n## One\n\nText\n\n## Two\n\nMore\n"));
        var windowId = await WindowIdOf(c, id);
        const string Dracula = "#FFBD93F9";
        async Task<List<(string Where, string? Fill)>> Indicators(string page, string when)
        {
            await c.Call("window.setView", new { windowId, sidePane = new { open = true, page } });
            await Task.Delay(1500);
            // The pointer over a row, as a hand leaves it: a row under the pointer draws its mark from keys of its own
            // (the selected heading turned the system blue whenever the mouse was over it).
            if (page == "outline" && Deep.First(System.Windows.Automation.AutomationElement.FromHandle(await WindowOf(windowId, c)),
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "One")) is { } row)
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
                var r = row.Current.BoundingRectangle;
                SetCursorPos((int)(r.Left + r.Width / 2) - 3, (int)(r.Top + r.Height / 2));
                for (var i = 0; i < 3; i++) { await Task.Delay(60); Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = 0x0001 } } }); }
                await Task.Delay(500);
            }
            else if (page == "outline") notes.Add($"{when}: no outline row named One to hover");
            var found = ((JArray)(await c.Call("test.pane.accent", new { windowId }))["indicators"]!).Select(x => ((string)x["where"]!, (string?)x["fill"])).ToList();
            notes.Add($"{when}, {page}: " + string.Join(", ", found.GroupBy(x => x).Select(g => $"{g.Key.Item1} {g.Key.Item2} x{g.Count()}")));
            return found;
        }
        var view = await c.Call("window.getView", new { windowId });
        try
        {
            await c.Call("test.theme.apply", new { windowId, customTheme = "dracula" });
            foreach (var (page, where) in new[] { ("outline", "outline"), ("files", "folder") })
            {
                var marks = await Indicators(page, "dracula");
                Check(marks.Any(x => x.Where == where), $"the {where} rows have selection pills ({marks.Count} indicators in all)");
                Check(marks.Where(x => x.Where == where).All(x => x.Fill == Dracula), $"dracula: the {where} rows' pills are its accent");
                Check(marks.Where(x => x.Where == "tabs").All(x => x.Fill == Dracula), "dracula: the bar under Files/Outline is its accent");
            }
            await c.Call("test.theme.apply", new { windowId, builtIn = "Default" });
            var plain = await Indicators("outline", "default");
            Check(plain.Any(x => x.Where == "outline") && plain.All(x => x.Fill != Dracula), "no custom theme: the system accent again");
        }
        finally
        {
            try { await c.Call("test.theme.apply", new { windowId, builtIn = "Default" }); } catch { }
            try { await c.Call("window.setView", new { windowId, sidePane = new { open = (bool)view["sidePane"]!["open"]!, page = (string)view["sidePane"]!["page"]! } }); } catch { }
        }
    }

    // Closing the shown tab left the outline on the closed document's headings (or empty) instead of the one shown next.
    private static async Task TC01(List<string> notes)
    {
        using var c = await Session("e2e TC01");
        var first = await Open(c, Fixture("tc01-a.md", "# Alpha one\n\n## Alpha two\n\nText\n"));
        var second = await Open(c, Fixture("tc01-b.md", "# Beta one\n\n## Beta two\n\n## Beta three\n\nText\n"));
        var windowId = await WindowIdOf(c, second);
        var window = await WindowOf(windowId, c);
        await c.Call("window.setView", new { windowId, sidePane = new { open = true, page = "outline" } });
        await Task.Delay(1500);
        List<string> Outline()
        {
            var names = new List<string>();
            foreach (System.Windows.Automation.AutomationElement e in System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition))
            {
                var n = e.Current.Name;
                if ((n.StartsWith("Alpha") || n.StartsWith("Beta")) && e.Current.ControlType != System.Windows.Automation.ControlType.Document && !names.Contains(n)) names.Add(n);
            }
            return names;
        }
        try
        {
            var before = Outline();
            notes.Add("outline with tc01-b shown: " + string.Join(", ", before));
            Check(before.Contains("Beta three") && !before.Contains("Alpha two"), "the outline shows the shown tab's headings");
            var tab = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.TabItem))
                .Cast<System.Windows.Automation.AutomationElement>().FirstOrDefault(t => t.Current.Name.Contains("tc01-b")) ?? throw new CaseFailed("no tc01-b tab");
            var close = tab.FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button))
                .Cast<System.Windows.Automation.AutomationElement>().FirstOrDefault() ?? throw new CaseFailed("no close button on the tab");
            await Activate(window);
            var r = close.Current.BoundingRectangle;
            SetCursorPos((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2));
            await Task.Delay(150);
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
            await Task.Delay(1000);
            // With many tabs open (earlier cases leave theirs) the button can be scrolled out of the strip and the click
            // land on nothing: then it is pressed through UI Automation, as an assistive tool would.
            if (((JArray)(await c.Call("document.list", new { windowId }))["documents"]!).Any(d => (string)d["documentId"]! == second))
            {
                notes.Add("the click missed the close button; invoked");
                ((System.Windows.Automation.InvokePattern)close.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            }
            // The tab shown next is the window's choice (other cases may have left tabs open): its headings, from its
            // text, are what the outline must show - and none of the closed document's.
            var active = "";
            List<string> headings = new(), names = new();
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(200);
                active = (string)((JArray)(await c.Call("window.list"))["windows"]!).First(w => (string)w["windowId"]! == windowId)["activeDocumentId"]!;
                headings = ((string?)(await Get(c, active))["text"] ?? "").Split('\n').Where(l => l.StartsWith("#")).Select(l => l.TrimStart('#').Trim()).Where(h => h.Length > 0).Take(3).ToList();
                names = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition)
                    .Cast<System.Windows.Automation.AutomationElement>().Select(e => e.Current.Name).ToList();
                if (active != second && headings.All(names.Contains) && !names.Contains("Beta three")) break;
            }
            notes.Add($"after closing tc01-b, shown {(active == first ? "tc01-a" : active)} with headings {string.Join(", ", headings)}; outline has them: {headings.All(names.Contains)}, has Beta three: {names.Contains("Beta three")}");
            Check(active != second, "another tab is shown");
            Check(headings.All(names.Contains) && !names.Contains("Beta three"), "the outline shows the shown document's headings, not the closed one's");
        }
        finally
        {
            try { await c.Call("window.setView", new { windowId, sidePane = new { open = false } }); } catch { }
        }
    }

    // Three long documents; the first one clicked, then closed. The tab strip selects its neighbour (B) while the window
    // goes back to the tab used last (C): two switches in a row, two page loads, and the outline must end up as the
    // shown document's.
    private static async Task TC02(List<string> notes)
    {
        using var c = await Session("e2e TC02");
        string Long(string name) => string.Concat(Enumerable.Range(1, 100).Select(i => $"## {name} {i}\n\nText {i} of {name}.\n\n"));
        var a = await Open(c, Fixture("tc02-a.md", "# Apple\n\n" + Long("Apple")));
        var b = await Open(c, Fixture("tc02-b.md", "# Banana\n\n" + Long("Banana")));
        var cc = await Open(c, Fixture("tc02-c.md", "# Cherry\n\n" + Long("Cherry")));
        var windowId = await WindowIdOf(c, cc);
        var window = await WindowOf(windowId, c);
        await c.Call("window.setView", new { windowId, sidePane = new { open = true, page = "outline" } });
        await Task.Delay(1500);
        var root = System.Windows.Automation.AutomationElement.FromHandle(window);
        System.Windows.Automation.AutomationElement Tab(string name) => root.FindAll(System.Windows.Automation.TreeScope.Descendants,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.TabItem))
            .Cast<System.Windows.Automation.AutomationElement>().FirstOrDefault(t => t.Current.Name.Contains(name)) ?? throw new CaseFailed($"no {name} tab");
        void ShowTab(string name)
        {
            if (Tab(name).TryGetCurrentPattern(System.Windows.Automation.ScrollItemPattern.Pattern, out var scroll))
                try { ((System.Windows.Automation.ScrollItemPattern)scroll).ScrollIntoView(); } catch (InvalidOperationException) { }
        }
        void Click(System.Windows.Rect r, double fx = 0.3) { SetCursorPos((int)(r.Left + r.Width * fx), (int)(r.Top + r.Height / 2)); Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } }); }
        // The outline's first rows (the tree is virtualized; its top shows which document it is of).
        string Top() => string.Join(", ", root.FindAll(System.Windows.Automation.TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition)
            .Cast<System.Windows.Automation.AutomationElement>().Select(e => e.Current.Name)
            .Where(n => n.StartsWith("Apple") || n.StartsWith("Banana") || n.StartsWith("Cherry")).Distinct().Take(3));
        async Task<string> Shown() => (string)((JArray)(await c.Call("window.list"))["windows"]!).First(w => (string)w["windowId"]! == windowId)["activeDocumentId"]!;
        try
        {
            await Activate(window);
            // With many tabs open (the edition's suite leaves more) the strip scrolls: tc02-a brought into view first, or
            // the click lands beside the window, on the desktop.
            ShowTab("tc02-a");
            notes.Add($"tc02-a at {Tab("tc02-a").Current.BoundingRectangle}, window {root.Current.BoundingRectangle}");
            Click(Tab("tc02-a").Current.BoundingRectangle);
            // A window just brought forward through a remote desktop session can take the first click as its activation:
            // once more if the tab did not come up (what is checked here is the outline after the close, below).
            if (!await Eventually(async () => await Shown() == a, 2000))
            {
                notes.Add("the first click on tc02-a did not bring it up; clicked again");
                await Activate(window);
                ShowTab("tc02-a");
                notes.Add($"tc02-a at {Tab("tc02-a").Current.BoundingRectangle}");
                Click(Tab("tc02-a").Current.BoundingRectangle);
                await Eventually(async () => await Shown() == a, 2000);
            }
            Check(await Shown() == a, "the click shows tc02-a");
            notes.Add("tc02-a shown, outline: " + Top());
            var close = Tab("tc02-a").FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.Button))
                .Cast<System.Windows.Automation.AutomationElement>().FirstOrDefault() ?? throw new CaseFailed("no close button");
            Click(close.Current.BoundingRectangle, 0.5);
            await Task.Delay(1000);
            if (((JArray)(await c.Call("document.list", new { windowId }))["documents"]!).Any(d => (string)d["documentId"]! == a))
            {
                notes.Add("the click missed the close button; invoked");
                ((System.Windows.Automation.InvokePattern)close.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            }
            string shownName = "", top = "";
            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(250);
                var shown = await Shown();
                shownName = shown == b ? "Banana" : shown == cc ? "Cherry" : shown == a ? "Apple" : shown;
                top = Top();
                if (i > 8 && top.StartsWith(shownName)) break;
            }
            notes.Add($"after closing tc02-a: shown {shownName}, outline {top}");
            Check(top.Length > 0 && top.Split(", ").All(n => n.StartsWith(shownName)), $"the outline is the shown document's ({shownName}: {top})");
        }
        finally
        {
            try { await c.Call("window.setView", new { windowId, sidePane = new { open = false } }); } catch { }
        }
    }

    // An export that worked said nothing at all: the reader looked for the file to know whether it had.
    private static async Task EX02(List<string> notes)
    {
        using var c = await Session("e2e EX02");
        var id = await Open(c, Fixture("ex02.md", "# EX02\n\nExported.\n"));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        var before = (int)(await c.Call("test.settings.get", new { windowId, name = "AfterExport" }))["value"]!;
        async Task<string?> Notice() => (string?)(await c.Call("test.export.notice", new { windowId }))["path"];
        try
        {
            notes.Add($"after export at first: {before}");
            await c.Call("test.settings.set", new { windowId, name = "AfterExport", value = 1 });
            var path = Path.Combine(testRoot, "ex02.html");
            if (File.Exists(path)) File.Delete(path);
            await c.Call("test.export.run", new { windowId, type = "html", path });
            string? shown = null;
            for (var i = 0; i < 75 && !(File.Exists(path) && shown == path); i++) { await Task.Delay(200); shown = await Notice(); }
            var text = (string?)(await c.Call("test.export.notice", new { windowId }))["text"];
            notes.Add($"written: {File.Exists(path)}; notice: {text}");
            Check(File.Exists(path), "the export is written");
            Check(shown == path && text != null && text.Contains(path), "a notice says where");
            // The notice's frame has no automation peer (a Border); its text and buttons do.
            Check(FindIn(window, "ExportNoticeText") != null && FindIn(window, "ExportNoticeOpen") != null && FindIn(window, "ExportNoticeFolder") != null, "it is on the window, with buttons to open the file and its folder");
            var close = FindIn(window, "ExportNoticeClose") ?? throw new CaseFailed("no close button on the notice");
            ((System.Windows.Automation.InvokePattern)close.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            await Task.Delay(500);
            Check(await Notice() == null && FindIn(window, "ExportNoticeText") == null, "closed, it is gone");

            await c.Call("test.settings.set", new { windowId, name = "AfterExport", value = 0 });
            var quiet = Path.Combine(testRoot, "ex02-quiet.html");
            if (File.Exists(quiet)) File.Delete(quiet);
            await c.Call("test.export.run", new { windowId, type = "html", path = quiet });
            for (var i = 0; i < 75 && !File.Exists(quiet); i++) await Task.Delay(200);
            await Task.Delay(1500);
            Check(File.Exists(quiet), "with no action the export is still written");
            Check(await Notice() == null, "and no notice shows");
        }
        finally
        {
            try { await c.Call("test.settings.set", new { windowId, name = "AfterExport", value = before }); } catch { }
            try { await c.Call("document.close", new { documentId = id }); } catch { }
        }
    }

    // Copy to path seemed not to work: the pictures were fetched one after another (the web addresses showed for many
    // seconds), with no referrer (a site with hotlink protection refused them, or answered with a page that was saved
    // as a .png), and an untitled document's pictures stayed in the default picture folder once it was saved elsewhere.
    private static async Task WP02(List<string> notes)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAIAAAB/FOjAAAAAE0lEQVR4nGPQqDhBEmIY1UALDQCJj7QBup2ubwAAAABJRU5ErkJggg==");
        var server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        server.Start();
        var port = ((System.Net.IPEndPoint)server.LocalEndpoint).Port;
        var site = $"http://127.0.0.1:{port}";
        var served = new List<string>();
        var serving = Task.Run(async () =>
        {
            while (true)
            {
                System.Net.Sockets.TcpClient client;
                try { client = await server.AcceptTcpClientAsync(); } catch { return; }
                _ = Task.Run(async () =>
                {
                    using (client)
                    {
                        var stream = client.GetStream();
                        var reader = new StreamReader(stream);
                        var request = await reader.ReadLineAsync() ?? "";
                        string referrer = "", line;
                        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                            if (line.StartsWith("Referer:", StringComparison.OrdinalIgnoreCase)) referrer = line.Substring(8).Trim();
                        var path = request.Split(' ').Skip(1).FirstOrDefault() ?? "";
                        lock (served) served.Add($"{path} referrer={referrer}");
                        string status = "200 OK", type = "image/png";
                        var body = png;
                        if (path == "/guarded.png" && !referrer.StartsWith(site + "/article/")) { status = "403 Forbidden"; type = "text/plain"; body = Encoding.ASCII.GetBytes("hotlink"); }
                        // As cnBeta's CDN: refused whenever a referrer is sent that it does not like - here, any.
                        if (path == "/bare.png" && referrer.Length > 0) { status = "403 Forbidden"; type = "text/plain"; body = Encoding.ASCII.GetBytes("hotlink"); }
                        if (path == "/login.png") { type = "text/html"; body = Encoding.ASCII.GetBytes("<html><body>Please sign in</body></html>"); }
                        await Task.Delay(1500);
                        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
                        await stream.WriteAsync(body);
                    }
                });
            }
        });
        using var c = await Session("e2e WP02");
        var windowId = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        var pictures = Path.Combine(testRoot, "wp02-pictures");
        var saved = Path.Combine(testRoot, "wp02-saved");
        foreach (var d in new[] { pictures, saved }) if (Directory.Exists(d)) Directory.Delete(d, true);
        var pictureFolderBefore = (string?)(await c.Call("test.settings.get", new { windowId, name = "DefaultImageBasePath" }))["value"];
        var id = (string)(await c.Call("document.create", new { text = "# WP02\n\n", normalizationPolicy = "allowUnknown" }))["documentId"]!;
        var fragment = $"<p><img src=\"{site}/guarded.png\" alt=\"guarded\"></p><p><img src=\"{site}/login.png\" alt=\"login\"></p>"
            + $"<p><img src=\"{site}/one.png\" alt=\"one\"></p><p><img src=\"{site}/two.png\" alt=\"two\"></p><p><img src=\"{site}/three.png\" alt=\"three\"></p>"
            + $"<p><img src=\"{site}/bare.png\" alt=\"bare\"></p>";
        var html = $"Version:0.9\r\nStartHTML:0\r\nEndHTML:0\r\nStartFragment:0\r\nEndFragment:0\r\nSourceURL:{site}/article/page.html\r\n<html><body><!--StartFragment-->{fragment}<!--EndFragment--></body></html>";
        try
        {
            // An untitled document's pictures go to the default picture folder: one of this run's, not the user's.
            await c.Call("test.settings.set", new { windowId, name = "DefaultImageBasePath", value = pictures });
            await c.Call("test.images.webAction", new { windowId, action = "copy", path = "./images" });
            await c.Call("document.focus", new { documentId = id });
            await c.Call("test.editor.focus", new { windowId });
            var started = DateTime.Now;
            var pasting = c.Call("test.paste.html", new { windowId, html });
            // The picture that is not one is said in a dialog once the others are in.
            var (_, said) = await PressDialogButton(c, windowId, DialogButton.Close, 20000);
            await pasting;
            var took = (DateTime.Now - started).TotalSeconds;
            string text = "";
            await Eventually(async () => (text = (string)(await Get(c, id))["text"]!).Contains("](./images/"), 5000);
            notes.Add($"took {took:0.0}s; said: {said?.Replace("\n", " | ")}");
            notes.Add("text: " + text.Replace("\n", "\\n"));
            notes.Add("served: " + string.Join(", ", served));
            Check(!text.Contains($"{site}/guarded.png") && served.Any(x => x.StartsWith("/guarded.png referrer=" + site + "/article/page.html")), "the protected picture is fetched with the page as referrer and copied");
            Check(text.Contains($"]({site}/login.png)") && said != null && said.Contains("login.png") && said.Contains("not a picture"), "a page that is not a picture keeps its web address and is said");
            Check(!text.Contains($"{site}/bare.png"), "a picture refused with the page as referrer is fetched again with none (cnBeta's)");
            Check(!text.Contains($"{site}/one.png") && !text.Contains($"{site}/two.png") && !text.Contains($"{site}/three.png"), "the others are copied");
            Check(took < 7, $"six pictures of 1.5 s each are fetched together, not one after another (9 s and more) ({took:0.0}s)");
            var inPictures = Directory.Exists(Path.Combine(pictures, "images")) ? Directory.GetFiles(Path.Combine(pictures, "images")).Length : 0;
            Check(inPictures == 5, $"untitled, its five pictures are in the default picture folder ({inPictures})");

            // Saved into a folder of its own: the pictures come along, so ./images/... still points at them.
            var file = Path.Combine(saved, "wp02.md");
            Directory.CreateDirectory(saved);
            var result = (string?)(await c.Call("test.file.saveAs", new { windowId, path = file }))["path"];
            var along = Directory.Exists(Path.Combine(saved, "images")) ? Directory.GetFiles(Path.Combine(saved, "images")).Select(Path.GetFileName).ToList() : new List<string?>();
            var referenced = System.Text.RegularExpressions.Regex.Matches(text, @"\]\(\./images/([^)]+)\)").Select(m => m.Groups[1].Value).ToList();
            notes.Add($"saved as {result}; images beside it: {string.Join(", ", along)}");
            Check(result == file && File.Exists(file), "the untitled document is saved where asked");
            Check(referenced.Count == 5 && referenced.All(r => along.Contains(r)), "every picture it points at is beside it now");
        }
        finally
        {
            try { await c.Call("test.images.webAction", new { windowId, action = "none" }); } catch { }
            try { if (pictureFolderBefore != null) await c.Call("test.settings.set", new { windowId, name = "DefaultImageBasePath", value = pictureFolderBefore }); } catch { }
            try { await c.Call("document.close", new { documentId = id }); } catch { }
            server.Stop();
        }
    }

    // Chosen on Settings > General, the file startup action was not there after the next start (the file had no
    // FileStartupAction at all), while the theme and font chosen the same day were.
    private static async Task SE01(List<string> notes)
    {
        using var c = await Session("e2e SE01");
        var windowId = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]["windowId"]!;
        var window = await WindowOf(windowId, c);
        string? Stored()
        {
            var file = JObject.Parse(File.ReadAllText(Path.Combine(testRoot, "data", "Settings.json")));
            return file["FileStartupAction"]?.ToString();
        }
        var before = (int)(await c.Call("test.settings.get", new { windowId, name = "FileStartupAction" }))["value"]!;
        try
        {
            await c.Call("test.window.navigate", new { windowId, route = "Settings/General" });
            await Activate(window);
            var combo = FindIn(window, "FileStartupAction");
            if (combo == null)
            {
                // Inside the startup action's expander, built only once it is opened.
                // Its title in either interface language, clicked as a person would (the expander itself has no name).
                var title = FindNamed(window, "启动操作") ?? FindNamed(window, "Startup");
                notes.Add("startup action title: " + (title == null ? "not found" : "found"));
                // The expander is the nearest element above the title that opens and closes.
                var walker = System.Windows.Automation.TreeWalker.ControlViewWalker;
                for (var node = title; node != null; node = walker.GetParent(node))
                {
                    if (node.TryGetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern, out var open))
                    {
                        notes.Add($"expander: {node.Current.ControlType.ProgrammaticName} '{node.Current.Name}'");
                        ((System.Windows.Automation.ExpandCollapsePattern)open).Expand();
                        break;
                    }
                }
                await Task.Delay(800);
                var combos = System.Windows.Automation.AutomationElement.FromHandle(window).FindAll(System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.ComboBox))
                    .Cast<System.Windows.Automation.AutomationElement>().ToList();
                notes.Add("combo boxes: " + string.Join(", ", combos.Select(x => $"'{x.Current.AutomationId}'/'{x.Current.Name}'")));
                // The first one in the expander is the file's (an automation id once the host has it).
                combo = FindIn(window, "FileStartupAction") ?? combos.FirstOrDefault() ?? throw new CaseFailed("no file startup action box");
            }
            ((System.Windows.Automation.ExpandCollapsePattern)combo.GetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern)).Expand();
            await Task.Delay(500);
            var items = combo.FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.ListItem))
                .Cast<System.Windows.Automation.AutomationElement>().ToList();
            notes.Add("items: " + string.Join(", ", items.Select(i => i.Current.Name)));
            Check(items.Count == 3, "three file startup actions are offered");
            // The last one: restore the session (as the reader chose).
            ((System.Windows.Automation.SelectionItemPattern)items[2].GetCurrentPattern(System.Windows.Automation.SelectionItemPattern.Pattern)).Select();
            await Task.Delay(300);
            try { ((System.Windows.Automation.ExpandCollapsePattern)combo.GetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern)).Collapse(); } catch { }
            var model = 0;
            string? stored = null;
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(200);
                model = (int)(await c.Call("test.settings.get", new { windowId, name = "FileStartupAction" }))["value"]!;
                stored = Stored();
                if (model == 2 && stored == "2") break;
            }
            notes.Add($"after choosing it: settings model {model}, Settings.json {stored ?? "(none)"}");
            Check(model == 2, "the settings take the choice");
            Check(stored == "2", "and the settings file holds it");

            // Leaving the settings page (back to the document) keeps it.
            await c.Call("test.window.navigate", new { windowId, route = "Main" });
            await Task.Delay(1000);
            notes.Add("after leaving the settings page: Settings.json " + (Stored() ?? "(none)"));
            Check(Stored() == "2", "leaving the settings page keeps it in the file");
        }
        finally
        {
            try { await c.Call("test.window.navigate", new { windowId, route = "Main" }); } catch { }
            try { await c.Call("test.settings.set", new { windowId, name = "FileStartupAction", value = before }); } catch { }
        }
    }

    // Resize table showed 4 rows and 3 columns whatever the table was, and Cancel broke the page's handling of it.
    private static async Task TB01(List<string> notes)
    {
        using var c = await Session("e2e TB01");
        const string text0 = "# TB01\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |\n";
        var id = await Open(c, Fixture("tb01.md", text0));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        IEnumerable<System.Windows.Automation.AutomationElement> HostElements(System.Windows.Automation.ControlType type)
        {
            foreach (System.Windows.Automation.AutomationElement top in System.Windows.Automation.AutomationElement.RootElement.FindAll(System.Windows.Automation.TreeScope.Children,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ProcessIdProperty, hostPid)))
                foreach (System.Windows.Automation.AutomationElement e in top.FindAll(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, type)))
                    yield return e;
        }
        List<System.Windows.Automation.AutomationElement> NumberBoxes() => HostElements(System.Windows.Automation.ControlType.Edit)
            .Where(e => e.TryGetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern, out _) && !e.Current.IsOffscreen).ToList();
        string Value(System.Windows.Automation.AutomationElement e) => ((System.Windows.Automation.ValuePattern)e.GetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern)).Current.Value;
        void Press(string zh, string en)
        {
            var button = HostElements(System.Windows.Automation.ControlType.Button).FirstOrDefault(b => b.Current.Name == zh || b.Current.Name == en) ?? throw new CaseFailed($"no {en} button");
            ((System.Windows.Automation.InvokePattern)button.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
        }
        // The caret in the table's first body cell (the toolbar shows while it is in the table), then the toolbar's
        // Resize table.
        async Task<List<System.Windows.Automation.AutomationElement>> OpenDialog()
        {
            // A click in the cell, as a hand does it: the pointer moved there first, then pressed and released. Tried
            // again when the toolbar does not show (a first click in a window can go to bringing it forward).
            bool? clicked = false;
            for (var attempt = 0; attempt < 3 && clicked != true; attempt++)
            {
                await Activate(window);
                // The page may not have drawn the table yet (the eval answers null then): asked again until it has.
                JToken at = JValue.CreateNull();
                for (var wait = 0; wait < 25 && at.Type != JTokenType.Object; wait++)
                {
                    if (wait > 0) await Task.Delay(200);
                    at = (await c.Call("test.editor.eval", new { windowId, script = "(() => { const td = document.querySelector('#ag-editor-id table tbody td'); if (!td) return null; const r = td.getBoundingClientRect(); return { x: Math.round(r.left + r.width / 2), y: Math.round(r.top + r.height / 2) } })()" }))["result"] ?? JValue.CreateNull();
                }
                if (at.Type != JTokenType.Object) throw new CaseFailed("the table's first cell is not on the page");
                var screen = await c.Call("test.editor.screenPoint", new { windowId, x = (int)at["x"]!, y = (int)at["y"]! });
                SetProcessDpiAwarenessContext(new IntPtr(-4));
                SetCursorPos((int)screen["x"]! - 6, (int)screen["y"]!);
                for (var i = 0; i < 3; i++) { await Task.Delay(60); Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = 2, dy = 0, dwFlags = 0x0001 } } }); }
                Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } });
                await Task.Delay(60);
                Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
                await Task.Delay(800);
                clicked = (bool?)(await c.Call("test.editor.eval", new { windowId, script = "(() => { const b = document.querySelector('[data-label=\"table\"]'); if (!b) return false; b.dispatchEvent(new MouseEvent('click', { bubbles: true })); return true })()" }))["result"];
            }
            Check(clicked == true, "the table toolbar has Resize table");
            List<System.Windows.Automation.AutomationElement> boxes = new();
            for (var i = 0; i < 30 && boxes.Count < 2; i++) { await Task.Delay(200); boxes = NumberBoxes(); }
            Check(boxes.Count >= 2, $"the Resize table dialog shows its two number boxes ({boxes.Count})");
            return boxes;
        }
        var shown = await OpenDialog();
        var values = shown.Select(Value).ToList();
        notes.Add("the dialog shows: " + string.Join(", ", values));
        Check(values.Count >= 2 && values[0].StartsWith("3") && values[1].StartsWith("2"), $"the dialog starts from the table's own size, 3 rows (the header counted) and 2 columns ({string.Join(", ", values)})");

        Press("取消", "Cancel");
        await Task.Delay(800);
        var afterCancel = (string)(await Get(c, id))["text"]!;
        Check(afterCancel == text0, $"Cancel leaves the table as it was ({JsonConvert.SerializeObject(afterCancel)})");

        // Again, with a row more, and OK.
        shown = await OpenDialog();
        ((System.Windows.Automation.ValuePattern)shown[0].GetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern)).SetValue("4");
        // A number box takes what was typed when it loses the focus: the other box takes it before OK.
        shown[1].SetFocus();
        await Task.Delay(400);
        Press("确定", "OK");
        string text = "";
        for (var i = 0; i < 20 && text.Split('\n').Count(l => l.StartsWith("|")) != 5; i++) { await Task.Delay(200); text = (string)(await Get(c, id))["text"]!; }
        notes.Add("after OK: " + JsonConvert.SerializeObject(text));
        Check(text.Split('\n').Count(l => l.StartsWith("|")) == 5, "OK with 4 rows gives the table a fourth row (5 lines with the delimiter row)");
    }

    // Reading mode, as a reader uses it: Ctrl+Z after an edit, Ctrl+A, a right-click on the text, copy as plain text
    // from the menu. Undo replaced the document from the host's history even there; Copy stayed disabled whatever was
    // selected (the page reported no selection without a caret); select-all selected nothing.
    private static async Task RD01(List<string> notes)
    {
        using var c = await Session("e2e RD01");
        // A picture in a folder with a Chinese name (Word showed none whose address was percent-encoded) and an SVG (Word
        // shows no SVG from pasted HTML: it goes in as PNG).
        // And one whose name has a space, written in angle brackets: the editor drew it as an empty picture.
        var path = Fixture("rd01.md", "# RD01 **bold**\n\nAlpha *beta* ![pic](图片/流程图.png) ![svg](rd01.svg) ![sp](<带 空格.png>).\n");
        var picture = Path.Combine(Path.GetDirectoryName(path)!, "图片", "流程图.png");
        Directory.CreateDirectory(Path.GetDirectoryName(picture)!);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "rd01.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"30\" height=\"20\"><rect width=\"30\" height=\"20\" fill=\"red\"/></svg>");
        // A real picture, drawn in the page (its <img> is what Copy turns into HTML).
        File.WriteAllBytes(picture, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        File.Copy(picture, Path.Combine(Path.GetDirectoryName(path)!, "带 空格.png"), true);
        var id = await Open(c, path);
        var windowId = await WindowIdOf(c, id);
        async Task Probe(string when) => notes.Add($"basePath {when}: " + (await c.Call("test.editor.eval", new { windowId, script = "String(window.basePath) + ' | ' + document.querySelectorAll('#ag-editor-id img').length + ' img' + ' | drawn: ' + [...document.querySelectorAll('#ag-editor-id img')].map(i => i.src).join(', ') + ' | failed: ' + [...document.querySelectorAll('#ag-editor-id .ag-image-fail')].map(e => e.getAttribute('data-raw')).join(', ')" }))["result"]);
        await Task.Delay(1000);
        await Probe("after open");
        await TypeInto(c, id, "X");
        await WaitForPage(c, id, t => t.Contains('X'), "the keystroke");
        await Task.Delay(500);
        var edited = (string)(await Get(c, id))["text"]!;
        await Probe("after typing");
        var window = await WindowOf(windowId, c);
        try
        {
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = true });
            await Task.Delay(800);
            await Probe("in reading mode");
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await Task.Delay(200);
            Keys(("ctrl", (ushort)0x5A));
            await Task.Delay(1000);
            Check((string)(await Get(c, id))["text"]! == edited, "Ctrl+Z in reading mode leaves the document as it was");
            // Edit > Undo, clicked the way a screen reader clicks it: this one went to the history directly.
            await InvokeMenuBarItem(window, "UndoItem");
            await Task.Delay(1000);
            Check((string)(await Get(c, id))["text"]! == edited, "Edit > Undo in reading mode leaves the document as it was");
            // The keyboard back to the page after the menu bar had it.
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await Task.Delay(300);
            // An automation client's undo is a write, which reading mode allows.
            await c.Call("document.undo", new { documentId = id, baseRevision = await Revision(c, id) });
            Check(!((string)(await Get(c, id))["text"]!).Contains('X'), "document.undo in reading mode still undoes");
            await c.Call("document.redo", new { documentId = id, baseRevision = await Revision(c, id) });
            Check((string)(await Get(c, id))["text"]! == edited, "document.redo puts it back");
            await Task.Delay(800);
            await Probe("after the redo");
            Check(notes[^1].Contains(" 3 img") && !notes[^1].Contains("undefined"), "after a redo the pictures are still drawn (the page kept the document's folder)");

            Keys(("ctrl", (ushort)0x41));
            await Task.Delay(500);
            var selected = (string?)(await c.Call("test.editor.eval", new { windowId, script = "getSelection().toString()" }))["result"];
            Check(selected?.Contains("Alpha") == true, $"Ctrl+A selects the document (selected {JsonConvert.SerializeObject(selected)})");

            var menu = await ContextMenuAt(c, windowId, window, "#ag-editor-id p");
            notes.Add("reading mode menu: " + string.Join(", ", menu.Select(m => m.Key + (m.Value.Current.IsEnabled ? "" : " (disabled)"))));
            foreach (var editing in new[] { "UndoItem", "CutItem", "PasteItem", "DeleteItem", "MenuFormatItem" })
                Check(!menu.ContainsKey(editing), $"no {editing} in reading mode");
            Check(menu.TryGetValue("CopyItem", out var copy) && copy.Current.IsEnabled, "Copy is there and enabled");
            Check(menu.TryGetValue("SelectAllItem", out _), "Select all is there");
            Check(menu.TryGetValue("CopyAsPlainTextItem", out var plain) && plain.Current.IsEnabled, "Copy as plain text is there and enabled");
            ((System.Windows.Automation.InvokePattern)plain!.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            string? clip = null;
            for (var i = 0; i < 30 && clip?.Contains("Alpha") != true; i++) { await Task.Delay(100); clip = ClipboardText(); }
            Check(clip == "RD01 bold\n\nAlpha beta pic svg sp.X", $"copy as plain text: the text without Markdown (clipboard {JsonConvert.SerializeObject(clip)})");

            // Copy puts the copy on the clipboard twice: formatted (HTML, for Word and mail) and as Markdown text. The text
            // used to replace the HTML, so Copy was Copy as Markdown.
            var again = await ContextMenuAt(c, windowId, window, "#ag-editor-id p");
            ((System.Windows.Automation.InvokePattern)again["CopyItem"].GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            string? markdown = null;
            for (var i = 0; i < 30 && markdown?.Contains("**bold**") != true; i++) { await Task.Delay(100); markdown = ClipboardText(); }
            var html = ClipboardHtml();
            notes.Add("Copy: text " + JsonConvert.SerializeObject(markdown) + ", HTML " + JsonConvert.SerializeObject(html?.Length > 300 ? html[..300] : html));
            Check(markdown?.Contains("**bold**") == true && markdown.Contains("Alpha *beta*"), "Copy: the text is the Markdown");
            Check(html?.Contains("<strong>bold</strong>") == true, "Copy: the formatted copy (HTML) is there too");
            // Word and mail have no document folder: a relative image showed as an empty frame there.
            var fileUrl = "file:///" + picture.Replace('\\', '/');
            Check(html?.Contains($"src=\"{fileUrl}\"") == true, $"Copy: the picture in the HTML is at its file address, its name as it is ({fileUrl})");
            Check(html?.Contains("src=\"data:image/png;base64,") == true && html.Contains("width=\"30\"") && !html.Contains(".svg"), "Copy: the SVG is in the HTML as a PNG of its size");
            Check(markdown?.Contains("![pic](图片/流程图.png) ![svg](rd01.svg) ![sp](<带 空格.png>)") == true, "Copy: the Markdown keeps the pictures as written");
            // The copy pasted into Word itself, where it is read (only where Word is installed).
            if (PasteIntoWord(Path.GetDirectoryName(path)!) is { } word)
            {
                notes.Add($"Word: {word.shapes} picture(s) pasted, {word.media} embedded; {word.detail}");
                // All three are pictures in the document (type 3), the SVG at the size it is shown at (30x20 px), not at
                // its PNG's twice as many pixels. The two PNGs are the same file, which Word embeds once.
                Check(word.detail == "type 3 0.8x0.8pt, type 3 22.5x15pt, type 3 0.8x0.8pt" && word.media == 2, "Word: every picture of the copy is in the pasted document, the SVG at its size");
            }
            else
                notes.Add("Word: not installed, not tried");

            // The visual editor keeps its editing commands.
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = false });
            await Task.Delay(800);
            var visual = await ContextMenuAt(c, windowId, window, "#ag-editor-id p");
            notes.Add("visual mode menu: " + string.Join(", ", visual.Keys));
            Check(visual.ContainsKey("UndoItem") && visual.ContainsKey("CutItem") && visual.ContainsKey("PasteItem") && visual.ContainsKey("CopyAsPlainTextItem"), "the visual editor's menu has undo, cut, paste and copy as plain text");
            Send(Key(0x1B, false), Key(0x1B, true));
            await Task.Delay(300);

            // Pasted back into the document, the copy is the Markdown it was copied as (not its HTML turned back).
            await Activate(window);
            await c.Call("test.editor.focus", new { windowId });
            await Task.Delay(200);
            Send(Key(0x11, false), Key(0x23, false), Key(0x23, true), Key(0x11, true));
            Keys((ushort)0x0D, ("ctrl", (ushort)0x56));
            string pasted = "";
            for (var i = 0; i < 30 && pasted.Split("Alpha").Length < 3; i++) { await Task.Delay(200); pasted = (string)(await Get(c, id))["text"]!; }
            notes.Add("after the paste: " + JsonConvert.SerializeObject(pasted));
            notes.Add("page after the paste: " + (await c.Call("test.editor.eval", new { windowId, script = "document.getElementById('ag-editor-id').innerHTML.replace(/<svg[\\s\\S]*?<\\/svg>/g, '').slice(0, 1500)" }))["result"]);
            Check(pasted.Split("**bold**").Length == 3 && pasted.Split("Alpha *beta*").Length == 3 && !pasted.Contains("<img") && !pasted.Contains("<h1"),
                "pasting the copy back gives the Markdown it was copied as");
        }
        finally
        {
            Send(Key(0x1B, false), Key(0x1B, true));
            await c.Call("test.settings.set", new { windowId, name = "ReadOnly", value = false });
        }
    }

    /// <summary>
    /// Right-clicks the first element matching the selector (a real click, at its left part) and returns the context
    /// menu's items by automation id (their x:Name), read the way a screen reader reads them. Collapsed items are not
    /// there. Where the page point is on screen comes from the test host (test.editor.screenPoint), in physical pixels.
    /// </summary>
    private static async Task<Dictionary<string, System.Windows.Automation.AutomationElement>> ContextMenuAt(Client c, string windowId, IntPtr window, string selector)
    {
        await Activate(window);
        var at = (await c.Call("test.editor.eval", new { windowId, script = $"(() => {{ const r = document.querySelector({JsonConvert.ToString(selector)}).getBoundingClientRect(); return {{ x: Math.round(r.left + 20), y: Math.round(r.top + r.height / 2) }} }})()" }))["result"]!;
        var screen = await c.Call("test.editor.screenPoint", new { windowId, x = (int)at["x"]!, y = (int)at["y"]! });
        // Physical pixels: the driver must not be scaled by Windows (a no-op when it already is DPI-aware).
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        SetCursorPos((int)screen["x"]! - 2, (int)screen["y"]!);
        // As a mouse does it: the pointer moves onto the text (the window's XAML sees it arrive; a cursor that was
        // only put there opened no menu), then the button goes down and, a moment later, up.
        for (var i = 0; i < 2; i++)
        {
            await Task.Delay(60);
            Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = 0x0001 } } });
        }
        await Task.Delay(150);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0008 } } });
        await Task.Delay(80);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0010 } } });
        var items = new Dictionary<string, System.Windows.Automation.AutomationElement>();
        var menuItem = new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuItem);
        for (var i = 0; i < 30 && !items.ContainsKey("SelectAllItem"); i++)
        {
            await Task.Delay(100);
            items.Clear();
            // The menu may be a popup window of its own: every top-level window of the test host is searched.
            var roots = new List<System.Windows.Automation.AutomationElement> { System.Windows.Automation.AutomationElement.FromHandle(window) };
            foreach (System.Windows.Automation.AutomationElement top in System.Windows.Automation.AutomationElement.RootElement.FindAll(System.Windows.Automation.TreeScope.Children,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ProcessIdProperty, hostPid)))
                if (top.Current.NativeWindowHandle != window.ToInt32()) roots.Add(top);
            foreach (var root in roots)
            {
                foreach (System.Windows.Automation.AutomationElement item in root.FindAll(System.Windows.Automation.TreeScope.Descendants, menuItem))
                    if (item.Current.AutomationId is { Length: > 0 } name) items[name] = item;
                // The format row is a control of its own, not a menu item.
                if (root.FindFirst(System.Windows.Automation.TreeScope.Descendants, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, "MenuFormatItem")) is { } format)
                    items["MenuFormatItem"] = format;
            }
        }
        if (!items.ContainsKey("SelectAllItem"))
            throw new CaseFailed($"the context menu did not open at {screen.ToString(Formatting.None)} (or has no Select all; found {string.Join(", ", items.Keys)}); screen: {Screenshot("context-menu-" + DateTime.Now.ToString("HHmmss"))}");
        return items;
    }

    /// <summary>
    /// Invokes a menu bar command by its automation id (x:Name): each menu is opened until the one holding it. A
    /// disabled item is not invoked (UI Automation refuses it), which is fine for a check that it changes nothing.
    /// </summary>
    /// <summary>Why the last InvokeMenuBarItem did not invoke its item ("disabled", "not found"), or null when it did.</summary>
    private static string? lastMenuBarItemSkipped;

    private static async Task InvokeMenuBarItem(IntPtr window, string automationId, bool escapeAfter = true)
    {
        var invoked = false;
        lastMenuBarItemSkipped = null;
        var root = System.Windows.Automation.AutomationElement.FromHandle(window);
        var menuItem = new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuItem);
        var bar = Deep.First(root, new System.Windows.Automation.AndCondition(
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty, System.Windows.Automation.ControlType.MenuBar),
            // Not the window's system menu bar (one item, System), which a WinUI 3 window also shows.
            new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.FrameworkIdProperty, "XAML")))
            ?? throw new CaseFailed("no menu bar in the window");
        try
        {
            foreach (System.Windows.Automation.AutomationElement top in bar.FindAll(System.Windows.Automation.TreeScope.Children, menuItem))
            {
                if (!top.TryGetCurrentPattern(System.Windows.Automation.ExpandCollapsePattern.Pattern, out var topPattern)) continue;
                ((System.Windows.Automation.ExpandCollapsePattern)topPattern).Expand();
                await Task.Delay(400);
                var item = Deep.First(root, new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, automationId));
                if (item == null)
                {
                    ((System.Windows.Automation.ExpandCollapsePattern)topPattern).Collapse();
                    await Task.Delay(200);
                    continue;
                }
                if (item.Current.IsEnabled)
                {
                    // A toggle item (the Paragraph menu's Table, Quote) has no Invoke pattern: clicked, as a person does.
                    if (item.TryGetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern, out var invoke)) ((System.Windows.Automation.InvokePattern)invoke).Invoke();
                    else await ClickInPopup(window, item);
                    invoked = true;
                }
                else lastMenuBarItemSkipped = "disabled";
                return;
            }
            lastMenuBarItemSkipped = "not found";
            throw new CaseFailed($"no menu holds {automationId}");
        }
        finally
        {
            await Task.Delay(200);
            // Menus left open are closed - unless the caller wants what the command opened (a dialog) left alone.
            if (escapeAfter || !invoked)
                for (var i = 0; i < 2; i++) { Send(Key(0x1B, false), Key(0x1B, true)); await Task.Delay(150); }
        }
    }

    /// <summary>
    /// Pastes the clipboard into a new document of a Word of its own (hidden), saves it as .docx in the folder given and
    /// counts the pictures in it and the picture files embedded in the .docx (a picture Word could not load is none).
    /// Null where Word is not installed. A Word already running is left alone: when the new instance turns out to be
    /// that one, only the document made here is closed.
    /// </summary>
    private static (int shapes, int media, string detail)? PasteIntoWord(string folder)
    {
        var type = Type.GetTypeFromProgID("Word.Application");
        if (type == null) return null;
        var before = Process.GetProcessesByName("WINWORD").Select(p => p.Id).ToHashSet();
        dynamic word = Activator.CreateInstance(type)!;
        var own = Process.GetProcessesByName("WINWORD").Any(p => !before.Contains(p.Id));
        dynamic? document = null;
        try
        {
            if (own) word.Visible = false;
            document = word.Documents.Add();
            document.Content.Paste();
            int shapes = document.InlineShapes.Count;
            var detail = new List<string>();
            for (var i = 1; i <= shapes; i++)
            {
                var shape = document.InlineShapes[i];
                detail.Add($"type {(int)shape.Type} {(double)shape.Width:0.#}x{(double)shape.Height:0.#}pt");
            }
            var file = Path.Combine(folder, "word-paste.docx");
            document.SaveAs2(file, 16);
            document.Close(0);
            document = null;
            using var zip = System.IO.Compression.ZipFile.OpenRead(file);
            var media = zip.Entries.Count(e => e.FullName.StartsWith("word/media/"));
            return (shapes, media, string.Join(", ", detail));
        }
        finally
        {
            if (document != null) document.Close(0);
            if (own) word.Quit(0);
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(word);
        }
    }

    /// <summary>The clipboard's HTML (the "HTML Format" a formatted copy carries, header included); null when there is none.</summary>
    private static string? ClipboardHtml()
    {
        var format = RegisterClipboardFormat("HTML Format");
        for (var i = 0; i < 10 && !OpenClipboard(IntPtr.Zero); i++) Thread.Sleep(50);
        try
        {
            var handle = GetClipboardData(format);
            if (handle == IntPtr.Zero) return null;
            var pointer = GlobalLock(handle);
            try { return Marshal.PtrToStringUTF8(pointer); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    /// <summary>The clipboard's text (CF_UNICODETEXT), line endings as "\n"; null when there is none.</summary>
    private static string? ClipboardText()
    {
        for (var i = 0; i < 10 && !OpenClipboard(IntPtr.Zero); i++) Thread.Sleep(50);
        try
        {
            var handle = GetClipboardData(13);
            if (handle == IntPtr.Zero) return null;
            var pointer = GlobalLock(handle);
            try { return Marshal.PtrToStringUni(pointer)?.Replace("\r\n", "\n"); }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr memory);

    // A reader's document as it was when Word showed blank frames: pictures written as absolute file:/// addresses (as an
    // image inserted from a browser cache is), a file name and an alt text with brackets, a graphviz SVG sized in pt.
    private static async Task CP01(List<string> notes)
    {
        using var c = await Session("e2e CP01");
        var folder = Path.Combine(fixtures, "cp01 图");
        Directory.CreateDirectory(folder);
        var png = Path.Combine(folder, "ja-dark[1].png");
        var svg = Path.Combine(folder, "overall.svg");
        var jpeg = Path.Combine(folder, "photo.JPEG");
        using (var bitmap = new System.Drawing.Bitmap(40, 30))
        {
            using (var g = System.Drawing.Graphics.FromImage(bitmap)) g.Clear(System.Drawing.Color.Red);
            bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            bitmap.Save(jpeg, System.Drawing.Imaging.ImageFormat.Jpeg);
        }
        File.WriteAllText(svg, "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n" +
            "<svg width=\"72pt\" height=\"40pt\" viewBox=\"0.00 0.00 72.00 40.00\" xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"72\" height=\"40\" fill=\"blue\"/></svg>\n");
        string Url(string file) => "file:///" + file.Replace('\\', '/');
        var text = $"PNG\n\n![ja-dark[1]]({Url(png)})\n\nSVG\n\n![overall]({Url(svg)})\n\nJPEG\n\n![photo]({Url(jpeg)})\n\nEND\n";
        var id = await Open(c, Fixture("cp01.md", text));
        var windowId = await WindowIdOf(c, id);
        var window = await WindowOf(windowId, c);
        await Task.Delay(1500);
        notes.Add("drawn: " + (await c.Call("test.editor.eval", new { windowId, script = "[...document.querySelectorAll('#ag-editor-id img')].map(i => i.src + ' ' + i.naturalWidth + 'x' + i.naturalHeight).join(' | ') + ' || failed: ' + [...document.querySelectorAll('#ag-editor-id .ag-image-fail')].map(e => e.getAttribute('data-raw')).join(', ')" }))["result"]);
        await Activate(window);
        await c.Call("test.editor.focus", new { windowId });
        await Task.Delay(200);
        Keys(("ctrl", (ushort)0x41));
        await Task.Delay(400);
        Keys(("ctrl", (ushort)0x43));
        string? html = null;
        for (var i = 0; i < 30 && html == null; i++) { await Task.Delay(100); html = ClipboardHtml(); }
        var fragment = html == null ? null : System.Text.RegularExpressions.Regex.Replace(html, "base64,[A-Za-z0-9+/=]{40,}", m => "base64,…(" + m.Value.Length + ")");
        notes.Add("HTML: " + fragment);
        notes.Add("text: " + JsonConvert.SerializeObject(ClipboardText()));
        if (PasteIntoWord(Path.GetDirectoryName(Fixture("cp01-word.txt", ""))!) is { } word)
        {
            notes.Add($"Word: {word.shapes} picture(s) pasted, {word.media} embedded; {word.detail}");
            Check(word.shapes == 3 && word.media == 3, "Word: the PNG, the SVG and the JPEG are all in the pasted document");
        }
        else notes.Add("Word: not installed, not tried");
    }

    private static async Task TH02(List<string> notes)
    {
        using var c = await Session("e2e TH02");
        var id = await Open(c, Fixture("th02.md", "# TH02\n"));
        var windowId = await WindowIdOf(c, id);
        async Task<JToken> Menu(bool reload = false) => await c.Call("test.theme.menu", new { windowId, reload });
        var folder = (string)(await Menu())["folder"]!;
        // The folder exists once the app has written its example theme there - not always before this case.
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "th02.css");
        string Theme(string name) => $"/* Typedown theme\n * name: {name}\n * base: dark\n */\n:root {{ --editorBgColor: #102030; }}\n";
        async Task<bool> Shows(string name)
        {
            for (var i = 0; i < 25; i++)
            {
                if (((JArray)(await Menu())["entries"]!).Any(e => (string?)e == name)) return true;
                await Task.Delay(200);
            }
            return false;
        }
        var window = await WindowOf(windowId, c);
        await Activate(window);
        try
        {
            // The submenu drawn once before the files change, as a person who looked at it would have it.
            notes.Add("shown before: " + string.Join(" | ", await ShownThemeMenu(window)));
            Directory.CreateDirectory(folder);
            File.WriteAllText(file, Theme("TH02 One"));
            Check(!((JArray)(await Menu())["entries"]!).Any(e => (string?)e == "TH02 One"), "the file was added after the menu was built");
            await Menu(reload: true);
            Check(await Shows("TH02 One"), "Reload themes lists the new file");
            var shownNew = await ShownThemeMenu(window);
            notes.Add("shown after adding: " + string.Join(" | ", shownNew));
            Check(shownNew.Contains("TH02 One"), "the menu on screen shows the new theme");
            File.WriteAllText(file, Theme("TH02 Two"));
            await Menu(reload: true);
            Check(await Shows("TH02 Two"), "Reload themes shows a theme's new name");
            var shownRenamed = await ShownThemeMenu(window);
            notes.Add("shown after renaming: " + string.Join(" | ", shownRenamed));
            Check(shownRenamed.Contains("TH02 Two") && !shownRenamed.Contains("TH02 One"), "the menu on screen shows the new name");
            notes.Add("menu: " + (await Menu())["entries"]!.ToString(Formatting.None));

            // The two settings apart: light as the built-in one, a dark custom theme. The window follows the theme
            // in force (the custom one's base), as the editor does - it used to follow the built-in setting alone.
            await c.Call("test.settings.set", new { windowId, name = "AppTheme", value = 1 });
            await c.Call("test.settings.set", new { windowId, name = "CustomTheme", value = "th02" });
            await Task.Delay(800);
            var actual = (string?)(await Menu())["actualTheme"];
            notes.Add($"built-in Light, custom th02 (base dark): window {actual}");
            Check(actual == "Dark", $"the window draws dark under a dark custom theme ({actual})");
        }
        finally
        {
            await c.Call("test.theme.apply", new { windowId, builtIn = "Default" });
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private static async Task IU03(List<string> notes)
    {
        using var c = await Session("e2e IU03");
        var first = (string)((JArray)(await c.Call("window.list"))["windows"]!)[0]!["windowId"]!;
        var doc = Fixture("iu03.md", "# IU03\n\n![fresh](iu03-images/fresh.png)\n");
        var folder = Path.Combine(Path.GetDirectoryName(doc)!, "iu03-images");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "fresh.png"), Png(33));
        var log = Path.Combine(folder, "uploads.log");
        File.Delete(log);
        var script = "function Upload-Image([string]$path) {\n" + $"  Add-Content -LiteralPath '{log}' -Value $path\n" + "  'https://img.test/fresh.png'\n}\n";
        await c.Call("test.images.configure", new { windowId = first, method = "powershell", config = new { script } });
        // A new window has an upload service of its own, which reads the configurations when it is first asked -
        // as every window does after a restart. Its first upload must find the configuration, not "none".
        var windowId = (string)(await c.Call("test.window.open"))["windowId"]!;
        await Task.Delay(1500);
        var window = await WindowOf(windowId, c);
        await Activate(window);
        var id = await Open(c, doc);
        Check(await WindowIdOf(c, id) == windowId, "the document opened in the new window");
        await Task.Delay(800);
        try
        {
            var result = await c.Call("test.images.uploadAll", new { windowId });
            notes.Add("result: " + result.ToString(Formatting.None));
            Check(!(bool)result["NoConfig"]!, "the first upload in a fresh window found the configuration");
            Check((int)result["Uploaded"]! == 1, "and uploaded the picture");
        }
        finally
        {
            try
            {
                await c.Call("document.save", new { documentId = id, baseRevision = await Revision(c, id) });
                await c.Call("document.close", new { documentId = id });
            }
            catch (Exception e) { notes.Add("cleanup: " + e.Message); }
            PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(1000);
        }
    }

    private static async Task IU02(List<string> notes)
    {
        // A bucket server: E2E_S3_ENDPOINT/ACCESS_KEY/SECRET_KEY, or else "rclone serve s3" (E2E_RCLONE, or
        // E:\tools\rclone\rclone.exe) on a folder of its own, for this case only.
        var endpoint = Environment.GetEnvironmentVariable("E2E_S3_ENDPOINT");
        var accessKey = Environment.GetEnvironmentVariable("E2E_S3_ACCESS_KEY") ?? "e2e-access";
        var secretKey = Environment.GetEnvironmentVariable("E2E_S3_SECRET_KEY") ?? "e2e-secret";
        Process? server = null;
        if (endpoint == null)
        {
            var rclone = Environment.GetEnvironmentVariable("E2E_RCLONE") ?? @"E:\tools\rclone\rclone.exe";
            Check(File.Exists(rclone), $"no S3 server: set E2E_S3_ENDPOINT, or E2E_RCLONE to rclone.exe ({rclone} is not there)");
            var data = Path.Combine(Path.GetTempPath(), "e2e-iu02-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data);
            var port = 19000 + Environment.ProcessId % 1000;
            endpoint = $"http://127.0.0.1:{port}";
            server = Process.Start(new ProcessStartInfo(rclone, $"serve s3 --auth-key {accessKey},{secretKey} --addr 127.0.0.1:{port} \"{data}\"") { UseShellExecute = false, CreateNoWindow = true })!;
            await Task.Delay(1500);
            notes.Add($"rclone serve s3 at {endpoint}, pid {server.Id}");
        }
        try { await IU02With(notes, endpoint, accessKey, secretKey); }
        finally { if (server != null) { try { server.Kill(); } catch { } server.Dispose(); } }
    }

    private static async Task IU02With(List<string> notes, string endpoint, string accessKey, string secretKey)
    {
        var bucket = "e2e-iu02-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var target = new Typedown.Core.Services.S3Uploader.Target { Endpoint = endpoint, Bucket = bucket, AccessKey = accessKey, SecretKey = secretKey, PathStyle = true };
        using var http = new System.Net.Http.HttpClient();
        async Task<System.Net.Http.HttpResponseMessage> Signed(System.Net.Http.HttpMethod method, Uri url)
        {
            var request = new System.Net.Http.HttpRequestMessage(method, url);
            var amzDate = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
            const string empty = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["host"] = url.Authority, ["x-amz-content-sha256"] = empty, ["x-amz-date"] = amzDate };
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", empty);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("Authorization", Typedown.Core.Services.S3Uploader.Authorization(method.Method, url.AbsolutePath, "", headers, empty, accessKey, secretKey, "us-east-1", "s3", amzDate));
            return await http.SendAsync(request);
        }
        var made = await Signed(System.Net.Http.HttpMethod.Put, new Uri(endpoint.TrimEnd('/') + "/" + bucket));
        Check(made.IsSuccessStatusCode, $"the bucket is made at {endpoint} ({(int)made.StatusCode})");

        using var c = await Session("e2e IU02");
        var picture = Png(7);
        var doc = Fixture("iu02.md", "# IU02\n\n![c](iu02-c.png)\n");
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(doc)!, "iu02-c.png"), picture);
        var id = await Open(c, doc);
        var windowId = await WindowIdOf(c, id);

        var wrong = await c.Call("test.images.configure", new { windowId, method = "s3", config = new { endpoint, bucket, accessKey, secretKey = secretKey + "x", pathStyle = true } });
        var refused = await c.Call("test.images.uploadAll", new { windowId });
        notes.Add("wrong secret: " + refused.ToString(Formatting.None));
        Check((int)refused["Uploaded"]! == 0 && ((string?)refused["Failures"]![0]!["Reason"] ?? "").Contains("SignatureDoesNotMatch"), "a wrong secret is refused by the server and reported");
        Check((string)(await Get(c, id))["text"]! == "# IU02\n\n![c](iu02-c.png)\n", "nothing changed in the document");

        var configured = await c.Call("test.images.configure", new { windowId, method = "s3", config = new { endpoint, bucket, accessKey, secretKey, pathStyle = true, prefix = "img/${year}" } });
        var stored = (string)configured["stored"]!;
        notes.Add("stored: " + stored);
        Check(!stored.Contains("\"" + secretKey + "\"") && stored.Contains("dp1:"), "the secret is stored protected, not in plain text");
        var result = await c.Call("test.images.uploadAll", new { windowId });
        notes.Add("result: " + result.ToString(Formatting.None));
        Check((int)result["Uploaded"]! == 1, "the picture is uploaded");
        var text = (string)(await Get(c, id))["text"]!;
        var key = Typedown.Core.Services.S3Uploader.KeyFor(new Typedown.Core.Services.S3Uploader.Target { KeyPrefix = "img/" + DateTime.Now.Year }, "iu02-c.png", picture);
        var url = Typedown.Core.Services.S3Uploader.ObjectUrl(target, key);
        notes.Add("text: " + JsonConvert.SerializeObject(text));
        Check(text == $"# IU02\n\n![c]({url.AbsoluteUri})\n", $"the address is the object's ({url})");
        var back = await Signed(System.Net.Http.HttpMethod.Get, url);
        Check(back.IsSuccessStatusCode && (await back.Content.ReadAsByteArrayAsync()).SequenceEqual(picture), "the object reads back with the picture's bytes");
    }

    // reveal: "change" (spec 2.3): a write scrolls its first changed block into view when it is off screen and leaves
    // the caret where the reader had it; "document" leaves the page where it was, and so does a change already on screen.
    private static async Task RV01(List<string> notes)
    {
        using var c = await Session("e2e RV01");
        var text = new StringBuilder("# RV01\n\n");
        for (var i = 1; i <= 120; i++) text.Append($"Paragraph {i} of the long document.\n\n");
        text.Append("Last paragraph.\n");
        var id = await Open(c, Fixture("rv01.md", text.ToString()));
        var windowId = await WindowIdOf(c, id);
        await c.Call("test.editor.focus", new { windowId });
        await Task.Delay(500);
        async Task ToTop() { await c.Call("test.editor.eval", new { windowId, script = "window.scrollTo(0, 0), 0" }); await Task.Delay(300); }
        // Where the page is, the window height, where the block holding mark is (null: not drawn), and the text the
        // caret is in.
        async Task<JToken> State(string mark)
        {
            var script = "(() => { const w = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT); let n, e = null; " +
                $"while ((n = w.nextNode())) if (n.textContent.includes({JsonConvert.ToString(mark)})) {{ e = n.parentElement; break }} " +
                "const r = e && e.getBoundingClientRect(); const s = getSelection(); " +
                "return { y: Math.round(scrollY), h: innerHeight, top: r ? Math.round(r.top) : null, " +
                "caret: s && s.anchorNode ? (s.anchorNode.textContent || '').slice(0, 30) : null, apply: window.__typedownLastApply || null } })()";
            return (await c.Call("test.editor.eval", new { windowId, script }))["result"]!;
        }
        async Task<JToken> Replace(string find, string replacement, string reveal, string normalizationPolicy = "requireKnownSafe")
        {
            await c.Call("document.replaceText", new { documentId = id, baseRevision = await Revision(c, id), find, replacement, expectedCount = 1, reveal, normalizationPolicy });
            await Task.Delay(600);
            return await State(replacement.Split('\n')[0]);
        }
        bool Shown(JToken s) => s["top"]!.Type == JTokenType.Integer && (int)s["top"]! >= 0 && (int)s["top"]! < (int)s["h"]!;

        await ToTop();
        var caret = (string?)(await State("RV01"))["caret"];
        notes.Add($"caret before: {caret}");
        Check(!string.IsNullOrEmpty(caret), "the caret is in the document");

        var a = await Replace("Last paragraph.", "Last paragraph, A.", "document");
        notes.Add("document, at the end: " + a.ToString(Formatting.None));
        Check((int)a["y"]! < 5, "reveal: \"document\" leaves the page at the top");

        var b = await Replace("Last paragraph, A.", "Last paragraph, B.", "change");
        notes.Add("change, at the end: " + b.ToString(Formatting.None));
        Check((int)b["y"]! > 0 && Shown(b), "reveal: \"change\" scrolls the change at the end into view");
        Check((string?)b["caret"] == caret, "the caret stays where it was");

        await ToTop();
        var d = await Replace("Paragraph 2 of", "Paragraph 2 (changed) of", "change");
        notes.Add("change on screen: " + d.ToString(Formatting.None));
        Check((int)d["y"]! < 5, "a change already on screen does not move the page");

        await ToTop();
        await c.Call("document.replaceText", new { documentId = id, baseRevision = await Revision(c, id), find = "Paragraph 100 of the long document.\n\n", replacement = "", expectedCount = 1, reveal = "change" });
        await Task.Delay(600);
        var deleted = await State("Paragraph 101 of");
        notes.Add("deletion: " + deleted.ToString(Formatting.None));
        Check((int)deleted["y"]! > 0 && Shown(deleted), "a deletion scrolls the block now in its place into view");

        try
        {
            await c.Call("window.setView", new { windowId, mode = "source" });
            await Task.Delay(500);
            await ToTop();
            // Source mode has no visual editor to tell what it would rewrite: every write there is "unknown".
            await Replace("Paragraph 120 of", "Paragraph 120 (source) of", "change", "allowUnknown");
            // CodeMirror marks only the changed characters, which splits the line into text nodes.
            var s = await State("Paragraph 120 ");
            notes.Add("source mode: " + s.ToString(Formatting.None));
            Check((int)s["y"]! > 0 && Shown(s), "in source mode the change is scrolled into view");
        }
        finally
        {
            await c.Call("window.setView", new { windowId, mode = "visual" });
        }

        // A new reference definition changes how other blocks may render: the editor loads the whole document, and the
        // hold that puts the page back where it was must hold it at the change instead.
        await ToTop();
        await Replace("Last paragraph, B.", "Last paragraph, [D][r].\n\n[r]: https://example.com/", "change");
        // The link splits the line into text nodes; nothing else says "Last paragraph, " by now.
        var wholeState = await State("Last paragraph, ");
        notes.Add("whole document: " + wholeState.ToString(Formatting.None));
        Check((string?)wholeState["apply"] == "whole", "a new reference definition loads the whole document");
        Check((int)wholeState["y"]! > 0 && Shown(wholeState), "after a whole load the change is in view too");
    }

    // The endpoint name a starting host writes, or null after 30 s. The host may still have the file open: the read
    // that came right after File.Exists failed with a sharing violation (Q01, once in a full run).
    private static async Task<string?> ReadEndpointFile(string path)
    {
        for (var i = 0; i < 300; i++)
        {
            try
            {
                if (File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } name) return name;
            }
            catch (IOException) { }
            await Task.Delay(100);
        }
        return null;
    }

    private static void TypeChar(char ch)
    {
        var inputs = new[] { new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0004 } } }, new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0006 } } } };
        if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new CaseFailed("SendInput was refused (is the desktop locked?)");
    }

    private static async Task F01(List<string> notes)
    {
        using var c = await Session("e2e F01");
        var id = await Open(c, Fixture("f01.md", "# F01\n"));
        // Written through the API, then typed into by a person: every protected payload must survive the first
        // visual edit (checked by exact strings, not by the editor's classifier).
        var keep = new[]
        {
            "---\ntitle: \"F01: kept\"\n---",
            "[site](https://example.com/a_b?x=1&y=2 \"Title\")",
            "![alt](./img/a%20b.png \"Pic\")",
            "claim[^note]",
            "[^note]: The note.",
            "- [x] done",
            "- [ ] todo",
            "```ts {title=\"x.ts\"}\nconst a:\tnumber = 1;\n```",
            "$$\nE = mc^2\n$$",
            "<kbd>Ctrl</kbd>",
        };
        // A plain last paragraph: Ctrl+End lands the caret there, not in an image or a maths block.
        var text = keep[0] + "\n\n# F01\n\n" + string.Join("\n\n", keep.Skip(1).Take(3)) + "\n\n" + keep[5] + "\n" + keep[6] + "\n\n" + string.Join("\n\n", keep.Skip(7)) + "\n\n" + keep[4] + "\n\nThe end.\n";
        var written = await c.Call("document.replace", new { documentId = id, baseRevision = await Revision(c, id), text, normalizationPolicy = "allowUnknown", reveal = "document" });
        notes.Add($"written, normalization {written["normalization"]?["pendingNormalization"]} {written["normalization"]?["reasons"]?.ToString(Formatting.None)}");
        Check((string?)written["normalization"]?["pendingNormalization"] != "unsafe", "the written text is not predicted to lose anything");
        await TypeInto(c, id, "Q");
        var after = await WaitForPage(c, id, t => t.Contains('Q'), "the keystroke");
        var latest = (string)(await Get(c, id))["text"]!;
        // A latest read is the page's text at that moment. The page may still settle after the keystroke shows (it
        // did on Windows 10, now and then): what differed is noted, and the two are read again together.
        if (latest != after)
        {
            var shorter = Math.Min(latest.Length, after.Length);
            var at = Enumerable.Range(0, shorter).Where(i => latest[i] != after[i]).DefaultIfEmpty(shorter).First();
            notes.Add($"latest read and the page differed at {at}: page {JsonConvert.SerializeObject(after.Substring(Math.Max(0, at - 20), Math.Min(60, after.Length - Math.Max(0, at - 20))))}, latest {JsonConvert.SerializeObject(latest.Substring(Math.Max(0, at - 20), Math.Min(60, latest.Length - Math.Max(0, at - 20))))}");
            for (var i = 0; i < 10 && latest != after; i++)
            {
                await Task.Delay(200);
                latest = (string)(await Get(c, id))["text"]!;
                after = (string?)(await c.Call("test.editor.pageText", new { documentId = id }))["text"] ?? "";
            }
        }
        Check(latest == after, "latest read is what the page holds");
        var lost = keep.Where(k => !latest.Contains(k)).ToList();
        Check(lost.Count == 0, $"every protected payload survives the first edit (lost: {JsonConvert.SerializeObject(lost)}; text {JsonConvert.SerializeObject(latest)})");
    }

    private static async Task F02(List<string> notes)
    {
        using var c = await Session("e2e F02");
        // CommonMark example 31: Muya rewrites the entity in this HTML block on the first visual edit.
        var id = await Open(c, Fixture("f02.md", "<a href=\"&ouml;&ouml;.html\">\n\nEDITHERE\n"));
        var windowId = await WindowIdOf(c, id);
        async Task<bool> Warning() => (bool?)(await c.Call("test.editor.eval", new
        {
            windowId,
            script = "!!document.querySelector('.td-first-edit-warning')"
        }))["result"] ?? false;

        Check(await Warning(), "unsafe raw HTML shows the first-edit warning");
        await Task.Delay(5000);
        Check(await Warning(), "the warning remains visible long enough to read");
        await Task.Delay(6000);
        Check(!await Warning(), "the warning closes after about 10 seconds without a click");
        notes.Add("warning was visible after 5 seconds and gone after 11 seconds");
    }

    private static async Task M01(List<string> notes)
    {
        using var c = await Session("e2e M01");
        var path = Fixture("m01.md", "# M01\n\nstart\n");
        var id = await Open(c, path);
        var window = (string)(await c.Call("document.focus", new { documentId = id }))["windowId"]!;
        // Text Muya would write back differently (setext heading, '*' bullets, a ragged table): only the mapping to the
        // source keeps it exact while nobody edits.
        const string text = "Title\n=====\n\n* one\n* two\n\n|a|b|\n|-|-|\n|1|2|\n";
        var written = await c.Call("document.replace", new { documentId = id, baseRevision = await Revision(c, id), text, normalizationPolicy = "allowUnknown" });
        var revision = (long)written["revision"]!;
        notes.Add($"written r{revision}, normalization {written["normalization"]?["pendingNormalization"]}");
        foreach (var (source, reading, name) in new[] { (false, true, "reading"), (true, false, "source"), (false, false, "visual"), (false, true, "reading"), (false, false, "visual") })
        {
            await c.Call("test.settings.set", new { windowId = window, name = "SourceCode", value = source });
            await c.Call("test.settings.set", new { windowId = window, name = "ReadOnly", value = reading });
            var page = await WaitForPage(c, id, t => t == text, $"the {name} view's text");
            var doc = await Get(c, id);
            Check((string)doc["text"]! == text, $"{name}: latest read is the written text ({JsonConvert.SerializeObject((string)doc["text"]!)})");
            Check((long)doc["revision"]! == revision, $"{name}: no revision without an edit (r{doc["revision"]})");
            Check(!(bool)doc["saved"]!, $"{name}: still unsaved, not marked saved or re-dirtied oddly");
        }
        await c.Call("test.settings.set", new { windowId = window, name = "SourceCode", value = false });
        await c.Call("test.settings.set", new { windowId = window, name = "ReadOnly", value = false });
        await c.Call("document.save", new { documentId = id, baseRevision = revision });
        Check(Disk(path) == text, $"the file holds exactly the written text ({JsonConvert.SerializeObject(Disk(path))})");
    }

    private static async Task P01(List<string> notes)
    {
        using var c = await Session("e2e P01");
        var a = await Open(c, Fixture("p01-a.md", "# A\n\nalpha\n"));
        await Open(c, Fixture("p01-b.md", "# B\n\nbeta\n")); // A is now a background tab
        var windowId = (string)(await c.Call("document.get", new { documentId = a, consistency = "snapshot" }))["windowId"]!;
        var rA = await Revision(c, a);
        Check(await c.ErrorCode("document.replace", new { documentId = a, baseRevision = rA, text = "x\n", awaitPresentation = true }) == -32602,
            "awaitPresentation without reveal is invalid_params");
        Check(await Revision(c, a) == rA, "the refused write changed nothing");

        var window = await WindowOf(windowId, c);
        ShowWindow(window, 6); // SW_MINIMIZE: reveal has to bring the window back
        await Task.Delay(500);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        JToken written;
        try
        {
            written = await c.Call("document.replace", new { documentId = a, baseRevision = rA, text = "# A\n\nalpha shown\n", reveal = "document", awaitPresentation = true, normalizationPolicy = "allowUnknown" });
        }
        catch (JsonRpcRemoteException e)
        {
            throw new CaseFailed($"{e.ErrorData?["kind"]} after {watch.ElapsedMilliseconds} ms: {e.ErrorData?.ToString(Formatting.None)}");
        }
        notes.Add($"presentation {written["presentation"]?.ToString(Formatting.None)} in {watch.ElapsedMilliseconds} ms");
        var presentation = written["presentation"];
        Check(presentation != null && (bool)presentation["windowVisible"]! && (bool)presentation["tabActive"]! && (bool)presentation["pageFramesPassed"]! && (bool)presentation["hostRenderPassed"]!,
            "every presentation target was observed");
        Check((long)written["revision"]! == rA + 1, "the write committed");
        var windows = (JArray)(await c.Call("window.list"))["windows"]!;
        Check(windows.Any(w => (string)w["windowId"]! == windowId && (string?)w["activeDocumentId"] == a), "the written document is the window's active tab");
        Check(!IsIconic(window), "the window is no longer minimized");
        Check((string)(await Get(c, a))["text"]! == "# A\n\nalpha shown\n", "latest read returns the written text");
    }

    private static async Task<string> WindowIdOf(Client c, string documentId) =>
        (string)(await c.Call("document.get", new { documentId, consistency = "snapshot" }))["windowId"]!;

    // Starts a write and holds it where the page has applied it but the host has not committed it yet.
    private static async Task<(Task<JToken> write, string barrier)> HoldAfterApply(Client writer, Client driver, string documentId, string text)
    {
        var barrier = (string)(await driver.Call("test.barrier.arm", new { point = EditBarrierPoints.AfterEditorMutationBeforeReport, documentId }))["barrierId"]!;
        var write = writer.Call("document.replace", new { documentId, baseRevision = await Revision(driver, documentId), text, normalizationPolicy = "allowUnknown" });
        var waiting = driver.Call("test.barrier.waitHit", new { barrierId = barrier, timeoutMs = 20000 });
        // A write refused before it reached the page (the page reloading, say) ends first: say why, without the wait.
        if (await Task.WhenAny(waiting, write) == write && !waiting.IsCompleted)
            throw new CaseFailed($"the write ended before it reached the page: {await Outcome(write)}");
        var hit = await waiting;
        if (!(bool)hit["hit"]!) throw new CaseFailed("the write never reached the page");
        return (write, barrier);
    }

    private static async Task<string> Outcome(Task<JToken> write)
    {
        try { var r = await write; return $"committed r{r["revision"]}"; }
        catch (JsonRpcRemoteException e) { return $"refused {e.ErrorData?["kind"]} {e.ErrorData?["reason"]}"; }
    }

    private static async Task W01(List<string> notes)
    {
        using var writer = await Session("e2e W01 writer");
        using var driver = await Session("e2e W01 driver");
        const string original = "# W01\n\noriginal\n";
        var a = await Open(driver, Fixture("w01.md", original));
        var windowId = await WindowIdOf(driver, a);
        var r = await Revision(driver, a);
        var (write, barrier) = await HoldAfterApply(writer, driver, a, "# W01\n\nwritten\n");
        Check((await WaitForPage(driver, a, t => t.Contains("written"), "the applied write")).Contains("written"), "the page shows the write before the commit");
        await driver.Call("test.editor.reload", new { windowId });
        await WaitForPage(driver, a, t => t == original, "the reloaded page");
        await driver.Call("test.barrier.release", new { barrierId = barrier });
        var outcome = await Outcome(write);
        notes.Add(outcome);
        Check(outcome.Contains("editorReloaded"), "the write is refused because the page reloaded");
        var doc = await Get(driver, a);
        Check((string)doc["text"]! == original && (long)doc["revision"]! == r, "host text and revision are the ones before the write");
        Check(await WaitForPage(driver, a, t => t == original, "the restored page") == original, "the page shows the same text as the host");
        var again = await driver.Call("document.replace", new { documentId = a, baseRevision = r, text = "# W01\n\nsecond try\n", normalizationPolicy = "allowUnknown" });
        Check((long)again["revision"]! == r + 1, "the document still takes writes");
    }

    private static async Task W02(List<string> notes)
    {
        using var writer = await Session("e2e W02 writer");
        using var driver = await Session("e2e W02 driver");
        var a = await Open(driver, Fixture("w02.md", "# W02\n\noriginal\n"));
        var windowId = await WindowIdOf(driver, a);
        const string written = "# W02\n\nwritten across a mode switch\n";
        try
        {
            var (write, barrier) = await HoldAfterApply(writer, driver, a, written);
            await driver.Call("test.settings.set", new { windowId, name = "SourceCode", value = true });
            await Task.Delay(500);
            await driver.Call("test.barrier.release", new { barrierId = barrier });
            var outcome = await Outcome(write);
            notes.Add(outcome);
            Check(outcome.StartsWith("committed"), "the write commits");
            Check((string)(await Get(driver, a))["text"]! == written, "the host holds the write");
            Check(await WaitForPage(driver, a, t => t == written, "source mode") == written, "source mode shows exactly the write");
            await driver.Call("test.settings.set", new { windowId, name = "SourceCode", value = false });
            Check(await WaitForPage(driver, a, t => t == written, "visual mode") == written, "visual mode shows exactly the write");
            Check((string)(await Get(driver, a))["text"]! == written, "the host still holds the write after switching back");
        }
        finally
        {
            await driver.Call("test.settings.set", new { windowId, name = "SourceCode", value = false });
        }
    }

    private static async Task W03(List<string> notes)
    {
        using var writer = await Session("e2e W03 writer");
        using var driver = await Session("e2e W03 driver");
        const string originalA = "# A\n\nalpha\n", originalB = "# B\n\nbeta\n";
        var b = await Open(driver, Fixture("w03-b.md", originalB));
        var a = await Open(driver, Fixture("w03-a.md", originalA));

        // Away and back: the page reloaded A from the host, which never took the write in.
        var rA = await Revision(driver, a);
        var (write, barrier) = await HoldAfterApply(writer, driver, a, "# A\n\nwritten\n");
        await driver.Call("document.focus", new { documentId = b });
        await driver.Call("document.focus", new { documentId = a });
        await driver.Call("test.barrier.release", new { barrierId = barrier });
        var outcome = await Outcome(write);
        notes.Add("away and back: " + outcome);
        Check(outcome.Contains("editorReloaded"), "away and back: refused");
        Check((string)(await Get(driver, a))["text"]! == originalA && await Revision(driver, a) == rA, "away and back: A is unchanged in the host");
        Check(await WaitForPage(driver, a, t => t == originalA, "A") == originalA, "away and back: the page shows A as the host holds it");
        Check((string)(await Get(driver, b))["text"]! == originalB, "away and back: B is untouched");

        // Away only: the write goes into A's tab, and shows when A comes back.
        const string written = "# A\n\nwritten while away\n";
        (write, barrier) = await HoldAfterApply(writer, driver, a, written);
        await driver.Call("document.focus", new { documentId = b });
        await driver.Call("test.barrier.release", new { barrierId = barrier });
        outcome = await Outcome(write);
        notes.Add("away: " + outcome);
        Check(outcome.StartsWith("committed"), "away: committed");
        Check(await WaitForPage(driver, b, t => t == originalB, "B") == originalB, "away: the page shows B untouched");
        Check((string)(await Get(driver, a))["text"]! == written, "away: A's tab holds the write");
        await driver.Call("document.focus", new { documentId = a });
        Check(await WaitForPage(driver, a, t => t == written, "A again") == written, "away: A shows the write when it comes back");
    }

    // typedownctl published next to the driver (e2e/cli) and run as "typedownctl mcp", started the way an agent host starts it: stdio, one message per line.
    private sealed class McpProcess : IDisposable
    {
        private readonly Process process;
        private int id;

        public McpProcess(string endpoint)
        {
            var dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "cli", Brand.CliName + ".dll"));
            if (!File.Exists(dll)) throw new CaseFailed(Brand.CliName + " is not published next to the driver: " + dll);
            process = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { dll, "mcp", "--endpoint", endpoint },
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                CreateNoWindow = true,
            })!;
            process.StandardInput.AutoFlush = true;
        }

        public async Task<JObject> Send(string method, object parameters)
        {
            var request = new JObject { ["jsonrpc"] = "2.0", ["id"] = ++id, ["method"] = method, ["params"] = JToken.FromObject(parameters) };
            await process.StandardInput.WriteLineAsync(request.ToString(Formatting.None));
            var line = process.StandardOutput.ReadLineAsync();
            if (await Task.WhenAny(line, Task.Delay(20000)) != line) throw new CaseFailed($"typedown-mcp did not answer {method}");
            return JObject.Parse(await line ?? throw new CaseFailed("typedown-mcp closed its output: " + process.StandardError.ReadToEnd()));
        }

        public async Task<JObject> Tool(string name, object arguments) =>
            (JObject)(await Send("tools/call", new { name, arguments }))["result"]!;

        /// <summary>Closing its input is how an agent host stops it; it must then exit by itself.</summary>
        public bool CloseAndWait()
        {
            process.StandardInput.Close();
            return process.WaitForExit(10000);
        }

        public void Dispose()
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
            process.Dispose();
        }
    }

    private static async Task MC01(List<string> notes)
    {
        using var driver = await Session("e2e MC01 driver");
        const string original = "# MC01\n\nThe quick brown fox.\n";
        var a = await Open(driver, Fixture("mc01.md", original));
        await Open(driver, Fixture("mc01-other.md", "# other\n")); // MC01 is a background tab until revealed

        using var mcp = new McpProcess(endpoint);
        var init = await mcp.Send("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "E2E agent", version = "1" } });
        Check((string?)init["result"]?["protocolVersion"] == "2025-06-18", "initialize answers with the protocol version");
        var tools = ((JArray)(await mcp.Send("tools/list", new { }))["result"]!["tools"]!).Select(t => (string)t["name"]!).ToList();
        Check(tools.Count == 8 && tools.Contains("typedown_replace_text") && tools.Contains("typedown_close_document"), $"eight tools ({string.Join(", ", tools)})");

        var list = await mcp.Tool("typedown_list_documents", new { });
        Check(!(bool)list["isError"]! && list["structuredContent"]!["documents"]!.Any(d => (string)d["documentId"]! == a), "the document is listed");
        var read = await mcp.Tool("typedown_read_document", new { documentId = a });
        Check((string)read["structuredContent"]!["text"]! == original, "read returns the exact text");
        var revision = (long)read["structuredContent"]!["revision"]!;

        var write = await mcp.Tool("typedown_replace_text", new { documentId = a, baseRevision = revision, find = "brown", replacement = "red", reveal = true, allowFormattingChanges = true });
        Check(!(bool)write["isError"]!, "replace_text succeeds: " + write["content"]?[0]?["text"]);
        // The notice is set after the reply, from the service thread: give it a moment.
        var title = WindowTitle();
        for (var i = 0; i < 40 && !title.Contains("E2E agent (MCP)"); i++) { await Task.Delay(50); title = WindowTitle(); }
        notes.Add("title after the write: " + title);
        Check(title.Contains("E2E agent (MCP)"), "the window title names the agent");
        const string written = "# MC01\n\nThe quick red fox.\n";
        Check(await WaitForPage(driver, a, t => t == written, "the MCP write") == written, "the revealed page shows the write");

        var stale = await mcp.Tool("typedown_replace_text", new { documentId = a, baseRevision = revision, find = "red", replacement = "blue" });
        Check((bool)stale["isError"]! && (string?)stale["structuredContent"]?["error"]?["data"]?["kind"] == "revision_conflict", "a stale revision is a revision_conflict");
        Check(((string?)stale["structuredContent"]?["next"] ?? "").Contains("typedown_read_document"), "the conflict tells the agent to read again");
        Check((string)(await Get(driver, a))["text"]! == written, "the stale write changed nothing");
        Check(mcp.CloseAndWait(), "typedownctl mcp exits when its input closes");
    }

    // Runs a program to its end (at most 60 s), for the client examples shipped next to the driver (e2e/examples).
    private static (int exit, string stdout, string stderr) RunTool(string file, params string[] args)
    {
        var start = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60000)) { try { process.Kill(true); } catch { } throw new CaseFailed($"{file} did not finish"); }
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    private static async Task EX01(List<string> notes)
    {
        using var driver = await Session("e2e EX01 driver");
        var examples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "examples"));
        var a = await Open(driver, Fixture("ex01.md", "# EX01\n\nold text, old news\n"));

        var ps = RunTool("powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command",
            $". '{Path.Combine(examples, "Typedown-Client.ps1")}'; $c = Connect-Typedown '{endpoint}'; " +
            "Initialize-Typedown $c @('document.read','document.write') | Out-Null; " +
            $"$r = Set-TypedownText $c '{a}' 'old' 'new'; $r | ConvertTo-Json -Compress; $c.Pipe.Dispose()");
        notes.Add($"powershell exit {ps.exit}: {ps.stdout.Trim()} {ps.stderr.Trim()}");
        Check(ps.exit == 0 && ps.stdout.Contains("\"revision\""), "the PowerShell example writes");
        Check((string)(await Get(driver, a))["text"]! == "# EX01\n\nnew text, new news\n", "the PowerShell example replaced both occurrences");

        (int exit, string stdout, string stderr) py;
        try { py = RunTool("python", Path.Combine(examples, "typedown_client.py"), "--endpoint", endpoint, "replace-text", a, "news", "notes"); }
        catch (System.ComponentModel.Win32Exception) { notes.Add("no python on this machine: the Python example was not run"); return; }
        notes.Add($"python exit {py.exit}: {py.stdout.Trim()} {py.stderr.Trim()}");
        Check(py.exit == 0, "the Python example writes");
        Check((string)(await Get(driver, a))["text"]! == "# EX01\n\nnew text, new notes\n", "the Python example replaced the word");

        var expected = Brand.Name + ".Automation.v1." + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        // The example as an edition ships it: add-cli (Tools/Installer) gives the copy beside the app the edition's endpoint.
        var shipped = Path.Combine(fixtures, "ex01-shipped");
        Directory.CreateDirectory(shipped);
        File.WriteAllText(Path.Combine(shipped, "typedown_client.py"),
            File.ReadAllText(Path.Combine(examples, "typedown_client.py")).Replace("Typedown.Automation.v1", Brand.Name + ".Automation.v1"));
        var name = RunTool("python", "-c", $"import sys; sys.path.insert(0, r'{shipped}'); import typedown_client; print(typedown_client.Client.default_endpoint())");
        Check(name.stdout.Trim() == expected, $"the Python example finds the application's pipe name ({name.stdout.Trim()} {name.stderr.Trim()})");
    }

    private static Task EQ01(List<string> notes)
    {
        var e2e = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        var run = RunTool("python", Path.Combine(e2e, "equivalence", "scenario.py"), "--examples", Path.Combine(e2e, "examples"),
            "--endpoint", endpoint, "--workdir", Path.Combine(fixtures, "equivalence"));
        Check(run.exit == 0, "the scenario ran: " + run.stderr);
        notes.Add("TRANSCRIPT " + run.stdout);
        return Task.CompletedTask;
    }

    private static async Task C01(List<string> notes)
    {
        using var c = await Session("e2e C01");
        async Task<List<string>> Ids(string window) => ((JArray)(await c.Call("document.list", new { windowId = window }))["documents"]!).Select(d => (string)d["documentId"]!).ToList();
        var a = await Open(c, Fixture("c01-a.md", "# A\n\nalpha\n"));
        var b = await Open(c, Fixture("c01-b.md", "# B\n\nbeta\n")); // b is shown, a is a background tab
        var window = await WindowIdOf(c, b);

        // 1. A saved background document.
        var closed = await c.Call("document.close", new { documentId = a });
        Check((bool)closed["closed"]! && (string)closed["windowId"]! == window, "a saved background document closes");
        Check(!(await Ids(window)).Contains(a), "and is gone from the list");
        Check(await c.ErrorKind("document.get", new { documentId = a, consistency = "snapshot" }) == "document_not_found", "its id is not found any more");

        // 2. Unsaved: refused, still open, the text as it was.
        var r = await Revision(c, b);
        await c.Call("document.replace", new { documentId = b, baseRevision = r, text = "# B\n\nchanged\n" });
        Check(await c.ErrorKind("document.close", new { documentId = b }) == "unsaved_changes", "an unsaved document is refused");
        Check((await Ids(window)).Contains(b) && (string)(await Get(c, b))["text"]! == "# B\n\nchanged\n", "it stays open with its text");

        // 3. Saved, it closes.
        await c.Call("document.save", new { documentId = b });
        var c2 = await Open(c, Fixture("c01-c.md", "# C\n\ngamma\n"));
        await c.Call("document.focus", new { documentId = b });
        Check(await c.ErrorKind("document.close", new { documentId = b, baseRevision = 0 }) == "revision_conflict", "a stale baseRevision is a conflict");
        await c.Call("document.close", new { documentId = b, baseRevision = await Revision(c, b) });
        Check(!(await Ids(window)).Contains(b), "once saved it closes");

        // 4. A keystroke the page has not reported yet makes the document unsaved: close asks the page first.
        // The page reports a key within milliseconds, so "not reported yet" is made to happen: the page's change
        // reports are held back and go out only just before its answer to a flush - where a late report would arrive.
        // Close finds the key only if it asks the page; without asking it closed the document with the key in it.
        // The page has to show c first (closing b reloads it, and a key typed into the reload is lost).
        await Revision(c, c2);
        var cWindow = await WindowIdOf(c, c2);
        const string hold = @"(() => {
            const wv = window.chrome.webview;
            if (!wv.__send) wv.__send = wv.postMessage.bind(wv);
            window.__held = [];
            wv.postMessage = m => {
                if (typeof m === 'string' && m.includes('""name"":""MarkdownChange""')) { window.__held.push(m); return; }
                if (typeof m === 'string' && m.includes('""name"":""ContentFlushed""')) { window.__held.splice(0).forEach(h => wv.__send(h)); wv.postMessage = wv.__send; }
                return wv.__send(m);
            };
            return wv.postMessage !== wv.__send;
        })()";
        Check((bool)(await c.Call("test.editor.eval", new { windowId = cWindow, script = hold }))["result"]!, "the page's change reports can be held");
        await TypeInto(c, c2, "Z");
        var held = 0;
        for (var i = 0; i < 60 && held == 0; i++)
        {
            if (i > 0) await Task.Delay(50);
            held = (int)(await c.Call("test.editor.eval", new { windowId = cWindow, script = "window.__held.length" }))["result"]!;
        }
        if (held == 0) throw new CaseFailed($"the keystroke never reached the page; screen: {Screenshot("c01-no-keystroke")}");
        var refused = await c.ErrorKind("document.close", new { documentId = c2 });
        notes.Add($"close right after a keystroke: {refused ?? "closed"}");
        Check(refused == "unsaved_changes", "a keystroke not yet reported counts as unsaved");
        Check(((string)(await Get(c, c2))["text"]!).Contains('Z'), "and the keystroke is in the document");
        await c.Call("document.save", new { documentId = c2 });

        // 5. The window's only document: the window stays, with an empty untitled document.
        foreach (var other in (await Ids(window)).Where(id => id != c2).ToList())
        {
            var info = (JObject)(await c.Call("document.get", new { documentId = other, consistency = "snapshot" }));
            if ((bool)info["saved"]!) await c.Call("document.close", new { documentId = other });
            else notes.Add($"left open (unsaved from an earlier case): {(string?)info["path"] ?? "untitled"}");
        }
        if ((await Ids(window)).Count == 1)
        {
            await c.Call("document.close", new { documentId = c2 });
            var left = ((JArray)(await c.Call("document.list", new { windowId = window }))["documents"]!);
            Check(left.Count == 1 && left[0]!["path"]!.Type == JTokenType.Null, $"the window stays with one empty untitled document ({left.ToString(Formatting.None)})");
        }
        else notes.Add("other unsaved documents in the window: the last-document part is not reached here");
    }

    private static async Task Q02(List<string> notes)
    {
        using var host = Process.GetProcessById(hostPid);
        using var c = await Session("e2e Q02");
        var opened = (string)(await c.Call("test.window.open"))["windowId"]!;
        var window = await WindowOf(opened, c);
        await Task.Delay(200);
        PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
        for (var i = 0; i < 10 && !host.HasExited; i++) await Task.Delay(500);
        Check(!host.HasExited, "the process lives on after a window was closed while its editor was still being created");
        using var again = await Session("e2e Q02 after");
        var left = ((JArray)(await again.Call("window.list"))["windows"]!).Count;
        Check(left == 1, $"one window left ({left})");
    }

    private static async Task Q01(List<string> notes)
    {
        // A host of its own, in a fresh folder: the cases before leave unsaved documents, and closing a window then
        // stops at the save question. This one has nothing unsaved. It is the last case: the suite's host goes.
        using (var previous = Process.GetProcessById(hostPid)) { previous.Kill(); previous.WaitForExit(10000); }
        var root = testRoot + "-q01";
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        var started = Process.Start(new ProcessStartInfo(hostExe, $"--automation-test-root \"{root}\"") { UseShellExecute = true })!;
        endpoint = await ReadEndpointFile(Path.Combine(root, "automation-endpoint.txt")) ?? throw new CaseFailed("the test host never published its endpoint");
        hostPid = started.Id;
        testRoot = root;
        await Task.Delay(3000);
        using var host = Process.GetProcessById(hostPid);
        using (var c = await Session("e2e Q01"))
        {
            var windows = ((JArray)(await c.Call("window.list"))["windows"]!).Select(w => (string)w["windowId"]!).ToList();
            if (windows.Count < 2) windows.Add((string)(await c.Call("test.window.open"))["windowId"]!);
            notes.Add($"{windows.Count} window(s)");
            var first = await WindowOf(windows[0], c);
            var second = await WindowOf(windows[1], c);
            await Task.Delay(8000); // both editors loaded: a window closed while its web view is created is Q02
            // XamlWindow.AllWindows lists a window until the garbage collector has finalized it: the first window is
            // kept in memory after it closes, as it was, minutes later, for the reader who saw the process stay.
            await c.Call("test.window.keep", new { windowId = windows[0] });
            // Closed the way the close button closes a window (nothing unsaved here, so no question).
            PostMessage(first, 0x0010, IntPtr.Zero, IntPtr.Zero);
            // Diagnosis: second by second, is the process alive, how many main windows are shown, does a fresh
            // connection get an answer (and the old one?).
            for (var i = 1; i <= 10; i++)
            {
                await Task.Delay(1000);
                string fresh;
                try
                {
                    using var probe = await Session($"e2e Q01 probe {i}").WaitAsync(TimeSpan.FromSeconds(4));
                    fresh = $"{((JArray)(await probe.Call("window.list").WaitAsync(TimeSpan.FromSeconds(4)))["windows"]!).Count} window(s) listed";
                }
                catch (Exception e) { fresh = e.GetType().Name; }
                string old;
                try { old = $"{((JArray)(await c.Call("window.list").WaitAsync(TimeSpan.FromSeconds(4)))["windows"]!).Count} listed"; }
                catch (Exception e) { old = e.GetType().Name; }
                notes.Add($"t+{i}s: exited={host.HasExited} first window alive={IsWindow(first)} visible={IsWindowVisible(first)} second alive={IsWindow(second)}; new connection: {fresh}; old connection: {old}");
                if (i == 2) notes.Add("screen: " + Screenshot("q01-after-first-close"));
                if (host.HasExited) break;
            }
            await Task.Delay(1000); // the log is written in batches
            try
            {
                using var reader = new StreamReader(new FileStream(Path.Combine(testRoot, "logs", "debug.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
                foreach (var line in reader.ReadToEnd().Split('\n').Where(l => (l.Contains("window") || l.Contains("automation")) && !l.StartsWith("   at ") && !l.Contains("FileDropTarget")).TakeLast(12))
                    notes.Add("log: " + line.Trim());
            }
            catch (Exception e) { notes.Add("no log: " + e.Message); }
            Check(!host.HasExited, "the process stays while a window is open");
            List<string> left;
            try { left = ((JArray)(await c.Call("window.list"))["windows"]!).Select(w => (string)w["windowId"]!).ToList(); }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                notes.Add($"the connection broke after the first close ({e.Message})");
                throw;
            }
            Check(left.Count == 1 && left[0] == windows[1], $"one window left ({string.Join(", ", left)})");
            PostMessage(second, 0x0010, IntPtr.Zero, IntPtr.Zero);
        }
        var exited = host.WaitForExit(20000);
        if (exited) notes.Add("the process ended");
        else { host.Kill(); host.WaitForExit(10000); }
        try { await Task.Delay(1000); Directory.Delete(testRoot, true); } catch (Exception e) { notes.Add("the folder stays: " + e.Message); }
        Check(exited, "the process exits once its last window is closed (it stayed running with no window)");
    }

    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    private static async Task R04(List<string> notes)
    {
        string u1, u2, t;
        var titled = Fixture("r04-t.md", "# T\n\nsaved\n");
        using (var c = await Session("e2e R04 before"))
        {
            var created = await c.Call("document.create", new { text = "# U1\n\nfirst untitled\n", reveal = "document", normalizationPolicy = "allowUnknown" });
            u1 = (string)created["documentId"]!;
            u2 = (string)(await c.Call("document.create", new { text = "# U2\n\nsecond untitled\n", reveal = "document", normalizationPolicy = "allowUnknown" }))["documentId"]!;
            t = await Open(c, titled);
            await c.Call("document.replace", new { documentId = t, baseRevision = await Revision(c, t), text = "# T\n\nedited, not saved\n", normalizationPolicy = "allowUnknown" });
            await c.Call("document.focus", new { documentId = u2 }); // u1 and t are background tabs now
        }
        // The backups exist, with each document's own text, before the host dies.
        var backup = Path.Combine(testRoot, "data", "Backup");
        string Untitled(string id) => Path.Combine(backup, $"untitled_{id}.md");
        using (var c = await Session("e2e R04 backup"))
        {
            // One backup pass now, as the save timer would do within five seconds.
            var pass = await c.Call("test.backup.run");
            Check(((JArray)pass["windows"]!).All(w => (bool?)w["ok"] == true), "the backup pass succeeded in every window: " + pass.ToString(Formatting.None));
        }
        notes.Add($"u1={u1} u2={u2} t={t}");
        Check(File.Exists(Untitled(u1)) && Disk(Untitled(u1)) == "# U1\n\nfirst untitled\n", $"the first untitled document (a background tab) has its own backup ({(File.Exists(Untitled(u1)) ? JsonConvert.SerializeObject(Disk(Untitled(u1))) : "missing")})");
        Check(File.Exists(Untitled(u2)) && Disk(Untitled(u2)) == "# U2\n\nsecond untitled\n", "the second untitled document has its own backup");
        var titledBackup = Directory.GetFiles(backup, "*_r04-t.md").FirstOrDefault();
        Check(titledBackup != null && Disk(titledBackup) == "# T\n\nedited, not saved\n", "the background titled document has its backup");

        notes.Add("backups checked");
        // Killed, not closed: nothing gets to save or clean up.
        using (var dying = Process.GetProcessById(hostPid))
        {
            dying.Kill();
            dying.WaitForExit(10000);
        }
        notes.Add("killed");
        File.Delete(Path.Combine(testRoot, "automation-endpoint.txt"));
        // Shell-started: a child started directly would inherit this driver's redirected output, and the script
        // running the driver would then wait for the test host to exit before it could finish.
        var restarted = Process.Start(new ProcessStartInfo(hostExe, $"--automation-test-root \"{testRoot}\"") { UseShellExecute = true })!;
        hostPid = restarted.Id;
        notes.Add($"restarted pid {hostPid}");
        var restartedEndpoint = await ReadEndpointFile(Path.Combine(testRoot, "automation-endpoint.txt")) ?? throw new CaseFailed("the restarted host never published its endpoint");
        Check(restartedEndpoint == endpoint, $"the restarted host listens on the same endpoint as before ({restartedEndpoint} vs {endpoint})");
        var listenError = Path.Combine(testRoot, "automation-endpoint-error.txt");
        Client? afterClient = null;
        for (var i = 0; i < 30 && afterClient == null; i++)
        {
            if (File.Exists(listenError)) throw new CaseFailed("the restarted host could not listen: " + File.ReadAllText(listenError));
            try { afterClient = await Connect(); }
            catch (TimeoutException) { }
        }
        if (afterClient == null) throw new CaseFailed("the restarted host never accepted a connection");
        using var after = afterClient;
        notes.Add("connected");
        await Initialize(after, "e2e R04 after");
        await WaitForWindow(after);
        notes.Add("window up");
        // The recovery question: answered as a person would, with Enter (Recover is the default button).
        JArray documents = new();
        for (var i = 0; i < 60; i++)
        {
            documents = (JArray)(await after.Call("document.list"))["documents"]!;
            if (documents.Any(d => (string)d["documentId"]! == u1)) break;
            var sw = Stopwatch.StartNew();
            var answered = AnswerDialog();
            notes.Add($"dialog attempt {i}: {(answered ? "answered" : lastDialogProblem[..Math.Min(300, lastDialogProblem.Length)])} ({sw.ElapsedMilliseconds} ms)");
            await Task.Delay(250);
        }
        if (!documents.Any(d => (string)d["documentId"]! == u1)) throw new CaseFailed("the recovery question was not answered: " + lastDialogProblem);
        async Task<string> TextOf(string id) => (string)(await Get(after, id))["text"]!;
        Check(documents.Any(d => (string)d["documentId"]! == u1) && documents.Any(d => (string)d["documentId"]! == u2), "both untitled documents came back with their ids");
        Check(await TextOf(u1) == "# U1\n\nfirst untitled\n", "the first untitled document has its own text");
        Check(await TextOf(u2) == "# U2\n\nsecond untitled\n", "the second untitled document has its own text");
        // The titled one comes back when opened again (its backup is matched by path) - the open asks, Enter recovers.
        var reopening = after.Call("document.open", new { path = titled, reveal = "document" });
        for (var i = 0; i < 60 && !reopening.IsCompleted; i++) { AnswerDialog(); await Task.Delay(250); }
        var reopened = (string)(await reopening)["documentId"]!;
        Check(await TextOf(reopened) == "# T\n\nedited, not saved\n", "the titled document recovers its unsaved text");
        Check(Disk(titled) == "# T\n\nsaved\n", "and its file was never written");
        notes.Add($"recovered {u1[..6]} {u2[..6]} and {Path.GetFileName(titled)}");
    }

    /// <summary>
    /// Answers the app's dialog with its primary button, through UI Automation - the button a person would press.
    /// (A key press does not reach a XAML dialog while keyboard focus is outside the XAML island.) True when a dialog
    /// was answered.
    /// </summary>
    private static string lastDialogProblem = "";

    private static bool AnswerDialog()
    {
        var window = MainWindow();
        if (window == IntPtr.Zero) { lastDialogProblem = "no main window"; return false; }
        try
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(window);
            var button = root.FindFirst(System.Windows.Automation.TreeScope.Descendants,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, "PrimaryButton"));
            if (button == null) { lastDialogProblem = "no PrimaryButton under " + window + ": " + Describe(root, 0); return false; }
            ((System.Windows.Automation.InvokePattern)button.GetCurrentPattern(System.Windows.Automation.InvokePattern.Pattern)).Invoke();
            return true;
        }
        catch (Exception e)
        {
            lastDialogProblem = e.GetType().Name + ": " + e.Message;
            return false;
        }
    }

    /// <summary>A short outline of the UI Automation tree, for a failure message.</summary>
    private static string Describe(System.Windows.Automation.AutomationElement element, int depth)
    {
        if (depth > 6) return "";
        var c = element.Current;
        var text = $"[{c.ControlType.ProgrammaticName.Replace("ControlType.", "")} '{c.Name}' id={c.AutomationId} cls={c.ClassName}";
        var children = element.FindAll(System.Windows.Automation.TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
        foreach (System.Windows.Automation.AutomationElement child in children) text += " " + Describe(child, depth + 1);
        return text + "]";
    }

    /// <summary>
    /// Brings the window to the front. SetForegroundWindow is refused when another process holds the foreground; a
    /// real click on the window's title strip activates it the way a person would.
    /// </summary>
    private static async Task Activate(IntPtr window)
    {
        AllowSetForegroundWindow(hostPid);
        SetForegroundWindow(window);
        for (var i = 0; i < 20 && GetForegroundWindow() != window; i++) await Task.Delay(25);
        if (GetForegroundWindow() == window) return;
        GetWindowRect(window, out var rect);
        SetCursorPos(rect.Left + 120, rect.Top + 12);
        Send(new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0002 } } }, new INPUT { type = 0, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = 0x0004 } } });
        for (var i = 0; i < 40 && GetForegroundWindow() != window; i++) await Task.Delay(25);
    }

    // ---- real input ----

    private static IntPtr MainWindow() => Process.GetProcessById(hostPid).MainWindowHandle;

    private static string WindowTitle()
    {
        var sb = new StringBuilder(512);
        GetWindowText(MainWindow(), sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// Real keystrokes into the window that shows the document (not the process's first window: with two windows the
    /// document may be in the other one). The caller waits for them to reach the page (WaitForPage).
    /// </summary>
    private static async Task TypeInto(Client c, string documentId, string text)
    {
        var windowId = (string)(await c.Call("document.focus", new { documentId }))["windowId"]!;
        await TypeIntoWindow(c, windowId, text);
    }

    private static async Task TypeIntoWindow(Client c, string windowId, string text)
    {
        var window = await WindowOf(windowId, c);
        await Activate(window);
        if (GetForegroundWindow() != window) throw new CaseFailed("the test host window could not be brought to the front (is the desktop locked?)");
        // Keyboard focus into the editor as a person gives it: a click in the text area, then Ctrl+End.
        GetWindowRect(window, out var rect);
        // Keyboard focus into the editor page as the app gives it, not by a click: what lies under a click depends on
        // the document (an image block takes the click, and then the keys). Ctrl+End then puts the caret at the end.
        await c.Call("test.editor.focus", new { windowId });
        await Task.Delay(150); // focus hand-over settling; whether the keys arrived is checked by the caller
        Send(Key(0x11, false), Key(0x23, false), Key(0x23, true), Key(0x11, true));
        var inputs = new List<INPUT>();
        foreach (var ch in text)
        {
            inputs.Add(new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0004 } } });
            inputs.Add(new INPUT { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wScan = ch, dwFlags = 0x0004 | 0x0002 } } });
        }
        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
        if (sent != inputs.Count) throw new CaseFailed($"SendInput sent {sent} of {inputs.Count} events (is the desktop locked?)");
    }

    private static INPUT Key(ushort vk, bool up) => new() { type = 1, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? 0x0002u : 0u } } };

    private static void Send(params INPUT[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) != inputs.Length) throw new CaseFailed("SendInput was refused (is the desktop locked?)");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanW(char ch);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int size);
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr data);

    // Which window has the keyboard: the foreground window, its thread's focus window with its parents, by class.
    private static string FocusInfo(IntPtr window)
    {
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        var fg = GetForegroundWindow();
        GetGUIThreadInfo(GetWindowThreadProcessId(fg, out _), ref info);
        string Name(IntPtr h) { var n = new System.Text.StringBuilder(256); GetClassName(h, n, 256); return n.Length > 0 ? n.ToString() : "-"; }
        var chain = new List<string>();
        for (var h = info.hwndFocus; h != IntPtr.Zero && chain.Count < 6; h = GetParent(h)) chain.Add(Name(h));
        return $"foreground {(fg == window ? "the test window" : Name(fg))}, focus {string.Join(" < ", chain)}";
    }

    [StructLayout(LayoutKind.Sequential)] private struct GUITHREADINFO { public int cbSize; public uint flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GUITHREADINFO info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);

    /// <summary>Waits until the page itself holds text satisfying the condition (test.editor.pageText), bounded.</summary>
    private static async Task<string> WaitForPage(Client c, string documentId, Func<string, bool> condition, string what)
    {
        string? text = null;
        for (var i = 0; i < 100; i++)
        {
            text = (string?)(await c.Call("test.editor.pageText", new { documentId }))["text"];
            if (text != null && condition(text)) return text;
            await Task.Delay(50);
        }
        throw new CaseFailed($"{what} never reached the page ({JsonConvert.SerializeObject(text)}); screen: {Screenshot("no-keystroke-" + DateTime.Now.ToString("HHmmss"))}");
    }

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int max);
}
