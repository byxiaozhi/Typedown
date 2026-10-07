using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // An email file in, one clean Markdown thread out: the quoted replies split into separate messages
    // (oldest first), signatures, disclaimers and banners removed, attachments converted in place, and —
    // unless turned off — personal data masked. Output matches the markitdown-caret-email plugin.
    internal static class EmailConverter
    {
        public const int MaxAttachmentBytes = 25 * 1024 * 1024;
        private const string Generator = "caret";
        private static readonly Regex DatePrefix = new(@"^\d{4}-\d{2}-\d{2}");
        private static readonly Regex NonWord = new(@"\W+");
        private static readonly Regex HeadingStart = new(@"^(#{1,6})(\s)");
        private static readonly string[] EmailExtensions = { ".eml", ".msg" };
        private static readonly string[] TextExtensions = { ".txt", ".md", ".markdown", ".log", ".json", ".xml", ".yaml", ".yml" };
        private static readonly string[] HtmlExtensions = { ".html", ".htm" };

        public static string ConvertMsg(Stream stream, ConversionContext context) =>
            Render(MsgFileReader.Read(stream, HtmlText.ToMarkdown), context);

        public static string ConvertEml(Stream stream, ConversionContext context) =>
            Render(EmlReader.Read(stream, HtmlText.ToMarkdown), context);

        private static EmailDocument Read(byte[] data, string kind) =>
            kind == ".msg" ? MsgFileReader.Read(new MemoryStream(data), HtmlText.ToMarkdown) : EmlReader.Read(new MemoryStream(data), HtmlText.ToMarkdown);

        public static string Render(EmailDocument email, ConversionContext context)
        {
            var options = context.Options;
            var rules = EmailRules.Builtin;
            var people = new List<EmailAddress>();
            var (body, subject, messages) = RenderThread(email, rules, context, people, depth: 0, title: true);

            var dates = messages.Select(m => m.Date).Where(d => d != null && DatePrefix.IsMatch(d)).ToList();
            var attachments = email.Attachments.Where(a => !a.Inline).Select(a => a.FileName).ToList();
            var front = new List<(string Key, object Value)>
            {
                ("type", "email-thread"),
                ("subject", subject),
                ("started", dates.Count > 0 ? dates.Min(StringComparer.Ordinal) : null),
                ("last", dates.Count > 0 ? dates.Max(StringComparer.Ordinal) : null),
                ("messages", messages.Count),
                ("participants", Unique(people).Select(p => p.ToString()).ToList()),
                ("attachments", attachments),
                ("source", context.SourceName),
                ("redacted", options.EmailRedact),
                ("generator", Generator),
            };
            var sb = new StringBuilder("---\n");
            foreach (var (key, value) in front)
            {
                if (value == null || value is List<string> { Count: 0 }) continue;
                sb.Append(key).Append(": ").Append(Json(value)).Append('\n'); // JSON is valid YAML
            }
            sb.Append("---\n\n").Append(body);
            var markdown = sb.ToString();

            if (options.EmailRedact)
            {
                // Every form seen, not just one per address: an attached mail may show the same address
                // under another display name, and that name must go too
                var redactor = new Redactor(people, options.EmailNames);
                redactor.LearnNames(markdown, rules);
                markdown = redactor.Redact(markdown);
            }
            return markdown.Trim() + "\n";
        }

        private static (string Body, string Subject, List<ThreadMessage> Messages) RenderThread(
            EmailDocument email, EmailRules rules, ConversionContext context, List<EmailAddress> people, int depth, bool title)
        {
            var options = context.Options;
            var messages = EmailThread.Split(email.Body, rules, options.EmailKeepSignatures);
            var top = messages[0];
            top.Sender = email.Sender;
            top.To = email.To;
            top.Cc = email.Cc;
            top.Bcc = email.Bcc;
            top.Date = email.Date;
            top.Subject = email.Subject;
            messages = Dedupe(messages);
            messages.Reverse(); // oldest first, the order people read a conversation in

            var subject = EmailThread.NormaliseSubject(
                !string.IsNullOrEmpty(email.Subject) ? email.Subject : messages.Select(m => m.Subject).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "", rules);
            foreach (var m in messages)
                people.AddRange(new[] { m.Sender }.Concat(m.To).Concat(m.Cc).Concat(m.Bcc).Where(a => a != null));

            var output = new List<string>();
            if (title) output.AddRange(new[] { $"{Heading(depth + 1)} {(subject.Length > 0 ? subject : "Email")}", "" });
            for (var i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                var heading = $"{Heading(depth + 2)} {i + 1}. {(m.Sender != null ? m.Sender.ToString() : "Unknown sender")}";
                if (!string.IsNullOrEmpty(m.Date)) heading += $" · {m.Date}";
                if (m.Forwarded) heading += " (forwarded)";
                output.Add(heading);
                var details = new List<string>();
                if (m.To.Count > 0) details.Add("To: " + string.Join("; ", m.To));
                if (m.Cc.Count > 0) details.Add("Cc: " + string.Join("; ", m.Cc));
                if (m.Bcc.Count > 0) details.Add("Bcc: " + string.Join("; ", m.Bcc));
                if (details.Count > 0) output.AddRange(new[] { "", "*" + string.Join(" · ", details) + "*" });
                output.AddRange(new[] { "", m.Body.Length > 0 ? m.Body : "*(no text)*", "" });
            }

            var attachments = email.Attachments.Where(a => !a.Inline).ToList();
            if (attachments.Count > 0)
            {
                output.AddRange(new[] { $"{Heading(depth + 2)} Attachments", "" });
                foreach (var a in attachments)
                    output.AddRange(new[] { $"{Heading(depth + 3)} {a.FileName}", "", Attachment(a, rules, context, people, depth), "" });
            }
            return (string.Join("\n", output).TrimEnd() + "\n", subject, messages);
        }

        private static string Attachment(EmailAttachment a, EmailRules rules, ConversionContext context, List<EmailAddress> people, int depth)
        {
            if (!context.Options.EmailAttachments) return "*(attachment not converted)*";
            if (a.Email != null)
            {
                // A mail attached to a mail: render it here, so its people are masked too
                var title = TitleNeeded(a.FileName, a.Email, rules);
                return RenderThread(a.Email, rules, context, people, depth + (title ? 3 : 2), title).Body.TrimEnd();
            }
            if (a.Data == null) return "*(attached Outlook item; save it separately to convert it)*";
            if (a.Data.Length > MaxAttachmentBytes) return "*(attachment too large to convert)*";

            var extension = Path.GetExtension(a.FileName).ToLowerInvariant();
            if (EmailExtensions.Contains(extension) || a.ContentType == "message/rfc822")
            {
                if (depth >= 3) return "*(attached email nested too deeply)*";
                try
                {
                    var inner = Read(a.Data, extension == ".msg" ? ".msg" : ".eml");
                    var title = TitleNeeded(a.FileName, inner, rules);
                    return RenderThread(inner, rules, context, people, depth + (title ? 3 : 2), title).Body.TrimEnd();
                }
                catch (Exception e) // a damaged attachment must not lose the whole mail
                {
                    return $"*(attached email could not be read: {e.GetType().Name})*";
                }
            }

            string text;
            try
            {
                if (DocumentConverter.TryConvertStream(extension, new MemoryStream(a.Data), context, out var converted))
                    text = converted;
                else if (TextExtensions.Contains(extension) || (a.ContentType.StartsWith("text/", StringComparison.Ordinal) && a.ContentType != "text/html"))
                    text = Utilities.TextFileEncoding.Decode(a.Data).Text;
                else if (HtmlExtensions.Contains(extension) || a.ContentType == "text/html")
                    text = HtmlText.ToMarkdown(Utilities.TextFileEncoding.Decode(a.Data).Text);
                else if (a.ContentType.StartsWith("image/", StringComparison.Ordinal) || Path.GetExtension(a.FileName) is ".png" or ".jpg" or ".jpeg" or ".gif")
                {
                    var link = context.SaveImage(new MemoryStream(a.Data), ImageType(a), Path.GetFileNameWithoutExtension(a.FileName));
                    return link.Length > 0 ? link : "*(attachment not converted)*";
                }
                else
                    return "*(attachment not converted)*";
            }
            catch (Exception e)
            {
                return $"*(attachment could not be converted: {e.GetType().Name})*";
            }
            text = text.Trim();
            return text.Length > 0 ? Demote(text, depth + 3) : "*(attachment is empty)*";
        }

        private static string ImageType(EmailAttachment a) =>
            a.ContentType.StartsWith("image/", StringComparison.Ordinal) ? a.ContentType : Path.GetExtension(a.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "image/jpeg",
            };

        private static string Heading(int level) => new('#', Math.Min(level, 6)); // Markdown has six levels; deeper nesting shares the last

        // An attached mail is usually named after its subject; don't show the name twice.
        private static bool TitleNeeded(string filename, EmailDocument email, EmailRules rules)
        {
            var name = EmailExtensions.Any(e => filename.EndsWith(e, StringComparison.OrdinalIgnoreCase)) ? Path.GetFileNameWithoutExtension(filename) : filename;
            return EmailThread.NormaliseSubject(name, rules) != EmailThread.NormaliseSubject(email?.Subject ?? "", rules);
        }

        // Pushes an attachment's headings below the email's own, outside code fences.
        private static string Demote(string markdown, int levels)
        {
            var output = new List<string>();
            string fence = null;
            foreach (var raw in markdown.Split('\n'))
            {
                var line = raw;
                var stripped = line.TrimStart();
                if (stripped.StartsWith("```", StringComparison.Ordinal) || stripped.StartsWith("~~~", StringComparison.Ordinal))
                {
                    var marker = stripped[..3];
                    fence = fence == marker ? null : (fence ?? marker);
                }
                else if (fence == null)
                {
                    var m = HeadingStart.Match(line);
                    if (m.Success)
                        line = new string('#', Math.Min(6, m.Groups[1].Length + levels)) + line[m.Groups[1].Length..];
                }
                output.Add(line);
            }
            return string.Join("\n", output);
        }

        // Drops repeats (the same message quoted twice) and empty fragments. The first message is always
        // kept: it is the mail itself, even when it is a forward with no text of its own.
        private static List<ThreadMessage> Dedupe(List<ThreadMessage> messages)
        {
            var seen = new HashSet<string>();
            var result = new List<ThreadMessage>();
            for (var i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                var key = NonWord.Replace(m.Body, " ").Trim().ToLowerInvariant();
                if (i > 0 && ((key.Length == 0 && m.Sender == null) || (key.Length > 0 && seen.Contains(key)))) continue;
                if (key.Length > 0) seen.Add(key);
                result.Add(m);
            }
            return result;
        }

        // One entry per person, preferring the version that has both name and address.
        private static List<EmailAddress> Unique(List<EmailAddress> people)
        {
            var byKey = new Dictionary<string, EmailAddress>();
            var order = new List<string>();
            var namesToKey = new Dictionary<string, string>();
            foreach (var p in people)
            {
                var nameKey = p.Name.ToLowerInvariant();
                var key = p.Email.ToLowerInvariant();
                if (key.Length == 0) key = namesToKey.TryGetValue(nameKey, out var known) && known.Length > 0 ? known : nameKey;
                if (key.Length == 0) continue;
                if (p.Name.Length > 0 && p.Email.Length > 0)
                {
                    // An earlier bare-name entry for the same person merges into this one
                    if (namesToKey.TryGetValue(nameKey, out var earlier) && earlier.Length > 0 && earlier != key && byKey.ContainsKey(earlier))
                    {
                        // Its slot takes the new key, unless the key is already listed (an address-only
                        // entry came first): then the slot goes, or the person would be listed twice
                        var index = order.IndexOf(earlier);
                        if (order.Contains(key)) order.RemoveAt(index);
                        else order[index] = key;
                        byKey.Remove(earlier);
                    }
                    namesToKey[nameKey] = key;
                }
                else if (p.Name.Length > 0)
                    namesToKey.TryAdd(nameKey, key);
                if (!byKey.ContainsKey(key))
                {
                    byKey[key] = p;
                    if (!order.Contains(key)) order.Add(key);
                }
                else if (p.Name.Length > 0 && byKey[key].Name.Length == 0)
                    byKey[key] = new EmailAddress(p.Name, byKey[key].Email);
            }
            return order.Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        }

        // The subset of JSON the front matter needs, written like Python's json.dumps(ensure_ascii=False).
        private static string Json(object value) => value switch
        {
            string s => JsonString(s),
            bool b => b ? "true" : "false",
            int n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
            IEnumerable<string> list => "[" + string.Join(", ", list.Select(JsonString)) + "]",
            _ => JsonString(value.ToString()),
        };

        private static string JsonString(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
