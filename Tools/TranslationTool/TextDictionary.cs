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
                    if (!string.IsNullOrEmpty(resItem.Comment))
                        item.Comment = resItem.Comment;
                    if (!string.IsNullOrEmpty(resItem.Value))
                        item.Values[resLang] = resItem.Value;
                }
                return item;
            });
        }

        public static IEnumerable<TextDictionaryItem> ReadItems(string folder)
        {
            if (!Directory.Exists(folder))
                return Enumerable.Empty<TextDictionaryItem>();
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
            {"af","Afrikaans"},
            {"am","አማርኛ"},
            {"ar","العربية"},
            {"as","অসমীয়া"},
            {"bg","Български"},
            {"bn","বাংলা"},
            {"bs","Bosnian"},
            {"ca","Català"},
            {"cs","Čeština"},
            {"cy","Cymraeg"},
            {"da","Dansk"},
            {"de","Deutsch"},
            {"el","Ελληνικά"},
            {"en","English"},
            {"es","Español"},
            {"et","Eesti"},
            {"eu","Euskara"},
            {"fa","فارسی"},
            {"fi","Suomi"},
            {"fil","Filipino"},
            {"fr","Français"},
            {"ga","Gaeilge"},
            {"gl","Galego"},
            {"gu","ગુજરાતી"},
            {"he","עברית"},
            {"hi","हिन्दी"},
            {"hr","Hrvatski"},
            {"hu","Magyar"},
            {"hy","Հայերեն"},
            {"id","Indonesia"},
            {"is","Íslenska"},
            {"it","Italiano"},
            {"ja","日本語"},
            {"ka","ქართული"},
            {"kk","Қазақ Тілі"},
            {"km","ខ្មែរ"},
            {"kn","ಕನ್ನಡ"},
            {"ko","한국어"},
            {"lo","ລາວ"},
            {"lt","Lietuvių"},
            {"lv","Latviešu"},
            {"mi","Te Reo Māori"},
            {"mk","Македонски"},
            {"ml","മലയാളം"},
            {"mr","मराठी"},
            {"ms","Melayu"},
            {"mt","Malti"},
            {"nb","Norsk Bokmål"},
            {"ne","नेपाली"},
            {"nl","Nederlands"},
            {"or","ଓଡ଼ିଆ"},
            {"pa","ਪੰਜਾਬੀ"},
            {"pl","Polski"},
            {"prs","دری"},
            {"pt","Português (Brasil)"},
            {"ro","Română"},
            {"ru","Русский"},
            {"sk","Slovenčina"},
            {"sl","Slovenščina"},
            {"sq","Shqip"},
            {"sr-Latn","Srpski (latinica)"},
            {"sv","Svenska"},
            {"sw","Kiswahili"},
            {"ta","தமிழ்"},
            {"te","తెలుగు"},
            {"th","ไทย"},
            {"ti","ትግር"},
            {"tr","Türkçe"},
            {"uk","Українська"},
            {"ur","اردو"},
            {"uz","Uzbek (Latin)"},
            {"vi","Tiếng Việt"},
            {"zh-Hans","中文 (简体)"},
            {"zh-Hant","繁體中文 (繁體)"},
            {"zu","Isi-Zulu"},
        };
    }
}
