using System;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The spell checker is Windows' own, so what it knows depends on the languages installed on the PC that runs
    // the tests: a test that needs English says nothing (and passes) where Windows has no English dictionary.
    public class SpellCheckingTests
    {
        [Theory]
        [InlineData("sentence", true)]
        [InlineData("don't", true)]
        [InlineData("l’été", true)]
        [InlineData("árvíztűrő", true)]
        [InlineData("Caret", true)]
        [InlineData("NASA", false)]     // an acronym
        [InlineData("iPhone", false)]   // an identifier
        [InlineData("GitHub", false)]
        [InlineData("x", false)]
        [InlineData("abc123", false)]
        [InlineData("snake_case", false)]
        [InlineData("'quoted", false)]
        [InlineData("", false)]
        public void Only_words_are_checked(string word, bool expected) =>
            Assert.Equal(expected, SpellChecking.IsCheckable(word));

        [Fact]
        public void An_English_misspelling_is_found_and_corrected()
        {
            var checker = SpellChecking.Create();
            if (checker == null || !checker.Supports("en-US")) return;
            var wrong = checker.Misspelled(new[] { "sentense", "sentence", "NASAA", "Caret" }, new[] { "en-US" });
            Assert.Contains("sentense", wrong);
            Assert.DoesNotContain("sentence", wrong);
            Assert.DoesNotContain("NASAA", wrong); // all capitals: not checked
            Assert.Contains("sentence", checker.Suggest("sentense", new[] { "en-US" }));
        }

        [Fact]
        public void A_word_is_wrong_only_if_it_is_wrong_in_every_language()
        {
            var checker = SpellChecking.Create();
            if (checker == null || !checker.Supports("en-US") || !checker.Supports("hu-HU")) return;
            var both = new[] { "en-US", "hu-HU" };
            // "sentence" is English, "mondat" Hungarian: each is right in one of the two
            var wrong = checker.Misspelled(new[] { "sentence", "mondat", "sentense", "mondaat" }, both);
            Assert.DoesNotContain("sentence", wrong);
            Assert.DoesNotContain("mondat", wrong);
            Assert.Contains("sentense", wrong);
            Assert.Contains("mondaat", wrong);
            // the first language's guesses lead (English text is not helped by Hungarian ones), the other's follow
            var suggestions = checker.Suggest("sentense", both);
            Assert.Equal("sentence", suggestions[0]);
            Assert.True(suggestions.Count <= 6);
            Assert.Equal("mondat", checker.Suggest("mondaat", new[] { "hu-HU", "en-US" })[0]);
        }

        [Fact]
        public void The_languages_come_from_the_choice_or_from_the_preferred_ones_that_have_a_dictionary()
        {
            var checker = SpellChecking.Create();
            if (checker == null) return;
            Assert.Empty(checker.Languages("", new[] { "xx-YY", "", null }));
            Assert.Empty(checker.Languages("xx-YY", Array.Empty<string>()));
            var english = checker.SupportedLanguages.FirstOrDefault(l => l.StartsWith("en-", StringComparison.OrdinalIgnoreCase));
            if (english == null) return;
            Assert.Equal(new[] { english }, checker.Languages(english, new[] { "hu-HU" }));
            // "en" alone, or an English region without a dictionary, finds a region that has one; no language twice
            var found = checker.Languages("", new[] { "en", english, "en-US" });
            Assert.Single(found.Where(l => l.StartsWith("en-", StringComparison.OrdinalIgnoreCase) && string.Equals(l, found[0], StringComparison.OrdinalIgnoreCase)));
            Assert.StartsWith("en-", found[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}
