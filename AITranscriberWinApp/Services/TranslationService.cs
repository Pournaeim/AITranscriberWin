using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AITranscriberWinApp.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;

        public TranslationService()
            : this(new Uri("https://translate.argosopentech.com/translate"))
        {
        }

        public TranslationService(Uri endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _httpClient = new HttpClient();
        }

        public async Task<string> TranslateToPersianAsync(string englishText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(englishText))
            {
                return string.Empty;
            }

            var payload = new JObject
            {
                ["q"] = englishText,
                ["source"] = "en",
                ["target"] = "fa",
                ["format"] = "text"
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, _endpoint))
            {
                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JObject.Parse(jsonResponse);
                    return json.Value<string>("translatedText") ?? string.Empty;
                }
            }
        }
    }
}
