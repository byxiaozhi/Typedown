using System.Text;
using TranslationTool;

Console.OutputEncoding = Encoding.Unicode;
Console.InputEncoding = Encoding.Unicode;

string projectPath = @"C:\GitHub\Typedown";

var manualLangs = new List<string>() { "en", "ja", "ko", "ru", "fr", "de", "es", "it", "nl", "ar" };
var batch = 30;

foreach (var lang in manualLangs)
{
    ManualTranslate(lang);
}

void ManualTranslate(string lang)
{
    var zhInputs = TextResource.ReadItems(@$"{projectPath}\Dev\Typedown.Core\Resources\Strings\zh-Hans\").ToList();
    var enInputs = TextResource.ReadItems(@$"{projectPath}\Dev\Typedown.Core\Resources\Strings\en\").ToList();
    var dictionary = TextDictionary.ReadItems(@$"{projectPath}\Tools\TranslationTool\Dictionary\").ToList();
    dictionary = dictionary.Merge(zhInputs, "zh-Hans").ToList();
    dictionary = dictionary.Merge(enInputs, "en").ToList();
    dictionary.WriteItems(@$"{projectPath}\Tools\TranslationTool\Dictionary\");
    while (true)
    {
        var inputs = dictionary.Where(x => !x.Values.ContainsKey(lang) || string.IsNullOrEmpty(x.Values[lang])).Take(batch++).ToList();
        if (!inputs.Any())
            break;

        var enWords = inputs.Select(x => enInputs.SingleOrDefault(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x?.Value).ToList();

        Console.WriteLine("相同位置的待翻译文本为同义词，输入输出均以竖线\"|\"分割，并且一一对应\n");

        var zhWords = inputs.Select(x => zhInputs.SingleOrDefault(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList();
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

        dictionary.WriteItems($@"{projectPath}\Tools\TranslationTool\Dictionary\");

        Console.WriteLine("\n");
    }

    var output = dictionary.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, lang)).Where(x => !string.IsNullOrEmpty(x.Value));
    output.WriteItems(@$"{projectPath}\Dev\Typedown.Core\Resources\Strings\{lang}\");
}

async Task AutoTranslate(string sourceLang, string targetLang)
{
    var sourceInputs = TextResource.ReadItems(@$"{projectPath}\Dev\Typedown.Core\Resources\Strings\{sourceLang}\").ToList();
    var dictionary = TextDictionary.ReadItems(@$"{projectPath}\Tools\TranslationTool\BingDictionary\").ToList();
    dictionary = dictionary.Merge(sourceInputs, sourceLang).ToList();
    var inputs = dictionary.Where(x => !x.Values.ContainsKey(targetLang) || string.IsNullOrEmpty(x.Values[targetLang])).ToList();
    if (!inputs.Any())
        return;
    var results = await BingTranslate.Translate(sourceLang, new List<string> { targetLang }, inputs.Select(x => sourceInputs.Single(y => (x.Table, x.Name) == (y.Table, y.Name))).Select(x => x.Value).ToList());
    for (var i = 0; i < inputs.Count; i++)
        foreach (var resultDic in results[i])
            inputs[i].Values[resultDic.Key] = resultDic.Value;
    dictionary.WriteItems(@$"{projectPath}\Tools\TranslationTool\TempDictionary\");
    var output = dictionary.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, targetLang)).Where(x => !string.IsNullOrEmpty(x.Value));
    output.WriteItems(@$"{projectPath}\Dev\Typedown.Core\Resources\Strings\{targetLang}\");
}
