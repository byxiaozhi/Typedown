using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Typedown.Automation.Tests
{
    public class FramingTests
    {
        private static MessageFraming Over(string raw, long max = 1024) => new(new MemoryStream(Encoding.UTF8.GetBytes(raw)), max);

        [Fact]
        public async Task Reads_back_to_back_messages_and_then_the_end()
        {
            var framing = Over("Content-Length: 2\r\n\r\n{}Content-Type: application/json\r\ncontent-length: 7\r\n\r\n{\"a\":1}");
            var first = await framing.ReadAsync();
            Assert.Equal("{}", Encoding.UTF8.GetString(first.Body!));
            var second = await framing.ReadAsync();
            Assert.Equal("{\"a\":1}", Encoding.UTF8.GetString(second.Body!));
            Assert.Equal(FrameStatus.EndOfStream, (await framing.ReadAsync()).Status);
        }

        [Fact]
        public async Task Length_counts_utf8_bytes_not_characters()
        {
            var body = "{\"t\":\"中文😀\"}";
            var framing = Over($"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");
            Assert.Equal(body, Encoding.UTF8.GetString((await framing.ReadAsync()).Body!));
        }

        [Fact]
        public async Task A_message_split_across_many_writes_is_reassembled()
        {
            var (server, client) = TestPipes.Create();
            var framing = new MessageFraming(server, 1024);
            var bytes = TestPipes.Frame("{\"jsonrpc\":\"2.0\"}");
            var read = framing.ReadAsync();
            foreach (var b in bytes) await TestPipes.SendRawAsync(client, new[] { b });
            Assert.Equal("{\"jsonrpc\":\"2.0\"}", Encoding.UTF8.GetString((await read).Body!));
        }

        [Theory]
        [InlineData("Content-Length: 5\n\n{}")] // LF only
        [InlineData("Content-Length: -1\r\n\r\n")]
        [InlineData("Content-Length: 1x\r\n\r\n")]
        [InlineData("Content-Length: 2\r\nContent-Length: 2\r\n\r\n{}")]
        [InlineData("Content-Type: x\r\n\r\n{}")] // no length
        [InlineData("garbage\r\n\r\n")]
        [InlineData("Content-Length: 10\r\n\r\n{}")] // body cut short
        [InlineData("Content-Length: 2\r\n")] // ends inside the header
        [InlineData("Content-Léngth: 2\r\n\r\n{}")] // not ASCII
        public async Task Malformed_streams_end_the_connection(string raw)
        {
            await Assert.ThrowsAsync<FramingException>(() => Over(raw).ReadAsync());
        }

        [Fact]
        public async Task An_endless_header_is_refused()
        {
            await Assert.ThrowsAsync<FramingException>(() => Over("X-Pad: " + new string('a', 5000) + "\r\n\r\n").ReadAsync());
        }

        [Fact]
        public async Task An_oversized_body_is_skipped_and_the_stream_stays_in_step()
        {
            var big = new string('x', 5000);
            var framing = Over($"Content-Length: {big.Length}\r\n\r\n{big}Content-Length: 2\r\n\r\n{{}}", max: 100);
            var skipped = await framing.ReadAsync();
            Assert.Equal(FrameStatus.TooLarge, skipped.Status);
            Assert.Equal(5000, skipped.DeclaredLength);
            Assert.Equal("{}", Encoding.UTF8.GetString((await framing.ReadAsync()).Body!));
        }

        [Fact]
        public async Task Concurrent_writers_never_interleave_frames()
        {
            var sink = new MemoryStream();
            var framing = new MessageFraming(sink, 1 << 20);
            var bodies = new byte[50][];
            for (var i = 0; i < bodies.Length; i++) bodies[i] = Encoding.UTF8.GetBytes("{\"n\":" + i + ",\"pad\":\"" + new string('p', 3000) + "\"}");
            await Task.WhenAll(System.Linq.Enumerable.Range(0, bodies.Length).Select(i => Task.Run(() => framing.WriteAsync(bodies[i]))));
            var reader = new MessageFraming(new MemoryStream(sink.ToArray()), 1 << 20);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (var i = 0; i < bodies.Length; i++) seen.Add(Encoding.UTF8.GetString((await reader.ReadAsync()).Body!));
            Assert.Equal(bodies.Length, seen.Count);
            Assert.Equal(FrameStatus.EndOfStream, (await reader.ReadAsync()).Status);
        }
    }
}
