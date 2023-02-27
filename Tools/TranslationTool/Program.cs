using TranslationTool;

var zhInputs = TextResource.ReadItems(@"C:\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\zh-Hans\").ToList();
var dictionary = TextDictionary.ReadItems(@"C:\GitHub\Typedown\Tools\TranslationTool\Dictionary\").ToList();

foreach (var lang in TextDictionary.SupportedLangs.Keys)
{
    var inputs = dictionary.Where(x => !x.Values.ContainsKey(lang) || string.IsNullOrEmpty(x.Values[lang])).ToList();
    if (!inputs.Any())
        continue;
    var results = await BingTranslate.Translate("zh-Hans", new List<string> { lang }, inputs.Select(x => zhInputs.Single(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList());
    for (var i = 0; i < inputs.Count; i++)
    {
        foreach (var resultDic in results[i])
        {
            inputs[i].Values[resultDic.Key] = resultDic.Value;
        }
    }
    dictionary.WriteItems(@"C:\GitHub\Typedown\Tools\TranslationTool\Dictionary\");
    Console.WriteLine(lang);
}

foreach (var lang in TextDictionary.SupportedLangs.Keys)
{
    var output = dictionary.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, lang)).Where(x => !string.IsNullOrEmpty(x.Value));
    output.WriteItems(@$"C:\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\{lang}\");
}

Console.WriteLine("完成");