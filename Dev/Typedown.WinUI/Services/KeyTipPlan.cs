using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Typedown.WinUI.Services
{
    // New since the fork: the key tips (Alt shows a small badge with a key on the menus, the groups of the toolbar and the items of the
    // sidebar; the key does what a click would). Which key goes where is decided here, without a window, so that it can be tested: one letter
    // or digit for each place, none twice, the same in the same language every time.
    public static class KeyTipPlan
    {
        public sealed record Request(string Id, string Label, string Preferred = null);

        // The menus keep the keys they have always had (Alt+F is File where the menus are in English; each language has its own), then every
        // other place takes the first letter of its name that is still free, then any other letter of it, then the first free letter or digit.
        public static IReadOnlyDictionary<string, string> Assign(IReadOnlyList<Request> requests)
        {
            var result = new Dictionary<string, string>();
            var used = new HashSet<char>();
            foreach (var request in requests.Where(r => Key(r.Preferred) is { } key && !used.Contains(key)))
            {
                var key = Key(request.Preferred).Value;
                if (result.ContainsKey(request.Id)) continue;
                used.Add(key);
                result[request.Id] = key.ToString();
            }
            foreach (var request in requests.Where(r => !result.ContainsKey(r.Id)))
            {
                var candidates = Letters(request.Label).Concat("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890");
                var key = candidates.FirstOrDefault(c => !used.Contains(c));
                if (key == default) continue; // more places than keys: this one has no tip
                used.Add(key);
                result[request.Id] = key.ToString();
            }
            return result;
        }

        // The letters and digits of a name, in order, upper case and without accents (É is E); anything that has no plain key is left out.
        public static IEnumerable<char> Letters(string text)
        {
            foreach (var c in text ?? "")
                if (Key(c.ToString()) is { } key) yield return key;
        }

        // "f" -> F, "É" -> E, "" or a letter with no plain form (Ł) -> null
        public static char? Key(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var plain = new string(text.Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
            if (plain.Length != 1) return null;
            var key = char.ToUpperInvariant(plain[0]);
            return key is >= 'A' and <= 'Z' or >= '0' and <= '9' ? key : null;
        }
    }
}
