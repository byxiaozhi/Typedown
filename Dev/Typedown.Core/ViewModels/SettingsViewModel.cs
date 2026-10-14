using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Typedown.Core.Controls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Windows.UI.Xaml.Controls;

namespace Typedown.Core.ViewModels
{
    public sealed partial class SettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        public PInvoke.WINDOWPLACEMENT? StartupPlacement { get => GetSettingValue<PInvoke.WINDOWPLACEMENT?>(null); set => SetSettingValue(value); }
        public bool SidePaneOpen { get => GetSettingValue(false); set => SetSettingValue(value); }
        public double SidePaneWidth { get => GetSettingValue(300d); set => SetSettingValue(value); }
        public bool StatusBarOpen { get => GetSettingValue(true); set => SetSettingValue(value); }
        public double FindReplaceDialogWidth { get => GetSettingValue(600d); set => SetSettingValue(value); }
        /// <summary>
        /// Source mode shows the raw Markdown, reading mode shows only the rendered document: the two contradict
        /// each other, so turning one on turns the other off (see also <see cref="ReadOnly"/>).
        /// </summary>
        public bool SourceCode { get => GetSettingValue(false); set { SetSettingValue(value); if (value && ReadOnly) ReadOnly = false; } }
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
        /// <summary>Spaces after the list marker for nested items: "1".."4", or "dfm" (4-space indentation). Muya default is 1.</summary>
        public string ListIndentation { get => GetSettingValue("1"); set => SetSettingValue(value); }
        /// <summary>Pad table cells so columns line up in the source (upstream #24/#70 ask for a way to turn this off).</summary>
        public bool TableAlignColumns { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>Show the paragraph type marker ("P", "H1"...) left of the active block (upstream #45).</summary>
        public bool ShowParagraphMarker { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>
        /// Reading mode: only the rendered document, no caret and no edits (upstream #38). Mutually exclusive
        /// with <see cref="SourceCode"/>; focus and typewriter mode follow the caret and are ignored while it
        /// is on (the menu greys them out).
        /// </summary>
        public bool ReadOnly { get => GetSettingValue(false); set { SetSettingValue(value); if (value && SourceCode) SourceCode = false; } }
        /// <summary>Outline: expand collapsed sections automatically to reveal the current heading (upstream #35).</summary>
        public bool TocAutoExpand { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>Reopen documents at the last caret position (upstream #50).</summary>
        public bool RememberCursorPosition { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>User CSS applied to the editor and to HTML/PDF export (upstream #30).</summary>
        public string CustomCss { get => GetSettingValue(""); set => SetSettingValue(value); }

        /// <summary>
        /// File name (without .css) of a theme in the themes folder, empty for none. The theme's CSS is applied
        /// to the editor after the built-in theme it builds on; see docs/custom-theme.md.
        /// </summary>
        public string CustomTheme { get => GetSettingValue(""); set => SetSettingValue(value); }

        /// <summary>True while a theme is being applied, so a handler can tell it from a user's own change.</summary>
        public bool ApplyingTheme { get; private set; }

        /// <summary>
        /// Picks a built-in theme: the custom theme goes with it. Themes are one choice, not two — a custom
        /// theme that stayed on while a built-in one was picked would paint over it and the two would fight.
        /// </summary>
        public void ApplyBuiltInTheme(AppTheme theme)
        {
            ApplyingTheme = true;
            try
            {
                CustomTheme = string.Empty;
                AppTheme = theme;
            }
            finally
            {
                ApplyingTheme = false;
            }
        }

        /// <summary>
        /// Picks a custom theme, which also sets the built-in theme it says it builds on: that one decides
        /// light or dark for the window and the editor, and the custom CSS is applied on top of it.
        /// </summary>
        public void ApplyCustomTheme(Utilities.CustomTheme theme)
        {
            if (theme == null) return;
            ApplyingTheme = true;
            try
            {
                AppTheme = theme.Base;
                CustomTheme = theme.Id;
            }
            finally
            {
                ApplyingTheme = false;
            }
        }

        /// <summary>
        /// The theme actually in force. A custom theme says which built-in theme it builds on, and that is what
        /// decides light or dark for the window and the editor — a dark theme picked while the app is set to
        /// light would otherwise paint dark colours over a light editor, and the two fight.
        /// </summary>
        public AppTheme EffectiveTheme => Utilities.ThemeFiles.Find(CustomTheme)?.Base ?? AppTheme;

        // Share to HedgeDoc (1.x): server, optional email login (password stored DPAPI-protected), publish read-only link
        public string HedgeDocServer { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string HedgeDocEmail { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string HedgeDocPassword { get => Secret.Unprotect(GetSettingValue("")); set => SetSettingValue(Secret.Protect(value)); }
        public bool HedgeDocPublishReadOnly { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>Show the document tab strip even with a single tab.</summary>
        public bool AlwaysShowTabBar { get => GetSettingValue(false); set => SetSettingValue(value); }
        /// <summary>Files opened from Explorer / the command line go into a tab of the current window instead of a new window.</summary>
        public bool OpenFilesInNewTab { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AutoPairMarkdownSyntax { get => GetSettingValue(true); set => SetSettingValue(value); }

        /// <summary>
        /// PlantUML blocks are drawn by plantuml.com from their source, so the text leaves the machine: off until the
        /// person turns it on. Off, a block shows a line saying so instead of the diagram.
        /// </summary>
        public bool RenderPlantUml { get => GetSettingValue(false); set => SetSettingValue(value); }
        /// <summary>The plantuml-server that draws PlantUML blocks; empty (or not an http(s) address): plantuml.com.</summary>
        public string PlantUmlServer { get => GetSettingValue(""); set => SetSettingValue(value ?? ""); }
        /// <summary>Vim in source mode, Vim-style moving around in reading mode (Utilities/VimKeys).</summary>
        public bool VimMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public string EditorAreaWidth { get => GetSettingValue("1200px"); set => SetSettingValue(value); }
        public string FontFamily { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string TextDirection { get => GetSettingValue("auto"); set => SetSettingValue(value); }
        public bool AutoSave { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AutoReload { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AskBeforeReload { get => GetSettingValue(true); set => SetSettingValue(value); }
        public AppTheme AppTheme { get => GetSettingValue(AppTheme.Default); set => SetSettingValue(value); }
        public string Language { get => GetSettingValue("default"); set => SetSettingValue(value); }
        public int WordCountMethod { get => GetSettingValue(0); set => SetSettingValue(value); }
        public int TabSize { get => GetSettingValue(4); set => SetSettingValue(value); }
        public bool SpellcheckEnabled { get => GetSettingValue(false); set => SetSettingValue(value); }
        public string SpellcheckLang { get => GetSettingValue(""); set => SetSettingValue(value); }
        /// <summary>
        /// Settings > General > Keep running: the process stays after the last window closes, for a faster next start.
        /// Off unless chosen, in every build (the Store edition had it on by default): a closed app is gone, holds no
        /// memory and takes its Store update at once. Who set it keeps what they set.
        /// </summary>
        public bool KeepRun { get => GetSettingValue(false); set => SetSettingValue(value); }
        /// <summary>
        /// Lets programs running as this user read and edit open documents through the local automation endpoint
        /// (docs/automation-api-spec.md). Off by default; turning it off closes the endpoint and every connection.
        /// </summary>
        public bool AllowLocalAutomation { get => GetSettingValue(false); set => SetSettingValue(value); }
        /// <summary>Editor pages briefly highlight what an automation write changed.</summary>
        public bool HighlightAutomationChanges { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AnimationEnable { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool UseMicaEffect { get => GetSettingValue(Config.IsMicaSupported); set => SetSettingValue(value); }
        public bool UseEditorMicaEffect { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool Topmost { get => GetSettingValue(false); set => SetSettingValue(value); }
        public FileStartupAction FileStartupAction { get => GetSettingValue(FileStartupAction.None); set => SetSettingValue(value); }
        public FolderStartupAction FolderStartupAction { get => GetSettingValue(FolderStartupAction.OpenLast); set => SetSettingValue(value); }
        public string StartupOpenFolder { get => GetSettingValue(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); set => SetSettingValue(value); }
        public bool AppCompactMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public InsertImageAction InsertClipboardImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertClipboardImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertClipboardImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public InsertImageAction InsertLocalImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertLocalImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertLocalImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        public InsertImageAction InsertWebImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertWebImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public int? InsertWebImageUseUploadConfigId { get => GetSettingValue<int?>(null); set => SetSettingValue(value); }
        /// <summary>
        /// The upload configuration every upload uses (inserted images when their action is Upload, File > Upload local
        /// images); stored 0 is "none". Until it is chosen, the one an inserted image used before (the three per-source
        /// settings it replaces).
        /// </summary>
        public int? DefaultImageUploadConfigId { get => NoneIsNull(GetSettingValue(InsertClipboardImageUseUploadConfigId ?? InsertLocalImageUseUploadConfigId ?? InsertWebImageUseUploadConfigId)); set => SetSettingValue<int?>(value ?? 0); }
        private static int? NoneIsNull(int? id) => id == 0 ? null : id;
        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();
        public string DefaultImageBasePath { get => GetSettingValue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Config.AppName)); set => SetSettingValue(value); }
        public bool AutoCopyRelativePathImage { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool PreferRelativeImagePaths { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AddSymbolBeforeRelativePath { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AutoEncodeImageURL { get => GetSettingValue(true); set => SetSettingValue(value); }
        /// <summary>The switch After export replaced: still read, so whoever had it on keeps the folder opening.</summary>
        public bool OpenFolderAfterExport { get => GetSettingValue(false); set => SetSettingValue(value); }
        /// <summary>
        /// Settings > Export > After export: nothing, a notice with the file's place (the default: an export said nothing
        /// at all once it was written), the file opened, or its folder.
        /// </summary>
        public ExportAfterAction AfterExport { get => GetSettingValue(OpenFolderAfterExport ? ExportAfterAction.OpenFolder : ExportAfterAction.Notify); set => SetSettingValue(value); }
        public bool FileExportDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool ImageUploadDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public IServiceProvider ServiceProvider { get; }

        public Command<Unit> ResetSettingsCommand { get; } = new();

        private readonly CompositeDisposable disposables = new();

        private readonly JsonSettingsStore settingsStore;

        private readonly HashSet<string> notifySet = new()
        {
            "SourceCode",
            "Typewriter",
            "FocusMode",
            "SearchIsCaseSensitive",
            "SearchIsRegexp",
            "SearchIsWholeWord",
            "FontSize",
            "TabSize",
            "LineHeight",
            "AutoPairBracket",
            "AutoPairQuote",
            "TrimUnnecessaryCodeBlockEmptyLines",
            "PreferLooseListItem",
            "ListIndentation",
            "TableAlignColumns",
            "ShowParagraphMarker",
            "ReadOnly",
            "CustomCss",
            "SpellcheckEnabled",
            "AutoPairMarkdownSyntax",
            "RenderPlantUml",
            "PlantUmlServer",
            "VimMode",
            "EditorAreaWidth",
            "FontFamily",
            "TextDirection",
            "HighlightAutomationChanges"
        };

        public SettingsViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            var settingsFile = Path.Combine(Config.GetLocalFolderPath(), "Settings.json");
            settingsStore = JsonSettingsStore.Shared(settingsFile, onWriteError: ex => Utilities.Log.WriteLocal("SettingsSave", ex.ToString()));
            if (!loggedLoad)
            {
                // What the file held at start: with the line each change writes (SetSettingValue), a value missing on
                // the next run shows when it went.
                loggedLoad = true;
                Utilities.Log.Debug(settingsStore.LoadError != null
                    ? $"settings: Settings.json could not be read: {settingsStore.LoadError.GetType().Name}: {settingsStore.LoadError.Message}"
                    : $"settings: read {settingsStore.LoadedNames.Count}: {string.Join(", ", settingsStore.LoadedNames)}");
            }
            // Created with its window, on that window's thread: other windows' changes are applied there.
            windowContext = System.Threading.SynchronizationContext.Current;
            // Window-local settings (modes, layout) are this window's own from now on: it starts from what was saved
            // last, and another window changing them only changes what the next new window starts with.
            foreach (var name in SettingsScope.WindowLocalNames)
                windowLocalValues[name] = settingsStore.GetToken(name);
            settingsStore.Changed += OnStoreChanged;
            ResetSettingsCommand.OnExecute.Subscribe(_ => ResetSetting());
        }

        private static bool loggedLoad;

        private readonly System.Threading.SynchronizationContext windowContext;

        private readonly Dictionary<string, Newtonsoft.Json.Linq.JToken> windowLocalValues = new();

        /// <summary>The settings revision: advances with every change from any window.</summary>
        public long SettingsRevision => settingsStore.Revision;

        // Another window (or, later, the automation API) changed a setting: this window applies it as if it had
        // been changed here - bindings, the editor page, theme and language handlers all hear of it.
        private void OnStoreChanged(string name, object origin)
        {
            if (ReferenceEquals(origin, this) || disposed) return;
            if (SettingsScope.IsWindowLocal(name)) return;
            void Apply()
            {
                if (disposed) return;
                if (name != null)
                {
                    RaiseChangedFromElsewhere(name);
                    return;
                }
                foreach (var property in GetType().GetProperties().Where(x => x.GetSetMethod() != null && !SettingsScope.IsWindowLocal(x.Name)))
                    RaiseChangedFromElsewhere(property.Name);
            }
            if (windowContext == null || windowContext == System.Threading.SynchronizationContext.Current) Apply();
            else windowContext.Post(_ => Apply(), null);
        }

        private void RaiseChangedFromElsewhere(string name)
        {
            var property = GetType().GetProperty(name);
            if (property == null || property.GetSetMethod() == null) return;
            object value;
            try { value = property.GetValue(this); }
            catch { return; }
            OnPropertyChanged(name, null, value);
        }

        private bool disposed;

        public T GetSettingValue<T>(T defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            if (windowLocalValues.TryGetValue(propertyName, out var own))
            {
                if (own == null || own.Type == Newtonsoft.Json.Linq.JTokenType.Null) return defaultValue;
                try { return own.ToObject<T>() is T value ? value : defaultValue; }
                catch { return defaultValue; }
            }
            return settingsStore.Get(propertyName, defaultValue);
        }

        public void SetSettingValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            // Which preference changed, to what: a choice that was gone after an update (the startup action) left no
            // trace of when it was made or lost. Window state that changes all the time is left out.
            if (!SettingsScope.IsWindowLocal(propertyName) && !Equals(GetSettingValue<T>(default, propertyName), value))
                // The value only for choices, switches and numbers: a text setting can be a token or a password.
                Utilities.Log.Debug($"setting: {propertyName} = {(value is Enum || value is bool || value is int || value is double ? value : "(changed)")}");
            if (windowLocalValues.ContainsKey(propertyName))
                windowLocalValues[propertyName] = value == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(value);
            settingsStore.Set(propertyName, value, this);
        }

        public Task FlushSettingsAsync() => settingsStore.FlushAsync();

        public void OnPropertyChanged(string propertyName, object before, object after)
        {
            // Notifications run inside the property setter, so anything that throws here (a handler on a control
            // that is no longer loaded, a binding update) escapes into the XAML dispatcher, where nothing catches
            // it and the process goes down without a report.
            try
            {
                PropertyChanged?.Invoke(this, new(propertyName));
            }
            catch (Exception ex)
            {
                Utilities.Log.WriteLocal("SettingsPropertyChanged", $"{propertyName}\n{ex}");
            }
            if (notifySet.Contains(propertyName))
                MarkdownEditor.PostMessage("SettingsChanged", new Dictionary<string, object>() { { propertyName, after } });
        }

        public async void ResetSetting()
        {
            var dialog = AppContentDialog.Create(Locale.GetString("General.RestoreDefault.Title"), Locale.GetDialogString("RestoreSettingsContent"), Locale.GetString("Cancel"), Locale.GetString("Ok"));
            dialog.DefaultButton = ContentDialogButton.Close;
            var result = await dialog.ShowAsync(ServiceProvider.GetService<AppViewModel>().XamlRoot);
            if (result != ContentDialogResult.Primary)
                return;
            settingsStore.Reset(this);
            foreach (var name in SettingsScope.WindowLocalNames)
                windowLocalValues[name] = null;
            foreach (var item in GetType().GetProperties().Where(x => x.GetSetMethod() != null).Select(x => x.Name))
                OnPropertyChanged(item);
        }

        public void Dispose()
        {
            disposed = true;
            settingsStore.Changed -= OnStoreChanged;
            disposables.Dispose();
        }
    }
}
