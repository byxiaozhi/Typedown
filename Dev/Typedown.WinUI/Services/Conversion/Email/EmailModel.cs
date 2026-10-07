using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Typedown.WinUI.Services.Conversion
{
    // New since the fork: Outlook .msg and .eml conversion. This folder is a C# port of the MarkItDown
    // plugin in plugins/markitdown-email (Python), kept behaviour-for-behaviour identical and driven by the
    // same JSON rule files, so the app and the `markitdown` command give the same Markdown for the same
    // mail. Everything is plain rules — no AI, no network.

    // One person as a mail client shows them.
    internal sealed class EmailAddress
    {
        public EmailAddress(string name, string email)
        {
            Name = name ?? "";
            Email = email ?? "";
        }

        public string Name { get; }

        public string Email { get; }

        public override string ToString() =>
            Name.Length > 0 && Email.Length > 0 && !string.Equals(Name, Email, StringComparison.OrdinalIgnoreCase)
                ? $"{Name} <{Email}>"
                : (Name.Length > 0 ? Name : Email);
    }

    internal sealed class EmailAttachment
    {
        public string FileName { get; init; } = "attachment";

        // Null when it can't be extracted (e.g. an attached Outlook item, which is read into Email instead).
        public byte[] Data { get; init; }

        public string ContentType { get; init; } = "";

        // An image shown in the body, usually a signature logo.
        public bool Inline { get; init; }

        // An Outlook item attached to a .msg, already read.
        public EmailDocument Email { get; set; }
    }

    // The email as the readers see it, before any cleanup.
    internal sealed class EmailDocument
    {
        public string Subject { get; set; } = "";

        public EmailAddress Sender { get; set; }

        public List<EmailAddress> To { get; set; } = new();

        public List<EmailAddress> Cc { get; set; } = new();

        // Only in a sender's own copy.
        public List<EmailAddress> Bcc { get; set; } = new();

        // "YYYY-MM-DD HH:MM", or the text as written.
        public string Date { get; set; }

        // Plain text; HTML-only mail is converted before it gets here.
        public string Body { get; set; } = "";

        public List<EmailAttachment> Attachments { get; set; } = new();
    }

    internal static class EmailAddressParser
    {
        private static readonly Regex Angle = new(@"^(?<name>.*?)\s*[<\[]\s*(?:mailto:)?(?<email>[^<>\[\]\s]+@[^<>\[\]\s]+)\s*[>\]]\s*$");
        private static readonly Regex NamePart = new(@"^[A-ZÀ-ÖØ-ÞĀ-Ž][\w'’\-]*(?: [A-ZÀ-ÖØ-ÞĀ-Ž][\w'’\-]*)?$");
        private static readonly Regex Bare = new(@"^[^@\s]+@[^@\s]+$");
        private static readonly Regex GmailParenthesis = new(@"\(\s*(<[^<>]+>)\s*\)");
        private static readonly Regex CommaAfterBracket = new(@"(?<=[>\]])\s*,");
        private static readonly Regex CommaOutsideQuotes = new(@",(?=(?:[^""]*""[^""]*"")*[^""]*$)");
        private static readonly Regex Spaces = new(@"\s+");

        // One address as mail clients write it in quoted headers: "Anna Nowak <anna@x.pl>",
        // "Nowak, Anna <anna@x.pl>", "Anna Nowak [mailto:anna@x.pl]" (older Outlook), bare addresses and
        // bare names.
        public static EmailAddress Parse(string text)
        {
            text = (text ?? "").Trim().Trim(';', ',').Trim();
            text = GmailParenthesis.Replace(text, "$1"); // Gmail in Spanish: "Ana (<ana@x.es>)"
            var m = Angle.Match(text);
            if (m.Success)
                return new EmailAddress(CleanName(m.Groups["name"].Value), m.Groups["email"].Value.Trim());
            if (Bare.IsMatch(text))
                return new EmailAddress("", text);
            return new EmailAddress(CleanName(text), "");
        }

        // A To/Cc line. Outlook separates with ';', everyone else with ','.
        public static List<EmailAddress> ParseList(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return new List<EmailAddress>();
            IEnumerable<string> parts;
            if (text.Contains(';'))
                parts = text.Split(';');
            else if (text.Contains('<') || text.Contains("[mailto:"))
                // Split on commas that follow a closing bracket, so "Nowak, Anna <a@x>" stays whole.
                parts = CommaAfterBracket.Split(text);
            else
                parts = CommaOutsideQuotes.Split(text); // "Jan Kowalski, anna@x.fr", '"Nowak, Anna" a@x.fr'
            return parts.Select(Parse).Where(a => a.Name.Length > 0 || a.Email.Length > 0).ToList();
        }

        private static string CleanName(string name) =>
            FlipName(Spaces.Replace(name.Trim().Trim('\'', '"').Trim(), " "));

        // "Nowak, Anna" -> "Anna Nowak" (the order of Outlook's company directory). Only when both sides
        // look like names, so "ACME, Inc." stays as it is.
        public static string FlipName(string name)
        {
            if (name.Count(c => c == ',') == 1)
            {
                var parts = name.Split(',');
                var last = parts[0].Trim();
                var first = parts[1].Trim();
                if (NamePart.IsMatch(last) && NamePart.IsMatch(first) && !last.Contains(' '))
                    return $"{first} {last}";
            }
            return name;
        }
    }
}
