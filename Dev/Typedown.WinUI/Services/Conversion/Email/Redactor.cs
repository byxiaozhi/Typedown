using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // Masking personal data before the text is pasted into an assistant.
    //
    // Everything is pattern matching, with checksums where the format has one (IBAN, card numbers, PESEL,
    // DNI/NIE, French NIR), so a random order number is not mistaken for an ID. People are masked by name
    // when the name is known: from the mail's own headers, from a greeting ("Hi Daniel,") or a sign-off
    // ("Kind regards," then "Anna Nowak"), plus any list the user supplies. A name that appears only in
    // running text ("ask Marta from finance") is not found — that would take a language model, which this
    // deliberately does not use.
    //
    // The same value always gets the same placeholder within one document ("[PERSON-2]" is the same person
    // everywhere), so the thread stays readable.
    internal sealed class Redactor
    {
        private static readonly Regex EmailPattern = new(@"(?<![\w.+\-])[\w.%+\-']+@[\w\-]+(?:\.[\w\-]+)*\.[A-Za-z]{2,}(?![\w\-])");
        private static readonly Regex Iban = new(@"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,4})?\b");
        private static readonly Regex Card = new(@"(?<![\w\-])\d(?:[ \-]?\d){12,18}(?![\w\-])");
        private static readonly Regex Pesel = new(@"(?<![\w\-])\d{11}(?![\w\-])");
        private static readonly Regex Nir = new(@"(?<![\w\-])[12][ ]?\d{2}[ ]?(?:0[1-9]|1[0-2]|[2-9]\d)[ ]?(?:\d{2}|2A|2B)[ ]?\d{3}[ ]?\d{3}[ ]?\d{2}(?![\w\-])");
        private static readonly Regex Dni = new(@"(?<![\w\-])\d{8}[ \-]?[A-Z](?![\w\-])");
        private static readonly Regex Nie = new(@"(?<![\w\-])[XYZ][ \-]?\d{7}[ \-]?[A-Z](?![\w\-])");
        private static readonly Regex Nino = new(@"(?<![\w\-])(?!BG|GB|NK|KN|TN|NT|ZZ)[A-CEGHJ-PR-TW-Z][A-CEGHJ-NPR-TW-Z] ?\d{2} ?\d{2} ?\d{2} ?[A-D](?![\w\-])");
        private static readonly Regex PhoneInternational = new(@"(?<![\w+])(?:\+|00)\d{1,3}(?:[ .\-]?\(?\d{1,4}\)?){2,6}(?![\w])");
        // "100 000 000 EUR" is an amount written with thousands separators, not a phone number
        private const string NotAmount = @"(?![  ]?(?:€|EUR|PLN|zł|USD|\$|£|GBP|CHF|[.,]\d))";
        private static readonly Regex[] PhoneNational =
        {
            new(@"(?<![\w+\-./])0[1-9](?:[ .\-]?\d{2}){4}(?![\w\-])"), // FR 01 23 45 67 89
            new(@"(?<![\w+\-./])0\d{2}[ ]?\d{4}[ ]?\d{4}(?![\w\-])"), // UK 020 7946 0958
            new(@"(?<![\w+\-./])0\d{3,4}[ ]?\d{3}[ ]?\d{3,4}(?![\w\-])"), // UK 07700 900123, 0161 496 0000
            new(@"(?<![\w+\-./])\d{3}[ \-]\d{3}[ \-]\d{3}(?![\w\-])" + NotAmount), // PL/ES 612 345 678
            new(@"(?<![\w+\-./])[6-9]\d{2}[ ]\d{2}[ ]\d{2}[ ]\d{2}(?![\w\-])"), // ES 912 34 56 78
        };
        // Anything after a phone label, whatever its format
        private static readonly Regex PhoneLabelled = new(
            @"(?i)\b(?:tel|tél|phone|mobile|mob|cell|móvil|movil|teléfono|telefono|telf|kom|komórka|tel\. kom)" +
            @"\.?\s*[:.]?\s*(\+?\d[\d ().\-/]{6,20}\d)", RegexOptions.CultureInvariant);

        private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";
        // Mail from these addresses is from a system or a team, and its display name is a product or
        // company ("Microsoft Azure"): mask the full name, not each of its words.
        private static readonly Regex RoleAddress = new(
            @"^(?:[\w.\-]*[.\-])?(?:no-?reply|do-?not-?reply|mailer-daemon|postmaster|notifications?|" +
            @"newsletters?|news|info|support|alerts?|marketing|hello|team|contact|service|admin|bounce)" +
            @"(?:[.\-][\w.\-]*)?@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NameToken = new(@"^[^\W\d_][\w'’\-]*$");
        // A capitalised name of one to three words ("Daniel", "Anna-Maria", "John Smith")
        // in any alphabet ("Ольга", "Νίκος")
        private const string GreetedName = @"\p{Lu}[\w'’\-]+(?:[ \t]+\p{Lu}[\w'’\-]+){0,2}";
        private static readonly Regex GreetedList = new("^" + GreetedName + @"(?:[ \t]+(?:&|and|et|y|e|i|oraz)[ \t]+" + GreetedName + ")*$");
        private static readonly Regex GreetedOne = new(GreetedName);
        // A name isn't part of a longer word or a hyphenated name, and neither is it a segment of an identifier
        // ("user_Anna_id"), but Markdown emphasis ("_Emma_") is not a word
        private const string EdgeBefore = @"(?<![^\W_])(?<!-)(?<![^\W_]_)";
        private const string EdgeAfter = @"(?![^\W_])(?!-)(?!_[^\W_])";
        private static readonly Regex SpacesAndHyphens = new(@"[\s\-]+");
        private static readonly Regex NonDigits = new(@"\D");
        private static readonly Regex SpacesAndDashes = new(@"[\s\-]");

        private sealed record Rule(Regex Pattern, string Kind, Func<string, string> Normalise, Func<string, bool> Valid, int Group);

        private static readonly Rule[] Rules = new[]
        {
            new Rule(EmailPattern, "EMAIL", v => v.ToLowerInvariant(), null, 0),
            new Rule(Iban, "IBAN", Compact, ValidIban, 0),
            // National IDs before card numbers: a 15-digit French NIR can pass the Luhn check
            new Rule(Nir, "ID", Compact, ValidNir, 0),
            new Rule(Pesel, "ID", Digits, ValidPesel, 0),
            new Rule(Nie, "ID", Compact, ValidNie, 0),
            new Rule(Dni, "ID", Compact, ValidDni, 0),
            new Rule(Nino, "ID", Compact, null, 0),
            new Rule(Card, "CARD", Digits, ValidCard, 0),
            new Rule(PhoneLabelled, "PHONE", Digits, null, 1),
            new Rule(PhoneInternational, "PHONE", Digits, v => Digits(v).Length is >= 8 and <= 15, 0),
        }.Concat(PhoneNational.Select(p => new Rule(p, "PHONE", Digits, null, 0))).ToArray();

        private readonly Dictionary<string, Dictionary<string, int>> numbers = new();
        private readonly List<(string Key, string Variant)> people = new();

        public Redactor(IEnumerable<EmailAddress> people = null, IEnumerable<string> names = null)
        {
            foreach (var person in people ?? Enumerable.Empty<EmailAddress>())
                AddPerson(person.Name, person.Email);
            foreach (var name in names ?? Enumerable.Empty<string>())
                AddPerson(name.Trim(), "");
        }

        public void AddPerson(string name, string email = "")
        {
            name = EmailAddressParser.FlipName((name ?? "").Trim());
            email ??= "";
            if (name.Length == 0 && email.Length == 0) return;
            var key = email.Length > 0 ? email.ToLowerInvariant() : name.ToLowerInvariant();
            // Someone known by email may also have been listed by name; keep one placeholder
            foreach (var (existingKey, variant) in people)
            {
                if (name.Length > 0 && string.Equals(variant.ToLowerInvariant(), name.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    key = existingKey;
                    break;
                }
            }
            var role = email.Length > 0 && RoleAddress.IsMatch(email);
            foreach (var variant in Variants(name, role))
                people.Add((key, variant));
        }

        // Adds the people who are greeted ("Hi Daniel and Emma,") or who sign a message. Only a name in one
        // of those two places is found, and only when it is written with capitals: that catches the
        // recipients and senders a mail's headers leave out, without guessing at names inside sentences.
        public void LearnNames(string text, EmailRules rules)
        {
            var names = new List<string>();
            if (rules.Greetings.Count > 0)
            {
                var greetings = string.Join("|", rules.Greetings.OrderByDescending(g => g.Length).Select(g => Regex.Escape(g).Replace(@"\ ", @"[ \t]+")));
                var titles = string.Join("|", rules.Titles.OrderByDescending(t => t.Length).Select(Regex.Escape));
                var titlePart = titles.Length > 0 ? @"(?:[*_]*(?i:" + titles + @")\.?[ \t]+)*" : "";
                var line = new Regex(@"^[ \t>*_]*(?i:" + greetings + @")[ \t]+" + titlePart + @"(?<who>[^\n,:;!]*?)[ \t*_]*(?:[,:;!]|$)",
                    RegexOptions.Multiline | RegexOptions.CultureInvariant);
                foreach (Match m in line.Matches(text))
                {
                    // Markdown emphasis around a name ("Hi **Sofia**,") isn't part of it
                    var who = Regex.Replace(m.Groups["who"].Value, "[*_]+", "").Trim();
                    if (who.Length > 0 && GreetedList.IsMatch(who))
                        names.AddRange(GreetedOne.Matches(who).Select(n => n.Value));
                }
            }
            names.AddRange(EmailThread.SignatureNames(text, rules));
            var skip = new HashSet<string>(rules.NotNames.Concat(rules.Titles));
            foreach (var name in names)
            {
                if (name.Split(' ').Any(w => skip.Contains(EmailRules.Fold(w.Trim('.'))))) continue;
                AddPerson(name);
            }
        }

        public string Placeholder(string kind, string value)
        {
            if (!numbers.TryGetValue(kind, out var table)) numbers[kind] = table = new Dictionary<string, int>();
            if (!table.TryGetValue(value, out var n)) table[value] = n = table.Count + 1;
            return $"[{kind}-{n}]";
        }

        public string Redact(string text)
        {
            // Find everything first, then number by position, so [ID-1] is the first ID in the text
            // whichever pattern found it. Earlier patterns win overlaps.
            var spans = new List<(int Start, int End, string Kind, string Value)>();
            foreach (var rule in Rules)
            {
                foreach (Match m in rule.Pattern.Matches(text))
                {
                    var group = m.Groups[rule.Group];
                    if (!group.Success) continue;
                    var value = group.Value;
                    if (rule.Valid != null && !rule.Valid(value)) continue;
                    int start = group.Index, end = group.Index + group.Length;
                    if (spans.Any(s => start < s.End && s.Start < end)) continue;
                    spans.Add((start, end, rule.Kind, rule.Normalise(value)));
                }
            }
            var sb = new StringBuilder();
            var last = 0;
            foreach (var span in spans.OrderBy(s => s.Start).ThenBy(s => s.End))
            {
                sb.Append(text, last, span.Start - last).Append(Placeholder(span.Kind, span.Value));
                last = span.End;
            }
            sb.Append(text, last, text.Length - last);
            return Names(sb.ToString());
        }

        private string Names(string text)
        {
            if (people.Count == 0) return text;
            var variants = people.OrderByDescending(p => p.Variant.Length).ToList();
            var byVariant = new Dictionary<string, string>();
            foreach (var (key, variant) in variants)
                byVariant.TryAdd(Lookup(variant), key);
            // Full names match in any case; single words ("Anna") only as capitalised words, so the name
            // "Will" does not swallow the verb "will".
            var multi = variants.Select(p => p.Variant).Where(v => v.Contains(' ') || v.Contains(',')).ToList();
            var single = variants.Select(p => p.Variant).Where(v => !v.Contains(' ') && !v.Contains(',')).ToList();
            var peopleKeys = new Dictionary<string, string>();

            string Replace(string matched)
            {
                if (!byVariant.TryGetValue(Lookup(matched), out var key)) return matched;
                if (!peopleKeys.TryGetValue(key, out var placeholder))
                    peopleKeys[key] = placeholder = Placeholder("PERSON", key);
                return placeholder;
            }

            if (multi.Count > 0)
            {
                var pattern = EdgeBefore + "(?:" + string.Join("|", multi.Select(Flexible)) + ")" + EdgeAfter;
                text = Regex.Replace(text, pattern, m => Replace(m.Value), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            if (single.Count > 0)
            {
                var pattern = EdgeBefore + "(?:" + string.Join("|", single.Select(Regex.Escape)) + ")" + EdgeAfter;
                text = Regex.Replace(text, pattern, m => Replace(m.Value));
            }
            return text;
        }

        // A name wrapped over two lines, written with a double space or with a hyphen between the first
        // names ("Anna-Maria" in a signature, "Anna Maria" in the address book) is still the name.
        private static string Flexible(string variant) => string.Join(@"[\s\-]+", variant.Split(' ').Select(Regex.Escape));

        private static string Lookup(string text) => SpacesAndHyphens.Replace(text, " ").ToLowerInvariant();

        private static List<string> Variants(string name, bool wholeOnly)
        {
            if (name.Length == 0 || name.Contains('@')) return new List<string>();
            if (wholeOnly) return new List<string> { name };
            var words = name.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Where(w => NameToken.IsMatch(w)).ToList();
            var result = new List<string> { name };
            if (words.Count >= 2)
            {
                var first = string.Join(" ", words.Take(words.Count - 1));
                result.Add($"{words[^1]}, {first}");
                result.Add($"{words[^1]} {first}");
                if (words.Count >= 3) result.Add(first); // "Anna Maria", also written "Anna-Maria"
                foreach (var word in words.Where(w => w.Length >= 3))
                {
                    result.Add(word);
                    // Company directories write surnames in capitals ("Anna NOWAK"); the text doesn't
                    if (word.Length > 1 && word.All(c => !char.IsLower(c)))
                        result.Add(word[0] + word[1..].ToLowerInvariant());
                }
            }
            return result;
        }

        private static string Digits(string value) => NonDigits.Replace(value, "");

        private static string Compact(string value) => SpacesAndDashes.Replace(value, "").ToUpperInvariant();

        private static int Digit(char c) => (int)char.GetNumericValue(c);

        private static bool ValidIban(string value)
        {
            var iban = Compact(value);
            if (iban.Length < 15 || iban.Length > 34) return false;
            var remainder = 0;
            foreach (var c in iban[4..] + iban[..4])
            {
                var n = char.IsDigit(c) ? Digit(c) : char.IsAsciiLetterUpper(c) ? c - 'A' + 10 : -1;
                if (n < 0) return false;
                remainder = (n >= 10 ? remainder * 100 + n : remainder * 10 + n) % 97;
            }
            return remainder == 1;
        }

        private static bool ValidCard(string value)
        {
            var digits = Digits(value);
            if (digits.Length < 13 || digits.Length > 19) return false;
            var total = 0;
            for (var i = 0; i < digits.Length; i++)
            {
                var n = Digit(digits[digits.Length - 1 - i]);
                if (i % 2 == 1) n = n > 4 ? n * 2 - 9 : n * 2;
                total += n;
            }
            return total % 10 == 0;
        }

        private static bool ValidPesel(string value)
        {
            var d = Digits(value).Select(Digit).ToArray();
            int[] weights = { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
            var sum = weights.Select((w, i) => w * d[i]).Sum();
            return (10 - sum % 10) % 10 == d[10];
        }

        private static bool ValidNir(string value)
        {
            var v = Compact(value);
            if (v.Length < 13) return false;
            var body = v[..13].Replace("2A", "19").Replace("2B", "18");
            var key = v[13..];
            if (key.Length == 0 || !body.All(char.IsDigit) || !key.All(char.IsDigit)) return false;
            return 97 - Mod97(body) == Number(key);
        }

        private static bool ValidDni(string value)
        {
            var v = Compact(value);
            return DniLetters[Mod23(v[..8])] == v[8];
        }

        private static bool ValidNie(string value)
        {
            var v = Compact(value);
            return DniLetters[Mod23("XYZ".IndexOf(v[0]) + v[1..8])] == v[8];
        }

        private static int Mod97(string digits) => digits.Aggregate(0, (r, c) => (r * 10 + Digit(c)) % 97);

        private static int Mod23(string digits) => digits.Aggregate(0, (r, c) => (r * 10 + Digit(c)) % 23);

        private static long Number(string digits) => digits.Aggregate(0L, (r, c) => r * 10 + Digit(c));
    }
}
