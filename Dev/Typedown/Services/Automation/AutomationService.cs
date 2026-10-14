using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Typedown.Automation;
using Typedown.Core;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.Services.Automation
{
    /// <summary>
    /// Local automation for the application (docs/automation-api-spec.md). Off by default: the "Allow local automation"
    /// setting starts the endpoint and turning it off closes the endpoint and every connection. While a client is
    /// connected every window says so in its title, and a write names the client and the document there briefly -
    /// neither can be turned off by a client.
    /// </summary>
    public static class AutomationService
    {
        public const string SettingName = "AllowLocalAutomation";
        private const int NoticeMs = 4000;

        private static AutomationServer server;
        private static string endpointName;
        private static JsonSettingsStore settings;
        private static readonly object gate = new();
        private static Task applying = Task.CompletedTask;

        public static void Initialize()
        {
            if (server != null) return;
            var sid = SecurePipeListener.CurrentUserSid();
            var buildType = Config.IsAutomationTestHost ? BuildTypes.AutomationTestHost : BuildTypes.Application;
            var info = new ServerInfo
            {
                Version = Core.Controls.AboutApp.GetAppVersion(),
                Commit = Config.TestBuild ?? "",
                Platform = "windows",
                BuildType = buildType,
                MaxMessageBytes = 32L * 1024 * 1024,
            };
            var barriers = TestHostHooks.Barriers;
            var host = new WindowsAutomationHost(info.Version, barriers);
            var settingsHost = new WindowsSettingsHost();
            var catalog = SettingsCatalog.Load();
            endpointName = EndpointName(buildType, sid);
            var name = endpointName;
            server = new AutomationServer(
                () => new SecurePipeListener(name, sid),
                () =>
                {
                    var methods = DocumentMethods.AddTo(new MethodTable(buildType), host, info.InstanceId, OnWrite);
                    SettingsMethods.AddTo(methods, settingsHost, catalog);
                    ViewMethods.AddTo(methods, host);
                    TestHostHooks.AddMethods(methods);
                    return new AutomationSession(info, methods);
                },
                info.MaxMessageBytes);
            server.ActivityChanged += OnActivityChanged;
            server.ListenerFailed += e =>
            {
                Log.Debug($"automation: the endpoint could not listen: {e.Message}");
                if (Config.IsAutomationTestHost)
                    try { File.WriteAllText(Path.Combine(Config.AutomationTestRoot, "automation-endpoint-error.txt"), e.ToString()); } catch { }
            };

            settings = JsonSettingsStore.Shared(Path.Combine(Config.GetLocalFolderPath(), "Settings.json"));
            settings.Changed += (setting, _) => { if (setting == null || setting == SettingName) Apply(); };
            Apply();
        }

        /// <summary>The pipe name; the test host's also carries its data root, so each run has its own endpoint.</summary>
        public static string EndpointName(string buildType, string sid) =>
            AutomationEndpoint.PipeName(buildType, sid) + (Config.IsAutomationTestHost ? "." + Config.InstanceName.Substring(Config.InstanceName.LastIndexOf('.') + 1) : "");

        private static void Apply()
        {
            // The test host exists to be driven: its endpoint is always on (and only it has test.* methods).
            var on = Config.IsAutomationTestHost || settings.Get(SettingName, false);
            lock (gate)
                applying = applying.ContinueWith(async _ =>
                {
                    try
                    {
                        if (on && !server.IsRunning)
                        {
                            server.Start();
                            Log.Debug("automation: listening");
                            // The test host tells its driver where it listens (the name carries the data root).
                            if (Config.IsAutomationTestHost)
                                File.WriteAllText(Path.Combine(Config.AutomationTestRoot, "automation-endpoint.txt"), endpointName);
                        }
                        else if (!on && server.IsRunning) { await server.StopAsync(); Log.Debug("automation: stopped"); }
                    }
                    catch (Exception e) { Log.Debug($"automation: {e.Message}"); }
                }, TaskScheduler.Default).Unwrap();
        }

        public static Task ShutdownAsync() => server?.StopAsync() ?? Task.CompletedTask;

        private static void OnActivityChanged(AutomationActivity activity)
        {
            var connected = activity.ConnectionCount > 0;
            foreach (var window in AutomationWindows.Registry.Snapshot())
                _ = AutomationWindows.Registry.OnWindowAsync(window.WindowId, app => app.UIViewModel.AutomationConnected = connected)
                    .ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static async void OnWrite(string client, string documentId)
        {
            try
            {
                var found = await AutomationWindows.FindDocumentAsync(documentId);
                if (found == null) return;
                var (window, tab) = found.Value;
                await AutomationWindows.Registry.OnWindowAsync(window.WindowId, app =>
                {
                    var notice = string.Format(Locale.GetString("AutomationWrote"), client, tab.Title);
                    app.UIViewModel.AutomationNotice = notice;
                    _ = Task.Delay(NoticeMs).ContinueWith(_ =>
                        AutomationWindows.Registry.OnWindowAsync(window.WindowId, a => { if (a.UIViewModel.AutomationNotice == notice) a.UIViewModel.AutomationNotice = null; return true; }),
                        TaskScheduler.Default);
                    return true;
                });
            }
            catch (Exception e)
            {
                Log.Debug($"automation: write notice: {e.Message}");
            }
        }
    }
}
