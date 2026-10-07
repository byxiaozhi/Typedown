using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Typedown.WinUI.Services
{
    // New since the fork: spell checking with Windows' own spell checker (ISpellChecker, spellcheck.h).
    //
    // It works offline, in every language Windows has a dictionary for (a language added in Settings > Time &
    // language > Language & region brings its dictionary), shares the user's own word list with other Windows
    // apps, and needs nothing bundled or downloaded; no AI is involved. The editor page finds the words and
    // draws the underlines (MainWindow.Spellcheck.cs); this class only answers "is this word wrong?" and "what
    // could it be?". A word is wrong only if it is wrong in *every* language in use, so a French text with an
    // English sentence in it is not marked all over.
    //
    // Plain .NET (no WinUI), so the tests in Caret.ConverterTests compile it as it is.
    internal sealed class SpellChecking
    {
        private static readonly Guid FactoryClass = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

        private readonly object gate = new();
        private readonly ISpellCheckerFactory factory;
        private readonly Dictionary<string, ISpellChecker> checkers = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> SupportedLanguages { get; }

        private SpellChecking(ISpellCheckerFactory factory, List<string> languages)
        {
            this.factory = factory;
            SupportedLanguages = languages;
        }

        // Null when this Windows has no spell checking at all (a Server Core, a stripped image).
        public static SpellChecking Create()
        {
            try
            {
                var type = Type.GetTypeFromCLSID(FactoryClass);
                if (type == null || Activator.CreateInstance(type) is not ISpellCheckerFactory factory) return null;
                var languages = new List<string>();
                var list = factory.get_SupportedLanguages();
                while (list.Next(1, out var tag, out var fetched) == 0 && fetched == 1) languages.Add(tag);
                return languages.Count == 0 ? null : new SpellChecking(factory, languages);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool Supports(string tag) => !string.IsNullOrEmpty(tag) && SupportedLanguages.Contains(tag, StringComparer.OrdinalIgnoreCase);

        // The languages to check in. A chosen language (Settings) when Windows has its dictionary; otherwise, in
        // order, each of the preferred languages (the user's Windows languages and the app's own) that has one:
        // the exact tag, or another region of the same language (fr-CA for fr-FR). Only a language Windows can
        // really build a checker for counts (it can list one it then fails to create): the Settings card says
        // "using" for what is returned, and a chosen language that cannot be built falls back to the preferred ones.
        public IReadOnlyList<string> Languages(string chosen, IEnumerable<string> preferred)
        {
            lock (gate)
            {
                var selected = SupportedLanguages.FirstOrDefault(l => string.Equals(l, chosen, StringComparison.OrdinalIgnoreCase));
                if (selected != null && Checker(selected) != null) return new[] { selected };
                var result = new List<string>();
                foreach (var tag in preferred ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    var primary = tag.Split('-')[0];
                    // The exact tag first, then the other regions of the language; the first of them that can be built.
                    var match = SupportedLanguages.Where(l => string.Equals(l, tag, StringComparison.OrdinalIgnoreCase))
                        .Concat(SupportedLanguages.Where(l => string.Equals(l.Split('-')[0], primary, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault(l => Checker(l) != null);
                    if (match != null && !result.Contains(match, StringComparer.OrdinalIgnoreCase)) result.Add(match);
                }
                return result;
            }
        }

        // Words that are wrong in every one of the languages. Words that are not worth checking are never wrong.
        public HashSet<string> Misspelled(IEnumerable<string> words, IReadOnlyList<string> languages)
        {
            var wrong = new HashSet<string>(StringComparer.Ordinal);
            if (languages.Count == 0) return wrong;
            lock (gate)
            {
                var active = languages.Select(Checker).Where(c => c != null).ToList();
                if (active.Count == 0) return wrong;
                foreach (var word in words)
                    if (IsCheckable(word) && active.All(c => IsWrong(c, word))) wrong.Add(word);
            }
            return wrong;
        }

        // Up to `max` suggestions for a wrong word: the best ones of the first language that has any (most of the
        // list, as a French text is not helped by Hungarian guesses), then the best of the others.
        public IReadOnlyList<string> Suggest(string word, IReadOnlyList<string> languages, int max = 6)
        {
            var found = new List<string>();
            if (!IsCheckable(word)) return found;
            lock (gate)
            {
                var lists = languages.Select(Checker).Where(c => c != null && IsWrong(c, word)).Select(c => Suggestions(c, word, max)).Where(l => l.Count > 0).ToList();
                if (lists.Count == 0) return found;
                var primary = lists.Count == 1 ? max : Math.Max(1, max - 2);
                foreach (var suggestion in lists[0])
                    if (found.Count < primary && !found.Contains(suggestion, StringComparer.Ordinal)) found.Add(suggestion);
                for (var rank = 0; found.Count < max && lists.Skip(1).Any(l => rank < l.Count); rank++)
                    foreach (var list in lists.Skip(1))
                        if (rank < list.Count && found.Count < max && !found.Contains(list[rank], StringComparer.Ordinal)) found.Add(list[rank]);
                for (var rank = primary; found.Count < max && rank < lists[0].Count; rank++)
                    if (!found.Contains(lists[0][rank], StringComparer.Ordinal)) found.Add(lists[0][rank]);
            }
            return found;
        }

        // Into the user's Windows word list, for the languages in use: the word is then right here and in other
        // Windows apps.
        public void Add(string word, IReadOnlyList<string> languages)
        {
            lock (gate)
                foreach (var checker in languages.Select(Checker).Where(c => c != null))
                    try { checker.Add(word); } catch (Exception) { }
        }

        // For this session only.
        public void Ignore(string word, IReadOnlyList<string> languages)
        {
            lock (gate)
                foreach (var checker in languages.Select(Checker).Where(c => c != null))
                    try { checker.Ignore(word); } catch (Exception) { }
        }

        // Names, acronyms, code and numbers are not what a spell checker is for: a word of letters (and
        // apostrophes inside it) that is not all capitals and has no capital in its middle.
        public static bool IsCheckable(string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2 || word.Length > 40) return false;
            var hasLower = false;
            var hasUpper = false;
            for (var i = 0; i < word.Length; i++)
            {
                var c = word[i];
                if (c is '\'' or '’')
                {
                    if (i == 0 || i == word.Length - 1) return false;
                    continue;
                }
                if (!char.IsLetter(c) && !IsMark(c)) return false;
                if (char.IsUpper(c))
                {
                    if (hasLower) return false; // iPhone, GitHub: an identifier, not a word
                    hasUpper = true;
                }
                else if (char.IsLower(c)) hasLower = true;
            }
            return hasLower || !hasUpper; // ALL CAPITALS is an acronym
        }

        private static bool IsMark(char c) => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark or System.Globalization.UnicodeCategory.EnclosingMark;

        private ISpellChecker Checker(string tag)
        {
            if (checkers.TryGetValue(tag, out var known)) return known;
            ISpellChecker checker = null;
            try { checker = factory.IsSupported(tag) != 0 ? factory.CreateSpellChecker(tag) : null; }
            catch (Exception) { }
            checkers[tag] = checker;
            return checker;
        }

        private static bool IsWrong(ISpellChecker checker, string word)
        {
            try
            {
                var errors = checker.Check(word);
                return errors.Next(out var error) == 0 && error != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static List<string> Suggestions(ISpellChecker checker, string word, int max)
        {
            var list = new List<string>();
            try
            {
                var found = checker.Suggest(word);
                while (list.Count < max && found.Next(1, out var suggestion, out var fetched) == 0 && fetched == 1) list.Add(suggestion);
            }
            catch (Exception) { }
            return list;
        }

        // spellcheck.h
        [ComImport, Guid("00000101-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumString
        {
            [PreserveSig] int Next(int count, [MarshalAs(UnmanagedType.LPWStr)] out string item, out int fetched);
            [PreserveSig] int Skip(int count);
            [PreserveSig] int Reset();
            [PreserveSig] int Clone(out IEnumString clone);
        }

        [ComImport, Guid("803E3BD4-2828-4410-8290-418D1D73C762"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumSpellingError
        {
            [PreserveSig] int Next(out ISpellingError error);
        }

        [ComImport, Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISpellingError
        {
            uint get_StartIndex();
            uint get_Length();
            uint get_CorrectiveAction();
            [return: MarshalAs(UnmanagedType.LPWStr)] string get_Replacement();
        }

        [ComImport, Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISpellChecker
        {
            [return: MarshalAs(UnmanagedType.LPWStr)] string get_LanguageTag();
            IEnumSpellingError Check([MarshalAs(UnmanagedType.LPWStr)] string text);
            IEnumString Suggest([MarshalAs(UnmanagedType.LPWStr)] string word);
            void Add([MarshalAs(UnmanagedType.LPWStr)] string word);
            void Ignore([MarshalAs(UnmanagedType.LPWStr)] string word);
            void AutoCorrect([MarshalAs(UnmanagedType.LPWStr)] string from, [MarshalAs(UnmanagedType.LPWStr)] string to);
        }

        [ComImport, Guid("8E018A9D-2415-4677-BF08-794EA61F94BB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISpellCheckerFactory
        {
            IEnumString get_SupportedLanguages();
            int IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
            ISpellChecker CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
        }
    }
}
