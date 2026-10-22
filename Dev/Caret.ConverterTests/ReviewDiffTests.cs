using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The differences between two versions of a text, written as a review (Services/ReviewDiff.cs). What matters most:
    // accepting every change gives the new version back, rejecting every change gives the old one.
    public class ReviewDiffTests
    {
        private static readonly DateTime Day = new(2026, 10, 7);
        private const string Stamp = "{>>@Ann 2026-10-07<<}";

        private static ReviewDiff.Result Mark(string before, string after) => ReviewDiff.Mark(before, after, "Ann", Day, "code changed");
        private static string Accept(string text) => ReviewMarks.Resolve(text, ReviewAction.Accept);
        private static string Reject(string text) => ReviewMarks.Resolve(text, ReviewAction.Reject);

        // Blank lines and spaces at the ends of lines are not what is compared.
        private static string Norm(string text)
        {
            var lines = new List<string>();
            foreach (var line in text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()))
            {
                if (line.Length == 0 && (lines.Count == 0 || lines[lines.Count - 1].Length == 0)) continue;
                lines.Add(line);
            }
            return string.Join("\n", lines).Trim();
        }

        // Accepting gives the new text and rejecting the old one, up to blank lines: a blank line that came or went by itself
        // is not a change this marks (the tests with whole paragraphs check the blank lines too).
        private static string NoBlanks(string text) => string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0));

        // Where two texts first differ, for a failure message.
        private static string Where(string expected, string actual)
        {
            var i = 0;
            while (i < expected.Length && i < actual.Length && expected[i] == actual[i]) i++;
            string Around(string t) => t.Substring(Math.Max(0, i - 25), Math.Min(60, t.Length - Math.Max(0, i - 25))).Replace("\n", "|");
            return $"at {i}: expected [{Around(expected)}] actual [{Around(actual)}]";
        }

        private static void RoundTrips(string before, string after)
        {
            var marked = Mark(before, after).Text;
            Assert.Equal(Norm(after), Norm(Accept(marked)));
            Assert.Equal(NoBlanks(before), NoBlanks(Reject(marked)));
        }

        [Fact]
        public void TheSameTextHasNoChanges()
        {
            var result = Mark("One\n\nTwo\n", "One\n\nTwo\n");
            Assert.Equal(0, result.Changes);
            Assert.Equal("One\n\nTwo\n", result.Text);
        }

        [Fact]
        public void ChangedWordsAreMarkedInTheLine()
        {
            var result = Mark("The report was late and short.\n", "The report is on time and short.\n");
            Assert.Equal(1, result.Changes);
            Assert.Equal("The report {~~was late~>is on time~~}" + Stamp + " and short.\n", result.Text);
        }

        [Fact]
        public void ADeletedWordTakesOneSpaceWithIt()
        {
            Assert.Equal("a {--b--}" + Stamp + " c\n", Mark("a b c\n", "a c\n").Text);
            RoundTrips("a b c\n", "a c\n");
        }

        [Fact]
        public void AnAddedWordKeepsItsSpaces()
        {
            Assert.Equal("a {++new++}" + Stamp + " c\n", Mark("a c\n", "a new c\n").Text);
            RoundTrips("a c\n", "a new c\n");
        }

        [Fact]
        public void ADeletedParagraphIsMarkedAndKeepsItsPlace()
        {
            Assert.Equal("First\n\n{--Second--}" + Stamp + "\n\nThird\n", Mark("First\n\nSecond\n\nThird\n", "First\n\nThird\n").Text);
            RoundTrips("First\n\nSecond\n\nThird\n", "First\n\nThird\n");
        }

        [Fact]
        public void AnAddedParagraphIsMarked()
        {
            Assert.Equal("First\n\n{++Second++}" + Stamp + "\n\nThird\n", Mark("First\n\nThird\n", "First\n\nSecond\n\nThird\n").Text);
            RoundTrips("First\n\nThird\n", "First\n\nSecond\n\nThird\n");
        }

        [Fact]
        public void TheListMarkerStaysOutsideTheMark()
        {
            var result = Mark("- one\n- two\n- three\n", "- one\n- three\n- four\n");
            Assert.Equal("- one\n- {--two--}" + Stamp + "\n- three\n- {++four++}" + Stamp + "\n", result.Text);
            Assert.Equal(2, result.Changes);
            RoundTrips("- one\n- two\n- three\n", "- one\n- three\n- four\n");
        }

        [Fact]
        public void TheHeadingMarkStaysOutsideTheMark()
        {
            Assert.Equal("# {~~Old~>New~~}" + Stamp + " title\n\nBody text here.\n", Mark("# Old title\n\nBody text here.\n", "# New title\n\nBody text here.\n").Text);
            RoundTrips("# Old title\n\nBody text here.\n", "# New title\n\nBody text here.\n");
        }

        [Fact]
        public void ALineThatWasRewrittenIsADeletionAndAnAddition()
        {
            var result = Mark("We will ship it in March after the review.\n", "Completely different sentence about budgets.\n");
            Assert.Equal("{--We will ship it in March after the review.--}" + Stamp + "\n{++Completely different sentence about budgets.++}" + Stamp + "\n", result.Text);
        }

        [Fact]
        public void AShortLineReplacedByAnotherIsAnEditInPlace() =>
            Assert.Equal("a\n{~~b~>c~~}" + Stamp + "\n", Mark("a\nb\n", "a\nc\n").Text);

        [Fact]
        public void ALinkOrACodeSpanIsOneWord()
        {
            var result = Mark("See [the docs](http://a.com/x) and `code()` now.\n", "See [the docs](http://b.com/x) and `code(1)` now.\n");
            Assert.Equal("See {~~[the docs](http://a.com/x)~>[the docs](http://b.com/x)~~}" + Stamp + " and {~~`code()`~>`code(1)`~~}" + Stamp + " now.\n", result.Text);
            RoundTrips("See [the docs](http://a.com/x) and `code()` now.\n", "See [the docs](http://b.com/x) and `code(1)` now.\n");
        }

        [Fact]
        public void CodeIsNeverMarkedAndTheBlockGetsAComment()
        {
            const string before = "Text\n\n```\nlet a = 1;\nlet b = 2;\n```\n\nEnd\n";
            const string after = "Text\n\n```\nlet a = 1;\nlet b = 3;\n```\n\nEnd\n";
            var result = Mark(before, after);
            Assert.Equal("Text\n\n```\nlet a = 1;\nlet b = 3;\n```\n\n{>>@Ann 2026-10-07: code changed<<}\n\nEnd\n", result.Text);
            Assert.Equal(1, result.Changes);
            // the comment is a comment, not a change: it stays when changes are accepted
            Assert.Equal(result.Text, Accept(result.Text));
        }

        [Fact]
        public void ARemovedCodeBlockIsSaidInAComment()
        {
            var result = Mark("Text\n\n```\ncode\n```\n\nEnd\n", "Text\n\nEnd\n");
            Assert.Equal("Text\n\n{>>@Ann 2026-10-07: code changed<<}\n\nEnd\n", result.Text);
        }

        [Fact]
        public void WindowsLineEndingsOfTheNewVersionAreKept()
        {
            var result = Mark("a\r\nb\r\n", "a\r\nc\r\n").Text;
            Assert.DoesNotContain("\n", result.Replace("\r\n", ""));
            Assert.Equal("a\nc\n", Accept(result.Replace("\r\n", "\n")));
        }

        [Fact]
        public void TheAuthorIsInEveryMark()
        {
            var text = Mark("a\n\nb\n\nc\n", "a\n\nx\n\nc\n\nd\n").Text;
            Assert.Equal(text.Split("{>>@Ann 2026-10-07<<}").Length - 1, ReviewMarks.Count(text).Changes);
        }

        [Fact]
        public void TextThatWouldEndAMarkIsKeptAsItIsButNotMarked()
        {
            const string after = "A line with ++} in it and more words here.\n";
            var result = Mark("A line with nothing in it and more words here.\n", after);
            // not marked, and said so: it is no change, and rejecting does not undo it (the documented limit)
            Assert.Equal(0, result.Changes);
            Assert.Equal(1, result.Unmarked);
            Assert.Equal(after, Accept(result.Text));
            Assert.Equal(after, Reject(result.Text));
        }

        [Fact]
        public void AWholeLineThatCannotBeMarkedIsCountedToo()
        {
            var result = Mark("First\n\nSecond\n", "First\n\nSecond\n\nA new line with ++} in it.\n");
            Assert.Equal(0, result.Changes);
            Assert.Equal(1, result.Unmarked);
        }

        [Fact]
        public void AReportWithEverythingInItRoundTrips()
        {
            const string before = "# Quarterly report\n\nThe numbers were late this quarter and we missed the target.\n\n## Plan\n\n- hire two people\n- review the budget in March\n- close the old office\n\n| Item | Cost |\n| --- | --- |\n| Rent | 1000 |\n\n> A quote that stays.\n\n```sh\nmake all\n```\n\nSee [the plan](http://a.com/plan) for **details**.\n";
            const string after = "# Quarterly report 2026\n\nThe numbers were on time this quarter and we met the target.\n\n## Plan\n\n- hire three people\n- review the budget in April\n- open the new office\n- train the team\n\n| Item | Cost |\n| --- | --- |\n| Rent | 1200 |\n\n> A quote that stays.\n\n```sh\nmake all\n```\n\nSee [the plan](http://b.com/plan) for **all the details**.\n\nNew closing paragraph.\n";
            RoundTrips(before, after);
        }

        private static readonly string[] Vocabulary = { "alpha", "beta", "gamma", "delta", "the", "report", "budget", "is", "on", "time", "and", "short", "review", "March", "plan", "team", "office" };

        private static string RandomLine(Random random)
        {
            var words = Enumerable.Range(0, random.Next(1, 12)).Select(_ => Vocabulary[random.Next(Vocabulary.Length)]);
            var prefix = random.Next(6) switch { 0 => "- ", 1 => "1. ", 2 => "# ", 3 => "> ", _ => "" };
            return prefix + string.Join(" ", words);
        }

        private static string RandomText(Random random, int paragraphs) =>
            string.Join("\n\n", Enumerable.Range(0, paragraphs).Select(_ => string.Join("\n", Enumerable.Range(0, random.Next(1, 4)).Select(__ => RandomLine(random))))) + "\n";

        private static string RandomEdit(Random random, string text)
        {
            var lines = text.Split('\n').ToList();
            for (var edits = random.Next(1, 7); edits > 0; edits--)
            {
                var at = random.Next(lines.Count);
                switch (random.Next(4))
                {
                    case 0: lines.RemoveAt(at); break;
                    case 1: lines.Insert(at, RandomLine(random)); break;
                    case 2:
                        var words = lines[at].Split(' ').ToList();
                        words[random.Next(words.Count)] = Vocabulary[random.Next(Vocabulary.Length)];
                        lines[at] = string.Join(" ", words);
                        break;
                    default: lines[at] = RandomLine(random); break;
                }
                if (lines.Count == 0) lines.Add("x");
            }
            return string.Join("\n", lines);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(18)]
        [InlineData(19)]
        [InlineData(20)]
        [InlineData(21)]
        [InlineData(22)]
        [InlineData(23)]
        [InlineData(24)]
        [InlineData(25)]
        [InlineData(26)]
        [InlineData(27)]
        [InlineData(28)]
        [InlineData(29)]
        [InlineData(30)]
        [InlineData(31)]
        [InlineData(32)]
        [InlineData(33)]
        [InlineData(34)]
        [InlineData(35)]
        [InlineData(36)]
        [InlineData(37)]
        [InlineData(38)]
        [InlineData(39)]
        [InlineData(40)]
        public void RandomEditsRoundTrip(int seed)
        {
            var random = new Random(seed);
            for (var round = 0; round < 80; round++)
            {
                var before = RandomText(random, random.Next(1, 12));
                var after = RandomEdit(random, before);
                var marked = Mark(before, after).Text;
                var accepted = Accept(marked);
                var rejected = Reject(marked);
                Assert.True(NoBlanks(after) == NoBlanks(accepted), $"seed {seed} round {round}: accept differs {Where(NoBlanks(after), NoBlanks(accepted))}\n--- before\n{before}\n--- after\n{after}\n--- marked\n{marked}\n--- accepted\n{accepted}");
                Assert.True(NoBlanks(before) == NoBlanks(rejected), $"seed {seed} round {round}: reject differs {Where(NoBlanks(before), NoBlanks(rejected))}\n--- before\n{before}\n--- after\n{after}\n--- marked\n{marked}\n--- rejected\n{rejected}");
            }
        }

        [Fact]
        public void TwoTextsWithNothingInCommonStillRoundTripAndAreQuick()
        {
            var random = new Random(7);
            var before = string.Join("\n\n", Enumerable.Range(0, 1500).Select(_ => RandomLine(random))) + "\n";
            var after = string.Join("\n\n", Enumerable.Range(0, 1500).Select(_ => RandomLine(random))) + "\n";
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var marked = Mark(before, after).Text;
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"took {watch.Elapsed}");
            Assert.Equal(NoBlanks(after), NoBlanks(Accept(marked)));
            Assert.Equal(NoBlanks(before), NoBlanks(Reject(marked)));
        }
    }
}
