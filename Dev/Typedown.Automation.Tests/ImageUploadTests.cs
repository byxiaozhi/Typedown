using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Typedown.Core.Services;
using Typedown.Core.Utilities;
using Xunit;

namespace Typedown.Automation.Tests
{
    /// <summary>File > Upload local images: which images a document has, and the S3-compatible upload.</summary>
    public class ImageUploadTests
    {
        private static List<string> Addresses(string text) => MarkdownImages.Find(text).Select(r => r.Address).ToList();

        [Fact]
        public void Images_are_found_where_Markdown_has_them_and_not_in_code()
        {
            var text = "![a](img/a.png) ![b](<my pics/b.png> \"Title\") ![c](c(1).png)\n" +
                       "[link](not-an-image.png) `![span](span.png)`\n\n" +
                       "```md\n![fenced](fenced.png)\n```\n\n" +
                       "![ref][r1] ![r2][] ![R3]\n\n[r1]: ref1.png\n[r2]: <ref 2.png>\n[r3]: ref3.png \"t\"\n[unused]: unused.png\n" +
                       "<img src=\"html.png\" alt=x> <IMG SRC='html2.png'>\n\\![escaped](escaped.png)";
            Assert.Equal(new[] { "img/a.png", "my pics/b.png", "c(1).png", "ref1.png", "ref 2.png", "ref3.png", "html.png", "html2.png" }, Addresses(text));
        }

        [Fact]
        public void Local_is_a_path_or_file_and_not_the_web()
        {
            foreach (var local in new[] { "a.png", "./img/a.png", "../a b.png", "C:\\pics\\a.png", "C:/pics/a.png", "/home/a.png", "file:///C:/a.png", "%E5%9B%BE.png" })
                Assert.True(MarkdownImages.IsLocal(local), local);
            foreach (var remote in new[] { "https://x.test/a.png", "http://x.test/a.png", "data:image/png;base64,AA==", "//cdn.test/a.png", "#anchor", "" })
                Assert.False(MarkdownImages.IsLocal(remote), remote);
        }

        [Fact]
        public void Replacing_changes_only_the_addresses_named()
        {
            var text = "![a](a.png) and ![again](a.png)\n`![a](a.png)`\n![b][b]\n\n[b]: <b 1.png>\n<img src=\"a.png\">";
            var replaced = MarkdownImages.Replace(text, new Dictionary<string, string> { ["a.png"] = "https://img.test/a.png", ["b 1.png"] = "https://img.test/b.png" });
            Assert.Equal("![a](https://img.test/a.png) and ![again](https://img.test/a.png)\n`![a](a.png)`\n![b][b]\n\n[b]: <https://img.test/b.png>\n<img src=\"https://img.test/a.png\">", replaced);
        }

        // https://docs.aws.amazon.com/AmazonS3/latest/API/sig-v4-header-based-auth.html (GET Object example)
        [Fact]
        public void The_signature_matches_AWSs_own_example()
        {
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = "examplebucket.s3.amazonaws.com",
                ["range"] = "bytes=0-9",
                ["x-amz-content-sha256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                ["x-amz-date"] = "20130524T000000Z",
            };
            var authorization = S3Uploader.Authorization("GET", "/test.txt", "", headers, headers["x-amz-content-sha256"],
                "AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1", "s3", "20130524T000000Z");
            Assert.Equal("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41", authorization);
        }

        // The same page, PUT Object example.
        [Fact]
        public void The_signature_of_a_put_matches_AWSs_own_example()
        {
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["date"] = "Fri, 24 May 2013 00:00:00 GMT",
                ["host"] = "examplebucket.s3.amazonaws.com",
                ["x-amz-content-sha256"] = "44ce7dd67c959e0d3524ffac1771dfbba87d2b6b4b4e99e42034a8b803f8b072",
                ["x-amz-date"] = "20130524T000000Z",
                ["x-amz-storage-class"] = "REDUCED_REDUNDANCY",
            };
            var authorization = S3Uploader.Authorization("PUT", "/test%24file.text", "", headers, headers["x-amz-content-sha256"],
                "AKIAIOSFODNN7EXAMPLE", "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY", "us-east-1", "s3", "20130524T000000Z");
            Assert.EndsWith("Signature=98ad721746da40c64f1a55b78f14c238d841ea1380cd77a1b5971af0ece108bd", authorization);
        }

        [Fact]
        public void Objects_are_addressed_by_host_or_by_path()
        {
            var target = new S3Uploader.Target { Endpoint = "https://s3.example.test", Bucket = "pics", AccessKey = "AK", SecretKey = "SK", KeyPrefix = "/blog/2026/" };
            var key = S3Uploader.KeyFor(target, "我的 图.PNG", Encoding.UTF8.GetBytes("x"));
            Assert.Equal("blog/2026/我的 图-2d711642.png", key);
            Assert.Equal("https://pics.s3.example.test/blog/2026/%E6%88%91%E7%9A%84%20%E5%9B%BE-2d711642.png", S3Uploader.PublicUrl(target, key));
            target.PathStyle = true;
            target.Endpoint = "http://127.0.0.1:9000";
            Assert.Equal("http://127.0.0.1:9000/pics/blog/2026/%E6%88%91%E7%9A%84%20%E5%9B%BE-2d711642.png", S3Uploader.PublicUrl(target, key));
            target.PublicBaseUrl = "https://img.example.test/";
            Assert.Equal("https://img.example.test/blog/2026/%E6%88%91%E7%9A%84%20%E5%9B%BE-2d711642.png", S3Uploader.PublicUrl(target, key));

            var put = S3Uploader.CreatePut(target, key, Encoding.UTF8.GetBytes("x"), "image/png", new DateTime(2026, 10, 3, 1, 2, 3, DateTimeKind.Utc));
            Assert.Equal("http://127.0.0.1:9000/pics/blog/2026/%E6%88%91%E7%9A%84%20%E5%9B%BE-2d711642.png", put.RequestUri!.AbsoluteUri);
            Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AK/20261003/us-east-1/s3/aws4_request,SignedHeaders=host;x-amz-content-sha256;x-amz-date,Signature=",
                put.Headers.GetValues("Authorization").Single());
            Assert.Equal("20261003T010203Z", put.Headers.GetValues("x-amz-date").Single());
        }

        private sealed class Answer : HttpMessageHandler
        {
            public HttpRequestMessage? Seen;
            public byte[]? Body;
            public HttpStatusCode Status = HttpStatusCode.OK;
            public string Reply = "";
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Seen = request;
                Body = await request.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(Status) { Content = new StringContent(Reply) };
            }
        }

        [Fact]
        public async Task An_upload_puts_the_file_and_says_where_it_is_or_what_the_store_refused()
        {
            var file = Path.Combine(Path.GetTempPath(), "s3-upload-test.png");
            File.WriteAllBytes(file, new byte[] { 1, 2, 3 });
            var target = new S3Uploader.Target { Endpoint = "https://s3.example.test", Bucket = "pics", AccessKey = "AK", SecretKey = "SK", Region = "auto" };
            var answer = new Answer();
            var url = await S3Uploader.UploadAsync(new HttpClient(answer), target, file);
            Assert.StartsWith("https://pics.s3.example.test/s3-upload-test-", url);
            Assert.Equal(HttpMethod.Put, answer.Seen!.Method);
            Assert.Equal(new byte[] { 1, 2, 3 }, answer.Body);
            Assert.Equal("image/png", answer.Seen.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("/auto/s3/aws4_request", answer.Seen.Headers.GetValues("Authorization").Single());

            answer.Status = HttpStatusCode.Forbidden;
            answer.Reply = "<Error><Code>SignatureDoesNotMatch</Code><Message>The request signature we calculated does not match</Message></Error>";
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => S3Uploader.UploadAsync(new HttpClient(answer), target, file));
            Assert.Contains("403", error.Message);
            Assert.Contains("SignatureDoesNotMatch", error.Message);
        }
    }
}
