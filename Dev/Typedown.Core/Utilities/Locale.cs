using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Typedown.Core.Utilities
{
    /// <summary>
    /// Cross-platform locale attribute for enum and type localization.
    /// Resource loading will be implemented per-platform via ILocalizationService.
    /// </summary>
    public class LocaleAttribute : Attribute
    {
        public string[] Keys { get; }

        public string Text => Keys.FirstOrDefault() ?? "";

        public IEnumerable<string> Texts => Keys;

        public LocaleAttribute(params string[] keys)
        {
            Keys = keys;
        }
    }

    /// <summary>
    /// Cross-platform locale utility. Resource loading is stubbed for now.
    /// Will be connected to platform-specific resource system later.
    /// </summary>
    public static class Locale
    {
        public static string GetString(string key, int source = 0)
        {
            // TODO: Connect to platform-specific resource system (Phase 3)
            return key;
        }

        public static string GetDialogString(string key) => GetString(key);

        public static string GetTypeString(Type type)
        {
            return (type.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute)?.Text ?? type.Name;
        }

        public static Dictionary<string, string> SupportedLangs { get; } = new()
        {
            {"en","English"},
            {"zh-Hans","中文 (简体)"},
            {"zh-Hant","繁體中文 (繁體)"},
            {"de","Deutsch"},
            {"fr","Français"},
            {"ja","日本語"},
            {"ko","한국어"},
            {"es","Español"},
            {"ru","Русский"},
        };
    }
}
