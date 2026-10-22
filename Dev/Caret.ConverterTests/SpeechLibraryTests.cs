using System;
using System.Globalization;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // The user's own speech marks (Services/SpeechLibrary.cs): the rules of a mark, the line it writes into a document,
    // the starting set, and the stored library.
    public class SpeechLibraryTests
    {
        private static SpeechMark Mark(string name = "very-slow", string kind = "pace", double percent = 50, string meaning = "about half speed") =>
            new() { Name = name, Kind = kind, Percent = percent, Meaning = meaning };

        private static readonly SpeechMark[] None = Array.Empty<SpeechMark>();

        [Theory]
        [InlineData("very-slow", null)]
        [InlineData("whisper", null)]
        [InlineData("a1", null)]
        [InlineData("Very-Slow", null)] // capitals are lower-cased: names do not depend on case
        [InlineData("  wave  ", null)]
        [InlineData("x", "name")]
        [InlineData("1abc", "name")]
        [InlineData("-abc", "name")]
        [InlineData("a b", "name")]
        [InlineData("under_score", "name")]
        [InlineData("a-name-that-is-way-too-long-to-keep", "name")]
        [InlineData("", "name")]
        [InlineData("slow", "builtin")]
        [InlineData("PAUSE", "builtin")]
        [InlineData("define", "builtin")]
        public void NamesFollowTheRulesOfTheEditor(string name, string error) =>
            Assert.Equal(error, SpeechLibrary.Check(Mark(name), None));

        [Fact]
        public void ANameCannotBeUsedTwice()
        {
            var others = new[] { Mark("whisper", "span") };
            Assert.Equal("taken", SpeechLibrary.Check(Mark("Whisper", "span"), others));
            Assert.Null(SpeechLibrary.Check(Mark("whispers", "span"), others));
        }

        [Fact]
        public void TheMeaningIsRequired()
        {
            Assert.Equal("meaning", SpeechLibrary.Check(Mark(meaning: ""), None));
            Assert.Equal("meaning", SpeechLibrary.Check(Mark(meaning: "  \n "), None));
            Assert.Equal("meaning", SpeechLibrary.Check(Mark(meaning: "{}"), None));
        }

        [Theory]
        [InlineData(9.9, "pace")]
        [InlineData(10, null)]
        [InlineData(300, null)]
        [InlineData(300.5, "pace")]
        [InlineData(double.NaN, "pace")]
        public void ASpeedIsTenToThreeHundredPercent(double percent, string error) =>
            Assert.Equal(error, SpeechLibrary.Check(Mark(percent: percent), None));

        [Fact]
        public void SecondsAddedAfterEveryWordAreAtMostTwo()
        {
            var mark = Mark();
            mark.PerWord = 2;
            Assert.Null(SpeechLibrary.Check(mark, None));
            mark.PerWord = 2.1;
            Assert.Equal("perword", SpeechLibrary.Check(mark, None));
            mark.PerWord = -1;
            Assert.Equal("perword", SpeechLibrary.Check(mark, None));
        }

        [Theory]
        [InlineData(0.09, "pause")]
        [InlineData(0.1, null)]
        [InlineData(600, null)]
        [InlineData(601, "pause")]
        public void APauseIsATenthOfASecondToTenMinutes(double seconds, string error)
        {
            var mark = new SpeechMark { Name = "long-pause", Kind = "pause", Seconds = seconds, Meaning = "before the key line" };
            Assert.Equal(error, SpeechLibrary.Check(mark, None));
        }

        [Fact]
        public void AnUnknownKindIsRefused() =>
            Assert.Equal("kind", SpeechLibrary.Check(new SpeechMark { Name = "thing", Kind = "voice", Meaning = "x" }, None));

        // The lines are those the editor reads (Muya/lib/parser/speech.test.js has the same seven).
        [Fact]
        public void TheStartersWriteTheLinesTheEditorAccepts()
        {
            var lines = SpeechLibrary.Starters(name => "meaning of " + name).Select(SpeechLibrary.DefinitionLine).ToArray();
            Assert.Equal(new[]
            {
                "{define very-slow pace 50%: meaning of very-slow}",
                "{define very-fast pace 160%: meaning of very-fast}",
                "{define word-by-word pace 60% +0.3s: meaning of word-by-word}",
                "{define long-pause pause 5s: meaning of long-pause}",
                "{define whisper span: meaning of whisper}",
                "{define sing-song span: meaning of sing-song}",
                "{define wave note: meaning of wave}",
            }, lines);
        }

        [Fact]
        public void EveryStarterIsValidAndTheNamesAreDistinct()
        {
            var starters = SpeechLibrary.Starters(name => "x");
            Assert.All(starters, s => Assert.Null(SpeechLibrary.Check(s, starters.Where(o => o != s))));
            Assert.Equal(starters.Count, starters.Select(s => s.Name).Distinct().Count());
            Assert.All(starters, s => Assert.Contains(s.Color, SpeechLibrary.Colors));
        }

        [Fact]
        public void NumbersAreWrittenWithADotWhateverTheRegionalSettingsAre()
        {
            var before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                var mark = Mark(percent: 62.5);
                mark.PerWord = 0.25;
                Assert.Equal("{define very-slow pace 62.5% +0.25s: about half speed}", SpeechLibrary.DefinitionLine(mark));
                var pause = new SpeechMark { Name = "long-pause", Kind = "pause", Seconds = 1.5, Meaning = "x" };
                Assert.Equal("{define long-pause pause 1.5s: x}", SpeechLibrary.DefinitionLine(pause));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        [Fact]
        public void TheMeaningGoesIntoTheLineAsOneCleanLine()
        {
            var mark = Mark(meaning: "  half {speed},\r\n every word   clear  ");
            Assert.Equal("{define very-slow pace 50%: half speed, every word clear}", SpeechLibrary.DefinitionLine(mark));
            // a colon in the meaning is fine: the editor reads the first one as the start of it
            Assert.Equal("{define very-slow pace 50%: say it: slowly}", SpeechLibrary.DefinitionLine(Mark(meaning: "say it: slowly")));
        }

        [Fact]
        public void ANameInCapitalsIsWrittenInLowerCase() =>
            Assert.StartsWith("{define whisper span:", SpeechLibrary.DefinitionLine(new SpeechMark { Name = " Whisper ", Kind = "span", Meaning = "x" }));

        [Theory]
        [InlineData("pace", "pace")]
        [InlineData("pause", "time")]
        [InlineData("span", "volume")]
        [InlineData("note", "cue")]
        public void TheGroupFollowsTheKindUnlessTheUserChoseOne(string kind, string group)
        {
            var mark = new SpeechMark { Kind = kind };
            Assert.Equal(group, SpeechLibrary.GroupOf(mark));
            mark.Group = "tone";
            Assert.Equal("tone", SpeechLibrary.GroupOf(mark));
            mark.Group = "nonsense";
            Assert.Equal(group, SpeechLibrary.GroupOf(mark));
        }

        [Fact]
        public void TheLibraryIsStoredAndReadBack()
        {
            var marks = SpeechLibrary.Starters(name => "meaning of " + name);
            marks[0].Pinned = true;
            var back = SpeechLibrary.Parse(SpeechLibrary.Serialize(marks));
            Assert.Equal(marks.Count, back.Count);
            Assert.Equal("very-slow", back[0].Name);
            Assert.True(back[0].Pinned);
            Assert.Equal(0.3, back[2].PerWord);
            Assert.Equal("blue", back[0].Color);
        }

        [Fact]
        public void DamagedOrBrokenEntriesAreLeftOutNotFatal()
        {
            Assert.Empty(SpeechLibrary.Parse(null));
            Assert.Empty(SpeechLibrary.Parse(""));
            Assert.Empty(SpeechLibrary.Parse("not json"));
            Assert.Empty(SpeechLibrary.Parse("{\"a\":1}"));
            var json = "[{\"Name\":\"ok-one\",\"Kind\":\"span\",\"Meaning\":\"fine\",\"Color\":\"mauve\",\"Icon\":\"?\"},"
                     + "{\"Name\":\"slow\",\"Kind\":\"span\",\"Meaning\":\"built in\"},"
                     + "{\"Name\":\"OK-ONE\",\"Kind\":\"span\",\"Meaning\":\"twice\"},"
                     + "{\"Name\":\"no-meaning\",\"Kind\":\"span\"},"
                     + "null]";
            var back = SpeechLibrary.Parse(json);
            var only = Assert.Single(back);
            Assert.Equal("ok-one", only.Name);
            // an unknown colour or symbol is dropped, the mark stays
            Assert.Equal("", only.Color);
            Assert.Equal("", only.Icon);
        }

        [Fact]
        public void AtMostSixtyMarksAreKept()
        {
            var many = Enumerable.Range(0, 70).Select(i => new SpeechMark { Name = "mark-" + i, Kind = "span", Meaning = "x" }).ToList();
            Assert.Equal(SpeechLibrary.MaxMarks, SpeechLibrary.Parse(SpeechLibrary.Serialize(many)).Count);
        }

        [Fact]
        public void TheEditorGetsTheColourAndSymbolOfTheMarksThatHaveOne()
        {
            var marks = new[]
            {
                new SpeechMark { Name = "whisper", Kind = "span", Meaning = "x", Color = "pink", Icon = "☾" },
                new SpeechMark { Name = "plain", Kind = "span", Meaning = "x" },
            };
            var styles = SpeechLibrary.Styles(marks);
            Assert.Equal(new[] { "whisper" }, styles.Keys.ToArray());
            Assert.Equal("{\"whisper\":{\"color\":\"pink\",\"icon\":\"☾\"}}", Newtonsoft.Json.JsonConvert.SerializeObject(styles));
        }

        [Fact]
        public void TheSymbolsAreDistinctSingleCharacters()
        {
            Assert.Equal(16, SpeechLibrary.Icons.Length);
            Assert.Equal(16, SpeechLibrary.Icons.Distinct().Count());
            Assert.All(SpeechLibrary.Icons, i => Assert.Equal(1, i.Length));
            Assert.Equal(8, SpeechLibrary.Colors.Length);
        }
    }
}
