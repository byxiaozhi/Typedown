using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The starter templates (Services/StarterTemplates.cs, Dev/Typedown.WinUI/Templates/<language>/*.md): what is copied to the
    // user's Templates folder and what ships.
    public class StarterTemplatesTests : IDisposable
    {
        private readonly string work = TestPaths.NewTempFolder();

        public void Dispose() => Directory.Delete(work, true);

        private static string TemplatesRoot => Path.GetFullPath(Path.Combine(TestPaths.ProjectFolder, "..", "Typedown.WinUI"));

        public static IEnumerable<object[]> Languages => new[] { "en", "es", "fr", "pl", "pt" }.Select(l => new object[] { l });

        private string Folder(string name, params (string Name, string Text)[] files)
        {
            var folder = Path.Combine(work, name);
            Directory.CreateDirectory(folder);
            foreach (var (fileName, text) in files) File.WriteAllText(Path.Combine(folder, fileName), text);
            return folder;
        }

        [Fact]
        public void Missing_templates_are_added_and_existing_ones_are_never_overwritten()
        {
            var source = Folder("source", ("A.md", "# A original"), ("B.md", "# B original"), ("notes.txt", "not a template"));
            var target = Folder("target", ("A.md", "# A changed by the user"));
            Assert.Equal(1, StarterTemplates.Install(source, target));
            Assert.Equal("# A changed by the user", File.ReadAllText(Path.Combine(target, "A.md")));
            Assert.Equal("# B original", File.ReadAllText(Path.Combine(target, "B.md")));
            Assert.False(File.Exists(Path.Combine(target, "notes.txt")));
            Assert.Equal(0, StarterTemplates.Install(source, target)); // the second time nothing is left to add
        }

        [Fact]
        public void The_target_folder_is_created_and_a_missing_source_adds_nothing()
        {
            var source = Folder("source", ("A.md", "# A"));
            var target = Path.Combine(work, "new", "Templates");
            Assert.Equal(1, StarterTemplates.Install(source, target));
            Assert.True(File.Exists(Path.Combine(target, "A.md")));
            Assert.Equal(0, StarterTemplates.Install(Path.Combine(work, "nope"), Path.Combine(work, "elsewhere")));
        }

        [Fact]
        public void A_language_without_templates_gets_the_English_ones()
        {
            Directory.CreateDirectory(Path.Combine(work, "Templates", "en"));
            Directory.CreateDirectory(Path.Combine(work, "Templates", "es"));
            Assert.Equal(Path.Combine(work, "Templates", "es"), StarterTemplates.SourceFolder(work, "es"));
            Assert.Equal(Path.Combine(work, "Templates", "en"), StarterTemplates.SourceFolder(work, "de"));
            Assert.Equal(Path.Combine(work, "Templates", "en"), StarterTemplates.SourceFolder(work, null));
            Assert.Equal(Path.Combine(work, "Templates", "en"), StarterTemplates.SourceFolder(work, ""));
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void Every_language_ships_the_same_number_of_templates(string language)
        {
            var names = StarterTemplates.Names(StarterTemplates.SourceFolder(TemplatesRoot, language));
            Assert.Equal(10, names.Length);   // eight documents for the language, and two with speech marks (a speech and a presentation)
            Assert.True(Directory.Exists(Path.Combine(TemplatesRoot, "Templates", language)), language);
        }

        [Theory]
        [MemberData(nameof(Languages))]
        public void A_shipped_template_is_a_valid_file_with_a_title_and_places_to_fill_in(string language)
        {
            var folder = StarterTemplates.SourceFolder(TemplatesRoot, language);
            var names = StarterTemplates.Names(folder);
            Assert.Equal(names.Length, names.Select(n => n.ToLowerInvariant()).Distinct().Count());
            foreach (var name in names)
            {
                Assert.True(name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, name);
                var bytes = File.ReadAllBytes(Path.Combine(folder, name));
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, name + " has a byte order mark");
                var text = new System.Text.UTF8Encoding(false, true).GetString(bytes); // throws on bad UTF-8
                Assert.True(text.Length > 200, name + " is nearly empty");
                Assert.Contains("[", text); // places to fill in
                Assert.DoesNotContain("&nbsp;", text);
                Assert.DoesNotContain("\t", text);
                Assert.True(text.Split('\n').Any(l => l.StartsWith("# ") || l.StartsWith("**")), name + " has no title");
                // a rule on a line of its own would turn the text above it into a heading
                Assert.DoesNotContain("\n---\n", text.Replace("\r\n", "\n"));
            }
        }

        // The two templates with speech marks (a speech, a presentation) are in every language, with the English mark words: the marks are the
        // same for everybody, only the text around them is translated (the jest test speechTemplates.test.js checks that the marks are valid).
        [Theory]
        [MemberData(nameof(Languages))]
        public void Every_language_has_a_speech_and_a_presentation_with_speech_marks(string language)
        {
            var folder = StarterTemplates.SourceFolder(TemplatesRoot, language);
            var withMarks = StarterTemplates.Names(folder)
                .Where(n => File.ReadAllText(Path.Combine(folder, n)).Contains("{budget ")).ToArray();
            Assert.Equal(2, withMarks.Length);
            foreach (var name in withMarks)
            {
                var text = File.ReadAllText(Path.Combine(folder, name));
                Assert.Contains("{wpm ", text);
                Assert.Contains("{pause", text);
                Assert.Contains("{cue:", text);
            }
        }

        [Fact]
        public void The_Portuguese_set_has_the_documents_of_Brazil()
        {
            var names = StarterTemplates.Names(Path.Combine(TemplatesRoot, "Templates", "pt"));
            Assert.Contains("Trabalho acadêmico (ABNT).md", names);
            Assert.Contains("Recibo de pagamento.md", names);
            Assert.Contains("Pedido de demissão.md", names);
            Assert.Contains("Currículo.md", names);
            var academic = File.ReadAllText(Path.Combine(TemplatesRoot, "Templates", "pt", "Trabalho acadêmico (ABNT).md"));
            Assert.Contains("NBR 6023", academic);
            Assert.Contains("(SOBRENOME, ano", academic);
        }
        [Fact]
        public void The_Spanish_set_has_the_documents_of_Colombia_and_the_formal_address()
        {
            var names = StarterTemplates.Names(Path.Combine(TemplatesRoot, "Templates", "es"));
            Assert.Contains("Cuenta de cobro (Colombia).md", names);
            Assert.Contains("Derecho de petición (Colombia).md", names);
            Assert.Contains("Trabajo académico (APA 7).md", names);
            var letter = File.ReadAllText(Path.Combine(TemplatesRoot, "Templates", "es", "Carta formal.md"));
            Assert.Contains("Respetado(a) señor(a)", letter);
            Assert.Contains("usted", letter);
        }
    }
}