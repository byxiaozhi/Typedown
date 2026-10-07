using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI;

namespace Typedown.WinUI
{
    // Ported from Typedown.Core\Config.cs. The System.Runtime.CompilerServices.IsExternalInit
    // polyfill the original needed for netcoreapp3.1/UAP is dropped — net8.0 already has it,
    // and redefining it here would collide.
    public static class Config
    {
        public static bool IsMicaSupported { get; } = Environment.OSVersion.Version.Build >= 22000;

        // Single source of truth for the handful of places that need the brand palette as a raw
        // Windows.UI.Color rather than a XAML brush — WebView2.DefaultBackgroundColor (no
        // {ThemeResource} binding support) and the theme payload pushed to the web editor
        // (BuildThemePayload, in MainWindow.xaml.cs). Matches Themes/Caret.xaml's
        // CaretBackgroundColor/CaretPrimaryColor/CaretSecondaryColor exactly — keep them in sync if
        // the design tokens ever change.
        public static Color BrandLightBackground { get; } = Color.FromArgb(0xFF, 0xF8, 0xEB, 0xDD);
        public static Color BrandDarkBackground { get; } = Color.FromArgb(0xFF, 0x0E, 0x12, 0x20);
        public static Color BrandLightAccent { get; } = Color.FromArgb(0xFF, 0xA5, 0x52, 0x2A);
        // The dark accent the native controls use too (Themes/Caret.xaml): #8F4A22 measured 2.82 : 1 on the page.
        public static Color BrandDarkAccent { get; } = Color.FromArgb(0xFF, 0xB5, 0x5E, 0x2A);

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

        public static string GetLocalFolderPath()
        {
            try
            {
                return ApplicationData.Current.LocalFolder.Path;
            }
            catch (Exception)
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string AppName => "Caret";

        public static bool IsPackaged { get; private set; }

        // Ported from Typedown.Core\Controls\AboutApp.xaml.cs's GetAppVersion() for the About settings
        // section — packaged builds read the MSIX identity's version, unpackaged builds fall back to
        // the assembly version and say so, since there's no package identity to ask.
        public static string AppVersion { get; private set; }

        // The same version as a comparable value, for the update check (Services/UpdateService.cs).
        public static Version AppVersionNumber { get; private set; }

        // Installed from the Microsoft Store (the Store signs its packages). The Store delivers updates
        // and its policies don't allow an app to download code on its own, so a Store install skips the
        // GitHub update check and never runs pip for MarkItDown.
        public static bool IsStoreInstall { get; private set; }

        // Organisations can switch features off for everyone through Group Policy or an Intune
        // registry setting: DWORD values under HKLM (or HKCU) \SOFTWARE\Policies\Caret.
        //   DisableUpdateCheck = 1        → no GitHub update check, and the setting is hidden
        //   DisableMarkItDownInstall = 1  → Caret never runs pip to install MarkItDown
        // See docs/deployment.md.
        public static bool PolicyDisablesUpdateCheck { get; } = ReadPolicy("DisableUpdateCheck");
        public static bool PolicyDisablesMarkItDownInstall { get; } = ReadPolicy("DisableMarkItDownInstall");
        // No "Convert to Markdown" in File Explorer's right-click menu, whatever the user chose.
        public static bool PolicyDisablesExplorerMenu { get; } = ReadPolicy("DisableExplorerMenu");

        // Whether Caret may check GitHub for new releases at all: not in the Store (the Store updates
        // it), not when an administrator turned it off, and not in an unpackaged dev build.
        public static bool UpdateCheckAvailable => IsPackaged && !IsStoreInstall && !PolicyDisablesUpdateCheck;

        // They can also choose what everyone starts with: string (REG_SZ) values under the same key.
        // These are defaults, not locks: a user who picks something else in Settings keeps it.
        //   DefaultLayout      = classic | streamlined
        //   DefaultColorScheme = copper | paper | sage | harbour (or harbor) | graphite
        //   DefaultAccentColor = scheme | windows
        //   DefaultTheme       = system | light | dark
        // An unknown value is ignored.
        public static string PolicyDefaultLayout { get; } = ReadPolicyChoice("DefaultLayout", "classic", "streamlined");
        public static string PolicyDefaultColorScheme { get; } = ReadPolicyChoice("DefaultColorScheme", "copper", "paper", "sage", "harbour", "harbor", "graphite")?.Replace("harbor", "harbour");
        public static string PolicyDefaultAccentColor { get; } = ReadPolicyChoice("DefaultAccentColor", "scheme", "windows");
        public static string PolicyDefaultTheme { get; } = ReadPolicyChoice("DefaultTheme", "system", "light", "dark");

        private static string ReadPolicyChoice(string name, params string[] allowed)
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                try
                {
                    using var key = hive.OpenSubKey(@"SOFTWARE\Policies\Caret");
                    var value = (key?.GetValue(name) as string)?.Trim().ToLowerInvariant();
                    if (value != null && Array.IndexOf(allowed, value) >= 0) return value;
                }
                catch { }
            }
            return null;
        }

        private static bool ReadPolicy(string name)
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                try
                {
                    using var key = hive.OpenSubKey(@"SOFTWARE\Policies\Caret");
                    if (key?.GetValue(name) is int value && value != 0) return true;
                }
                catch { }
            }
            return false;
        }

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
            if (IsPackaged)
            {
                var v = Package.Current.Id.Version;
                AppVersionNumber = new Version(v.Major, v.Minor, v.Build, v.Revision);
                try { IsStoreInstall = Package.Current.SignatureKind == PackageSignatureKind.Store; } catch { }
                AppVersion = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
            else
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                AppVersionNumber = v;
                AppVersion = $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision} (Unpackaged)";
            }
        }
    }
}
