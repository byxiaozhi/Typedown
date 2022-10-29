using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Typedown.Universal.Enums;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Typedown.Universal.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        public IReadOnlyList<string> History { get => GetSettingValue(new List<string>()); set => SetSettingValue(value); }
        public bool SidePaneOpen { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool StatusBarOpen { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool SourceCode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool Typewriter { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool FocusMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsCaseSensitive { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsRegexp { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool SearchIsWholeWord { get => GetSettingValue(false); set => SetSettingValue(value); }
        public int SideBarIndex { get => GetSettingValue(0); set => SetSettingValue(value); }
        public float FontSize { get => GetSettingValue(16f); set => SetSettingValue(value); }
        public float LineHeight { get => GetSettingValue(1.6f); set => SetSettingValue(value); }
        public bool AutoPairBracket { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AutoPairQuote { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool TrimUnnecessaryCodeBlockEmptyLines { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool PreferLooseListItem { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool AutoPairMarkdownSyntax { get => GetSettingValue(true); set => SetSettingValue(value); }
        public string EditorAreaWidth { get => GetSettingValue("1000px"); set => SetSettingValue(value); }
        public bool AutoSave { get => GetSettingValue(false); set => SetSettingValue(value); }
        public AppTheme AppTheme { get => GetSettingValue(AppTheme.Default); set => SetSettingValue(value); }
        public string WorkFolder { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string Language { get => GetSettingValue("default"); set => SetSettingValue(value); }
        public int WordCountMethod { get => GetSettingValue(0); set => SetSettingValue(value); }
        public int TabSize { get => GetSettingValue(4); set => SetSettingValue(value); }
        public bool SpellcheckEnabled { get => GetSettingValue(false); set => SetSettingValue(value); }
        public string SpellcheckLang { get => GetSettingValue(""); set => SetSettingValue(value); }
        public bool KeepRun { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AnimationEnable { get => GetSettingValue(true); set => SetSettingValue(value); }
        public bool UseMicaEffect { get => GetSettingValue(Config.IsMicaSupported); set => SetSettingValue(value); }
        public bool Topmost { get => GetSettingValue(false); set => SetSettingValue(value); }
        public FileStartupAction FileStartupAction { get => GetSettingValue(FileStartupAction.None); set => SetSettingValue(value); }
        public FolderStartupAction FolderStartupAction { get => GetSettingValue(FolderStartupAction.OpenLast); set => SetSettingValue(value); }
        public string StartupOpenFolder { get => GetSettingValue(KnownFolders.DocumentsLibrary.Path); set => SetSettingValue(value); }
        public bool AppCompactMode { get => GetSettingValue(false); set => SetSettingValue(value); }
        public InsertImageAction InsertLocalImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertLocalImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public InsertImageAction InsertWebImageAction { get => GetSettingValue(InsertImageAction.None); set => SetSettingValue(value); }
        public string InsertWebImageCopyPath { get => GetSettingValue("./images"); set => SetSettingValue(value); }
        public IMarkdownEditor MarkdownEditor => ServiceProvider.GetService<IMarkdownEditor>();
        public bool PreferRelativeImagePaths { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool AutoEncodeImageURL { get => GetSettingValue(true); set => SetSettingValue(value); }
        public ImageUploadMethod ImageUploadMethod { get => GetSettingValue(ImageUploadMethod.None); set => SetSettingValue(value); }
        public string ImageUploadConfig { get => GetSettingValue(""); set => SetSettingValue(value); }

        public IServiceProvider ServiceProvider { get; }

        public SettingsViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

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

        private IPropertySet Store => ApplicationData.Current.LocalSettings.Values;

        private readonly Dictionary<string, object> cache = new();

        public T GetSettingValue<T>(T defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            if (cache.TryGetValue(propertyName, out var obj) && obj is T cacheResult)
                return cacheResult;
            try
            {
                if (Store[propertyName] is string str)
                {
                    var result = JsonConvert.DeserializeObject<T>(str);
                    cache[propertyName] = result;
                    return result;
                }
                else
                {
                    cache[propertyName] = defaultValue;
                    return defaultValue;
                }
            }
            catch
            {
                cache[propertyName] = defaultValue;
                return defaultValue;
            }
        }

        public void SetSettingValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            cache[propertyName] = value;
            Store[propertyName] = JsonConvert.SerializeObject(value);
            OnSettingChanged(propertyName, value);
        }

        public void OnSettingChanged(string propertyName, object newValue)
        {
            if (notifySet.Contains(propertyName))
            {
                MarkdownEditor.PostMessage("SettingsChanged", new
                {
                    name = string.Concat(propertyName[0].ToString().ToLower(), propertyName.Substring(1)),
                    value = newValue
                });
            }
        }
    }
}
