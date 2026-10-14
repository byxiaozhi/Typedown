using System;
using System.Collections.Generic;
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

namespace Typedown.Cli
{
    /// <summary>
    /// typedownctl. Every command connects, initializes with only the scopes it needs, makes one call and prints the
    /// result: with --json exactly one JSON value on stdout (the result, or {"error": ...}), otherwise a short text for
    /// people. The exit code says what kind of failure it was; the JSON error's code and data.kind say exactly which.
    /// </summary>
    public sealed class Cli
    {
        public const int Ok = 0, Usage = 2, NotRunning = 3, Incompatible = 4, NotFound = 5, Conflict = 6, NotReady = 7, SaveFailed = 8, Other = 9;

        public const string Help = $@"{Brand.CliName} - control a running {Brand.Name} through its local automation API

usage: {Brand.CliName} [--json] [--endpoint NAME] [--client-id UUID] <command> [options]

  status                                  application, version and windows
  windows                                 open windows
  documents [--window ID]                 open documents (no text)
  get <documentId> [--latest] [--text] [--headings]
                                          metadata; --latest flushes the editor first (default: snapshot)
  open <path> [--window ID] [--reveal]    open a file (an open one is returned as is)
  create [--text-file F | --stdin] [--window ID] [--reveal] [--allow-unknown-normalization]
  replace <documentId> --base-revision N (--text-file F | --stdin) [--save] [--reveal | --reveal-change] [--lf]
          [--allow-unknown-normalization] [--client-operation-id S]
  replace-text <documentId> --base-revision N --find S --replacement S --expected-count N [--save]
          [--reveal | --reveal-change]
          [--allow-unknown-normalization] [--client-operation-id S]
  save <documentId> [--base-revision N]
  close <documentId> [--base-revision N]  close a saved document (an unsaved one is refused, exit 6)
  settings describe | settings get [key ...] | settings set <key> <json value> --base-revision N
                                          external settings (appearance.theme, editor.fontSize, ...);
                                          the theme also as dark / light / black / system or custom:<id>
  view <windowId> [--mode visual|source|reading] [--side-pane closed|files|outline] [--status-bar on|off]
          [--focus on|off] [--typewriter on|off] [--bounds X,Y,W,H | --size WxH]
                                          how the window shows its document; no option only reads it
  mcp [--endpoint NAME]                   run as an MCP server on stdin/stdout, for AI agents

--reveal brings the document's window and tab to the front; --reveal-change also scrolls the first changed passage
into view when it is off screen (the cursor stays).
Document text uses \n line endings; text with \r is refused unless --lf converts it on the way in.
Exit codes: 0 ok, 2 usage, 3 not running or not reachable, 4 version or scope, 5 window/document not found,
6 revision, match-count or unsaved-changes conflict, 7 editor not ready, 8 save failed, 9 other.
";

        private readonly TextReader stdin;
        private readonly TextWriter stdout;
        private readonly TextWriter stderr;
        private readonly Func<string, CancellationToken, Task<Stream>> connect;

        public Cli(TextReader stdin, TextWriter stdout, TextWriter stderr, Func<string, CancellationToken, Task<Stream>>? connect = null)
        {
            this.stdin = stdin;
            this.stdout = stdout;
            this.stderr = stderr;
            this.connect = connect ?? ConnectPipeAsync;
        }

        private sealed class UsageException : Exception
        {
            public UsageException(string message) : base(message) { }
        }

        private sealed class Args
        {
            public readonly List<string> Positional = new();
            public readonly Dictionary<string, string?> Options = new();
            private readonly HashSet<string> used = new();

            public bool Flag(string name) { used.Add(name); return Options.ContainsKey(name); }
            public string? Value(string name) { used.Add(name); return Options.TryGetValue(name, out var v) ? v ?? throw new UsageException($"--{name} needs a value") : null; }
            public string Required(string name) => Value(name) ?? throw new UsageException($"--{name} is required");
            public long RequiredLong(string name) => long.TryParse(Required(name), out var v) && v >= 0 ? v : throw new UsageException($"--{name} must be a non-negative integer");
            public long? OptionalLong(string name) => Value(name) is string s ? (long.TryParse(s, out var v) && v >= 0 ? v : throw new UsageException($"--{name} must be a non-negative integer")) : null;
            public string Positional0(string what) => Positional.Count > 0 ? Positional[0] : throw new UsageException($"{what} is required");
            public void CheckAllUsed(int positionalAllowed)
            {
                var unknown = Options.Keys.Where(k => !used.Contains(k)).ToList();
                if (unknown.Count > 0) throw new UsageException($"unknown option --{unknown[0]}");
                if (Positional.Count > positionalAllowed) throw new UsageException($"unexpected argument '{Positional[positionalAllowed]}'");
            }
        }

        private static readonly HashSet<string> Flags = new() { "json", "latest", "text", "headings", "reveal", "reveal-change", "save", "stdin", "lf", "allow-unknown-normalization", "help" };

        private static (string command, Args args) Parse(string[] argv)
        {
            var args = new Args();
            string? command = null;
            for (var i = 0; i < argv.Length; i++)
            {
                var a = argv[i];
                if (a.StartsWith("--"))
                {
                    var name = a.Substring(2);
                    if (Flags.Contains(name)) args.Options[name] = null;
                    else if (i + 1 < argv.Length) args.Options[name] = argv[++i];
                    else throw new UsageException($"--{name} needs a value");
                }
                else if (command == null) command = a;
                else args.Positional.Add(a);
            }
            return (command ?? "", args);
        }

        public async Task<int> RunAsync(string[] argv, CancellationToken cancellationToken = default)
        {
            bool json = argv.Contains("--json");
            try
            {
                var (command, args) = Parse(argv);
                if (command == "" || command == "help" || args.Options.ContainsKey("help"))
                {
                    stdout.Write(Help);
                    return command == "" && !args.Options.ContainsKey("help") ? Usage : Ok;
                }
                args.Flag("json");
                var endpoint = args.Value("endpoint") ?? DefaultEndpoint();
                var clientId = args.Value("client-id") ?? ClientId.Load();
                var (method, parameters, scopes) = Build(command, args);

                Stream stream;
                try { stream = await connect(endpoint, cancellationToken).ConfigureAwait(false); }
                catch (Exception e) when (e is TimeoutException || e is IOException || e is UnauthorizedAccessException)
                {
                    return Fail(json, NotRunning, null, $"{Brand.Name} is not running, or its automation switch is off ({e.Message}).");
                }
                using (stream)
                {
                    var framing = new MessageFraming(stream, 64L * 1024 * 1024);
                    var initialize = new JObject
                    {
                        ["apiVersion"] = AutomationSession.ApiVersion,
                        ["client"] = new JObject { ["id"] = clientId, ["name"] = Brand.CliName, ["version"] = typeof(Cli).Assembly.GetName().Version?.ToString() ?? "" },
                        ["requestedScopes"] = new JArray(scopes),
                    };
                    var init = await CallAsync(framing, 1, "system.initialize", initialize, cancellationToken).ConfigureAwait(false);
                    if (init["error"] is JObject initError) return FailWith(json, initError);
                    var denied = init["result"]?["deniedScopes"] as JArray;
                    if (denied != null && denied.Count > 0)
                        return Fail(json, Incompatible, null, $"This {Brand.Name} does not grant: {string.Join(", ", denied.Select(d => d["scope"]))}.");
                    var reply = await CallAsync(framing, 2, method, parameters, cancellationToken).ConfigureAwait(false);
                    if (reply["error"] is JObject error) return FailWith(json, error);
                    var result = reply["result"] ?? JValue.CreateNull();
                    if (json) stdout.WriteLine(result.ToString(Formatting.None));
                    else stdout.Write(Describe(command, result));
                    return Ok;
                }
            }
            catch (UsageException e)
            {
                return Fail(json, Usage, null, e.Message + $" ({Brand.CliName} help)");
            }
            catch (FramingException e)
            {
                return Fail(json, Other, null, $"The connection broke: {e.Message}");
            }
            catch (IOException e)
            {
                return Fail(json, NotRunning, null, $"The connection closed: {e.Message}");
            }
        }

        private (string method, JObject parameters, string[] scopes) Build(string command, Args a)
        {
            JObject p;
            switch (command)
            {
                case "status":
                    a.CheckAllUsed(0);
                    return ("app.getState", new JObject(), new[] { Scopes.AppRead });
                case "windows":
                    a.CheckAllUsed(0);
                    return ("window.list", new JObject(), new[] { Scopes.AppRead });
                case "documents":
                    p = new JObject();
                    if (a.Value("window") is string w) p["windowId"] = w;
                    a.CheckAllUsed(0);
                    return ("document.list", p, new[] { Scopes.DocumentRead });
                case "get":
                {
                    p = new JObject { ["documentId"] = a.Positional0("documentId"), ["consistency"] = a.Flag("latest") ? "latest" : "snapshot" };
                    var include = new JArray();
                    if (a.Flag("text")) include.Add("text");
                    if (a.Flag("headings")) include.Add("headings");
                    if (include.Count > 0) p["include"] = include;
                    a.CheckAllUsed(1);
                    return ("document.get", p, new[] { Scopes.DocumentRead });
                }
                case "open":
                    p = new JObject { ["path"] = Path.GetFullPath(a.Positional0("path")) };
                    if (a.Value("window") is string ow) p["windowId"] = ow;
                    var openScopes = Reveal(a, p, Scopes.DocumentWrite);
                    a.CheckAllUsed(1);
                    return ("document.open", p, openScopes);
                case "create":
                {
                    p = new JObject();
                    if (a.Value("window") is string cw) p["windowId"] = cw;
                    var text = ReadText(a, required: false);
                    if (text != null) p["text"] = text;
                    Policy(a, p);
                    var scopes = Reveal(a, p, Scopes.DocumentWrite);
                    a.CheckAllUsed(0);
                    return ("document.create", p, scopes);
                }
                case "replace":
                {
                    p = new JObject { ["documentId"] = a.Positional0("documentId"), ["baseRevision"] = a.RequiredLong("base-revision"), ["text"] = ReadText(a, required: true) };
                    return Write("document.replace", p, a);
                }
                case "replace-text":
                {
                    p = new JObject
                    {
                        ["documentId"] = a.Positional0("documentId"),
                        ["baseRevision"] = a.RequiredLong("base-revision"),
                        ["find"] = a.Required("find"),
                        ["replacement"] = a.Required("replacement"),
                        ["expectedCount"] = a.RequiredLong("expected-count"),
                    };
                    return Write("document.replaceText", p, a);
                }
                case "settings":
                {
                    var sub = a.Positional0("settings describe | get | set");
                    switch (sub)
                    {
                        case "describe":
                            a.CheckAllUsed(1);
                            return ("settings.describe", new JObject(), new[] { Scopes.SettingsRead });
                        case "get":
                            p = new JObject();
                            if (a.Positional.Count > 1) p["keys"] = new JArray(a.Positional.Skip(1));
                            return ("settings.get", p, new[] { Scopes.SettingsRead });
                        case "set":
                            if (a.Positional.Count < 3) throw new UsageException("settings set <key> <json value> --base-revision N");
                            p = new JObject { ["key"] = a.Positional[1], ["value"] = SettingValue(a.Positional[1], a.Positional[2]), ["baseSettingsRevision"] = a.RequiredLong("base-revision") };
                            a.CheckAllUsed(3);
                            return ("settings.set", p, new[] { Scopes.SettingsWrite });
                        default:
                            throw new UsageException($"unknown settings command '{sub}'");
                    }
                }
                case "view":
                {
                    p = new JObject { ["windowId"] = a.Positional0("windowId") };
                    if (a.Value("mode") is string mode) p["mode"] = mode;
                    if (a.Value("side-pane") is string pane)
                        p["sidePane"] = pane switch
                        {
                            "closed" => new JObject { ["open"] = false },
                            "files" or "outline" => new JObject { ["open"] = true, ["page"] = pane },
                            _ => throw new UsageException("--side-pane is closed, files or outline"),
                        };
                    if (OnOff(a, "status-bar") is bool statusBar) p["statusBar"] = statusBar;
                    if (OnOff(a, "focus") is bool focus) p["focusMode"] = focus;
                    if (OnOff(a, "typewriter") is bool typewriter) p["typewriter"] = typewriter;
                    var bounds = a.Value("bounds");
                    var size = a.Value("size");
                    if (bounds != null && size != null) throw new UsageException("give --bounds or --size, not both");
                    if (bounds != null)
                    {
                        var n = Numbers(bounds, ',', 4, "--bounds X,Y,W,H");
                        p["bounds"] = new JObject { ["x"] = n[0], ["y"] = n[1], ["width"] = n[2], ["height"] = n[3] };
                    }
                    if (size != null)
                    {
                        var n = Numbers(size, 'x', 2, "--size WxH");
                        p["bounds"] = new JObject { ["width"] = n[0], ["height"] = n[1] };
                    }
                    a.CheckAllUsed(1);
                    return p.Count == 1 ? ("window.getView", p, new[] { Scopes.AppRead }) : ("window.setView", p, new[] { Scopes.WindowView });
                }
                case "close":
                    p = new JObject { ["documentId"] = a.Positional0("documentId") };
                    if (a.OptionalLong("base-revision") is long cr) p["baseRevision"] = cr;
                    a.CheckAllUsed(1);
                    return ("document.close", p, new[] { Scopes.DocumentWrite });
                case "save":
                    p = new JObject { ["documentId"] = a.Positional0("documentId") };
                    if (a.OptionalLong("base-revision") is long r) p["baseRevision"] = r;
                    a.CheckAllUsed(1);
                    return ("document.save", p, new[] { Scopes.DocumentSave });
                default:
                    throw new UsageException($"unknown command '{command}'");
            }
        }

        private (string, JObject, string[]) Write(string method, JObject p, Args a)
        {
            Policy(a, p);
            if (a.Value("client-operation-id") is string op) p["clientOperationId"] = op;
            var scopes = new List<string> { Scopes.DocumentWrite };
            if (a.Flag("save")) { p["save"] = true; scopes.Add(Scopes.DocumentSave); }
            scopes.AddRange(Reveal(a, p).Where(s => !scopes.Contains(s)));
            a.CheckAllUsed(1);
            return (method, p, scopes.ToArray());
        }

        /// <summary>
        /// A setting's value as typed. JSON as it is; a bare word is a string (rtl, auto). The theme also takes a short
        /// form - a built-in theme's name (dark), or custom:&lt;id&gt; - and an object whose quotes Windows PowerShell 5
        /// dropped on the way ({kind:builtIn,id:dark}) gets them back: that shell strips the double quotes inside an
        /// argument to a native program, so the JSON a person typed arrived as invalid JSON.
        /// </summary>
        public static JToken SettingValue(string key, string text)
        {
            try { return JToken.Parse(text); }
            catch (JsonException) { }
            var trimmed = text.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                // Each bare name or value between the separators, spaces inside it included (a custom theme
                // named My Theme); numbers, true, false and null stay as they are.
                var quoted = System.Text.RegularExpressions.Regex.Replace(trimmed, @"(?<=[{,:])\s*([^{}\[\],:""]+?)\s*(?=[,:}])",
                    m =>
                    {
                        var word = m.Groups[1].Value;
                        var literal = word is "true" or "false" or "null" ||
                            double.TryParse(word, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                        return literal ? word : JsonConvert.ToString(word);
                    });
                try { return JToken.Parse(quoted); }
                catch (JsonException) { }
            }
            if (key == "appearance.theme")
            {
                if (trimmed is "system" or "light" or "dark" or "black") return new JObject { ["kind"] = "builtIn", ["id"] = trimmed };
                if (trimmed.StartsWith("custom:") && trimmed.Length > 7) return new JObject { ["kind"] = "custom", ["id"] = trimmed.Substring(7) };
            }
            return text;
        }

        private static bool? OnOff(Args a, string name) => a.Value(name) switch
        {
            null => null,
            "on" or "true" => true,
            "off" or "false" => false,
            _ => throw new UsageException($"--{name} is on or off"),
        };

        private static long[] Numbers(string text, char separator, int count, string usage)
        {
            var parts = text.Split(separator);
            var numbers = new long[parts.Length];
            for (var i = 0; i < parts.Length; i++)
                if (!long.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out numbers[i]))
                    throw new UsageException(usage);
            return parts.Length == count ? numbers : throw new UsageException(usage);
        }

        private static void Policy(Args a, JObject p)
        {
            // Accepting an unclassified normalization is the caller's explicit choice, never a default.
            if (a.Flag("allow-unknown-normalization")) p["normalizationPolicy"] = "allowUnknown";
        }

        private static string[] Reveal(Args a, JObject p, params string[] scopes)
        {
            var document = a.Flag("reveal");
            var change = a.Flag("reveal-change");
            if (!document && !change) return scopes;
            if (document && change) throw new UsageException("give --reveal or --reveal-change, not both");
            // --reveal-change: also scroll the first changed block into view (reveal: "change").
            p["reveal"] = change ? "change" : "document";
            return scopes.Append(Scopes.WindowFocus).ToArray();
        }

        private string? ReadText(Args a, bool required)
        {
            var file = a.Value("text-file");
            var fromStdin = a.Flag("stdin");
            var lf = a.Flag("lf");
            if (file != null && fromStdin) throw new UsageException("give --text-file or --stdin, not both");
            string? text = file != null ? File.ReadAllText(file, new UTF8Encoding(false)) : fromStdin ? stdin.ReadToEnd() : null;
            if (text == null && required) throw new UsageException("the text is required: --text-file F or --stdin");
            if (text != null && text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            if (text != null && lf) text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            return text;
        }

        private static async Task<JObject> CallAsync(MessageFraming framing, int id, string method, JObject parameters, CancellationToken ct)
        {
            var message = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters };
            await framing.WriteAsync(new UTF8Encoding(false).GetBytes(message.ToString(Formatting.None)), ct).ConfigureAwait(false);
            while (true)
            {
                var frame = await framing.ReadAsync(ct).ConfigureAwait(false);
                if (frame.Status == FrameStatus.EndOfStream) throw new IOException($"{Brand.Name} closed the connection.");
                if (frame.Status != FrameStatus.Message) continue;
                var reply = JObject.Parse(new UTF8Encoding(false).GetString(frame.Body!));
                if (reply["id"]?.Type == JTokenType.Integer && (int)reply["id"]! == id) return reply;
                if (reply["id"]?.Type == JTokenType.Null && reply["error"] != null) return reply;
            }
        }

        public static int ExitCodeFor(string? kind) => kind switch
        {
            "unsupported_version" or "scope_required" or "not_initialized" or "method_not_found" => Incompatible,
            "window_not_found" or "document_not_found" => NotFound,
            "revision_conflict" or "match_count_mismatch" or "unsaved_changes" => Conflict,
            "editor_not_ready" or "content_sync_timeout" or "presentation_timeout" => NotReady,
            "save_failed" or "persistence_failed" => SaveFailed,
            "invalid_params" => Usage,
            _ => Other,
        };

        private int FailWith(bool json, JObject error)
        {
            var kind = (string?)error["data"]?["kind"];
            return Fail(json, ExitCodeFor(kind), error, $"{error["message"]} [{kind}]");
        }

        private int Fail(bool json, int exit, JObject? error, string message)
        {
            stderr.WriteLine($"{Brand.CliName}: " + message);
            if (json)
            {
                error ??= new JObject { ["code"] = 0, ["message"] = message, ["data"] = new JObject { ["kind"] = exit == Usage ? "cli_usage" : exit == NotRunning ? "cli_not_connected" : "cli_error" } };
                stdout.WriteLine(new JObject { ["error"] = error }.ToString(Formatting.None));
            }
            return exit;
        }

        private static string Describe(string command, JToken result)
        {
            var sb = new StringBuilder();
            switch (command)
            {
                case "status":
                    sb.AppendLine($"{Brand.Name} {result["version"]}, {result["windowCount"]} window(s), active window {result["activeWindowId"]}");
                    break;
                case "windows":
                    foreach (var w in result["windows"]!) sb.AppendLine($"{w["windowId"]}  {(bool)w["active"]! switch { true => "active", false => "      " }}  {w["documentCount"]} document(s)");
                    break;
                case "documents":
                    foreach (var d in result["documents"]!)
                        sb.AppendLine($"{d["documentId"]}  r{d["revision"]}  {((bool)d["saved"]! ? "saved  " : "unsaved")}  {(string?)d["path"] ?? "(untitled) " + d["title"]}");
                    break;
                case "get":
                    if (result["text"] is JToken t) { sb.Append((string?)t); break; }
                    sb.AppendLine($"{result["documentId"]}  revision {result["revision"]}  {((bool)result["saved"]! ? "saved" : "unsaved")}  {(string?)result["path"] ?? "(untitled)"}");
                    sb.AppendLine($"contentHash {result["contentHash"]}  pendingNormalization {result["normalization"]?["pendingNormalization"]}");
                    if (result["headings"] is JArray hs) foreach (var h in hs) sb.AppendLine($"{new string('#', (int)h["level"]!)} {h["text"]}  ({h["slug"]})");
                    break;
                case "view":
                    sb.AppendLine($"mode {result["mode"]}  side pane {((bool)result["sidePane"]!["open"]! ? (string?)result["sidePane"]!["page"] : "closed")}  status bar {((bool)result["statusBar"]! ? "on" : "off")}"
                        + $"  focus {((bool)result["focusMode"]! ? "on" : "off")}  typewriter {((bool)result["typewriter"]! ? "on" : "off")}");
                    var b = result["bounds"]!;
                    sb.AppendLine($"bounds {b["x"]},{b["y"]},{b["width"]},{b["height"]}{((bool)result["maximized"]! ? "  maximized" : "")}");
                    break;
                default:
                    foreach (var property in ((JObject)result).Properties().Where(p => p.Value.Type != JTokenType.Object))
                        sb.AppendLine($"{property.Name}: {property.Value}");
                    break;
            }
            return sb.ToString();
        }

        /// <summary>The installed application's endpoint: its named pipe on Windows, its socket path elsewhere.</summary>
        public static string DefaultEndpoint() =>
#if NET7_0_OR_GREATER
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? UnixSocketListener.DefaultPath(BuildTypes.Application) :
#endif
            AutomationEndpoint.PipeName(BuildTypes.Application, CurrentUserId());

        public static string CurrentUserId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsUser.CurrentSid();
            return geteuid().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        [DllImport("libc")]
        private static extern uint geteuid();

        /// <summary>A pipe name, or a socket path (anything with a '/') on Linux and macOS.</summary>
        public static async Task<Stream> ConnectPipeAsync(string endpoint, CancellationToken ct)
        {
#if NET7_0_OR_GREATER
            if (endpoint.Contains('/') && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(3000);
                try { return await UnixSocketListener.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("No answer from " + endpoint); }
            }
#endif
            var pipe = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(3000, ct).ConfigureAwait(false);
                return pipe;
            }
            catch
            {
                pipe.Dispose();
                throw;
            }
        }
    }

    /// <summary>The CLI's own client id: a UUID made once and kept (self-reported, for logs and the connection marker).</summary>
    public static class ClientId
    {
        public static string Load()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), Brand.Name);
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "typedownctl-client-id");
                if (File.Exists(file) && Guid.TryParseExact(File.ReadAllText(file).Trim(), "D", out var existing)) return existing.ToString("D");
                var id = Guid.NewGuid().ToString("D");
                File.WriteAllText(file, id);
                return id;
            }
            catch
            {
                return Guid.NewGuid().ToString("D");
            }
        }
    }
}
