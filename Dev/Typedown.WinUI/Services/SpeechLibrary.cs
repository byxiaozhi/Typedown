using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Typedown.WinUI.Services
{
    // New since the fork: the user's own speech marks (docs/speech-marks-design.md, 3.7 and 5.5). A mark is a word the
    // user invents (very-slow, word-by-word, whisper) with one of four kinds that Caret can calculate or draw: `pace`
    // (a speed, and seconds added after every word), `pause` (a length), `span` (a style and a meaning) and `note`
    // (a cue). The library lives in the user's Settings; a document never depends on it: using a mark writes its
    // `{define ...}` line into the document (the page does that), so the file always says what its marks mean.
    //
    // The rules here are those of the editor (Typedown.Editor, Muya/lib/parser/speech.js `parseDefine`); the tests check
    // that the lines written here are ones the editor accepts. Plain .NET (no WinUI), so Caret.ConverterTests compiles
    // it as it is.
    internal sealed class SpeechMark
    {
        public string Name { get; set; } = "";
        // pace | pause | span | note
        public string Kind { get; set; } = "span";
        // pace: the speed as a percentage of the speaker's baseline, and the seconds added after every word
        public double Percent { get; set; } = 100;
        public double PerWord { get; set; }
        // pause: the length
        public double Seconds { get; set; } = 1;
        // one line in the user's own words: what an AI reads, and what the legend of "Copy for AI" prints
        public string Meaning { get; set; } = "";
        // where it is listed: time | pace | volume | tone | cue (empty: by kind)
        public string Group { get; set; } = "";
        // how it is drawn (not in the file): one of SpeechLibrary.Colors and one of SpeechLibrary.Icons, or empty
        public string Color { get; set; } = "";
        public string Icon { get; set; } = "";
        // listed in the Mine group
        public bool Pinned { get; set; }

        public SpeechMark Clone() => (SpeechMark)MemberwiseClone();
    }

    internal static class SpeechLibrary
    {
        public const int MaxMarks = 60;
        public const double PaceMin = 10, PaceMax = 300, PerWordMax = 2, PauseMin = 0.1, PauseMax = 600;

        public static readonly string[] Kinds = { "pace", "pause", "span", "note" };
        public static readonly string[] Groups = { "time", "pace", "volume", "tone", "cue" };
        public static readonly string[] Colors = { "red", "orange", "yellow", "green", "teal", "blue", "purple", "pink" };
        // Plain symbols, drawn by the font of the system: a star, notes, a face, a flag, a pencil, a check, sparkles...
        public static readonly string[] Icons =
        {
            "★", "♪", "♫", "☺", "⚑", "✎", "✓", "✦", "❖", "♥", "➜", "◆", "●", "▲", "✚", "☾",
        };

        // The words of the built-in set: a user's word cannot be one of them (a document can only change their numbers).
        public static readonly HashSet<string> BuiltInWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "beat", "pause", "wait", "cue", "wpm", "budget", "slow", "fast", "loud", "soft", "emphasis", "tone", "define",
        };

        private static readonly Regex NameRule = new("^[a-z][a-z0-9-]{1,23}$", RegexOptions.Compiled);

        public static string GroupOf(SpeechMark mark) =>
            Array.IndexOf(Groups, mark.Group) >= 0 ? mark.Group : mark.Kind switch { "pace" => "pace", "pause" => "time", "note" => "cue", _ => "volume" };

        // The meaning as it goes into a document: one line, and none of the characters the markup is made of.
        public static string CleanMeaning(string meaning) =>
            Regex.Replace((meaning ?? "").Replace("{", "").Replace("}", ""), @"\s+", " ").Trim();

        public static string CleanName(string name) => (name ?? "").Trim().ToLowerInvariant();

        // Why a mark cannot be kept, as a short code the page of settings turns into words, or null when it can.
        // `others` are the other marks of the library (a mark being edited is not among them).
        public static string Check(SpeechMark mark, IEnumerable<SpeechMark> others)
        {
            var name = CleanName(mark.Name);
            if (!NameRule.IsMatch(name)) return "name";
            if (BuiltInWords.Contains(name)) return "builtin";
            if (others.Any(o => string.Equals(CleanName(o.Name), name, StringComparison.Ordinal))) return "taken";
            if (CleanMeaning(mark.Meaning).Length == 0) return "meaning";
            if (Array.IndexOf(Kinds, mark.Kind) < 0) return "kind";
            if (mark.Kind == "pace")
            {
                if (!(mark.Percent >= PaceMin && mark.Percent <= PaceMax)) return "pace";
                if (!(mark.PerWord >= 0 && mark.PerWord <= PerWordMax)) return "perword";
            }
            if (mark.Kind == "pause" && !(mark.Seconds >= PauseMin && mark.Seconds <= PauseMax)) return "pause";
            return null;
        }

        // A number as the file has it: a dot, no trailing zeros, whatever the regional settings are.
        public static string Number(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

        // `{define very-slow pace 50%: about half speed}`: the line a document carries for the mark.
        public static string DefinitionLine(SpeechMark mark)
        {
            var value = mark.Kind switch
            {
                "pace" => " " + Number(mark.Percent) + "%" + (mark.PerWord > 0 ? " +" + Number(mark.PerWord) + "s" : ""),
                "pause" => " " + Number(mark.Seconds) + "s",
                _ => "",
            };
            return "{define " + CleanName(mark.Name) + " " + mark.Kind + value + ": " + CleanMeaning(mark.Meaning) + "}";
        }

        // The marks offered as a starting set; `meaning` gives the words of a mark in the language of the interface.
        public static IReadOnlyList<SpeechMark> Starters(Func<string, string> meaning) => new[]
        {
            new SpeechMark { Name = "very-slow", Kind = "pace", Percent = 50, Meaning = meaning("very-slow"), Color = "blue" },
            new SpeechMark { Name = "very-fast", Kind = "pace", Percent = 160, Meaning = meaning("very-fast"), Color = "orange" },
            new SpeechMark { Name = "word-by-word", Kind = "pace", Percent = 60, PerWord = 0.3, Meaning = meaning("word-by-word"), Color = "purple" },
            new SpeechMark { Name = "long-pause", Kind = "pause", Seconds = 5, Meaning = meaning("long-pause"), Color = "teal" },
            new SpeechMark { Name = "whisper", Kind = "span", Meaning = meaning("whisper"), Color = "pink", Icon = "☾" },
            new SpeechMark { Name = "sing-song", Kind = "span", Meaning = meaning("sing-song"), Color = "green", Icon = "♪" },
            new SpeechMark { Name = "wave", Kind = "note", Meaning = meaning("wave"), Color = "yellow", Icon = "⚑" },
        };

        public static string Serialize(IEnumerable<SpeechMark> marks) => JsonConvert.SerializeObject(marks);

        // The library as stored. Anything that cannot be kept (damaged text, a mark that breaks a rule, a name used twice,
        // more than MaxMarks) is left out, so a hand-edited or damaged Settings file never breaks the card.
        public static List<SpeechMark> Parse(string json)
        {
            var result = new List<SpeechMark>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            List<SpeechMark> stored;
            try { stored = JsonConvert.DeserializeObject<List<SpeechMark>>(json); }
            catch (JsonException) { return result; }
            foreach (var mark in stored ?? new List<SpeechMark>())
            {
                if (mark == null || result.Count >= MaxMarks) continue;
                mark.Name = CleanName(mark.Name);
                mark.Meaning = CleanMeaning(mark.Meaning);
                if (Array.IndexOf(Colors, mark.Color) < 0) mark.Color = "";
                if (Array.IndexOf(Icons, mark.Icon) < 0) mark.Icon = "";
                if (Array.IndexOf(Groups, mark.Group) < 0) mark.Group = "";
                if (Check(mark, result) == null) result.Add(mark);
            }
            return result;
        }

        // What the page of the editor needs to draw the marks the way the user chose: { name: { color, icon } }.
        public static Dictionary<string, object> Styles(IEnumerable<SpeechMark> marks) =>
            marks.Where(m => m.Color != "" || m.Icon != "").ToDictionary(m => m.Name, m => (object)new { color = m.Color, icon = m.Icon });

        // --- recipes: several marks in one click ---

        public const int MaxRecipes = 20;
        public const string TextPlaceholder = "{text}";

        public static string CleanRecipeName(string name) => Regex.Replace(name ?? "", @"\s+", " ").Trim();

        // A mark of Caret: how it is used (a pair or a single mark), whether a value and a note are allowed. The settings
        // ({wpm}, {budget}) and definitions are not allowed in a recipe.
        private sealed record Word(bool Pair, bool Value, bool NoteRequired);

        private static readonly Dictionary<string, Word> BuiltInUse = new(StringComparer.OrdinalIgnoreCase)
        {
            ["beat"] = new(false, false, false),
            ["pause"] = new(false, true, false),
            ["wait"] = new(false, true, false),
            ["cue"] = new(false, false, true),
            ["slow"] = new(true, false, false),
            ["fast"] = new(true, false, false),
            ["loud"] = new(true, false, false),
            ["soft"] = new(true, false, false),
            ["emphasis"] = new(true, false, false),
            ["tone"] = new(true, false, true),
        };

        private static readonly Regex Token = new(@"\{(/?)([A-Za-z][A-Za-z0-9-]*)(?: ([^{}\n:]*))?(?::[ ]?([^{}\n]*))?\}", RegexOptions.Compiled);
        private static readonly Regex DurationRule = new(@"^(?:(\d+(?:[.,]\d+)?)m)?(?:(\d+(?:[.,]\d+)?)s)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static double? DurationSeconds(string text)
        {
            var m = DurationRule.Match((text ?? "").Trim());
            if (!m.Success || (!m.Groups[1].Success && !m.Groups[2].Success)) return null;
            double Part(Group g) => g.Success ? double.Parse(g.Value.Replace(',', '.'), CultureInfo.InvariantCulture) : 0;
            return Math.Round(Part(m.Groups[1]) * 60 + Part(m.Groups[2]), 3);
        }

        // What a word of the library is, as a recipe uses it.
        private static Word UseOf(SpeechMark mark) => mark.Kind switch
        {
            "pace" or "span" => new Word(true, false, false),
            "pause" => new Word(false, true, false),
            _ => new Word(false, false, false),
        };

        // Why a template cannot be a recipe, or null when it can. The same rules the editor applies when the recipe is used:
        // only marks of Caret or of the library, a value and a note where the word allows them, every pair closed in order,
        // no definitions or settings, and {text} at most once (it stands for the text the recipe is applied to).
        public static string RecipeProblem(string template, IEnumerable<SpeechMark> marks)
        {
            template ??= "";
            var library = marks.ToDictionary(m => CleanName(m.Name), m => m);
            var text = template.IndexOf(TextPlaceholder, StringComparison.Ordinal);
            if (text >= 0 && template.IndexOf(TextPlaceholder, text + 1, StringComparison.Ordinal) >= 0) return "rtext";
            if (text >= 0)
            {
                // Removing {text} must not join two incomplete pieces into a mark.
                foreach (var part in new[] { template.Substring(0, text), template.Substring(text + TextPlaceholder.Length) })
                    if (Token.Replace(part, "").IndexOfAny(new[] { '{', '}' }) >= 0) return "runknown";
            }
            var rest = template.Replace(TextPlaceholder, "");
            var stack = new Stack<string>();
            var seen = 0;
            var cut = new System.Text.StringBuilder();
            var last = 0;
            foreach (Match token in Token.Matches(rest))
            {
                cut.Append(rest, last, token.Index - last);
                last = token.Index + token.Length;
                var closing = token.Groups[1].Value == "/";
                var name = token.Groups[2].Value.ToLowerInvariant();
                var value = token.Groups[3].Success ? token.Groups[3].Value.Trim() : "";
                var note = token.Groups[4].Success ? token.Groups[4].Value.Trim() : null;
                if (name is "define" or "wpm" or "budget") return "rforbidden";
                Word word;
                if (BuiltInUse.TryGetValue(name, out var built)) word = built;
                else if (library.TryGetValue(name, out var mine)) word = UseOf(mine);
                else return "runknown";
                seen++;
                if (closing)
                {
                    if (!word.Pair || value != "" || note != null || stack.Count == 0 || stack.Pop() != name) return "ropen";
                    continue;
                }
                if (value != "")
                {
                    if (!word.Value) return "runknown";
                    var seconds = DurationSeconds(value);
                    if (seconds == null || seconds < PauseMin || seconds > PauseMax) return "runknown";
                }
                if (word.NoteRequired && string.IsNullOrWhiteSpace(note)) return "runknown";
                if (word.Pair) stack.Push(name);
            }
            cut.Append(rest, last, rest.Length - last);
            // a brace that is not part of a mark
            if (cut.ToString().IndexOfAny(new[] { '{', '}' }) >= 0) return "runknown";
            if (stack.Count > 0) return "ropen";
            return seen == 0 ? "rempty" : null;
        }

        // Why a recipe cannot be kept, or null. `others` are the other recipes, `marks` the library.
        public static string CheckRecipe(SpeechRecipe recipe, IEnumerable<SpeechRecipe> others, IEnumerable<SpeechMark> marks)
        {
            var name = CleanRecipeName(recipe.Name);
            if (name.Length < 1 || name.Length > 40) return "rname";
            if (others.Any(o => string.Equals(CleanRecipeName(o.Name), name, StringComparison.CurrentCultureIgnoreCase))) return "rtaken";
            return RecipeProblem(recipe.Template, marks);
        }

        // The definition lines the document needs when the recipe uses words of the library.
        public static List<string> RecipeDefinitions(string template, IEnumerable<SpeechMark> marks)
        {
            var used = new List<string>();
            var library = marks.ToDictionary(m => CleanName(m.Name), m => m);
            foreach (Match token in Token.Matches((template ?? "").Replace(TextPlaceholder, "")))
            {
                var name = token.Groups[2].Value.ToLowerInvariant();
                if (library.TryGetValue(name, out var mark) && !used.Contains(name)) used.Add(name);
            }
            return used.Select(n => DefinitionLine(library[n])).ToList();
        }

        // What the recipe writes on a sample sentence: the text goes where {text} is; a recipe without it is written after the
        // sample, as it would be at a caret there.
        public static string RecipePreview(string template, string sample) =>
            (template ?? "").Contains(TextPlaceholder) ? template.Replace(TextPlaceholder, sample) : sample + " " + template;

        public static string SerializeRecipes(IEnumerable<SpeechRecipe> recipes) => JsonConvert.SerializeObject(recipes);

        // The recipes as stored; ones that break a rule, repeat a name or are over the limit are left out.
        public static List<SpeechRecipe> ParseRecipes(string json, IEnumerable<SpeechMark> marks)
        {
            var result = new List<SpeechRecipe>();
            if (string.IsNullOrWhiteSpace(json)) return result;
            List<SpeechRecipe> stored;
            try { stored = JsonConvert.DeserializeObject<List<SpeechRecipe>>(json); }
            catch (JsonException) { return result; }
            var library = marks.ToList();
            foreach (var recipe in stored ?? new List<SpeechRecipe>())
            {
                if (recipe == null || result.Count >= MaxRecipes) continue;
                recipe.Name = CleanRecipeName(recipe.Name);
                recipe.Template = (recipe.Template ?? "").Trim();
                if (CheckRecipe(recipe, result, library) == null) result.Add(recipe);
            }
            return result;
        }

        // --- the library as a file ---

        public const string FileFormat = "caret-speech-marks";

        // The marks and recipes as a file the user can keep or give to a colleague (plain JSON, a local file).
        public static string Export(IEnumerable<SpeechMark> marks, IEnumerable<SpeechRecipe> recipes) =>
            JsonConvert.SerializeObject(new { format = FileFormat, version = 1, marks, recipes }, Formatting.Indented);

        public sealed record ImportResult(int Marks, int Recipes, int Skipped);

        // Adds what a file holds to the library, leaving what is already there: a mark or recipe that breaks a rule, repeats a
        // name or does not fit under the limit is skipped and counted. Throws FormatException when the file is not one of ours.
        public static ImportResult Import(string json, List<SpeechMark> marks, List<SpeechRecipe> recipes)
        {
            JObject file;
            try { file = JObject.Parse(json); }
            catch (JsonException ex) { throw new FormatException("not a speech marks file", ex); }
            if (file["format"]?.ToString() != FileFormat) throw new FormatException("not a speech marks file");
            int addedMarks = 0, addedRecipes = 0, skipped = 0;
            foreach (var token in file["marks"] as JArray ?? new JArray())
            {
                SpeechMark mark;
                try { mark = token.ToObject<SpeechMark>(); }
                catch (Exception) { mark = null; }
                if (mark == null) { skipped++; continue; }
                mark.Name = CleanName(mark.Name);
                mark.Meaning = CleanMeaning(mark.Meaning);
                if (Array.IndexOf(Colors, mark.Color) < 0) mark.Color = "";
                if (Array.IndexOf(Icons, mark.Icon) < 0) mark.Icon = "";
                if (Array.IndexOf(Groups, mark.Group) < 0) mark.Group = "";
                if (marks.Count >= MaxMarks || Check(mark, marks) != null) { skipped++; continue; }
                marks.Add(mark);
                addedMarks++;
            }
            foreach (var token in file["recipes"] as JArray ?? new JArray())
            {
                SpeechRecipe recipe;
                try { recipe = token.ToObject<SpeechRecipe>(); }
                catch (Exception) { recipe = null; }
                if (recipe == null) { skipped++; continue; }
                recipe.Name = CleanRecipeName(recipe.Name);
                recipe.Template = (recipe.Template ?? "").Trim();
                if (recipes.Count >= MaxRecipes || CheckRecipe(recipe, recipes, marks) != null) { skipped++; continue; }
                recipes.Add(recipe);
                addedRecipes++;
            }
            return new ImportResult(addedMarks, addedRecipes, skipped);
        }
    }

    internal sealed class SpeechRecipe
    {
        public string Name { get; set; } = "";
        // marks in one template; {text} stands for the text the recipe is applied to
        public string Template { get; set; } = "";

        public SpeechRecipe Clone() => (SpeechRecipe)MemberwiseClone();
    }
}
