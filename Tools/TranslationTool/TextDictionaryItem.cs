namespace TranslationTool
{
    public class TextDictionaryItem
    {
        public string Table { get; set; }

        public string Name { get; set; }

        public string Comment { get; set; }

        public Dictionary<string, string> Values { get; set; } = new();
    }
}
