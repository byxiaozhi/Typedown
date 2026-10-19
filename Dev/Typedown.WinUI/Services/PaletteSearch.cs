using System;

namespace Typedown.WinUI.Services
{
    // How the command palette (Ctrl+Shift+P) matches what is typed against a command. Plain .NET, so that it is tested
    // without the window (Caret.ConverterTests).
    public static class PaletteSearch
    {
        // The score of a command for the typed words, or -1 when one of the words is not in it. Every word counts on its own:
        // a word that is in the title as it stands scores by where it is (the start of a word best), a word that is only in
        // the menu name scores low, and a word that is there letter by letter in the title (in order) scores lowest.
        // Shorter titles come first among equals.
        public static int Score(string query, string title, string category)
        {
            var words = (query ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0;
            var lowerTitle = (title ?? "").ToLowerInvariant();
            var lowerCategory = (category ?? "").ToLowerInvariant();
            var score = 0;
            foreach (var word in words)
            {
                // a word in the title: the start of a word is worth most, a stretch inside a word (the "view" of "preview") little
                var at = IndexOfWord(lowerTitle, word);
                if (at >= 0)
                {
                    score += 260 - Math.Min(at, 100);
                    continue;
                }
                at = lowerTitle.IndexOf(word, StringComparison.Ordinal);
                if (at >= 0)
                {
                    score += 100 - Math.Min(at, 60);
                    continue;
                }
                // in the name of the menu (or sub-menu) the command is in
                if (IndexOfWord(lowerCategory, word) >= 0)
                {
                    score += 150;
                    continue;
                }
                if (lowerCategory.Contains(word, StringComparison.Ordinal))
                {
                    score += 50;
                    continue;
                }
                var position = 0;
                foreach (var letter in word)
                {
                    position = lowerTitle.IndexOf(letter, position);
                    if (position < 0) return -1;
                    position++;
                }
                score += 20;
            }
            return score - Math.Min(lowerTitle.Length, 60) / 4;
        }

        // Where the word starts a word of the text (after a space, a bracket, a dash...), or -1.
        private static int IndexOfWord(string text, string word)
        {
            for (var at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
                if (at == 0 || !char.IsLetterOrDigit(text[at - 1])) return at;
            return -1;
        }
    }
}
