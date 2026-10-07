using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // Language rules: the phrase lists and patterns that drive thread splitting and cleanup. They are the
    // plugin's own JSON files (plugins/markitdown-email/src/markitdown_caret_email/rules/*.json), embedded
    // at build time, so a new language or a company disclaimer is added in one place for both tools.
    // All languages apply at once: a French reply to an English mail forwarded from Poland is ordinary in
    // a European company, so there is no language detection step.
    internal sealed class EmailRules
    {
        public static readonly string[] BuiltinLanguages = { "en", "fr", "es", "pl" };

        // Header fields understood in a quoted header block.
        private static readonly string[] HeaderFields = { "from", "sent", "to", "cc", "bcc", "subject", "other" };

        private static readonly Lazy<EmailRules> builtin = new(() => Load());

        public static EmailRules Builtin => builtin.Value;

        public Dictionary<string, string> HeaderKeys { get; } = new(); // folded label -> field
        public List<string> ReplyMarkers { get; } = new(); // folded, dashes stripped
        public List<string> ForwardMarkers { get; } = new();
        public List<Regex> WrotePatterns { get; } = new();
        public List<string> SubjectPrefixes { get; } = new();
        public List<string> MobileSignatures { get; } = new(); // folded prefixes
        public List<string> Closings { get; } = new(); // folded
        public List<string> Greetings { get; } = new(); // folded, may be several words
        public List<string> Titles { get; } = new(); // folded, without the dot
        public List<string> NotNames { get; } = new(); // folded: words that are not a person
        public List<string> DisclaimerPhrases { get; } = new(); // folded
        public List<string> BannerPhrases { get; } = new(); // folded
        public Dictionary<string, int> Months { get; } = new();

        // Lowercase and unify the characters that differ between mail clients: Outlook writes typographic
        // apostrophes and non-breaking spaces (French puts one before every colon).
        public static string Fold(string text) =>
            text.Replace('’', '\'').Replace(' ', ' ').Replace(' ', ' ').ToLowerInvariant();

        // "-----Original Message-----", "---------- Forwarded message ---------" and "Original Message"
        // all compare equal.
        private static string Marker(string text) => Fold(text).Trim(' ', '-', '_', '*', '\t');

        public bool IsReplyMarker(string line) => ReplyMarkers.Contains(Marker(line));

        public bool IsForwardMarker(string line) => ForwardMarkers.Contains(Marker(line));

        // The built-in rule files, then any extra files (a company's own disclaimers) on top of them.
        public static EmailRules Load(IEnumerable<string> extraFiles = null)
        {
            var rules = new EmailRules();
            var assembly = typeof(EmailRules).Assembly;
            foreach (var language in BuiltinLanguages)
            {
                using var stream = assembly.GetManifestResourceStream($"EmailRules.{language}.json")
                    ?? throw new InvalidOperationException($"Missing email rules: {language}");
                using var document = JsonDocument.Parse(stream);
                rules.Merge(document.RootElement);
            }
            foreach (var path in extraFiles ?? Enumerable.Empty<string>())
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                rules.Merge(document.RootElement);
            }
            return rules;
        }

        private void Merge(JsonElement data)
        {
            if (data.TryGetProperty("header_keys", out var headerKeys))
                foreach (var field in headerKeys.EnumerateObject())
                {
                    if (!HeaderFields.Contains(field.Name)) continue;
                    foreach (var label in field.Value.EnumerateArray())
                        HeaderKeys[Fold(label.GetString())] = field.Name;
                }
            ReplyMarkers.AddRange(Strings(data, "reply_markers").Select(Marker));
            ForwardMarkers.AddRange(Strings(data, "forward_markers").Select(Marker));
            WrotePatterns.AddRange(Strings(data, "wrote_patterns").Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
            SubjectPrefixes.AddRange(Strings(data, "subject_prefixes"));
            MobileSignatures.AddRange(Strings(data, "mobile_signatures").Select(Fold));
            Closings.AddRange(Strings(data, "closings").Select(Fold));
            Greetings.AddRange(Strings(data, "greetings").Select(Fold));
            Titles.AddRange(Strings(data, "titles").Select(Fold));
            NotNames.AddRange(Strings(data, "not_names").Select(Fold));
            DisclaimerPhrases.AddRange(Strings(data, "disclaimer_phrases").Select(Fold));
            BannerPhrases.AddRange(Strings(data, "banner_phrases").Select(Fold));
            if (data.TryGetProperty("months", out var months))
                foreach (var month in months.EnumerateObject())
                    Months[Fold(month.Name)] = month.Value.GetInt32();
        }

        private static IEnumerable<string> Strings(JsonElement data, string name) =>
            data.TryGetProperty(name, out var list) ? list.EnumerateArray().Select(e => e.GetString()).ToList() : Enumerable.Empty<string>();
    }
}
