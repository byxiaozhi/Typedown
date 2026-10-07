using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit.Utils;
using OpenMcdf;

namespace Typedown.WinUI.Services.Conversion
{
    // Reading Outlook .msg files (the file you get by dragging a mail out of Outlook).
    //
    // A .msg is an OLE compound file: each MAPI property is either its own stream, named
    // "__substg1.0_<tag><type>", or (for fixed-width values such as dates and numbers) an entry in the
    // "__properties_version1.0" stream. Recipients and attachments are sub-storages with the same layout.
    internal static class MsgFileReader
    {
        private const string Properties = "__properties_version1.0";
        private const ushort PtLong = 0x0003;
        private const ushort PtSysTime = 0x0040;

        // Property ids (PidTag...)
        private const string Subject = "0037";
        private const int ClientSubmitTime = 0x0039;
        private const int MessageDeliveryTime = 0x0E06;
        private const string TransportHeaders = "007D";
        private const string SenderName = "0C1A";
        private const string SenderEmail = "0C1F";
        private const string SenderSmtp = "5D01";
        private const string DisplayTo = "0E04";
        private const string DisplayCc = "0E03";
        private const string BodyTag = "1000";
        private const string BodyHtml = "1013";
        private const int MessageCodePage = 0x3FFD;
        private const int InternetCodePage = 0x3FDE;
        private const int MessageLocale = 0x3FF1;

        private const int RecipientType = 0x0C15; // 1 To, 2 Cc, 3 Bcc
        private const string RecipientName = "3001";
        private const string RecipientEmail = "3003";
        private const string RecipientSmtp = "39FE";

        private const string AttachData = "3701";
        private const string AttachFileName = "3704";
        private const string AttachLongFileName = "3707";
        private const string AttachMime = "370E";
        private const string AttachContentId = "3712";
        private const string AttachDisplayName = "3001";

        private const string Embedded = "__substg1.0_3701000D"; // an Outlook item attached to the mail ("Forward as attachment")
        private const int MaxDepth = 3;

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
        private static readonly Regex DateHeader = new(@"^Date:[ \t]*(.*(?:\r?\n[ \t].*)*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public static EmailDocument Read(Stream stream, Func<string, string> htmlToText)
        {
            using var root = RootStorage.Open(stream, StorageModeFlags.LeaveOpen);
            return Read(root, htmlToText, embedded: false, depth: 0);
        }

        // `embedded` is an attached Outlook item, whose layout is the same as a top-level message except
        // for a shorter property stream header.
        private static EmailDocument Read(Storage storage, Func<string, string> htmlToText, bool embedded, int depth)
        {
            var reader = new PropertyReader(storage);
            var top = reader.Fixed(embedded ? 24 : 32);
            // 8-bit text is in the message's code page; when the file does not say, in the ANSI page of its locale (a Japanese mail saved
            // without one is Shift-JIS), and only then in the page the mail travelled in, which is often a different one (ISO-2022-JP).
            var internet = CodePage(top, InternetCodePage);
            var codec = CodePage(top, MessageCodePage) ?? LocaleCodePage(top) ?? internet;
            var bodyCodec = codec;

            var email = new EmailDocument { Subject = reader.String(Subject, codec) ?? "" };

            var name = reader.String(SenderName, codec) ?? "";
            var address = reader.String(SenderSmtp, codec) ?? Smtp(reader.String(SenderEmail, codec));
            if (name.Length > 0 || !string.IsNullOrEmpty(address))
                email.Sender = new EmailAddress(name, address ?? "");

            (email.To, email.Cc, email.Bcc) = Recipients(reader, codec);
            if (email.To.Count == 0 && email.Cc.Count == 0 && email.Bcc.Count == 0)
            {
                email.To = EmailAddressParser.ParseList(reader.String(DisplayTo, codec) ?? "");
                email.Cc = EmailAddressParser.ParseList(reader.String(DisplayCc, codec) ?? "");
            }

            email.Date = Date(reader, top, codec);

            var body = reader.String(BodyTag, bodyCodec);
            if (string.IsNullOrEmpty(body))
            {
                var html = reader.Binary(BodyHtml);
                // The HTML is kept as it arrived: in the page the mail travelled in, when that is known.
                body = html != null ? htmlToText(Decode(html, internet ?? bodyCodec, bodyCodec)) : htmlToText(reader.String(BodyHtml, bodyCodec) ?? "");
            }
            email.Body = body ?? "";

            email.Attachments = Attachments(reader, codec, htmlToText, depth);
            return email;
        }

        private static (List<EmailAddress>, List<EmailAddress>, List<EmailAddress>) Recipients(PropertyReader reader, Encoding codec)
        {
            var to = new List<EmailAddress>();
            var cc = new List<EmailAddress>();
            var bcc = new List<EmailAddress>();
            foreach (var storage in reader.Storages("__recip_version1.0_#"))
            {
                var recipient = new PropertyReader(storage);
                var kind = recipient.Fixed(8).TryGetValue(RecipientType, out var value) && value is uint k ? k : 1;
                var address = new EmailAddress(
                    recipient.String(RecipientName, codec) ?? "",
                    recipient.String(RecipientSmtp, codec) ?? Smtp(recipient.String(RecipientEmail, codec)) ?? "");
                if (kind == 1) to.Add(address);
                else if (kind == 2) cc.Add(address);
                else if (kind == 3) bcc.Add(address);
            }
            return (to, cc, bcc);
        }

        private static List<EmailAttachment> Attachments(PropertyReader reader, Encoding codec, Func<string, string> htmlToText, int depth)
        {
            var result = new List<EmailAttachment>();
            foreach (var storage in reader.Storages("__attach_version1.0_#"))
            {
                var attachment = new PropertyReader(storage);
                var filename = attachment.String(AttachLongFileName, codec)
                    ?? attachment.String(AttachFileName, codec)
                    ?? attachment.String(AttachDisplayName, codec)
                    ?? "attachment";
                var contentType = attachment.String(AttachMime, codec) ?? "";
                var data = attachment.Binary(AttachData); // null for an attached Outlook item
                var inline = !string.IsNullOrEmpty(attachment.String(AttachContentId, codec))
                    && (contentType.StartsWith("image/", StringComparison.Ordinal) || ImageExtensions.Any(e => filename.EndsWith(e, StringComparison.OrdinalIgnoreCase)));
                var item = new EmailAttachment { FileName = filename, Data = data, ContentType = contentType, Inline = inline };
                if (data == null && depth < MaxDepth && storage.TryOpenStorage(Embedded, out var embedded))
                    item.Email = Read(embedded, htmlToText, embedded: true, depth + 1);
                result.Add(item);
            }
            return result;
        }

        private static string Date(PropertyReader reader, Dictionary<int, object> top, Encoding codec)
        {
            var headers = reader.String(TransportHeaders, codec);
            if (!string.IsNullOrEmpty(headers))
            {
                var headerSection = Regex.Split(headers.Replace("\r\n", "\n"), @"\n[ \t]*\n")[0];
                var m = DateHeader.Match(headerSection);
                if (m.Success && DateUtils.TryParse(Regex.Replace(m.Groups[1].Value, @"\r?\n", ""), out var date))
                    return EmailDates.Format(date.DateTime);
            }
            foreach (var tag in new[] { ClientSubmitTime, MessageDeliveryTime })
                // Stored in UTC; shown in this computer's time zone, like Outlook does.
                if (top.TryGetValue(tag, out var value) && value is DateTime utc)
                    return EmailDates.Format(utc.ToLocalTime());
            return null;
        }

        // Exchange-internal senders carry an X.500 path ("/O=EXCHANGELABS/OU=...") here.
        private static string Smtp(string address) =>
            address != null && address.Contains('@') && !address.StartsWith('/') ? address : null;

        private static Encoding CodePage(Dictionary<int, object> properties, int tag)
        {
            if (!properties.TryGetValue(tag, out var value) || value is not uint codePage || codePage == 0) return null;
            // UTF-16 and UTF-32 code pages can't describe 8-bit strings
            if (codePage is 1200 or 1201 or 12000 or 12001) return null;
            return Strict((int)codePage);
        }

        private static Encoding LocaleCodePage(Dictionary<int, object> properties)
        {
            if (!properties.TryGetValue(MessageLocale, out var value) || value is not uint lcid || lcid == 0) return null;
            try
            {
                var ansi = new System.Globalization.CultureInfo((int)lcid).TextInfo.ANSICodePage;
                return ansi is 0 or 65001 or 1200 or 1201 ? null : Strict(ansi);
            }
            catch (Exception) { return null; }
        }

        private static Encoding Strict(int codePage)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try { return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }
            catch (Exception) { return null; }
        }

        private static string Decode(byte[] data, Encoding codec, Encoding fallback = null)
        {
            var length = data.Length;
            while (length > 0 && data[length - 1] == 0) length--;
            foreach (var candidate in new[] { codec, fallback, Strict(65001), Strict(1252) })
            {
                if (candidate == null) continue;
                try { return candidate.GetString(data, 0, length); }
                catch (DecoderFallbackException) { }
            }
            return Encoding.UTF8.GetString(data, 0, length);
        }

        private sealed class PropertyReader
        {
            private readonly Storage storage;

            public PropertyReader(Storage storage) => this.storage = storage;

            private byte[] ReadStream(string name)
            {
                try
                {
                    if (!storage.TryOpenStream(name, out var stream)) return null;
                    using (stream)
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        return buffer.ToArray();
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // Only direct children: an attached Outlook item has its own recipients and attachments
            // nested deeper, which belong to that item.
            public IEnumerable<Storage> Storages(string prefix) =>
                storage.EnumerateEntries()
                    .Where(e => e.Type == EntryType.Storage && e.Name.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(e => e.Name)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .Select(n => storage.OpenStorage(n))
                    .ToList();

            public string String(string tag, Encoding codec)
            {
                var data = ReadStream($"__substg1.0_{tag}001F"); // PT_UNICODE
                if (data != null)
                {
                    var text = Encoding.Unicode.GetString(data, 0, data.Length & ~1).TrimEnd('\0').Trim();
                    return text.Length > 0 ? text : null;
                }
                data = ReadStream($"__substg1.0_{tag}001E"); // PT_STRING8, in the message's code page
                if (data != null)
                {
                    var text = Decode(data, codec).Trim();
                    return text.Length > 0 ? text : null;
                }
                return null;
            }

            public byte[] Binary(string tag) => ReadStream($"__substg1.0_{tag}0102");

            public Dictionary<int, object> Fixed(int headerSize)
            {
                var data = ReadStream(Properties) ?? Array.Empty<byte>();
                var values = new Dictionary<int, object>();
                for (var offset = headerSize; offset < data.Length - 15; offset += 16)
                {
                    var tag = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
                    var kind = (ushort)(tag & 0xFFFF);
                    var prop = (int)(tag >> 16);
                    if (kind == PtLong)
                        values[prop] = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 8));
                    else if (kind == PtSysTime)
                    {
                        var ticks = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(offset + 8));
                        if (ticks > 0 && ticks < DateTime.MaxValue.ToFileTimeUtc())
                            values[prop] = DateTime.FromFileTimeUtc(ticks);
                    }
                }
                return values;
            }
        }
    }
}
