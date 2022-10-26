using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Utilities;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace Typedown.Universal.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
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
            RaisePropertyChanged(propertyName);
        }
    }
}
