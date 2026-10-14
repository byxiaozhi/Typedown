using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation
{
    public static class Scopes
    {
        public const string AppRead = "app.read";
        public const string DocumentRead = "document.read";
        public const string DocumentWrite = "document.write";
        public const string DocumentSave = "document.save";
        public const string WindowFocus = "window.focus";
        public const string SettingsRead = "settings.read";
        public const string SettingsWrite = "settings.write";
        /// <summary>Changing how a window shows its document: mode, side pane, status bar, size and position.</summary>
        public const string WindowView = "window.view";

        /// <summary>Every scope name the protocol defines; ones a build does not serve are denied as <c>notAvailable</c>.</summary>
        public static readonly IReadOnlyList<string> Defined = new[] { AppRead, DocumentRead, DocumentWrite, DocumentSave, WindowFocus, SettingsRead, SettingsWrite, WindowView };
    }

    public static class BuildTypes
    {
        public const string Application = "application";
        public const string AutomationTestHost = "automationTestHost";
    }

    /// <summary>What <c>system.initialize</c> reports about this process.</summary>
    public sealed class ServerInfo
    {
        public string Name { get; set; } = Brand.Name;
        public string Version { get; set; } = "";
        public string Commit { get; set; } = "";
        public string Platform { get; set; } = "";
        public string BuildType { get; set; } = BuildTypes.Application;
        public long MaxMessageBytes { get; set; } = 16 * 1024 * 1024;
        /// <summary>Changes with every process start; revisions are read together with it.</summary>
        public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    }

    /// <summary>The client a session belongs to, as initialize declared it. Self-reported: never proof of identity.</summary>
    public sealed class ClientInfo
    {
        public ClientInfo(string id, string name, string? version)
        {
            Id = id;
            Name = name;
            Version = version;
        }

        public string Id { get; }
        public string Name { get; }
        public string? Version { get; }
    }

    public sealed class MethodContext
    {
        internal MethodContext(AutomationSession session, JsonRpcRequest request)
        {
            Session = session;
            Request = request;
        }

        public AutomationSession Session { get; }
        public JsonRpcRequest Request { get; }
        public Params Params => new(Request.Params);
    }

    public sealed class MethodDescriptor
    {
        public MethodDescriptor(string name, string? scope, string schema, Func<MethodContext, CancellationToken, Task<JToken?>> handler)
        {
            Name = name;
            Scope = scope;
            Schema = schema;
            Handler = handler;
        }

        public string Name { get; }
        /// <summary>The scope the caller must hold; null for none.</summary>
        public string? Scope { get; }
        /// <summary>Identifier of the method's params/result schema, e.g. <c>document.get/1</c>.</summary>
        public string Schema { get; }
        public Func<MethodContext, CancellationToken, Task<JToken?>> Handler { get; }
    }

    /// <summary>
    /// The methods a build serves. <c>test.*</c> methods exist only in the automation test host: an application
    /// build refuses to register them, so its method table, capability response and schema cannot contain them and
    /// a call to one is <c>method_not_found</c> like any other unknown method.
    /// </summary>
    public sealed class MethodTable
    {
        private readonly Dictionary<string, MethodDescriptor> methods = new(StringComparer.Ordinal);

        public MethodTable(string buildType)
        {
            if (buildType != BuildTypes.Application && buildType != BuildTypes.AutomationTestHost) throw new ArgumentOutOfRangeException(nameof(buildType));
            BuildType = buildType;
        }

        public string BuildType { get; }

        public MethodTable Add(MethodDescriptor method)
        {
            if (method.Name.StartsWith("test.", StringComparison.Ordinal) && BuildType != BuildTypes.AutomationTestHost)
                throw new InvalidOperationException($"'{method.Name}' is a test method; only the automation test host may serve it.");
            if (method.Name.StartsWith("system.", StringComparison.Ordinal))
                throw new InvalidOperationException("system.* methods belong to the session.");
            if (method.Scope != null && !Scopes.Defined.Contains(method.Scope))
                throw new InvalidOperationException($"Unknown scope '{method.Scope}'.");
            methods.Add(method.Name, method);
            return this;
        }

        public bool TryGet(string name, out MethodDescriptor method) => methods.TryGetValue(name, out method!);

        public IEnumerable<MethodDescriptor> All => methods.Values.OrderBy(m => m.Name, StringComparer.Ordinal);

        /// <summary>The scopes some method of this build needs: the ones initialize can grant.</summary>
        public IReadOnlyCollection<string> ServedScopes => methods.Values.Where(m => m.Scope != null).Select(m => m.Scope!).Distinct().ToList();
    }

    /// <summary>
    /// One connection's protocol state (docs/automation-api-spec.md, sections 1.2 and 1.3): the first request must be
    /// <c>system.initialize</c>; afterwards each method is checked against the scopes granted there.
    /// </summary>
    public sealed class AutomationSession : IJsonRpcHandler
    {
        public const int ApiVersion = 1;

        private readonly ServerInfo server;
        private readonly MethodTable methods;
        private readonly object gate = new();
        private HashSet<string> granted = new(StringComparer.Ordinal);
        private int initializing;

        public AutomationSession(ServerInfo server, MethodTable methods)
        {
            this.server = server;
            this.methods = methods;
            if (server.BuildType != methods.BuildType) throw new ArgumentException("The server and its method table disagree on the build type.");
        }

        public bool IsInitialized { get; private set; }

        /// <summary>Raised once initialize succeeded (the client's name is known from then on).</summary>
        public event Action? Initialized;
        public string? ClientSessionId { get; private set; }
        public ClientInfo? Client { get; private set; }
        public bool ClientAcceptsServerRequests { get; private set; }

        public bool HasScope(string scope)
        {
            lock (gate) return granted.Contains(scope);
        }

        public Task HandleNotificationAsync(JsonRpcRequest notification, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<JToken?> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            if (request.Method == "system.initialize") return Initialize(new Params(request.Params));
            if (!IsInitialized)
                throw new AutomationException(AutomationErrorKind.not_initialized, "The first request must be system.initialize.");
            if (request.Method == "system.ping")
                return new JObject { ["apiVersion"] = ApiVersion, ["instanceId"] = server.InstanceId };
            if (!methods.TryGet(request.Method, out var method))
                throw new AutomationException(AutomationErrorKind.method_not_found, $"No method '{request.Method}'.",
                    new Dictionary<string, object?> { ["method"] = request.Method });
            if (method.Scope != null && !HasScope(method.Scope))
                throw new AutomationException(AutomationErrorKind.scope_required, $"'{method.Name}' needs the '{method.Scope}' scope.",
                    new Dictionary<string, object?> { ["scope"] = method.Scope });
            return await method.Handler(new MethodContext(this, request), cancellationToken).ConfigureAwait(false);
        }

        private JToken Initialize(Params p)
        {
            if (IsInitialized || Interlocked.Exchange(ref initializing, 1) == 1)
                throw Params.Invalid("method", "alreadyInitialized", "The connection is already initialized.");
            try
            {
                var version = p.RequiredInteger("apiVersion", 0, int.MaxValue);
                if (version != ApiVersion)
                    throw new AutomationException(AutomationErrorKind.unsupported_version, $"API version {version} is not supported.",
                        new Dictionary<string, object?> { ["supportedVersions"] = new[] { ApiVersion } });
                var client = p.OptionalObject("client") ?? throw Params.Invalid("client", "required");
                var cp = new Params(client);
                var clientId = cp.RequiredString("id", allowEmpty: false);
                if (!Guid.TryParseExact(clientId, "D", out _)) throw Params.Invalid("client.id", "notAUuid");
                var clientName = cp.RequiredString("name", allowEmpty: false);
                var clientVersion = cp.OptionalString("version");
                var requested = p.OptionalStringArray("requestedScopes") ?? Array.Empty<string>();
                var capabilities = p.OptionalObject("capabilities");
                var serverRequests = capabilities != null && new Params(capabilities).OptionalBoolean("serverRequests", false);

                var served = methods.ServedScopes;
                var grant = new HashSet<string>(StringComparer.Ordinal);
                var denied = new JArray();
                foreach (var scope in requested.Distinct(StringComparer.Ordinal))
                {
                    if (served.Contains(scope)) grant.Add(scope);
                    else denied.Add(new JObject { ["scope"] = scope, ["reason"] = Scopes.Defined.Contains(scope) ? "notAvailable" : "unknown" });
                }

                lock (gate)
                {
                    granted = grant;
                    Client = new ClientInfo(clientId, clientName, clientVersion);
                    ClientSessionId = Guid.NewGuid().ToString("N");
                    // The MVP never sends requests to the client, whatever it declares.
                    ClientAcceptsServerRequests = serverRequests;
                    IsInitialized = true;
                }
                try { Initialized?.Invoke(); } catch { }
                return new JObject
                {
                    ["apiVersion"] = ApiVersion,
                    ["server"] = new JObject
                    {
                        ["name"] = server.Name,
                        ["version"] = server.Version,
                        ["commit"] = server.Commit,
                        ["platform"] = server.Platform,
                        ["buildType"] = server.BuildType,
                    },
                    ["instanceId"] = server.InstanceId,
                    ["clientSessionId"] = ClientSessionId,
                    ["grantedScopes"] = new JArray(grant.OrderBy(s => s, StringComparer.Ordinal)),
                    ["deniedScopes"] = denied,
                    ["maxMessageBytes"] = server.MaxMessageBytes,
                    ["methods"] = new JArray(methods.All.Select(m => new JObject
                    {
                        ["name"] = m.Name,
                        ["scope"] = m.Scope == null ? JValue.CreateNull() : m.Scope,
                        ["schema"] = m.Schema,
                    })),
                    ["capabilities"] = new JObject
                    {
                        ["replaceText"] = methods.TryGet("document.replaceText", out _),
                        ["presentationAcknowledgement"] = true,
                        ["events"] = false,
                        ["offsetEdits"] = false,
                        ["selectionEdits"] = false,
                        ["serverRequests"] = false,
                    },
                };
            }
            catch
            {
                Interlocked.Exchange(ref initializing, 0);
                throw;
            }
        }
    }
}
