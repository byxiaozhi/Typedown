// See https://aka.ms/new-console-template for more information
using TranslationTool;

var inputs = TextResource.ReadItems(@"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\zh-Hans\").ToList();
var dictionary = TextDictionary.ReadItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\Dictionary\").ToList();

foreach (var lang in TextDictionary.SupportedLangs.Keys)
{
    var output = inputs.Select(x => dictionary.GetTextResourceItem(x.Table, x.Name, lang)).Where(x => !string.IsNullOrEmpty(x.Value));
    output.WriteItems(@$"C:\Users\12283\Documents\GitHub\Typedown\Dev\Typedown.Core\Resources\Strings\{lang}\");
}

// dictionary = dictionary.Merge(inputs, "zh-Hans").ToList();
// dictionary.WriteItems(@"C:\Users\12283\Documents\GitHub\Typedown\Tools\TranslationTool\Dictionary\");

Console.WriteLine("完成");