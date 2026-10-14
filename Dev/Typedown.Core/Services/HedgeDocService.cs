using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Typedown.Core.Utilities;

namespace Typedown.Core.Services
{
    public class HedgeDocShareResult
    {
        /// <summary>The editable note (what `POST /new` redirected to).</summary>
        public string NoteUrl { get; set; }

        /// <summary>The published read-only view (`/s/…`), when publishing was requested and succeeded.</summary>
        public string PublishedUrl { get; set; }

        public string ShareUrl => PublishedUrl ?? NoteUrl;
    }

    /// <summary>
    /// HedgeDoc 1.x HTTP API: notes are created by posting markdown to <c>/new</c> (302 → note URL), optionally
    /// published via <c>/{id}/publish</c> (302 → <c>/s/{shortId}</c>). Authentication is a session cookie from the
    /// email login form; anonymous use works when the server allows it. HedgeDoc 2 has a different (token) API and
    /// is not covered yet.
    /// </summary>
    public static class HedgeDocService
    {
        public static string NormalizeServer(string server)
        {
            server = server?.Trim() ?? "";
            if (server.Length == 0) return "";
            if (!server.Contains("://")) server = "https://" + server;
            return server.TrimEnd('/');
        }

        private static HttpClient CreateClient(CookieContainer cookies)
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = cookies };
            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Typedown.Automation.Brand.Name);
            return client;
        }

        public static async Task<HedgeDocShareResult> ShareAsync(string server, string markdown, string email, string password, bool publish)
        {
            server = NormalizeServer(server);
            if (server.Length == 0) throw new ArgumentException("HedgeDoc server is not configured.");
            var cookies = new CookieContainer();
            using var client = CreateClient(cookies);
            if (!string.IsNullOrWhiteSpace(email))
                await LoginAsync(client, server, email, password);

            using var content = new StringContent(markdown ?? "", Encoding.UTF8, "text/markdown");
            using var response = await client.PostAsync($"{server}/new", content);
            var noteUrl = ResolveRedirect(server, response, "create the note");
            var result = new HedgeDocShareResult { NoteUrl = noteUrl };
            if (publish)
            {
                try
                {
                    result.PublishedUrl = await FollowRedirectsAsync(client, server, $"{noteUrl}/publish", "publish the note");
                }
                catch (Exception ex)
                {
                    // The note exists; a failed publish (guests not allowed to publish) still yields an editable link.
                    Log.Debug($"HedgeDoc publish failed: {ex.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Walks a redirect chain by hand (the client never auto-follows) and returns the final address. A server
        /// behind a proxy may answer with an <c>http://</c> location that the proxy then bounces back to https and only
        /// afterwards to <c>/s/…</c>, so a single hop would report an intermediate URL.
        /// </summary>
        private static async Task<string> FollowRedirectsAsync(HttpClient client, string server, string url, string action)
        {
            for (var hop = 0; hop < 6; hop++)
            {
                using var response = await client.GetAsync(url);
                var code = (int)response.StatusCode;
                if (code < 300 || code >= 400 || response.Headers.Location == null)
                {
                    if (hop == 0) ResolveRedirect(server, response, action); // throws with a readable reason
                    return url;
                }
                url = NormalizeLocation(server, response.Headers.Location);
            }
            return url;
        }

        /// <summary>Checks the server and login; returns a short human-readable status line.</summary>
        public static async Task<string> TestAsync(string server, string email, string password)
        {
            server = NormalizeServer(server);
            if (server.Length == 0) throw new ArgumentException("Server address is empty.");
            var cookies = new CookieContainer();
            using var client = CreateClient(cookies);
            using var status = await client.GetAsync($"{server}/status");
            if (status.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"{server}/status returned {(int)status.StatusCode}; this does not look like a HedgeDoc 1.x server.");
            if (string.IsNullOrWhiteSpace(email))
                return "HedgeDoc 1.x, anonymous";
            var name = await LoginAsync(client, server, email, password);
            return $"HedgeDoc 1.x, signed in as {name}";
        }

        /// <summary>Email/password form login; returns the display name. Throws when the session is not established.</summary>
        private static async Task<string> LoginAsync(HttpClient client, string server, string email, string password)
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = email, ["password"] = password ?? "" });
            using var login = await client.PostAsync($"{server}/login", form);
            using var me = await client.GetAsync($"{server}/me");
            var json = await me.Content.ReadAsStringAsync();
            var token = Newtonsoft.Json.Linq.JObject.Parse(json);
            if (token["status"]?.ToString() != "ok")
                throw new UnauthorizedAccessException("HedgeDoc login failed: check the email address and password (email login must be enabled on the server).");
            return token["name"]?.ToString() ?? email;
        }

        // Relative locations are resolved against the server; absolute ones for the same host take the configured
        // scheme (a misconfigured serverURL/proxy commonly hands out http:// for an https site).
        private static string NormalizeLocation(string server, Uri location)
        {
            if (!location.IsAbsoluteUri)
                return $"{server}/{location.ToString().TrimStart('/')}".TrimEnd('/');
            var serverUri = new Uri(server);
            if (string.Equals(location.Host, serverUri.Host, StringComparison.OrdinalIgnoreCase) && location.Scheme != serverUri.Scheme)
                return $"{serverUri.Scheme}://{serverUri.Authority}{location.PathAndQuery}".TrimEnd('/');
            return location.ToString().TrimEnd('/');
        }

        private static string ResolveRedirect(string server, HttpResponseMessage response, string action)
        {
            var code = (int)response.StatusCode;
            if (code >= 300 && code < 400 && response.Headers.Location != null)
                return NormalizeLocation(server, response.Headers.Location);
            var hint = code switch
            {
                403 => "the server does not allow anonymous notes; sign in with an email account",
                413 => "the document exceeds the server's size limit",
                404 => "endpoint not found; is this a HedgeDoc 1.x server?",
                _ => $"HTTP {code}",
            };
            throw new HttpRequestException($"HedgeDoc could not {action}: {hint}.");
        }
    }
}
