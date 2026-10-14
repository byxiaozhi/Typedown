using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Typedown.Automation
{
    /// <summary>The text rules every document method shares (docs/automation-api-spec.md, sections 1.4 and 3.2).</summary>
    public static class DocumentText
    {
        /// <summary>SHA-256 of the exact text's UTF-8 bytes, lowercase hex: <c>contentHash</c> and <c>baseContentHash</c>.</summary>
        public static string ContentHash(string text)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(text ?? ""));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }

        /// <summary>
        /// Document text uses "\n" only; the file's own line ending is metadata. Text with "\r" is refused rather than
        /// converted, and so is text that is not valid UTF-16 (a lone surrogate cannot be saved or hashed faithfully).
        /// </summary>
        public static void Validate(string field, string text)
        {
            if (text.IndexOf('\r') >= 0) throw Params.Invalid(field, "carriageReturn", $"'{field}' contains \"\\r\"; document text uses \"\\n\" line endings only.");
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) { i++; continue; }
                if (char.IsSurrogate(c)) throw Params.Invalid(field, "loneSurrogate");
            }
        }

        /// <summary>
        /// <c>document.replaceText</c>: replaces every ordinal, non-overlapping, left-to-right occurrence of
        /// <paramref name="find"/>, but only when there are exactly <paramref name="expectedCount"/> of them.
        /// </summary>
        /// <exception cref="AutomationException"><c>match_count_mismatch</c> with the actual count; nothing is replaced.</exception>
        public static string ReplaceText(string text, string find, string replacement, long expectedCount)
        {
            if (find.Length == 0) throw Params.Invalid("find", "empty");
            var count = 0L;
            for (var at = text.IndexOf(find, StringComparison.Ordinal); at >= 0; at = text.IndexOf(find, at + find.Length, StringComparison.Ordinal))
                count++;
            if (count != expectedCount)
                throw new AutomationException(AutomationErrorKind.match_count_mismatch, $"Expected {expectedCount} match(es), found {count}.",
                    new Dictionary<string, object?> { ["expectedCount"] = expectedCount, ["actualCount"] = count });
            return count == 0 ? text : text.Replace(find, replacement);
        }
    }
}
