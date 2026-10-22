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
    // DNI/NIE, French NIR, and in Latin America the Chilean RUT, the Argentine CUIT/CUIL, the Colombian NIT, the Brazilian CPF and CNPJ,
    // the Mexican CURP and RFC), so a random order number is not mistaken for an ID. People are masked by name
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
            new(@"(?<![\w+\-./(])\(?\d{2,3}\)?[ \-]\d{3,4}[ \-]\d{4}(?![\w\-])" + NotAmount), // CO 300 123 4567, AR 011 4123-4567, MX 55 1234 5678
            new(@"(?<![\w+\-./])9[ ]\d{4}[ ]\d{4}(?![\w\-])"), // CL 9 1234 5678
            new(@"(?<![\w+\-./(])\(?\d{2}\)?[ ]?9\d{4}[ \-]\d{4}(?![\w\-])"), // BR (11) 91234-5678, 11 91234 5678
        };
        // Anything after a phone label, whatever its format
        private static readonly Regex PhoneLabelled = new(
            @"(?i)\b(?:tel|tél|phone|mobile|mob|cell|móvil|movil|teléfono|telefono|telf|tlf|cel|celular|whatsapp|whats|wsp|zap|fone|contato|fono|fijo|kom|komórka|tel\. kom)" +
            @"\.?\s*[:.]?\s*(\+?\d[\d ().\-/]{6,20}\d)", RegexOptions.CultureInvariant);

        // Latin America. A number with a check digit is found on its own; one without (the Colombian cédula, the DNI of
        // Argentina and Peru, the Peruvian RUC) only after its label, so that a random number is not taken for an ID.
        private static readonly Regex Rut = new(@"(?<![\w\-.])(?:\d{1,2}(?:\.\d{3}){2}|\d{7,8})-[\dKk](?![\w\-])"); // Chile 12.345.678-5
        private static readonly Regex Cuit = new(@"(?<![\w\-])(?:20|23|24|27|30|33|34)-\d{8}-\d(?![\w\-])"); // Argentina 20-12345678-6
        private static readonly Regex Nit = new(@"(?<![\w\-.])(?:\d{1,3}(?:\.\d{3}){2,3}|\d{6,10})-\d(?![\w\-])"); // Colombia 800.197.268-4
        private static readonly Regex Curp = new(
            @"(?<![\w\-])[A-Z][AEIOUX][A-Z]{2}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])[HMX]" +
            @"(?:AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)" +
            @"[B-DF-HJ-NP-TV-Z]{3}[A-Z\d]\d(?![\w\-])"); // Mexico HEGG560427MVZRRL04
        private static readonly Regex Rfc = new(@"(?<![\w\-&])[A-ZÑ&]{3,4}\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])[A-Z\d]{2}[A\d](?![\w\-&])"); // Mexico GODE561231GR8
        private static readonly Regex HondurasId = new(@"(?<![\w\-])(?:0[1-9]|1[0-8])\d{2}-(?:19|20)\d{2}-\d{5,6}(?![\w\-])"); // 0801-1990-12345, RTN 0801-1990-123456
        private static readonly Regex Cpf = new(@"(?<![\w\-./])\d{3}\.\d{3}\.\d{3}-\d{2}(?![\w\-])"); // Brazil 529.982.247-25 (without its points it is found after its label)
        private static readonly Regex Cnpj = new(@"(?<![\w\-./])\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}(?![\w\-])"); // Brazil 11.222.333/0001-81
        private const string IdNumber = @"(\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{1,3}(?:[. ]\d{3}){1,3}(?:-[\dKkXx]{1,2})?|\d{6,14}(?:-[\dKkXx]{1,2})?)(?!\w)";
        private static readonly Regex IdLabelled = new(
            @"(?<!\w)(?:(?-i:DNI|D\.N\.I\.?|CC|C\.C\.?|CE|C\.E\.?|NUIP|CUIT|CUIL|RUC|NIT|RUT|RFC|CURP|CPF|CNPJ|RG|CNH|PIS|PASEP|NIS)" +
            @"|(?i:c[eé]dula(?: de (?:ciudadan[ií]a|extranjer[ií]a|identidad))?|documento(?: (?:nacional )?de identidad)?|pasaporte|passaporte|carteira de (?:identidade|motorista|habilita[cç][aã]o)|t[ií]tulo de eleitor))" +
            @"(?!\w)\.?\s*(?i:n[°º]|no\.?|num\.?|n[uú]mero|#)?\s*[:.\-]?\s*" + IdNumber);
        private static readonly Regex SpacesDotsDashes = new(@"[\s.\-/]");

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
            new Rule(Rut, "ID", IdKey, ValidRut, 0),
            new Rule(Cuit, "ID", IdKey, ValidCuit, 0),
            new Rule(Nit, "ID", IdKey, ValidNit, 0),
            new Rule(Curp, "ID", IdKey, ValidCurp, 0),
            new Rule(Rfc, "ID", IdKey, ValidRfc, 0),
            new Rule(HondurasId, "ID", IdKey, null, 0),
            new Rule(Cnpj, "ID", IdKey, ValidCnpj, 0),
            new Rule(Cpf, "ID", IdKey, ValidCpf, 0),
            // Numbers with no check digit, only after their label ("DNI 12.345.678", "Cédula de ciudadanía No. 1.234.567.890")
            new Rule(IdLabelled, "ID", IdKey, v => Digits(v).Length is >= 6 and <= 14, 1),
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

        private static string IdKey(string value) => SpacesDotsDashes.Replace(value, "").ToUpperInvariant();

        // Chile: the digits from the right times 2, 3, 4, 5, 6, 7, 2, 3...; 11 minus the sum mod 11 (10 is K, 11 is 0)
        private static bool ValidRut(string value)
        {
            var key = IdKey(value);
            var body = key[..^1];
            if (body.Length is < 7 or > 8 || !body.All(char.IsAsciiDigit)) return false;
            var r = 11 - body.Reverse().Select((c, i) => Digit(c) * (2 + i % 6)).Sum() % 11;
            return key[^1] == (r == 11 ? '0' : r == 10 ? 'K' : (char)('0' + r));
        }

        private static readonly int[] CuitWeights = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

        private static int CuitRemainder(string firstTen) => 11 - firstTen.Select((c, i) => Digit(c) * CuitWeights[i]).Sum() % 11;

        // Argentina (CUIT and CUIL): weights 5 4 3 2 7 6 5 4 3 2; 11 minus the sum mod 11 (11 is 0)
        private static bool ValidCuit(string value)
        {
            var key = IdKey(value);
            if (key.Length != 11 || !key.All(char.IsAsciiDigit)) return false;
            var r = CuitRemainder(key[..10]);
            if (r != 10 && key[10] == (char)('0' + (r == 11 ? 0 : r))) return true;
            // When the digit would have been 10, 23 or 24 stands in for 20 or 27, with 9 (men) or 4 (women) as the digit
            if (key[..2] is "23" or "24" && key[10] is '9' or '4')
                return new[] { "20", "27" }.Any(prefix => CuitRemainder(prefix + key[2..10]) == 10);
            return false;
        }

        private static readonly int[] NitWeights = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43 };

        // Colombia (DIAN): weights 3 7 13 17 19 23 29 37 41 43 from the right; the sum mod 11 if that is 0 or 1, else 11 minus it
        private static bool ValidNit(string value)
        {
            var key = IdKey(value);
            var body = key[..^1];
            if (!key.All(char.IsAsciiDigit) || body.Length is < 6 or > 10) return false;
            var r = body.Reverse().Select((c, i) => Digit(c) * NitWeights[i]).Sum() % 11;
            return key[^1] == (char)('0' + (r < 2 ? r : 11 - r));
        }

        private const string CurpChars = "0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ";

        // Mexico: the first 17 characters by their place in 0-9 A-N Ñ O-Z, times 18 down to 2; 10 minus the sum mod 10
        private static bool ValidCurp(string value)
        {
            var key = IdKey(value);
            if (key.Length != 18 || key.Any(c => CurpChars.IndexOf(c) < 0)) return false;
            var total = key.Take(17).Select((c, i) => CurpChars.IndexOf(c) * (18 - i)).Sum();
            return key[17] == (char)('0' + (10 - total % 10) % 10);
        }

        private const string RfcChars = "0123456789ABCDEFGHIJKLMN&OPQRSTUVWXYZ Ñ";

        // Mexico (SAT): a company has 12 characters and gets a space in front; each by its place in 0-9 A-N & O-Z space Ñ,
        // times 13 down to 2; 11 minus the sum mod 11 (10 is A, 11 is 0)
        private static bool ValidRfc(string value)
        {
            var key = IdKey(value);
            var body = key.Length == 13 ? key[..^1] : key.Length == 12 ? " " + key[..^1] : "";
            if (body.Length == 0 || (body + key[^1]).Any(c => RfcChars.IndexOf(c) < 0)) return false;
            var r = 11 - body.Select((c, i) => RfcChars.IndexOf(c) * (13 - i)).Sum() % 11;
            return key[^1] == (r == 11 ? '0' : r == 10 ? 'A' : (char)('0' + r));
        }

        // Brazil: two check digits, each the sum of the digits times 10 (then 11) down to 2, times 10, mod 11, mod 10. All digits the same is no CPF.
        private static bool ValidCpf(string value)
        {
            var d = Digits(value).Select(Digit).ToArray();
            if (d.Length != 11 || d.All(x => x == d[0])) return false;
            foreach (var n in new[] { 9, 10 })
            {
                var total = Enumerable.Range(0, n).Sum(i => d[i] * (n + 1 - i));
                if (total * 10 % 11 % 10 != d[n]) return false;
            }
            return true;
        }

        private static readonly int[] CnpjWeights = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        // Brazil: two check digits, weights 5 4 3 2 9 8 7 6 5 4 3 2 (and 6 in front for the second); 0 when the sum mod 11 is below 2, else 11 minus it
        private static bool ValidCnpj(string value)
        {
            var d = Digits(value).Select(Digit).ToArray();
            if (d.Length != 14 || d.All(x => x == d[0])) return false;
            foreach (var n in new[] { 12, 13 })
            {
                var weights = n == 12 ? CnpjWeights : new[] { 6 }.Concat(CnpjWeights).ToArray();
                var r = Enumerable.Range(0, n).Sum(i => d[i] * weights[i]) % 11;
                if ((r < 2 ? 0 : 11 - r) != d[n]) return false;
            }
            return true;
        }

        private static int Mod97(string digits) => digits.Aggregate(0, (r, c) => (r * 10 + Digit(c)) % 97);

        private static int Mod23(string digits) => digits.Aggregate(0, (r, c) => (r * 10 + Digit(c)) % 23);

        private static long Number(string digits) => digits.Aggregate(0L, (r, c) => r * 10 + Digit(c));
    }
}
