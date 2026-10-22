using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The help topics (Dev/Typedown.WinUI/Help/<language>/*.md, Services/HelpTopics.cs): every language has every topic, and the names of
    // menus and buttons in them are the words of that language's interface.
    public class HelpTopicsTests
    {
        private static string AppRoot => Path.GetFullPath(Path.Combine(TestPaths.ProjectFolder, "..", "Typedown.WinUI"));

        public static IEnumerable<object[]> Languages => new[] { "en", "es", "fr", "pl", "pt" }.Select(l => new object[] { l });

        private static Dictionary<string, string> Strings(string language) =>
            XDocument.Load(Path.Combine(AppRoot, "Strings", language, "AppResources.resw")).Root.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));

        [Theory]
        [MemberData(nameof(Languages))]
        public void Every_language_has_every_topic_as_a_valid_file_with_a_title(string language)
        {
            foreach (var topic in HelpTopics.Names)
            {
                var path = Path.Combine(AppRoot, "Help", language, topic + ".md");
                Assert.True(File.Exists(path), path);
                var bytes = File.ReadAllBytes(path);
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, topic + " has a byte order mark");
                var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
                Assert.True(text.Length > 400, topic + " is nearly empty");
                Assert.StartsWith("# ", text);
                Assert.DoesNotContain("	", text);
            }
        }

        [Fact]
        public void A_topic_that_a_language_does_not_have_falls_back_to_English()
        {
            Assert.Equal(Path.Combine(AppRoot, "Help", "en", "review.md"), HelpTopics.PathOf(AppRoot, "de", "review"));
            Assert.Equal(Path.Combine(AppRoot, "Help", "en", "review.md"), HelpTopics.PathOf(AppRoot, null, "review"));
            Assert.Equal(Path.Combine(AppRoot, "Help", "es", "review.md"), HelpTopics.PathOf(AppRoot, "es", "review"));
        }

        // **Name** in a topic is a menu item, button or setting of the interface (or a topic of this help): it must be one of the
        // words of the interface in that language, so that the help sends the reader to something that is there.
        [Theory]
        [MemberData(nameof(Languages))]
        public void The_names_in_bold_are_words_of_the_interface_of_that_language(string language)
        {
            var strings = Strings(language);
            string Plain(string s) => Regex.Replace(s.Replace("…", "").Replace("...", "").Replace("’", "'"), @"\s+", " ").Trim().TrimEnd('.', ':').ToLowerInvariant();
            var known = new HashSet<string>(strings.Values.Where(v => v != null).Select(Plain));
            var unknown = new List<string>();
            foreach (var topic in HelpTopics.Names)
            {
                var text = File.ReadAllText(Path.Combine(AppRoot, "Help", language, topic + ".md"));
                foreach (Match m in Regex.Matches(text, @"\*\*([^*\n]+)\*\*"))
                {
                    var name = m.Groups[1].Value;
                    // a path "A > B" is checked part by part
                    foreach (var part in name.Split('>').Select(Plain))
                        if (!known.Contains(part)) unknown.Add($"{language}/{topic}: **{name}** ({part})");
                }
            }
            Assert.True(unknown.Count == 0, string.Join(System.Environment.NewLine, unknown));
        }
    }
}
