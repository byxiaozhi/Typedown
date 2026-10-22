using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    internal sealed class ThreadMessage
    {
        public EmailAddress Sender { get; set; }
        public List<EmailAddress> To { get; set; } = new();
        public List<EmailAddress> Cc { get; set; } = new();
        public List<EmailAddress> Bcc { get; set; } = new();
        public string Date { get; set; }
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool Forwarded { get; set; }
    }

    // Splitting one mail body into the messages of its thread, and removing the clutter.
    //
    // A reply carries the whole conversation below it. Mail clients introduce each quoted message in one
    // of two ways, both recognised in every language in the rules:
    //   * a header block, as Outlook writes it ("De : ... / Envoyé : ... / À : ... / Objet : ...");
    //   * a "wrote" line followed by ">"-quoted text, as Gmail, Apple Mail and most others do.
    internal static class EmailThread
    {
        private static readonly Regex HeaderLine = new(@"^\s*\**\s*([^\W\d_][\w .\-/]{0,24}?)\s*\**\s*:\s*\**\s*(.*?)\s*\**\s*$");
        private static readonly Regex Separator = new(@"^\s*[_\-=]{10,}\s*$");
        private static readonly Regex BlankRuns = new(@"\n{3,}");
        private static readonly Regex BareLink = new(@"^\s*(?:\[[^\]\n]*\]\([^)\s]*\)|<?(?:https?://|www\.)\S+?>?)\s*$");
        private static readonly Regex ParagraphBreak = new(@"\n\s*\n");
        private static readonly Regex TimeInText = new(@"\d{1,2}[:h]\d{2}(?:\s*[AaPp]\.?\s?[Mm]\b\.?)?");
        private static readonly Regex NameWord = new(@"^\p{Lu}[\w'’\-.]*$"); // capitalised, in any alphabet
        private static readonly Regex WroteWord = new(@"\s*\S+\s*:\s*$");
        private static readonly Regex WroteAuxiliary = new(@"\s*\b(?:a|napisał(?:\(a\)|a)?)\s*$"); // "a écrit", "napisał(a)"
        private static readonly Regex WroteIntro = new(@"^\s*(?:On|Le|El|W dniu)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] RecipientFields = { "to", "cc", "bcc" };

        private sealed record Boundary(int Start, int BodyStart, Dictionary<string, string> Headers, bool Forwarded = false, bool Quoted = false);

        // Messages newest first (the order they appear in).
        public static List<ThreadMessage> Split(string body, EmailRules rules, bool keepSignatures = false)
        {
            var lines = Normalise(body).Split('\n').ToList();
            var messages = new List<ThreadMessage>();
            var headers = new Dictionary<string, string>();
            var forwarded = false;

            while (true)
            {
                var boundary = FindBoundary(lines, rules);
                if (boundary == null)
                {
                    messages.Add(Message(headers, lines, forwarded, rules, keepSignatures));
                    break;
                }

                messages.Add(Message(headers, lines.GetRange(0, boundary.Start), forwarded, rules, keepSignatures));
                var bodyStart = Math.Min(boundary.BodyStart, lines.Count);
                var rest = lines.GetRange(bodyStart, lines.Count - bodyStart);
                if (boundary.Quoted)
                {
                    var (quoted, after) = TakeQuoted(rest);
                    if (after.Count > 0) // text written below the quote belongs to the newer message
                        messages[^1].Body = (messages[^1].Body + "\n\n" + CleanBody(string.Join("\n", after), rules, keepSignatures)).Trim();
                    rest = quoted;
                }
                lines = rest;
                headers = boundary.Headers;
                forwarded = boundary.Forwarded;
            }
            return messages;
        }

        private static string Normalise(string text)
        {
            text = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Replace(' ', ' ');
            return string.Join("\n", text.Split('\n').Select(line => line.TrimEnd()));
        }

        private static Boundary FindBoundary(List<string> lines, EmailRules rules)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var stripped = lines[i].Trim();
                if (stripped.Length == 0) continue;

                // "On ... wrote:", possibly wrapped over two lines by the sending client
                foreach (var (joined, used) in new[] { (stripped, 1), (stripped + " " + LineAt(lines, i + 1), 2) })
                {
                    if (joined.Length < 400 && rules.WrotePatterns.Any(p => p.IsMatch(joined)))
                    {
                        if (used == 2 && LineAt(lines, i + 1).Length == 0) continue;
                        return new Boundary(i, i + used, ParseWrote(joined), Quoted: true);
                    }
                }

                int? headerStart = i;
                var forwarded = false;
                var replyMarker = rules.IsReplyMarker(stripped);
                if (replyMarker || Separator.IsMatch(stripped))
                {
                    forwarded = rules.IsForwardMarker(stripped);
                    headerStart = NextNonblank(lines, i + 1);
                    if (headerStart == null)
                    {
                        if (replyMarker) return new Boundary(i, lines.Count, new(), forwarded);
                        continue;
                    }
                }

                var block = HeaderBlock(lines, headerStart.Value, rules);
                if (block != null) return new Boundary(i, block.Value.End, block.Value.Headers, forwarded);
                if (replyMarker) return new Boundary(i, i + 1, new(), forwarded);
            }
            return null;
        }

        // A quoted header block starting at `start`, or null if it isn't one.
        private static (Dictionary<string, string> Headers, int End)? HeaderBlock(List<string> lines, int start, EmailRules rules)
        {
            var first = start < lines.Count ? Header(lines[start], rules) : null;
            if (first == null || first.Value.Key != "from") return null;

            var headers = new Dictionary<string, string>();
            string lastKey = null;
            var i = start;
            while (i < lines.Count && i < start + 16)
            {
                var line = lines[i];
                if (line.Trim().Length == 0)
                {
                    // Outlook's HTML, turned into text, can leave a blank line inside the block (after
                    // "From:"); the block goes on if the next line is another field of it
                    var next = NextNonblank(lines, i + 1);
                    var field = next < start + 16 ? Header(lines[next.Value], rules) : null;
                    if (field == null || headers.ContainsKey(field.Value.Key)) break;
                    i = next.Value;
                    continue;
                }
                var parsed = Header(line, rules);
                if (parsed != null && !headers.ContainsKey(parsed.Value.Key))
                {
                    lastKey = parsed.Value.Key;
                    headers[lastKey] = parsed.Value.Value;
                }
                else if (RecipientFields.Contains(lastKey) && parsed == null)
                    headers[lastKey] += " " + line.Trim(); // a long recipient list, wrapped
                else
                    break;
                i++;
            }

            if (new[] { "sent", "to", "subject" }.Count(headers.ContainsKey) < 2) return null;
            return (headers, i);
        }

        private static (string Key, string Value)? Header(string line, EmailRules rules)
        {
            var m = HeaderLine.Match(line);
            if (!m.Success) return null;
            return rules.HeaderKeys.TryGetValue(EmailRules.Fold(m.Groups[1].Value).Trim(), out var key) ? (key, m.Groups[2].Value) : null;
        }

        // The sender and date out of an "On <date>, <sender> wrote:" line.
        private static Dictionary<string, string> ParseWrote(string text)
        {
            var body = WroteWord.Replace(text, "");
            body = WroteAuxiliary.Replace(body, "");
            body = WroteIntro.Replace(body, "");
            var time = TimeInText.Matches(body).LastOrDefault();
            if (time == null) return new() { ["from"] = body.Trim(' ', ',') };
            var end = time.Index + time.Length;
            return new() { ["sent"] = body[..end].Trim(' ', ','), ["from"] = body[end..].Trim(' ', ',') };
        }

        // ">"-quoted lines (one level removed), and the unquoted text after them.
        private static (List<string> Quoted, List<string> After) TakeQuoted(List<string> lines)
        {
            var quoted = new List<string>();
            var i = 0;
            while (i < lines.Count && (lines[i].StartsWith('>') || lines[i].Trim().Length == 0))
            {
                var line = lines[i];
                quoted.Add(line.StartsWith("> ") ? line[2..] : (line.Length > 0 ? line[1..] : line));
                i++;
            }
            var after = lines.GetRange(i, lines.Count - i);
            if (!quoted.Any(l => l.Trim().Length > 0))
                return (after, new List<string>()); // "wrote:" with the earlier message below it, not quoted
            return (quoted, after.Any(l => l.Trim().Length > 0) ? after : new List<string>());
        }

        private static ThreadMessage Message(Dictionary<string, string> headers, List<string> lines, bool forwarded, EmailRules rules, bool keepSignatures)
        {
            string Get(string key) => headers.TryGetValue(key, out var value) ? value : "";
            var sent = Get("sent");
            return new ThreadMessage
            {
                Sender = Get("from").Length > 0 ? EmailAddressParser.Parse(Get("from")) : null,
                To = EmailAddressParser.ParseList(Get("to")),
                Cc = EmailAddressParser.ParseList(Get("cc")),
                Bcc = EmailAddressParser.ParseList(Get("bcc")),
                Date = EmailDates.Parse(sent, rules.Months) ?? (sent.Length > 0 ? sent : null),
                Subject = Get("subject"),
                Body = CleanBody(string.Join("\n", lines), rules, keepSignatures),
                Forwarded = forwarded,
            };
        }

        // Removes banners at the top and signatures and disclaimers at the bottom.
        public static string CleanBody(string text, EmailRules rules, bool keepSignatures = false)
        {
            var paragraphs = ParagraphBreak.Split(Normalise(text)).Select(p => p.Trim('\n')).Where(p => p.Trim().Length > 0).ToList();

            // External-sender and "you don't often get email from" banners, at the top
            while (paragraphs.Count > 0 && Contains(paragraphs[0], rules.BannerPhrases))
                paragraphs.RemoveAt(0);

            paragraphs = StripTrailing(paragraphs, rules);
            if (!keepSignatures)
            {
                var cut = CutSignature(string.Join("\n\n", paragraphs), rules);
                paragraphs = StripTrailing(ParagraphBreak.Split(cut).Where(p => p.Trim().Length > 0).ToList(), rules);
            }

            return BlankRuns.Replace(string.Join("\n\n", paragraphs).Trim(), "\n\n");
        }

        // Drops disclaimers, mobile signatures and separators from the end — only from the end, so that a
        // sentence in the message itself that mentions confidentiality is never touched.
        private static List<string> StripTrailing(List<string> paragraphs, EmailRules rules)
        {
            paragraphs = new List<string>(paragraphs);
            while (paragraphs.Count > 0)
            {
                var last = paragraphs[^1];
                if (Contains(last, rules.DisclaimerPhrases) || Separator.IsMatch(last))
                {
                    paragraphs.RemoveAt(paragraphs.Count - 1);
                    continue;
                }
                // The company's website under its disclaimer ("www.example.com")
                if (paragraphs.Count >= 2 && BareLink.IsMatch(last)
                    && (Contains(paragraphs[^2], rules.DisclaimerPhrases) || Separator.IsMatch(paragraphs[^2])))
                {
                    paragraphs.RemoveAt(paragraphs.Count - 1);
                    continue;
                }
                var lines = last.Split('\n');
                var kept = lines.Where(l => !IsMobileSignature(l, rules) && !Separator.IsMatch(l)).ToList();
                if (kept.Count != lines.Length)
                {
                    if (kept.Any(l => l.Trim().Length > 0)) paragraphs[^1] = string.Join("\n", kept);
                    else paragraphs.RemoveAt(paragraphs.Count - 1);
                    continue;
                }
                break;
            }
            return paragraphs;
        }

        private static string CutSignature(string text, EmailRules rules)
        {
            var lines = text.Split('\n');

            // The standard "-- " delimiter: everything after it is signature
            for (var i = lines.Length - 1; i >= 0; i--)
                if (lines[i] == "-- " || lines[i] == "--")
                    return string.Join("\n", lines.Take(i)).TrimEnd();

            // A closing ("Best regards", "Cordialement", "Pozdrawiam") near the end, followed by the
            // sender's name. Only then is the rest cut, and only if it looks like a signature block: short
            // lines, no questions. Anything less certain is left alone.
            var nonblank = Enumerable.Range(0, lines.Length).Where(i => lines[i].Trim().Length > 0).ToList();
            foreach (var i in nonblank.Skip(Math.Max(0, nonblank.Count - 25)).Reverse())
            {
                var (closing, nameInLine) = Closing(lines[i], rules);
                if (!closing) continue;
                int keepUntil;
                if (nameInLine) keepUntil = i;
                else
                {
                    var following = nonblank.Where(j => j > i).ToList();
                    if (following.Count == 0 || !LooksLikeName(lines[following[0]])) return text;
                    keepUntil = following[0];
                }
                var rest = nonblank.Where(j => j > keepUntil).Select(j => lines[j]).ToList();
                if (rest.Count > 15 || rest.Any(l => l.TrimEnd().EndsWith('?') || l.Length > 100)) return text;
                return string.Join("\n", lines.Take(keepUntil + 1)).TrimEnd();
            }
            return text;
        }

        // Is this line a closing, and does it already carry the name ("Thanks, Anna")?
        private static (bool Closing, bool NameInLine) Closing(string line, EmailRules rules)
        {
            var folded = EmailRules.Fold(line).Trim().TrimEnd('!', '.', ',', ';', ':', ' ');
            if (rules.Closings.Contains(folded)) return (true, false);
            // Bilingual closings: "Pozdrawiam / With Regards", "Cordialement / Best regards"
            if (folded.Contains('/') && folded.Split('/').Any(part => rules.Closings.Contains(part.Trim().TrimEnd('!', '.', ',', ';', ':', ' '))))
                return (true, false);
            var rest = ClosingRest(line, rules);
            return rest.Length > 0 && LooksLikeName(rest) ? (true, true) : (false, false);
        }

        // What follows a closing on the same line ("Thanks, Anna!" gives "Anna"), or "".
        private static string ClosingRest(string line, EmailRules rules)
        {
            var folded = EmailRules.Fold(line).Trim().TrimEnd('!', '.', ',', ';', ':', ' ');
            var trimmed = line.Trim();
            foreach (var closing in rules.Closings)
            {
                if (folded.StartsWith(closing + ",", StringComparison.Ordinal) || folded.StartsWith(closing + " -", StringComparison.Ordinal))
                    return closing.Length + 1 < trimmed.Length ? trimmed[(closing.Length + 1)..].Trim(' ', ',', '-', '!', '.', ';', ':') : "";
            }
            return "";
        }

        // The names written under a closing ("Kind regards," then "Anna Nowak", or "Thanks, Anna").
        internal static List<string> SignatureNames(string text, EmailRules rules)
        {
            var lines = text.Split('\n');
            var names = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var (closing, nameInLine) = Closing(lines[i], rules);
                if (!closing) continue;
                string candidate;
                if (nameInLine) candidate = ClosingRest(lines[i], rules);
                else
                {
                    var following = lines.Skip(i + 1).FirstOrDefault(l => l.Trim().Length > 0) ?? "";
                    candidate = LooksLikeName(following) ? following : "";
                }
                candidate = string.Join(" ", candidate.Trim().Trim('*', '_').Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('.', ',')));
                if (candidate.Length > 0 && LooksLikeName(candidate)) names.Add(candidate);
            }
            return names;
        }

        private static bool LooksLikeName(string line)
        {
            // HTML mail often has the name in bold: "**Anna Nowak**"
            var words = line.Trim().Trim('*', '_').Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 1 && words.Length <= 4 && words.All(w => NameWord.IsMatch(w));
        }

        private static bool IsMobileSignature(string line, EmailRules rules)
        {
            var folded = EmailRules.Fold(line).Trim();
            return folded.Length < 90 && rules.MobileSignatures.Any(s => folded.StartsWith(s, StringComparison.Ordinal));
        }

        private static bool Contains(string text, List<string> phrases)
        {
            var folded = EmailRules.Fold(text);
            return phrases.Any(p => folded.Contains(p, StringComparison.Ordinal));
        }

        private static string LineAt(List<string> lines, int i) => i < lines.Count ? lines[i].Trim() : "";

        private static int? NextNonblank(List<string> lines, int start)
        {
            for (var i = start; i < lines.Count; i++)
                if (lines[i].Trim().Length > 0) return i;
            return null;
        }

        // Strips RE:/TR:/RV:/Odp:/[EXTERNAL] prefixes, however many are stacked up.
        public static string NormaliseSubject(string subject, EmailRules rules)
        {
            var prefixes = rules.SubjectPrefixes.Select(EmailRules.Fold).Distinct().OrderByDescending(p => p.Length).ToList();
            subject = (subject ?? "").Trim();
            var changed = true;
            while (changed)
            {
                changed = false;
                var folded = EmailRules.Fold(subject);
                foreach (var p in prefixes)
                {
                    if (p.StartsWith('[') || p.EndsWith(':'))
                    {
                        if (folded.StartsWith(p, StringComparison.Ordinal))
                        {
                            subject = subject[p.Length..].TrimStart();
                            changed = true;
                            break;
                        }
                    }
                    else
                    {
                        var m = Regex.Match(folded, "^" + Regex.Escape(p) + @"\s?:\s*");
                        if (m.Success)
                        {
                            subject = subject[m.Length..];
                            changed = true;
                            break;
                        }
                    }
                }
            }
            return subject.Trim();
        }
    }
}
