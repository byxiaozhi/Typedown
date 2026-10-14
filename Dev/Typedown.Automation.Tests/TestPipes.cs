using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Typedown.Automation.Tests
{
    /// <summary>Two real OS pipes joined into a duplex stream per side, like a named-pipe connection.</summary>
    internal sealed class DuplexStream : Stream
    {
        private readonly Stream input, output;
        public DuplexStream(Stream input, Stream output) { this.input = input; this.output = output; }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => output.Flush();
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken ct) => input.ReadAsync(buffer, offset, count, ct);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken ct) => output.WriteAsync(buffer, offset, count, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public void CloseOutput() => output.Dispose();
        protected override void Dispose(bool disposing) { if (disposing) { input.Dispose(); output.Dispose(); } base.Dispose(disposing); }
    }

    internal static class TestPipes
    {
        public static (DuplexStream server, DuplexStream client) Create()
        {
            var toServer = new AnonymousPipeServerStream(PipeDirection.Out);
            var serverIn = new AnonymousPipeClientStream(PipeDirection.In, toServer.ClientSafePipeHandle);
            var toClient = new AnonymousPipeServerStream(PipeDirection.Out);
            var clientIn = new AnonymousPipeClientStream(PipeDirection.In, toClient.ClientSafePipeHandle);
            return (new DuplexStream(serverIn, toClient), new DuplexStream(clientIn, toServer));
        }

        public static byte[] Frame(string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            var all = new byte[header.Length + body.Length];
            header.CopyTo(all, 0);
            body.CopyTo(all, header.Length);
            return all;
        }

        public static async Task SendRawAsync(Stream stream, byte[] bytes)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        public static async Task<JObject> ReadMessageAsync(MessageFraming framing)
        {
            var frame = await framing.ReadAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (frame.Status != FrameStatus.Message) throw new InvalidOperationException($"expected a message, got {frame.Status}");
            return JObject.Parse(Encoding.UTF8.GetString(frame.Body!));
        }
    }
}
