using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Typedown.Automation;

namespace Typedown.Mcp
{
    /// <summary>Typedown is not reachable: not running, or its automation switch is off.</summary>
    public sealed class NotConnectedException : Exception
    {
        public NotConnectedException(string message) : base(message) { }
    }

    /// <summary>
    /// One session with Typedown, opened on first use and kept. A broken connection is dropped and the next call opens a
    /// new one; the call that saw it break is not repeated, since a write may or may not have been applied.
    /// </summary>
    public sealed class TypedownConnection : IDisposable
    {
        public static readonly string[] RequestedScopes =
            { Scopes.AppRead, Scopes.DocumentRead, Scopes.DocumentWrite, Scopes.DocumentSave, Scopes.WindowFocus, Scopes.WindowView };

        private readonly string endpoint;
        private readonly string clientId;
        private readonly Func<string> clientName;
        private readonly Func<string, CancellationToken, Task<Stream>> connect;
        private readonly SemaphoreSlim gate = new(1, 1);
        private Stream? stream;
        private MessageFraming? framing;
        private int nextId = 1;

        public TypedownConnection(string endpoint, string clientId, Func<string> clientName, Func<string, CancellationToken, Task<Stream>> connect)
        {
            this.endpoint = endpoint;
            this.clientId = clientId;
            this.clientName = clientName;
            this.connect = connect;
        }

        /// <summary>Scopes Typedown refused for this session (tools that need them fail with scope_required).</summary>
        public string[] DeniedScopes { get; private set; } = Array.Empty<string>();

        /// <summary>The reply's result, or its error object as an <see cref="ApiError"/>.</summary>
        public async Task<JToken> CallAsync(string method, JObject parameters, CancellationToken ct)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var framing = await OpenAsync(ct).ConfigureAwait(false);
                JObject reply;
                try { reply = await SendAsync(framing, method, parameters, ct).ConfigureAwait(false); }
                catch (Exception e) when (e is IOException || e is FramingException || e is ObjectDisposedException)
                {
                    Drop();
                    throw new NotConnectedException($"The connection to {Brand.Name} closed during {method}: {e.Message}");
                }
                if (reply["error"] is JObject error) throw new ApiError(error);
                return reply["result"] ?? JValue.CreateNull();
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<MessageFraming> OpenAsync(CancellationToken ct)
        {
            if (framing != null) return framing;
            try { stream = await connect(endpoint, ct).ConfigureAwait(false); }
            catch (Exception e) when (e is TimeoutException || e is IOException || e is UnauthorizedAccessException)
            {
                throw new NotConnectedException($"{Brand.Name} is not running, or its automation switch is off ({e.Message}).");
            }
            var opened = new MessageFraming(stream, 64L * 1024 * 1024);
            JObject init;
            try
            {
                init = await SendAsync(opened, "system.initialize", new JObject
                {
                    ["apiVersion"] = AutomationSession.ApiVersion,
                    ["client"] = new JObject { ["id"] = clientId, ["name"] = clientName(), ["version"] = typeof(TypedownConnection).Assembly.GetName().Version?.ToString() ?? "" },
                    ["requestedScopes"] = new JArray(RequestedScopes),
                }, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException || e is FramingException)
            {
                Drop();
                throw new NotConnectedException($"{Brand.Name} closed the connection while starting the session: {e.Message}");
            }
            if (init["error"] is JObject error) { Drop(); throw new ApiError(error); }
            DeniedScopes = (init["result"]?["deniedScopes"] as JArray)?.Select(d => (string?)d["scope"] ?? "").ToArray() ?? Array.Empty<string>();
            framing = opened;
            return framing;
        }

        private async Task<JObject> SendAsync(MessageFraming framing, string method, JObject parameters, CancellationToken ct)
        {
            var id = nextId++;
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

        private void Drop()
        {
            try { stream?.Dispose(); } catch { }
            stream = null;
            framing = null;
        }

        public void Dispose() => Drop();
    }

    /// <summary>A JSON-RPC error from Typedown, kept whole: {code, message, data: {kind, ...}}.</summary>
    public sealed class ApiError : Exception
    {
        public ApiError(JObject error) : base((string?)error["message"] ?? "error") => Error = error;

        public JObject Error { get; }
        public string Kind => (string?)Error["data"]?["kind"] ?? "";
    }
}
