using System.Text;
using TranslationTool;

Console.OutputEncoding = Encoding.Unicode;
Console.InputEncoding = Encoding.Unicode;

var zhInputs = TextResource.ReadItems(@"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\zh-Hans\").ToList();
var enInputs = TextResource.ReadItems(@"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\en\").ToList();
var dictionary = TextDictionary.ReadItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\Dictionary\").ToList();
dictionary = dictionary.Merge(zhInputs, "zh-Hans").ToList();
dictionary = dictionary.Merge(enInputs, "en").ToList();
dictionary.WriteItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\Dictionary\");

var manualLangs = new List<string>() { "en", "ja", "ko", "ru", "fr", "de", "es", "it", "nl", "ar" };
var batch = 30;
foreach (var lang in manualLangs)
{
    while (true)
    {
        var inputs = dictionary.Where(x => !x.Values.ContainsKey(lang) || string.IsNullOrEmpty(x.Values[lang])).Take(batch++).ToList();
        if (!inputs.Any())
            break;

        var enWords = inputs.Select(x => enInputs.Single(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList();

        Console.WriteLine("相同位置的待翻译文本为同义词，输入输出均以竖线\"|\"分割，并且一一对应\n");

        var zhWords = inputs.Select(x => zhInputs.Single(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList();
        Console.WriteLine($"待翻译文本({TextDictionary.SupportedLangs["zh-Hans"]})：" + string.Join("|", zhWords));
        Console.WriteLine();

        Console.WriteLine($"待翻译文本({TextDictionary.SupportedLangs["en"]})：" + string.Join("|", enWords));
        Console.WriteLine();

        Console.WriteLine($"翻译结果({TextDictionary.SupportedLangs[lang]})：");
        var results = Console.ReadLine().Split("|").ToList();

        if (zhWords.Count != results.Count)
            continue;

        for (var i = 0; i < inputs.Count; i++)
            inputs[i].Values[lang] = results[i].Trim();

        dictionary.WriteItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\Dictionary\");

        Console.WriteLine("\n");
    }

    var output = dictionary.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, lang)).Where(x => !string.IsNullOrEmpty(x.Value));
    output.WriteItems(@$"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\{lang}\");
}

//foreach (var lang in TextDictionary.SupportedLangs.Keys)
//{
//    var inputs = dictionary.Where(x => !x.Values.ContainsKey(lang) || string.IsNullOrEmpty(x.Values[lang])).ToList();
//    if (!inputs.Any())
//        continue;
//    var results = await BingTranslate.Translate("zh-Hans", new List<string> { lang }, inputs.Select(x => zhInputs.Single(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList());
//    for (var i = 0; i < inputs.Count; i++)
//    {
//        foreach (var resultDic in results[i])
//        {
//            inputs[i].Values[resultDic.Key] = resultDic.Value;
//        }
//    }
//    dictionary.WriteItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\TempDictionary\");
//    Console.WriteLine(lang);
//}

//foreach (var lang in TextDictionary.SupportedLangs.Keys)
//{
//    var output = dictionary.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, lang)).Where(x => !string.IsNullOrEmpty(x.Value));
//    output.WriteItems(@$"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\{lang}\");
//}

//Console.WriteLine("完成");