using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Typedown.WinUI.Enums;
using Typedown.WinUI.Interfaces;

namespace Typedown.WinUI.ViewModels
{
    // Ported from Typedown.Core\ViewModels\SettingsViewModel.cs. The property list, JSON-file-backed
    // store (GetSettingValue/SetSettingValue/LoadAllSettings/SaveAllSettings), and change notification
    // are all unchanged in shape — this was already framework-agnostic despite living in the UWP
    // project. What's deferred to later milestones:
    //   - ResetSettingsCommand / ResetSetting() (needs AppContentDialog + XamlRoot — milestone #5)
    //   - Full IServiceProvider-based DI (this takes a plain IMarkdownEditor reference instead, since
    //     that's the only service this class actually needs)
    // WindowX/Y/Width/Height/Maximized (below) replace the original's single StartupPlacement.
    // PropertyChanged.Fody (see FodyWeavers.xml) weaves in the INotifyPropertyChanged raises and the
    // OnPropertyChanged(name, before, after) calls automatically, exactly like the original — no
    // change needed to how the properties themselves are declared.
    public sealed partial class SettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        // Reimplemented against WinUI 3's AppWindow (position/size/maximized as plain scalars) rather
        // than a literal port of the original's single StartupPlacement property (a raw Win32
        // WINDOWPLACEMENT struct via PInvoke.GetWindowPlacement/SetWindowPlacement) — see the
        // SetUpWindowPlacement comment in MainWindow.xaml.cs for why. Null X/Y/Width/Height means
        // "never saved yet", so the window falls back to WinUI 3's own default placement.
        public int? WindowX { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public int? WindowY { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public int? WindowWidth { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public int? WindowHeight { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public bool WindowMaximized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SidePaneOpen { get => GetSettingValue(false); set => SetSettingValue(value); }
        public double SidePaneWidth { get => GetSettingValue(300d); set => SetSettingValue(value); }
        public bool StatusBarOpen { get => GetSettingValue(true); set => SetSettingValue(value); }
        public double FindReplaceDialogWidth { get => GetSettingValue(600d); set => SetSettingValue(value); }
        public bool SourceCode { get => GetSettingValue(false); set => SetSettingValue(value); }
        // New since the fork: with SourceCode on, also show a live rendered preview next to the code
        // (the split view; Typedown.Editor/src/components/Preview).
        public bool SplitPreview { get => GetSettingValue(false); set => SetSettingValue(value); }
        // New since the fork: Speech mode (docs/speech-marks-design.md). Off until the user switches it on in the View
        // menu; only then are the delivery marks of a talk read and drawn. Never switched on by opening a file.
        public bool SpeechMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool Typewriter { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool FocusMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsCaseSensitive { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsRegexp { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsWholeWord { get => GetSettingValue(false); set => SetSettingValue(value); }
        public int SidePaneIndex { get => GetSettingValue(0); set => SetSettingValue(value); }
        public double FontSize { get => GetSettingValue(16d); set => SetSettingValue(value); }
        public double LineHeight { get => GetSettingValue(1.6d); set => SetSettingValue(value); }
        public bool AutoPairBracket { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AutoPairQuote { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool TrimUnnecessaryCodeBlockEmptyLines { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool PreferLooseListItem { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AutoPairMarkdownSyntax { get => GetSettingValue(true); set => SetSettingValue(value); }
        public string EditorAreaWidth { get => GetSettingValue("1200px"); set => SetSettingValue(value); }
        public bool AutoSave { get => GetSettingValue(false); set => SetSettingValue(value); }
        // An administrator can set the defaults of AppTheme, ColorScheme, AccentSource and LayoutPreset
        // (Config.PolicyDefault*, docs/deployment.md); a user's own choice still wins.
        public AppTheme AppTheme { get => GetSettingValue(Config.PolicyDefaultTheme switch { "light" => AppTheme.Light, "dark" => AppTheme.Dark, _ => AppTheme.Default }); set => SetSettingValue(value); }
        public string Language { get => GetSettingValue("default"); set => SetSettingValue(value); }
        public int WordCountMethod { get => GetSettingValue(0); set => SetSettingValue(value); }
        public int TabSize { get => GetSettingValue(4); set => SetSettingValue(value); }
        public bool SpellcheckEnabled { get => GetSettingValue(true); set => SetSettingValue(value); }
        public string SpellcheckLang { get => GetSettingValue(""); set => SetSettingValue(value); }
        public bool KeepRun { get => GetSettingValue(Config.IsPackaged); set => SetSettingValue(value); }
        public bool AnimationEnable { get => GetSettingValue(true); set => SetSettingValue(value); }
        // Defaults to off, not Config.IsMicaSupported: Caret's warm-autumn brand background
        // (Themes/Caret.xaml) is a flat illustrated color field, and Mica's system-mixed translucent
        // tint washes it out on a fresh install. The toggle in Settings still turns it back on for
        // anyone who prefers the glass look.
        public bool UseMicaEffect { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool UseEditorMicaEffect { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool Topmost { get => GetSettingValue(false); set => SetSettingValue(value); }
        public FileStartupAction FileStartupAction { get => GetSettingValue(FileStartupAction.None); set => SetSettingValue(value); }
        public FolderStartupAction FolderStartupAction { get => GetSettingValue(FolderStartupAction.OpenLast); set => SetSettingValue(value); }
        public string StartupOpenFolder { get => GetSettingValue(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); set => SetSettingValue(value); }
        // New since the fork: FolderStartupAction had no equivalent to the original's AccessHistory
        // (EF Core, not ported) to read "the last folder" back from — this is that, a single path
        // instead of a full history, since FolderStartupAction.OpenLast only ever needs the most recent
        // one anyway. Updated wherever a folder is actually opened (MainWindow.xaml.cs's OpenFolderTree).
        public string LastOpenedFolder { get => GetSettingValue(""); set => SetSettingValue(value); }
        public bool AppCompactMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        // New since the fork: the daily GitHub Releases check (Services/UpdateService.cs).
        public bool CheckForUpdates { get => GetSettingValue(true); set => SetSettingValue(value); }
        public DateTime? LastUpdateCheck { get => GetSettingValue<DateTime?>(null); set => SetSettingValue(value); }
        public string SkippedUpdateVersion { get => GetSettingValue(""); set => SetSettingValue(value); }
        // New since the fork: the Convert to Markdown page (MainWindow.Convert.cs). Empty folder = save
        // next to the original files.
        public string ConvertOutputFolder { get => GetSettingValue(""); set => SetSettingValue(value); }
        public bool ConvertExtractImages { get => GetSettingValue(true); set => SetSettingValue(value); }
        // New since the fork: document tabs (MainWindow.Tabs.cs). Off = one document per window, as before.
        public bool UseTabs { get => GetSettingValue(true); set => SetSettingValue(value); }
        // Reopen the documents that were open when Caret last closed.
        public bool RestoreTabs { get => GetSettingValue(true); set => SetSettingValue(value); }
        // Those documents' paths, one per line (the settings store keeps scalars, not lists), and
        // which one was in front.
        public string OpenTabs { get => GetSettingValue(""); set => SetSettingValue(value); }
        public int ActiveTab { get => GetSettingValue(0); set => SetSettingValue(value); }
        // Emails (.msg, .eml): replace names, addresses, phone numbers, IBANs and IDs with placeholders.
        public bool ConvertEmailRedact { get => GetSettingValue(true); set => SetSettingValue(value); }
        // "Convert to Markdown" in File Explorer's right-click menu (Services/ExplorerCommandServer.cs).
        public bool ExplorerMenu { get => GetSettingValue(true); set => SetSettingValue(value); }
        // Interface review, phase 2: the colour scheme (Utilities/ColorSchemes.cs), where the accent comes
        // from ("scheme" or "windows"), and the window material ("solid", "mica", "micaalt"; it replaces
        // UseMicaEffect, whose value is its default, and UseEditorMicaEffect: the page stays solid).
        public string ColorScheme { get => GetSettingValue(Config.PolicyDefaultColorScheme ?? "copper"); set => SetSettingValue(value); }
        public string AccentSource { get => GetSettingValue(Config.PolicyDefaultAccentColor ?? "scheme"); set => SetSettingValue(value); }
        public string WindowMaterial { get => GetSettingValue(UseMicaEffect ? "mica" : "solid"); set => SetSettingValue(value); }
        // The sidebar's illustrated card.
        public bool ShowDecorativeCard { get => GetSettingValue(true); set => SetSettingValue(value); }
        // The sidebar's sections that collapse from their headings: the Library (Recent, Favorites...), the
        // folder tree and the outline. Remembered between sessions.
        public bool LibraryCollapsed { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool FolderCollapsed { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool OutlineCollapsed { get => GetSettingValue(false); set => SetSettingValue(value); }
        // Phase 3: "classic", "streamlined" or "distraction". A new install starts Streamlined (set when
        // there's no settings file yet, LoadAllSettings); an existing one keeps Classic, the layout it had.
        // An administrator's default layout applies to both.
        public string LayoutPreset { get => GetSettingValue(Config.PolicyDefaultLayout ?? "classic"); set => SetSettingValue(value); }
        // Where F11 goes back to.
        public string LayoutBeforeDistraction { get => GetSettingValue("streamlined"); set => SetSettingValue(value); }
        public string SidebarPosition { get => GetSettingValue("left"); set => SetSettingValue(value); }
        public bool SidebarRail { get => GetSettingValue(false); set => SetSettingValue(value); }
        // Section colours over the scheme, per theme: "band=#EAD9C4;page=#DCE6EF" (MainWindow.Sections.cs).
        public string SectionColorsLight { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string SectionColorsDark { get => GetSettingValue(""); set => SetSettingValue(value); }
        // Tab colours, by file: "path<TAB>#RRGGBB" per line.
        public string TabColors { get => GetSettingValue(""); set => SetSettingValue(value); }
        public InsertImageAction InsertClipboardImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertClipboardImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertClipboardImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public InsertImageAction InsertLocalImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertLocalImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertLocalImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public InsertImageAction InsertWebImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertWebImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertWebImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public string DefaultImageBasePath { get => GetSettingValue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Config.AppName)); set => SetSettingValue(value); }
        public bool AutoCopyRelativePathImage { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool PreferRelativeImagePaths { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AddSymbolBeforeRelativePath { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AutoEncodeImageURL { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool OpenFolderAfterExport { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool FileExportDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool ImageUploadDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }

        private readonly IMarkdownEditor markdownEditor;

        private readonly CompositeDisposable disposables = new();

        private readonly string settingsFile = Path.Combine(Config.GetLocalFolderPath(), "Settings.json");

        private JToken store;

        private readonly HashSet<string> notifySet = new()
        {
            "SourceCode", "SplitPreview", "SpeechMode", "Typewriter", "FocusMode", "SearchIsCaseSensitive", "SearchIsRegexp",
            "SearchIsWholeWord", "FontSize", "LineHeight", "AutoPairBracket", "AutoPairQuote",
            "TrimUnnecessaryCodeBlockEmptyLines", "PreferLooseListItem", "AutoPairMarkdownSyntax", "EditorAreaWidth"
        };

        public SettingsViewModel(IMarkdownEditor markdownEditor = null)
        {
            this.markdownEditor = markdownEditor;
            LoadAllSettings();
        }

        private void LoadAllSettings()
        {
            try
            {
                store = JToken.Parse(File.ReadAllText(settingsFile));
            }
            catch
            {
                store = new JObject();
            }
            // A new install. Not written when an administrator set a default layout, so a later change to
            // that policy still reaches this user.
            if (!store.HasValues && Config.PolicyDefaultLayout == null) store["LayoutPreset"] = "streamlined";
        }

        private async void SaveAllSettings()
        {
            try
            {
                await File.WriteAllTextAsync(settingsFile, store.ToString());
            }
            catch
            {
                // Ignore
            }
        }

        public T GetSettingValue<T>(T defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            return (T)(store[propertyName]?.ToObject(typeof(T)) ?? defaultValue);
        }

        public void SetSettingValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            if (value is null || value is string || value is long || value is int || value is short || value is sbyte || value is ulong ||
                value is uint || value is ushort || value is byte || value is Enum || value is double || value is float || value is decimal ||
                value is DateTime || value is byte[] || value is bool || value is Guid || value is Uri || value is TimeSpan)
                store[propertyName] = new JValue(value);
            else
                store[propertyName] = JObject.FromObject(value);
            SaveAllSettings();
        }

        public void OnPropertyChanged(string propertyName, object before, object after)
        {
            if (notifySet.Contains(propertyName))
                markdownEditor?.PostMessage("SettingsChanged", new Dictionary<string, object>() { { propertyName, after } });
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
