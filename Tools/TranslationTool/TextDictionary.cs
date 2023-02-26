using Newtonsoft.Json;

namespace TranslationTool
{
    public static class TextDictionary
    {
        public static TextResourceItem GetTextResourceItem(this IEnumerable<TextDictionaryItem> dicItems, string table, string name, string lang)
        {
            var dicItem = dicItems.Single(x => x.Table == table && x.Name == name);
            return new()
            {
                Table = table,
                Name = name,
                Comment = dicItem.Comment,
                Value = dicItem.Values[lang],
            };
        }

        public static IEnumerable<TextDictionaryItem> Merge(this IEnumerable<TextDictionaryItem> dicItems, IEnumerable<TextResourceItem> resItems, string resLang)
        {
            if (!SupportedLangs.ContainsKey(resLang))
                throw new ArgumentOutOfRangeException(nameof(resLang));
            var dic = dicItems.ToDictionary(x => (x.Table, x.Name), x => x);
            var res = resItems.ToDictionary(x => (x.Table, x.Name), x => x);
            var keys = dic.Keys.Union(res.Keys).Distinct().OrderBy(x => x.Table).ThenBy(x => x.Name);
            return keys.Select(x =>
            {
                var item = new TextDictionaryItem
                {
                    Name = x.Name,
                    Table = x.Table
                };
                foreach (var lang in SupportedLangs.Keys)
                {
                    item.Values[lang] = string.Empty;
                }
                if (dic.TryGetValue(x, out var dicItem))
                {
                    item.Comment = dicItem.Comment;
                    foreach (var dicItemValue in dicItem.Values)
                        item.Values[dicItemValue.Key] = dicItemValue.Value;
                }
                if (res.TryGetValue(x, out var resItem))
                {
                    item.Comment = resItem.Comment;
                    item.Values[resLang] = resItem.Value;
                }
                return item;
            });
        }

        public static IEnumerable<TextDictionaryItem> ReadItems(string folder)
        {
            return Directory.GetFiles(folder, "*.json")
                .SelectMany(x => JsonConvert.DeserializeObject<List<TextDictionaryItem>>(File.ReadAllText(x)));
        }

        public static void WriteItems(this IEnumerable<TextDictionaryItem> items, string folder)
        {
            var tables = new Dictionary<string, List<TextDictionaryItem>>();
            foreach (var item in items)
            {
                if (!tables.TryGetValue(item.Table, out var table))
                {
                    table = new List<TextDictionaryItem>();
                    tables.Add(item.Table, table);
                }
                table.Add(item);
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            foreach (var item in tables)
            {
                File.WriteAllText(Path.Combine(folder, item.Key + ".json"), JsonConvert.SerializeObject(item.Value, Formatting.Indented));
            }
        }

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
    }
}
