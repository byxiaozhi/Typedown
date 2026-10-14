using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel;
using Windows.Storage;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown.Core
{
    public static class Config
    {
        // Build scripts stamp local/CI test builds. Stable release tags deliberately leave this empty.
        public const string TestBuild = "";

        /// <summary>Windows 11 (build 22000+). Uses RtlGetVersion so the compatibility manifest cannot mask the real build.</summary>
        public static int WindowsBuild { get; } = Math.Max(Utilities.PInvoke.GetWindowsBuildNumber(), Environment.OSVersion.Version.Build);

        public static bool IsMicaSupported { get; } = WindowsBuild >= 22000;

        public static IReadOnlyList<string> WebView2Args { get; } = new List<string>()
        {
            "--disable-web-security",
            "--allow-file-access-from-files",
            "--flag-switches-begin",
            "--enable-features=msOverlayScrollbarWinStyle",
            "--flag-switches-end"
        };

        public static JsonSerializerSettings EditorJsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy(true, true)
            },
            MaxDepth = 256
        };

        private static string localFolderPath;

        /// <summary>
        /// Where settings, the database and the remembered state live. Worked out once: an installed build has
        /// no package identity, so asking the platform for its folder throws, and this is called from the
        /// property that names every one of those files — which meant an exception (and a line in the log)
        /// several times a second.
        /// </summary>
        public static string GetLocalFolderPath() => localFolderPath ??= ResolveLocalFolderPath();

        private static string ResolveLocalFolderPath()
        {
            if (IsAutomationTestHost)
                return Directory.CreateDirectory(Path.Combine(AutomationTestRoot, "data")).FullName;
            if (IsPackaged)
            {
                try { return ApplicationData.Current.LocalFolder.Path; }
                catch (Exception) { /* fall through to the folder an installed build uses */ }
            }
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string AppName => Typedown.Automation.Brand.Name;

        /// <summary>
        /// True in the automation test host build (docs/automation-api-analysis-plan.md, 10.7): everything it keeps -
        /// settings, session, backups, logs, WebView2 data - lives under <see cref="AutomationTestRoot"/>, and its
        /// single-instance names differ, so it never reads or writes the everyday Typedown's data.
        /// </summary>
        public static bool IsAutomationTestHost { get; private set; }

        public static string AutomationTestRoot { get; private set; }

        public const string AutomationTestRootArgument = "--automation-test-root";

        /// <summary>Called first thing in Main by the test host build, before anything names a file.</summary>
        public static void UseAutomationTestHost(string root)
        {
            IsAutomationTestHost = true;
            AutomationTestRoot = Directory.CreateDirectory(root).FullName;
            localFolderPath = null;
        }

        /// <summary>
        /// Base of the single-instance mutex and hand-over pipe names: the application's own names, or for the test host
        /// names tied to its data root, so two test runs and the everyday instance never answer for each other.
        /// </summary>
        public static string InstanceName => IsAutomationTestHost ? AppName + ".AutomationTestHost." + StableHash(AutomationTestRoot.ToLowerInvariant()) : AppName + ".App";

        // Not string.GetHashCode: that is randomized per process, so a restarted test host got other instance names
        // than the one before it on the same root - another mutex, another endpoint.
        private static string StableHash(string text)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hash, 0, 6).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Folder for logs and browser data beside the settings folder.</summary>
        public static string LocalAppDataFolder => IsAutomationTestHost
            ? AutomationTestRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

        /// <summary>The command line without the test host's own arguments, which are not files to open.</summary>
        public static string[] StripHostArguments(string[] args)
        {
            var list = new System.Collections.Generic.List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == AutomationTestRootArgument) { i++; continue; }
                list.Add(args[i]);
            }
            return list.ToArray();
        }

        public static bool IsPackaged { get; private set; }

        static Config()
        {
            try
            {
                IsPackaged = Package.Current != null;
            }
            catch
            {
                IsPackaged = false;
            }
        }
    }
}
