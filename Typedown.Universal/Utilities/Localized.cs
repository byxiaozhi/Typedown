using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;

namespace Typedown.Universal.Utilities
{
    public static class Localize
    {
        public static ResourceLoader Resources { get; } = ResourceLoader.GetForViewIndependentUse("Resources");

        public static ResourceLoader DialogMessages { get; } = ResourceLoader.GetForViewIndependentUse("DialogMessages");

        public static Dictionary<string, string> Langs { get; } = new()
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

        public static Dictionary<string, string> LangsOptions { get; } = new(Langs.Append(new("default", Resources.GetString("LanguageSystemSetting"))));

        public static string GetLangOptionDisplayName(string key) => LangsOptions[key];

        public static string GetString(string key) => Resources.GetString(key) ?? DialogMessages.GetString(key);
    }

    public class LocalizeAttribute : Attribute
    {
        public string ResourceKey { get; }

        public string Text => Localize.GetString(ResourceKey);

        public LocalizeAttribute(string resourceKey)
        {
            ResourceKey = resourceKey;
        }
    }
}
