using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Typedown.WinUI.Utilities
{
    // Reimplemented, not ported: the original Typedown.Core\Utilities\Locale.cs reads strings via
    // UWP's compiled PRI resource system (Windows.ApplicationModel.Resources.Core.ResourceManager),
    // which is tied to app package identity and doesn't carry over cleanly to an unpackaged WinUI 3
    // app. This reads the same .resw files directly as plain XML. Same public GetString/GetDialogString
    // surface as the original.
    //
    // Caret ships English, French, Spanish and Polish (Strings\en, Strings\fr, Strings\es). English is always
    // loaded underneath the chosen language, so a string that isn't translated yet shows in English
    // instead of disappearing. The language is chosen once per process (App startup, from the Language
    // setting) before any window is created, because XAML resolves its {u:Loc} strings as it loads.
    public static class Locale
    {
        public enum ResourceSource
        {
            All,
            AppResources,
            CommonResources,
            DialogResources,
            SettingsResources,
            Resources
        }

        // The languages Caret has translations for, in the order the Language setting lists them.
        public static IReadOnlyList<string> SupportedLanguages { get; } = new[] { "en", "fr", "es", "pl" };

        private const string FallbackLang = "en";

        private static readonly Dictionary<ResourceSource, Dictionary<string, string>> resources = new();
        private static readonly Dictionary<ResourceSource, Dictionary<string, string>> fallbackResources = new();

        public static string CurrentLang { get; private set; } = FallbackLang;

        static Locale()
        {
            Load("default");
        }

        // "default" (or empty) follows the Windows display language; anything Caret has no
        // translation for falls back to English.
        public static void Load(string lang)
        {
            if (string.IsNullOrEmpty(lang) || lang == "default")
                lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            lang = lang.ToLowerInvariant();
            if (!SupportedLanguages.Contains(lang)) lang = FallbackLang;
            CurrentLang = lang;

            var stringsRoot = Path.Combine(AppContext.BaseDirectory, "Strings");
            LoadInto(fallbackResources, Path.Combine(stringsRoot, FallbackLang));
            LoadInto(resources, Path.Combine(stringsRoot, lang));
            // Dates, numbers and sorting in the app's own UI follow the same language.
            try { CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = new CultureInfo(lang); } catch { }
        }

        private static void LoadInto(Dictionary<ResourceSource, Dictionary<string, string>> target, string langRoot)
        {
            target.Clear();
            foreach (ResourceSource source in Enum.GetValues(typeof(ResourceSource)))
            {
                if (source == ResourceSource.All) continue;
                var path = Path.Combine(langRoot, $"{source}.resw");
                target[source] = File.Exists(path) ? ParseResw(path) : new Dictionary<string, string>();
            }
        }

        private static Dictionary<string, string> ParseResw(string path)
        {
            var doc = XDocument.Load(path);
            return doc.Root
                .Elements("data")
                .Select(x => new { Name = x.Attribute("name")?.Value, Value = x.Element("value")?.Value })
                .Where(x => x.Name != null && x.Value != null)
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.First().Value);
        }

        public static string GetString(string key, ResourceSource source = ResourceSource.All)
        {
            key = key.Replace('.', '/');
            return Find(resources, key, source) ?? Find(fallbackResources, key, source);
        }

        // Shorthand for code: the string for `key`, with {0}, {1}... filled in.
        public static string Format(string key, params object[] args)
        {
            var text = GetString(key) ?? key;
            try { return string.Format(text, args); } catch (FormatException) { return text; }
        }

        private static string Find(Dictionary<ResourceSource, Dictionary<string, string>> from, string key, ResourceSource source)
        {
            if (source == ResourceSource.All)
                return from.Values
                    .Select(dict => dict.TryGetValue(key, out var value) ? value : null)
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x));
            return from.TryGetValue(source, out var d) && d.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
        }

        public static string GetDialogString(string key) => GetString(key, ResourceSource.DialogResources);
    }

    // Ported verbatim from Typedown.Core\Utilities\Locale.cs — pure reflection/attribute code, no UWP dependency.
    public class LocaleAttribute : Attribute
    {
        public string[] Keys { get; }

        public string Text => Texts.FirstOrDefault();

        public IEnumerable<string> Texts => Keys.Select(x => Locale.GetString(x));

        public LocaleAttribute(params string[] keys)
        {
            Keys = keys;
        }
    }
}
