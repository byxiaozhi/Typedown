using System.Xml.Linq;

namespace TranslationTool
{
    public static class TextResource
    {
        public static IEnumerable<TextResourceItem> ReadItems(string folder)
        {
            foreach (var item in Directory.GetFiles(folder, "*.resw"))
            {
                var table = Path.GetFileNameWithoutExtension(item);
                var doc = XDocument.Load(item);
                var root = doc.Element("root");
                foreach (var data in root.Elements("data"))
                {
                    var name = data.Attribute("name").Value;
                    var value = data.Element("value").Value;
                    var comment = data.Element("comment")?.Value ?? string.Empty;
                    yield return new()
                    {
                        Table = table,
                        Name = name,
                        Value = value,
                        Comment = comment
                    };
                }
            }
        }

        public static void WriteItems(this IEnumerable<TextResourceItem> items, string folder)
        {
            var tables = new Dictionary<string, XDocument>();
            foreach (var item in items)
            {
                if (!tables.TryGetValue(item.Table, out XDocument doc))
                {
                    doc = XDocument.Load("Template.resw");
                    doc.Element("root").Elements("data").Remove();
                    tables.Add(item.Table, doc);
                }
                var root = doc.Element("root");
                var node = new XElement("data", 
                    new XAttribute("name", item.Name), 
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", item.Value),
                    new XElement("comment", item.Comment));
                root.Add(node);
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            foreach (var item in tables)
            {
                item.Value.Save(Path.Combine(folder, item.Key + ".resw"));
            }
        }
    }
}
