using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using Typedown.Core.Controls;
using Typedown.Core.Enums;
using Typedown.Core.Interfaces;
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
        public bool SourceCode { get => GetSettingValue(false); set => SetSettingValue(value); }
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
        public AppTheme AppTheme { get => GetSettingValue(AppTheme.Default); set => SetSettingValue(value); }
        public string Language { get => GetSettingValue("default"); set => SetSettingValue(value); }
        public int WordCountMethod { get => GetSettingValue(0); set => SetSettingValue(value); }
        public int TabSize { get => GetSettingValue(4); set => SetSettingValue(value); }
        public bool SpellcheckEnabled { get => GetSettingValue(false); set => SetSettingValue(value); }
        public string SpellcheckLang { get => GetSettingValue(""); set => SetSettingValue(value); }
        public bool KeepRun { get => GetSettingValue(Config.IsPackaged); set => SetSettingValue(value); }
        public bool AnimationEnable { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool UseMicaEffect { get => GetSettingValue(Config.IsMicaSupported); set => SetSettingValue(value); }
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
        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();
        public string DefaultImageBasePath { get => GetSettingValue(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Config.AppName)); set => SetSettingValue(value); }
        public bool AutoCopyRelativePathImage { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool PreferRelativeImagePaths { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AddSymbolBeforeRelativePath { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AutoEncodeImageURL { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool OpenFolderAfterExport { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool FileExportDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool ImageUploadDatabaseInitialized { get => GetSettingValue(false); set => SetSettingValue(value); }
        public IServiceProvider ServiceProvider { get; }

        public Command<Unit> ResetSettingsCommand { get; } = new();

        private readonly CompositeDisposable disposables = new();

        private readonly string settingsFile = Path.Combine(Config.GetLocalFolderPath(), "Settings.json");

        private JToken store;

        private readonly HashSet<string> notifySet = new()
        {
            "SourceCode",
            "Typewriter",
            "FocusMode",
            "SearchIsCaseSensitive",
            "SearchIsRegexp",
            "SearchIsWholeWord",
            "FontSize",
            "LineHeight",
            "AutoPairBracket",
            "AutoPairQuote",
            "TrimUnnecessaryCodeBlockEmptyLines",
            "PreferLooseListItem",
            "AutoPairMarkdownSyntax",
            "EditorAreaWidth"
        };

        public SettingsViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            ResetSettingsCommand.OnExecute.Subscribe(_ => ResetSetting());
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
        }

        private async void SaveAllSettings()
        {
            await File.WriteAllTextAsync(settingsFile, store.ToString());
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
            PropertyChanged?.Invoke(this, new(propertyName));
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
            store = new JObject();
            SaveAllSettings();
            foreach (var item in GetType().GetProperties().Where(x => x.GetSetMethod() != null).Select(x => x.Name))
                OnPropertyChanged(item);
        }

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
