using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Typedown.WinUI.Utilities
{
    // New since the fork: reads a text file the way Notepad does instead of assuming UTF-8.
    //   1. A byte-order mark decides (UTF-8, UTF-16, UTF-32).
    //   2. Otherwise the bytes are decoded as strict UTF-8.
    //   3. Only if they aren't valid UTF-8, Windows' own ANSI code page is used (Windows-1250 on
    //      Central European systems, Windows-1252 on Western European ones...).
    // Step 3 is for Markdown written by tools that use the legacy code page — most commonly
    // `markitdown file.docx > file.md` in a Command Prompt, where Python writes in the ANSI code page:
    // read as UTF-8, a Polish "Zażółć" came out as "Za���". Caret always saves UTF-8, so such a file is
    // converted the next time it's saved.
    public static class TextFileEncoding
    {
        public sealed record Result(string Text, Encoding LegacyEncoding)
        {
            // Null when the file was UTF-8 (or had a byte-order mark): nothing to tell the user.
            public string LegacyName => LegacyEncoding == null ? null : $"Windows-{LegacyEncoding.CodePage}";
        }

        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        public static async Task<Result> ReadAsync(string path) => Decode(await File.ReadAllBytesAsync(path));

        public static Result Decode(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new Result(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), null);
            if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
                return new Result(Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4), null);
            if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                return new Result(new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetString(bytes, 4, bytes.Length - 4), null);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return new Result(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), null);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return new Result(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), null);
            try
            {
                return new Result(StrictUtf8.GetString(bytes), null);
            }
            catch (DecoderFallbackException)
            {
                var legacy = LegacyEncoding();
                return new Result(legacy.GetString(bytes), legacy);
            }
        }

        // The system's ANSI code page — the one Python and other legacy tools write with. A system set
        // to "Beta: Use Unicode UTF-8" reports 65001, which can't be the answer for bytes that just
        // failed as UTF-8, so Windows-1252 is used then.
        public static Encoding LegacyEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var codePage = (int)GetACP();
            if (codePage == 65001 || codePage == 0) codePage = 1252;
            try { return Encoding.GetEncoding(codePage); }
            catch { return Encoding.GetEncoding(1252); }
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetACP();
    }
}
