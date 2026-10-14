using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    /// <summary>A request or notification from the peer. <see cref="Id"/> is null for a notification.</summary>
    public sealed class JsonRpcRequest
    {
        public JsonRpcRequest(string method, JToken? id, JToken? parameters)
        {
            Method = method;
            Id = id;
            Params = parameters;
        }

        public string Method { get; }
        public JToken? Id { get; }
        public JToken? Params { get; }
        public bool IsNotification => Id == null;
    }

    public interface IJsonRpcHandler
    {
        /// <summary>Answers a request. Throw <see cref="AutomationException"/> for an error the client should see.</summary>
        Task<JToken?> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken);

        Task HandleNotificationAsync(JsonRpcRequest notification, CancellationToken cancellationToken);
    }

    /// <summary>An error response to a request this side sent.</summary>
    public sealed class JsonRpcRemoteException : Exception
    {
        public JsonRpcRemoteException(int code, string message, JToken? data) : base(message)
        {
            Code = code;
            ErrorData = data;
        }

        public int Code { get; }
        public JToken? ErrorData { get; }
    }

    /// <summary>
    /// One JSON-RPC 2.0 connection over <see cref="MessageFraming"/>, in both directions (docs/automation-api-spec.md,
    /// section 1.3): the peer's requests go to the handler, each on its own task so a slow one does not hold up the
    /// reading of the next; responses are matched to this side's own pending requests by id. Batches are not
    /// supported and are answered with <c>invalid_request</c>. When the connection ends, handlers still running are
    /// cancelled and this side's pending requests fail.
    /// </summary>
    public sealed class JsonRpcConnection : IDisposable
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int InternalError = -32603;

        /// <summary>Requests from the peer that may run at once; beyond this they are answered with <c>busy</c>.</summary>
        public const int MaxConcurrentRequests = 32;

        private readonly MessageFraming framing;
        private readonly IJsonRpcHandler handler;
        private readonly CancellationTokenSource lifetime = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JToken?>> pending = new();
        private readonly ConcurrentDictionary<Task, bool> running = new();
        private int inFlight;
        private long nextId;

        public JsonRpcConnection(MessageFraming framing, IJsonRpcHandler handler)
        {
            this.framing = framing ?? throw new ArgumentNullException(nameof(framing));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>Cancelled when the connection ends.</summary>
        public CancellationToken Lifetime => lifetime.Token;

        /// <summary>Reads and dispatches until the peer closes the stream or the framing breaks.</summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            using var link = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            try
            {
                while (true)
                {
                    var frame = await framing.ReadAsync(link.Token).ConfigureAwait(false);
                    if (frame.Status == FrameStatus.EndOfStream) break;
                    if (frame.Status == FrameStatus.TooLarge)
                    {
                        await SendErrorAsync(null, (int)AutomationErrorKind.message_too_large, "The message exceeds the negotiated size limit.",
                            new JObject { ["kind"] = nameof(AutomationErrorKind.message_too_large), ["maxMessageBytes"] = framing.MaxMessageBytes }).ConfigureAwait(false);
                        continue;
                    }
                    await ReceiveAsync(frame.Body!).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (link.IsCancellationRequested) { }
            catch (FramingException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                Shutdown();
            }
            // Let the handlers observe the cancellation and finish before the connection is reported closed.
            foreach (var task in running.Keys)
            {
                try { await task.ConfigureAwait(false); } catch { }
            }
        }

        private static JToken? Parse(byte[] body)
        {
            using var reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(body)))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 64,
            };
            var token = JToken.ReadFrom(reader);
            if (reader.Read()) throw new JsonReaderException("Additional content after the JSON value.");
            return token;
        }

        private async Task ReceiveAsync(byte[] body)
        {
            JToken? message;
            try { message = Parse(body); }
            catch (Exception e) when (e is JsonException || e is DecoderFallbackException)
            {
                await SendErrorAsync(null, ParseError, "The message is not valid UTF-8 JSON.", Kind("parse_error")).ConfigureAwait(false);
                return;
            }
            if (message is JArray)
            {
                await SendErrorAsync(null, InvalidRequest, "Batches are not supported.", Kind("invalid_request")).ConfigureAwait(false);
                return;
            }
            if (!(message is JObject obj) || obj.Value<JToken>("jsonrpc")?.Type != JTokenType.String || (string?)obj["jsonrpc"] != "2.0")
            {
                await SendErrorAsync(null, InvalidRequest, "Not a JSON-RPC 2.0 message.", Kind("invalid_request")).ConfigureAwait(false);
                return;
            }
            var id = obj["id"];
            var validId = id == null || id.Type == JTokenType.String || id.Type == JTokenType.Integer || id.Type == JTokenType.Null;
            if (obj.ContainsKey("method"))
            {
                var method = obj["method"];
                var parameters = obj["params"];
                if (!validId || method?.Type != JTokenType.String || (parameters != null && parameters.Type != JTokenType.Object && parameters.Type != JTokenType.Array))
                {
                    await SendErrorAsync(validId ? id : null, InvalidRequest, "Malformed request.", Kind("invalid_request")).ConfigureAwait(false);
                    return;
                }
                // An id of null is a request that cannot be answered by id; treat it like any request with that id.
                Dispatch(new JsonRpcRequest((string)method!, id, parameters));
                return;
            }
            if (obj.ContainsKey("result") || obj.ContainsKey("error"))
            {
                // A response to one of this side's requests; one nobody waits for (late, or unknown) is dropped.
                if (id != null && pending.TryRemove(id.ToString(Formatting.None), out var waiter))
                {
                    if (obj["error"] is JObject error)
                        waiter.TrySetException(new JsonRpcRemoteException(error.Value<int?>("code") ?? InternalError, error.Value<string>("message") ?? "", error["data"]));
                    else
                        waiter.TrySetResult(obj["result"]);
                }
                return;
            }
            await SendErrorAsync(validId ? id : null, InvalidRequest, "Neither a request nor a response.", Kind("invalid_request")).ConfigureAwait(false);
        }

        private void Dispatch(JsonRpcRequest request)
        {
            if (!request.IsNotification && Interlocked.Increment(ref inFlight) > MaxConcurrentRequests)
            {
                Interlocked.Decrement(ref inFlight);
                _ = SendErrorAsync(request.Id, (int)AutomationErrorKind.busy, "Too many requests are running on this connection.", Kind(nameof(AutomationErrorKind.busy)));
                return;
            }
            var task = Task.Run(() => HandleAsync(request));
            running[task] = true;
            _ = task.ContinueWith(t => running.TryRemove(t, out _), TaskScheduler.Default);
        }

        private async Task HandleAsync(JsonRpcRequest request)
        {
            var token = lifetime.Token;
            if (request.IsNotification)
            {
                try { await handler.HandleNotificationAsync(request, token).ConfigureAwait(false); } catch { }
                return;
            }
            try
            {
                JToken? result;
                try
                {
                    result = await handler.HandleRequestAsync(request, token).ConfigureAwait(false);
                }
                catch (AutomationException e)
                {
                    await SendErrorAsync(request.Id, e.Code, e.Message, ErrorData(e)).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return; // the connection is gone; nobody to answer
                }
                catch (Exception)
                {
                    // No exception text or stack reaches the client (section 4).
                    await SendErrorAsync(request.Id, InternalError, "Internal error.", Kind("internal_error")).ConfigureAwait(false);
                    return;
                }
                await SendAsync(new JObject { ["jsonrpc"] = "2.0", ["id"] = request.Id, ["result"] = result ?? JValue.CreateNull() }).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        private static JObject Kind(string kind) => new() { ["kind"] = kind };

        public static JObject ErrorData(AutomationException e)
        {
            var data = Kind(e.Kind.ToString());
            foreach (var pair in e.ErrorData)
                if (pair.Key != "kind") data[pair.Key] = pair.Value == null ? JValue.CreateNull() : JToken.FromObject(pair.Value);
            return data;
        }

        private Task SendErrorAsync(JToken? id, int code, string message, JObject data) =>
            SendAsync(new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject { ["code"] = code, ["message"] = message, ["data"] = data },
            });

        private async Task SendAsync(JObject message)
        {
            if (lifetime.IsCancellationRequested) return;
            try
            {
                await framing.WriteAsync(new UTF8Encoding(false).GetBytes(message.ToString(Formatting.None)), lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is OperationCanceledException)
            {
                Shutdown();
            }
        }

        /// <summary>Sends a request to the peer and waits for its response.</summary>
        public async Task<JToken?> SendRequestAsync(string method, JToken? parameters, CancellationToken cancellationToken = default)
        {
            var id = "s" + Interlocked.Increment(ref nextId);
            var key = new JValue(id).ToString(Formatting.None);
            var waiter = new TaskCompletionSource<JToken?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[key] = waiter;
            using var registration = cancellationToken.Register(() => { if (pending.TryRemove(key, out var w)) w.TrySetCanceled(); });
            var message = new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
            if (parameters != null) message["params"] = parameters;
            await SendAsync(message).ConfigureAwait(false);
            if (lifetime.IsCancellationRequested && pending.TryRemove(key, out _)) throw new IOException("The connection is closed.");
            return await waiter.Task.ConfigureAwait(false);
        }

        public Task NotifyAsync(string method, JToken? parameters)
        {
            var message = new JObject { ["jsonrpc"] = "2.0", ["method"] = method };
            if (parameters != null) message["params"] = parameters;
            return SendAsync(message);
        }

        private void Shutdown()
        {
            if (!lifetime.IsCancellationRequested)
            {
                try { lifetime.Cancel(); } catch (ObjectDisposedException) { }
            }
            foreach (var key in new List<string>(pending.Keys))
                if (pending.TryRemove(key, out var waiter)) waiter.TrySetException(new IOException("The connection is closed."));
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
