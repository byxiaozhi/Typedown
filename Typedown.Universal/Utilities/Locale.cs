using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Markup;

namespace Typedown.Universal.Utilities
{
    public static class Locale
    {
        public enum ResourceSource
        {
            All = 0,
            CommonResources = 1,
            DialogMessages = 2,
            Resources = 3,
        }

        public static IReadOnlyDictionary<ResourceSource, ResourceLoader> ResourcesDictionary = new Dictionary<ResourceSource, ResourceLoader>()
        {
            {ResourceSource.CommonResources,  ResourceLoader.GetForViewIndependentUse(nameof(ResourceSource.CommonResources))},
            {ResourceSource.DialogMessages,  ResourceLoader.GetForViewIndependentUse(nameof(ResourceSource.DialogMessages))},
            {ResourceSource.Resources,  ResourceLoader.GetForViewIndependentUse(nameof(ResourceSource.Resources))}
        };

        public static Dictionary<string, string> SupportedLangs { get; } = new()
        {
            { "af", "Afrikaans" },
            { "am", "አማርኛ" },
            { "ar", "العربية" },
            { "as", "অসমীয়া" },
            { "az-arab", "Azərbaycan" },
            { "bg", "Български" },
            { "bn", "বাংলা" },
            { "bs", "Bosnian" },
            { "ca", "Català" },
            { "cs", "Čeština" },
            { "cy", "Cymraeg" },
            { "da", "Dansk" },
            { "de", "Deutsch" },
            { "el", "Ελληνικά" },
            { "en", "English" },
            { "es", "Español" },
            { "et", "Eesti" },
            { "fa", "فارسی" },
            { "fi", "Suomi" },
            { "fil", "Filipino" },
            { "fr", "Français" },
            { "fr-CA", "Français (Canada)" },
            { "ga", "Gaeilge" },
            { "gu", "ગુજરાતી" },
            { "he", "עברית" },
            { "hi", "हिन्दी" },
            { "hr", "Hrvatski" },
            { "hu", "Magyar" },
            { "hy", "Հայերեն" },
            { "id", "Indonesia" },
            { "is", "Íslenska" },
            { "it", "Italiano" },
            { "iu-cans", "ᐃᓄᒃᑎᑐᑦ" },
            { "iu-Latn", "Inuktitut (Latin)" },
            { "ja", "日本語" },
            { "kk", "Қазақ Тілі" },
            { "km", "ខ្មែរ" },
            { "kn", "ಕನ್ನಡ" },
            { "ko", "한국어" },
            { "ku-arab", "Kurdî (Navîn)" },
            { "ky-kg", "Kyrgyz" },
            { "lo", "ລາວ" },
            { "lt", "Lietuvių" },
            { "lv", "Latviešu" },
            { "mg", "Malagasy" },
            { "mi", "Te Reo Māori" },
            { "mk", "Македонски" },
            { "ml", "മലയാളം" },
            { "mn-Cyrl", "Mongolian (Cyrillic)" },
            { "mn-Mong", "ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ" },
            { "mr", "मराठी" },
            { "ms", "Melayu" },
            { "mt", "Malti" },
            { "nb", "Norsk Bokmål" },
            { "ne", "नेपाली" },
            { "nl", "Nederlands" },
            { "or", "ଓଡ଼ିଆ" },
            { "pa", "ਪੰਜਾਬੀ" },
            { "pl", "Polski" },
            { "prs", "دری" },
            { "pt", "Português (Brasil)" },
            { "pt-PT", "Português (Portugal)" },
            { "ro", "Română" },
            { "ru", "Русский" },
            { "sk", "Slovenčina" },
            { "sl", "Slovenščina" },
            { "sq", "Shqip" },
            { "sv", "Svenska" },
            { "sw", "Kiswahili" },
            { "ta", "தமிழ்" },
            { "te", "తెలుగు" },
            { "th", "ไทย" },
            { "ti", "ትግር" },
            { "tk-cyrl", "Türkmen Dili" },
            { "tr", "Türkçe" },
            { "tt-arab", "Татар" },
            { "ug-arab", "ئۇيغۇرچە" },
            { "uk", "Українська" },
            { "ur", "اردو" },
            { "vi", "Tiếng Việt" },
            { "zh-Hans", "中文 (简体)" },
            { "zh-Hant", "繁體中文 (繁體)" },
        };

        public static Dictionary<string, string> LangsOptions { get; } = new(SupportedLangs.Append(new("default", GetString("UseSystemSetting"))));

        public static string GetLangOptionDisplayName(string key) => LangsOptions[key];

        public static string GetString(string key, ResourceSource source = 0)
        {
            if (source == 0 || !ResourcesDictionary.ContainsKey(source))
                return ResourcesDictionary.Values.Select(x => x.GetString(key)).Where(x => !string.IsNullOrEmpty(x)).FirstOrDefault();
            return ResourcesDictionary[source].GetString(key);
        }

        public static string GetDialogString(string key)
        {
            return GetString(key, ResourceSource.DialogMessages);
        }

        public static string GetTypeString(Type type)
        {
            return (type.GetCustomAttribute(typeof(LocaleAttribute)) as LocaleAttribute)?.Text;
        }
    }

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

    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class LocaleString : MarkupExtension
    {
        public string Key { get; set; }

        public Locale.ResourceSource Source { get; set; }

        protected override object ProvideValue()
        {
            return Locale.GetString(Key, Source);
        }
    }
}
