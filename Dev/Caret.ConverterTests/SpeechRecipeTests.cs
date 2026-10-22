using System;
using System.Collections.Generic;
using System.Linq;
using Typedown.WinUI.Services;
using Xunit;

namespace Caret.ConverterTests
{
    // Recipes (several marks in one click) and the library as a file (Services/SpeechLibrary.cs). The templates in the first two
    // tests are those of Muya/lib/parser/speechRecipe.test.js: both sides must agree on what a recipe may hold.
    public class SpeechRecipeTests
    {
        private static readonly SpeechMark[] None = Array.Empty<SpeechMark>();

        private static SpeechMark Mine(string name, string kind = "span") => new() { Name = name, Kind = kind, Meaning = "x", Percent = 50, Seconds = 5 };

        [Theory]
        [InlineData("{pause 5s}{soft}{emphasis}{text}{/emphasis}{/soft}{wait}")]
        [InlineData("{tone: joke}{text}{/tone}{wait 3s: laugh}")]
        [InlineData("{beat}{cue: look up}")]
        [InlineData("{pause} and then {text} done {beat}")]
        [InlineData("{PAUSE 2S}{text}")]
        public void TemplatesThatAreRecipes(string template) => Assert.Null(SpeechLibrary.RecipeProblem(template, None));

        [Theory]
        [InlineData("{text}and{text}", "rtext")]
        [InlineData("{pa{text}use}", "runknown")]
        [InlineData("{pause 1{text}s}", "runknown")]
        [InlineData("{cue: look {text}up}", "runknown")]
        [InlineData("{soft}{/so{text}ft}", "runknown")]
        [InlineData("{{text}pause}", "runknown")]
        [InlineData("{pause{text}}", "runknown")]
        [InlineData("{pa{text}use}{text}", "rtext")]
        [InlineData("{soft}{text}", "ropen")]
        [InlineData("{soft}{loud}{text}{/soft}{/loud}", "ropen")]
        [InlineData("{text}{/soft}", "ropen")]
        [InlineData("{pauze}{text}", "runknown")]
        [InlineData("{pause fast}{text}", "runknown")]
        [InlineData("{pause 0.01s}{text}", "runknown")]
        [InlineData("{beat 2s}{text}", "runknown")]
        [InlineData("{cue}{text}", "runknown")]
        [InlineData("{text} {oops", "runknown")]
        [InlineData("{define x span: y}{text}", "rforbidden")]
        [InlineData("{wpm 140}{text}", "rforbidden")]
        [InlineData("{budget 3m}{text}", "rforbidden")]
        [InlineData("", "rempty")]
        [InlineData("{text}", "rempty")]
        [InlineData("   ", "rempty")]
        public void TemplatesThatAreNot(string template, string reason) => Assert.Equal(reason, SpeechLibrary.RecipeProblem(template, None));

        [Fact]
        public void WordsOfTheLibraryAreMarksInARecipe()
        {
            var library = new[] { Mine("whisper"), Mine("long-pause", "pause"), Mine("wave", "note"), Mine("very-slow", "pace") };
            Assert.Null(SpeechLibrary.RecipeProblem("{whisper}{text}{/whisper}{long-pause 8s}{wave}", library));
            Assert.Null(SpeechLibrary.RecipeProblem("{very-slow}{text}{/very-slow}", library));
            // a pair must be closed, a note takes no value, and a word that is not in the library is not a mark
            Assert.Equal("ropen", SpeechLibrary.RecipeProblem("{whisper}{text}", library));
            Assert.Equal("runknown", SpeechLibrary.RecipeProblem("{wave 2s}", library));
            Assert.Equal("runknown", SpeechLibrary.RecipeProblem("{whisper}{text}{/whisper}{mumble}", library));
        }

        [Fact]
        public void TheDefinitionsARecipeNeedsAreTheOnesOfTheLibraryWordsItUses()
        {
            var library = new[] { Mine("whisper"), Mine("long-pause", "pause"), Mine("unused") };
            var lines = SpeechLibrary.RecipeDefinitions("{pause}{whisper}{text}{/whisper}{long-pause}{whisper}x{/whisper}", library);
            Assert.Equal(new[]
            {
                "{define whisper span: x}",
                "{define long-pause pause 5s: x}",
            }, lines);
            Assert.Empty(SpeechLibrary.RecipeDefinitions("{pause}{soft}{text}{/soft}", library));
        }

        [Theory]
        [InlineData("Dramatic reveal", null)]
        [InlineData("  Dramatic   reveal  ", null)]
        [InlineData("", "rname")]
        [InlineData("   ", "rname")]
        [InlineData("A name that is far too long to be a name for a recipe", "rname")]
        public void RecipeNames(string name, string error) =>
            Assert.Equal(error, SpeechLibrary.CheckRecipe(new SpeechRecipe { Name = name, Template = "{pause}{text}" }, Array.Empty<SpeechRecipe>(), None));

        [Fact]
        public void ANameCannotBeUsedTwiceWhateverTheCase()
        {
            var others = new[] { new SpeechRecipe { Name = "Reveal", Template = "{beat}{text}" } };
            Assert.Equal("rtaken", SpeechLibrary.CheckRecipe(new SpeechRecipe { Name = " reveal ", Template = "{pause}{text}" }, others, None));
            Assert.Null(SpeechLibrary.CheckRecipe(new SpeechRecipe { Name = "reveal two", Template = "{pause}{text}" }, others, None));
        }

        [Fact]
        public void ThePreviewShowsWhatIsWritten()
        {
            Assert.Equal("{pause 5s}{soft}We lost the file.{/soft}", SpeechLibrary.RecipePreview("{pause 5s}{soft}{text}{/soft}", "We lost the file."));
            Assert.Equal("We lost the file. {beat}{cue: look up}", SpeechLibrary.RecipePreview("{beat}{cue: look up}", "We lost the file."));
        }

        [Fact]
        public void RecipesAreStoredAndReadBackWithoutTheOnesThatBreakARule()
        {
            var recipes = new[]
            {
                new SpeechRecipe { Name = "Reveal", Template = "{pause 5s}{soft}{text}{/soft}{wait}" },
                new SpeechRecipe { Name = "reveal", Template = "{beat}{text}" },
                new SpeechRecipe { Name = "Broken", Template = "{soft}{text}" },
                new SpeechRecipe { Name = "Joke", Template = "{tone: joke}{text}{/tone}{wait 3s: laugh}" },
            };
            var back = SpeechLibrary.ParseRecipes(SpeechLibrary.SerializeRecipes(recipes), None);
            Assert.Equal(new[] { "Reveal", "Joke" }, back.Select(r => r.Name).ToArray());
            Assert.Empty(SpeechLibrary.ParseRecipes(null, None));
            Assert.Empty(SpeechLibrary.ParseRecipes("not json", None));
        }

        [Fact]
        public void AtMostTwentyRecipesAreKept()
        {
            var many = Enumerable.Range(0, 30).Select(i => new SpeechRecipe { Name = "recipe " + i, Template = "{beat}{text}" });
            Assert.Equal(SpeechLibrary.MaxRecipes, SpeechLibrary.ParseRecipes(SpeechLibrary.SerializeRecipes(many), None).Count);
        }

        [Fact]
        public void TheLibraryIsExportedAndImportedBack()
        {
            var marks = SpeechLibrary.Starters(n => "meaning of " + n).Take(3).ToList();
            var recipes = new List<SpeechRecipe> { new() { Name = "Reveal", Template = "{very-slow}{text}{/very-slow}{wait}" } };
            var json = SpeechLibrary.Export(marks, recipes);
            Assert.Contains("\"format\": \"caret-speech-marks\"", json);

            var intoMarks = new List<SpeechMark>();
            var intoRecipes = new List<SpeechRecipe>();
            var result = SpeechLibrary.Import(json, intoMarks, intoRecipes);
            Assert.Equal(new SpeechLibrary.ImportResult(3, 1, 0), result);
            Assert.Equal(marks.Select(m => m.Name), intoMarks.Select(m => m.Name));
            Assert.Equal("Reveal", Assert.Single(intoRecipes).Name);
        }

        [Fact]
        public void WhatIsAlreadyThereIsLeftAndCounted()
        {
            var marks = SpeechLibrary.Starters(n => "meaning of " + n).Take(2).ToList();
            var json = SpeechLibrary.Export(SpeechLibrary.Starters(n => "other " + n).Take(4), new List<SpeechRecipe>());
            var result = SpeechLibrary.Import(json, marks, new List<SpeechRecipe>());
            Assert.Equal(new SpeechLibrary.ImportResult(2, 0, 2), result);
            Assert.Equal(4, marks.Count);
            // the ones that were there keep their own meaning
            Assert.Equal("meaning of very-slow", marks[0].Meaning);
        }

        [Fact]
        public void ARecipeThatUsesAWordOfTheFileIsAcceptedBecauseTheMarksComeFirst()
        {
            var json = "{\"format\":\"caret-speech-marks\",\"version\":1,"
                     + "\"marks\":[{\"Name\":\"whisper\",\"Kind\":\"span\",\"Meaning\":\"quiet\"}],"
                     + "\"recipes\":[{\"Name\":\"Secret\",\"Template\":\"{whisper}{text}{/whisper}\"},{\"Name\":\"Bad\",\"Template\":\"{nope}\"}]}";
            var marks = new List<SpeechMark>();
            var recipes = new List<SpeechRecipe>();
            Assert.Equal(new SpeechLibrary.ImportResult(1, 1, 1), SpeechLibrary.Import(json, marks, recipes));
            Assert.Equal("Secret", recipes[0].Name);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("")]
        [InlineData("[1,2]")]
        [InlineData("{\"format\":\"something-else\",\"marks\":[]}")]
        [InlineData("{\"marks\":[]}")]
        public void AFileThatIsNotOneOfOursIsRefused(string json) =>
            Assert.Throws<FormatException>(() => SpeechLibrary.Import(json, new List<SpeechMark>(), new List<SpeechRecipe>()));

        [Fact]
        public void BrokenEntriesInAFileAreSkippedNotFatal()
        {
            var json = "{\"format\":\"caret-speech-marks\",\"marks\":[null,{\"Name\":\"slow\",\"Kind\":\"span\",\"Meaning\":\"built in\"},"
                     + "{\"Name\":\"fine-one\",\"Kind\":\"span\",\"Meaning\":\"ok\",\"Color\":\"mauve\"},42],\"recipes\":[null,{\"Name\":\"\",\"Template\":\"{beat}\"}]}";
            var marks = new List<SpeechMark>();
            var result = SpeechLibrary.Import(json, marks, new List<SpeechRecipe>());
            Assert.Equal(1, result.Marks);
            Assert.Equal(5, result.Skipped);
            Assert.Equal("", marks[0].Color);
        }

        [Fact]
        public void TheFileHoldsNothingButTheLibrary()
        {
            // no paths, no names of the user or the machine: marks and recipes, a format and a version
            var json = SpeechLibrary.Export(SpeechLibrary.Starters(n => "m"), new List<SpeechRecipe>());
            var keys = Newtonsoft.Json.Linq.JObject.Parse(json).Properties().Select(p => p.Name).ToArray();
            Assert.Equal(new[] { "format", "version", "marks", "recipes" }, keys);
        }
    }
}
