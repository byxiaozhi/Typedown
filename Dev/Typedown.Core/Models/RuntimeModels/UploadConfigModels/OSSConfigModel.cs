using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Typedown.Core.Services;
using Typedown.Core.Utilities;

namespace Typedown.Core.Models.UploadConfigModels
{
    /// <summary>
    /// An S3-compatible bucket (Amazon S3, Cloudflare R2, Aliyun OSS, Tencent COS, MinIO, ...). The stored names are the
    /// upstream ones: ServiceURL is the endpoint, RegionEndpoint the region, UploadPath the folder in the bucket
    /// (${year} ${month} ${day} ${filename} expand), ExternalURL the public address of the bucket (a CDN or a custom
    /// domain; empty: the object's address at the endpoint).
    /// </summary>
    public class OSSConfigModel : ConfigModel
    {
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>Kept encrypted for this Windows user (DPAPI) in the configuration, like the HedgeDoc password.</summary>
        [JsonIgnore]
        public string SecretKey { get => Secret.Unprotect(ProtectedSecretKey); set => ProtectedSecretKey = Secret.Protect(value ?? string.Empty); }

        public string ProtectedSecretKey { get; set; } = string.Empty;

        public string BucketName { get; set; } = string.Empty;

        public string ServiceURL { get; set; } = string.Empty;

        public string RegionEndpoint { get; set; } = string.Empty;

        /// <summary>endpoint/bucket/key instead of bucket.endpoint/key.</summary>
        public bool PathStyle { get; set; }

        private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(60) };

        public override async Task<string> Upload(IServiceProvider serviceProvider, string filePath)
        {
            if (string.IsNullOrWhiteSpace(ServiceURL) || string.IsNullOrWhiteSpace(BucketName) || string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrEmpty(SecretKey))
                throw new InvalidOperationException(Locale.GetString("ImageUpload.S3.Incomplete"));
            var prefix = serviceProvider.GetService<ImageAction>()?.ExpandPathVariables(UploadPath) ?? UploadPath;
            var target = new S3Uploader.Target
            {
                Endpoint = ServiceURL.Trim(),
                Region = RegionEndpoint,
                Bucket = BucketName.Trim(),
                AccessKey = AccessKey.Trim(),
                SecretKey = SecretKey,
                PathStyle = PathStyle,
                KeyPrefix = prefix,
                PublicBaseUrl = ExternalURL,
            };
            return await S3Uploader.UploadAsync(client, target, filePath);
        }
    }
}
