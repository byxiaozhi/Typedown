using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Typedown.Core.Services
{
    /// <summary>
    /// Puts an image into an S3-compatible bucket (Amazon S3, Cloudflare R2, Aliyun OSS, Tencent COS, MinIO, ...) with
    /// a Signature Version 4 request, no SDK, and gives back the address it can be read at. Plain .NET, so the tests
    /// run it on any machine.
    /// </summary>
    public static class S3Uploader
    {
        public sealed class Target
        {
            /// <summary>https://s3.us-east-1.amazonaws.com, https://&lt;account&gt;.r2.cloudflarestorage.com, http://127.0.0.1:9000 ...</summary>
            public string Endpoint { get; set; } = "";
            /// <summary>us-east-1 when empty; R2 takes "auto".</summary>
            public string Region { get; set; } = "";
            public string Bucket { get; set; } = "";
            public string AccessKey { get; set; } = "";
            public string SecretKey { get; set; } = "";
            /// <summary>endpoint/bucket/key rather than bucket.endpoint/key (MinIO; some providers want one or the other).</summary>
            public bool PathStyle { get; set; }
            /// <summary>Folder in the bucket, already expanded (2026/10); empty for the bucket's root.</summary>
            public string KeyPrefix { get; set; } = "";
            /// <summary>Where the bucket is public (a CDN or custom domain); empty: the object's address at the endpoint.</summary>
            public string PublicBaseUrl { get; set; } = "";
        }

        /// <summary>
        /// The object's key: the prefix, then the file's name with the first 8 hex digits of its SHA-256 (the same
        /// picture twice is one object; two pictures with one name are two).
        /// </summary>
        public static string KeyFor(Target target, string fileName, byte[] content)
        {
            var hash = Hex(SHA256.Create().ComputeHash(content)).Substring(0, 8);
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var name = $"{(string.IsNullOrWhiteSpace(stem) ? "image" : stem.Trim())}-{hash}{ext}";
            var prefix = (target.KeyPrefix ?? "").Replace('\\', '/').Trim('/');
            return prefix.Length == 0 ? name : prefix + "/" + name;
        }

        /// <summary>Uploads the file and returns its address.</summary>
        public static async Task<string> UploadAsync(HttpClient client, Target target, string filePath, CancellationToken cancellationToken = default)
        {
            var content = File.ReadAllBytes(filePath);
            var key = KeyFor(target, Path.GetFileName(filePath), content);
            var request = CreatePut(target, key, content, ContentType(filePath), DateTime.UtcNow);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var code = System.Text.RegularExpressions.Regex.Match(body, "<Code>([^<]*)</Code>").Groups[1].Value;
                var message = System.Text.RegularExpressions.Regex.Match(body, "<Message>([^<]*)</Message>").Groups[1].Value;
                throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}" +
                    (code.Length > 0 ? $": {code}" : "") + (message.Length > 0 ? $" - {message}" : ""));
            }
            return PublicUrl(target, key);
        }

        /// <summary>The signed PUT for one object (separate, so a test can look at it).</summary>
        public static HttpRequestMessage CreatePut(Target target, string key, byte[] content, string contentType, DateTime utcNow)
        {
            var url = ObjectUrl(target, key);
            var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new ByteArrayContent(content) };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            var amzDate = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var payloadHash = Hex(SHA256.Create().ComputeHash(content));
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["host"] = url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}",
                ["x-amz-content-sha256"] = payloadHash,
                ["x-amz-date"] = amzDate,
            };
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("Authorization",
                Authorization("PUT", url.AbsolutePath, "", headers, payloadHash, target.AccessKey, target.SecretKey, Region(target), "s3", amzDate));
            return request;
        }

        /// <summary>
        /// The Authorization header of a Signature Version 4 request. <paramref name="canonicalPath"/> is the path as
        /// sent (already URI-encoded, as S3 wants it once); <paramref name="headers"/> are the signed headers, lower case.
        /// </summary>
        public static string Authorization(string method, string canonicalPath, string canonicalQuery, SortedDictionary<string, string> headers,
            string payloadHash, string accessKey, string secretKey, string region, string service, string amzDate)
        {
            var date = amzDate.Substring(0, 8);
            var signedHeaders = string.Join(";", headers.Keys);
            var canonicalRequest = string.Join("\n",
                method,
                canonicalPath,
                canonicalQuery,
                string.Concat(headers.Select(h => $"{h.Key}:{h.Value.Trim()}\n")),
                signedHeaders,
                payloadHash);
            var scope = $"{date}/{region}/{service}/aws4_request";
            var stringToSign = string.Join("\n", "AWS4-HMAC-SHA256", amzDate, scope, Hex(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(canonicalRequest))));
            var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + secretKey), date);
            key = Hmac(key, region);
            key = Hmac(key, service);
            key = Hmac(key, "aws4_request");
            var signature = Hex(Hmac(key, stringToSign));
            return $"AWS4-HMAC-SHA256 Credential={accessKey}/{scope},SignedHeaders={signedHeaders},Signature={signature}";
        }

        public static Uri ObjectUrl(Target target, string key)
        {
            var endpoint = new Uri(target.Endpoint.Contains("://") ? target.Endpoint : "https://" + target.Endpoint);
            var path = "/" + EncodeKey(key);
            if (target.PathStyle)
                return new Uri($"{endpoint.Scheme}://{endpoint.Authority}/{Uri.EscapeDataString(target.Bucket)}{path}");
            return new Uri($"{endpoint.Scheme}://{target.Bucket}.{endpoint.Authority}{path}");
        }

        public static string PublicUrl(Target target, string key) =>
            string.IsNullOrWhiteSpace(target.PublicBaseUrl)
                ? ObjectUrl(target, key).AbsoluteUri
                : target.PublicBaseUrl.TrimEnd('/') + "/" + EncodeKey(key);

        private static string Region(Target target) => string.IsNullOrWhiteSpace(target.Region) ? "us-east-1" : target.Region.Trim();

        // Each segment URI-encoded (RFC 3986 unreserved characters kept), the slashes between them kept.
        private static string EncodeKey(string key) => string.Join("/", key.Split('/').Select(Uri.EscapeDataString));

        private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".avif" => "image/avif",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };

        private static byte[] Hmac(byte[] key, string data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));
    }
}
