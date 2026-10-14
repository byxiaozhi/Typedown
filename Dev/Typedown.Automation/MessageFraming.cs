using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Automation
{
    public enum FrameStatus
    {
        /// <summary>A whole message; <see cref="Frame.Body"/> holds its bytes.</summary>
        Message,
        /// <summary>The peer closed the stream between messages.</summary>
        EndOfStream,
        /// <summary>
        /// The declared length exceeds the limit. The body was read and thrown away, so the stream is still in step
        /// and the connection can answer <c>message_too_large</c> and carry on.
        /// </summary>
        TooLarge,
    }

    public readonly struct Frame
    {
        public Frame(FrameStatus status, byte[]? body, long declaredLength)
        {
            Status = status;
            Body = body;
            DeclaredLength = declaredLength;
        }

        public FrameStatus Status { get; }
        public byte[]? Body { get; }
        public long DeclaredLength { get; }
    }

    /// <summary>The stream no longer carries valid frames: a malformed header or a body cut short. The connection ends.</summary>
    public sealed class FramingException : Exception
    {
        public FramingException(string message) : base(message) { }
    }

    /// <summary>
    /// <c>Content-Length</c> framing as in the Language Server Protocol (docs/automation-api-spec.md, section 1.1):
    /// ASCII header lines ending in CRLF, a blank line, then exactly that many bytes of UTF-8 JSON. Headers other than
    /// Content-Length (Content-Type) are accepted and ignored.
    /// </summary>
    public sealed class MessageFraming
    {
        /// <summary>A header block larger than this is not a header.</summary>
        public const int MaxHeaderBytes = 1024;

        private readonly Stream stream;
        private readonly long maxMessageBytes;
        private readonly byte[] one = new byte[1];
        private readonly SemaphoreSlim writeLock = new(1, 1);

        public MessageFraming(Stream stream, long maxMessageBytes)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (maxMessageBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxMessageBytes));
            this.maxMessageBytes = maxMessageBytes;
        }

        public long MaxMessageBytes => maxMessageBytes;

        /// <summary>Reads the next frame. Only one reader at a time.</summary>
        public async Task<Frame> ReadAsync(CancellationToken cancellationToken = default)
        {
            long? length = null;
            var headerBytes = 0;
            var line = new StringBuilder();
            while (true)
            {
                var read = await stream.ReadAsync(one, 0, 1, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    if (headerBytes == 0) return new Frame(FrameStatus.EndOfStream, null, 0);
                    throw new FramingException("The stream ended inside a header.");
                }
                if (++headerBytes > MaxHeaderBytes) throw new FramingException("The header is too long.");
                var b = one[0];
                if (b > 0x7f) throw new FramingException("The header is not ASCII.");
                if (b != '\n')
                {
                    line.Append((char)b);
                    continue;
                }
                if (line.Length == 0 || line[line.Length - 1] != '\r') throw new FramingException("A header line does not end in CRLF.");
                line.Length--;
                if (line.Length == 0) break;
                var text = line.ToString();
                line.Clear();
                var colon = text.IndexOf(':');
                if (colon <= 0) throw new FramingException("A header line has no name.");
                var name = text.Substring(0, colon).Trim();
                var value = text.Substring(colon + 1).Trim();
                if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                if (length != null) throw new FramingException("Content-Length appears twice.");
                if (value.Length == 0 || value.Length > 18 || !IsDigits(value)) throw new FramingException("Content-Length is not a non-negative integer.");
                length = long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (length == null) throw new FramingException("The header has no Content-Length.");

            if (length.Value > maxMessageBytes)
            {
                await SkipAsync(length.Value, cancellationToken).ConfigureAwait(false);
                return new Frame(FrameStatus.TooLarge, null, length.Value);
            }
            var body = new byte[length.Value];
            var offset = 0;
            while (offset < body.Length)
            {
                var read = await stream.ReadAsync(body, offset, body.Length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new FramingException("The stream ended inside a message body.");
                offset += read;
            }
            return new Frame(FrameStatus.Message, body, length.Value);
        }

        private async Task SkipAsync(long count, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (count > 0)
            {
                var read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, count), cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new FramingException("The stream ended inside a message body.");
                count -= read;
            }
        }

        private static bool IsDigits(string value)
        {
            foreach (var c in value) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>Writes one frame. Safe to call from several threads; frames never interleave.</summary>
        public async Task WriteAsync(byte[] body, CancellationToken cancellationToken = default)
        {
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, 0, body.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }
    }
}
