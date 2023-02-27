using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace TranslationTool
{
    public class BingTranslate
    {
        private static readonly string key = "4afab82abb7a4871a6f0c8083b522f39";

        private static readonly string endpoint = "https://api.cognitive.microsofttranslator.com/";

        public static async Task<List<Dictionary<string, string>>> Translate(string from, List<string> to, List<string> text)
        {
            var route = $"/translate?api-version=3.0&from={from}&{string.Join('&', to.Select(x => $"to={x}"))}";
            var body = text.Select(t => new { Text = t }).ToArray();
            var requestBody = JsonConvert.SerializeObject(body);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(endpoint + route);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", key);
            request.Headers.Add("Ocp-Apim-Subscription-Region", "eastasia");
            var response = await client.SendAsync(request).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync();
            var result = JArray.Parse(content);
            return result.Select(x => x["translations"].ToDictionary(x => x["to"].ToString(), x => x["text"].ToString())).ToList();
        }

        public static async Task<Dictionary<string, string>> GetSupportedLangs()
        {
            var route = $"/languages?api-version=3.0";
            using var client = new HttpClient();
            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri(endpoint + route);
            var response = await client.SendAsync(request).ConfigureAwait(false);
            var content = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(content)["translation"].ToObject<Dictionary<string, JObject>>().ToDictionary(x => x.Key, x => x.Value["nativeName"].ToString());
            return result;
        }
    }
}
