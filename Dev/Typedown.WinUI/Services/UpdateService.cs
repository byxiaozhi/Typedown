using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Typedown.WinUI.Services
{
    // New since the fork: Caret is distributed through GitHub Releases rather than the Store, so
    // nothing else tells an installed copy that a newer version exists. This asks GitHub's public
    // "latest release" endpoint (drafts and pre-releases are never returned by it) and compares the
    // tag with the running version. The request carries no information about the user or their notes,
    // only the User-Agent GitHub requires.
    public static class UpdateService
    {
        public const string ReleasesPageUrl = "https://github.com/fegyenc/Caret/releases/latest";

        private const string LatestReleaseApiUrl = "https://api.github.com/repos/fegyenc/Caret/releases/latest";

        private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public record ReleaseInfo(Version Version, string DisplayVersion, string PageUrl);

        // Returns null when GitHub can't be reached or the answer isn't usable — callers treat that as
        // "nothing to report" rather than an error.
        public static async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
                request.Headers.UserAgent.ParseAdd($"Caret/{Normalize(Config.AppVersionNumber)}");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var version = ParseVersion(json.Value<string>("tag_name"));
                if (version == null) return null;
                // Only ever open a page in this repository's releases, whatever the response says.
                var pageUrl = json.Value<string>("html_url");
                if (pageUrl == null || !pageUrl.StartsWith("https://github.com/fegyenc/Caret/releases/", StringComparison.OrdinalIgnoreCase))
                    pageUrl = ReleasesPageUrl;
                return new ReleaseInfo(version, ToDisplay(version), pageUrl);
            }
            catch
            {
                return null;
            }
        }

        public static bool IsNewerThanRunning(ReleaseInfo release) =>
            release != null && Normalize(release.Version) > Normalize(Config.AppVersionNumber);

        // "v1.0.8" / "1.0.8" / "1.0.8.0" → 1.0.8.0; anything else → null.
        public static Version ParseVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var text = tag.Trim().TrimStart('v', 'V');
            return Version.TryParse(text, out var version) ? Normalize(version) : null;
        }

        // "1.0.8.0" → "1.0.8", "1.2.0.0" → "1.2.0", "1.0.8.1" → "1.0.8.1".
        public static string ToDisplay(Version version)
        {
            var v = Normalize(version);
            return v.Revision > 0 ? v.ToString(4) : v.ToString(3);
        }

        private static Version Normalize(Version v) =>
            new(Math.Max(v.Major, 0), Math.Max(v.Minor, 0), Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }
}
