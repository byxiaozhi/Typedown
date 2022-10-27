using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Typedown.Universal.Interfaces;
using Typedown.Universal.Utilities;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Typedown.Universal.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        public IList<string> History { get => JObject.Parse(GetSettingValue("[]")).ToObject<List<string>>(); set => SetSettingValue(JsonConvert.SerializeObject(value)); }
        public bool IsSideBarOpen { get => GetSettingValue(false); set => SetSettingValue(value); }
        public bool IsStatusBarOpen { get => GetSettingValue(true); set => SetSettingValue(value); }
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
        public string AppTheme { get => GetSettingValue("Default"); set => SetSettingValue(value); }
        public string WorkFolder { get => GetSettingValue(""); set => SetSettingValue(value); }
        public string Language { get => GetSettingValue(""); set => SetSettingValue(value); }
        public int WordCountMethod { get => GetSettingValue(0); set => SetSettingValue(value); }
        public bool KeepRun { get => GetSettingValue(false); set => SetSettingValue(value); }

        public IMarkdownEditor Transport => ServiceProvider.GetService<IMarkdownEditor>();

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

        public T GetSettingValue<T>(T defaultValue = default, [CallerMemberName] string propertyName = null)
        {
            if (Store[propertyName] is object obj) return (T)obj;
            else return defaultValue;
        }

        public void SetSettingValue<T>(T value, [CallerMemberName] string propertyName = null)
        {
            Store[propertyName] = value;
            if (notifySet.Contains(propertyName))
            {
                Transport.PostMessage("SettingsChanged", new
                {
                    name = string.Concat(propertyName[0].ToString().ToLower(), propertyName.Substring(1)),
                    value
                });
            }
        }
    }
}
