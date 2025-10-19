using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AITranscriberWinApp.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;

        public TranslationService()
            : this(new Uri("https://libretranslate.com/translate"))
        {
        }

        public TranslationService(string endpoint)
            : this(new Uri(endpoint ?? throw new ArgumentNullException(nameof(endpoint))))
        {
        }

        public TranslationService(Uri endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
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

                try
                {
                    using (var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            var detail = ExtractErrorDetail(responseBody);
                            var statusCode = (int)response.StatusCode;
                            var reason = response.ReasonPhrase ?? string.Empty;
                            var message = string.IsNullOrWhiteSpace(detail)
                                ? $"Translation service returned {statusCode} {reason}."
                                : $"Translation service returned {statusCode} {reason}: {detail}";

                            throw new InvalidOperationException(message);
                        }

                        try
                        {
                            var json = JObject.Parse(responseBody);
                            return json.Value<string>("translatedText") ?? string.Empty;
                        }
                        catch (JsonReaderException ex)
                        {
                            var preview = responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody;
                            throw new InvalidOperationException($"Translation service returned malformed JSON: {preview}", ex);
                        }
                    }
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Translation request timed out.", ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(BuildHttpRequestErrorDetail(ex), ex);
                }
            }
        }

        private static string ExtractErrorDetail(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            try
            {
                var json = JObject.Parse(responseBody);
                return json.Value<string>("error")
                    ?? json.SelectToken("error.message")?.ToString()
                    ?? json.SelectToken("message")?.ToString()
                    ?? string.Empty;
            }
            catch (JsonReaderException)
            {
                return responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody;
            }
        }

        private static string BuildHttpRequestErrorDetail(HttpRequestException exception)
        {
            var builder = new StringBuilder();
            builder.Append("Translation request failed: ");
            builder.Append(exception.Message);

            if (exception.InnerException != null && !string.Equals(exception.InnerException.Message, exception.Message, StringComparison.Ordinal))
            {
                builder.Append(" (" + exception.InnerException.Message + ")");
            }

            if (exception.InnerException is SocketException socketException)
            {
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.HostNotFound:
                    case SocketError.NoData:
                    case SocketError.TryAgain:
                        builder.Append(" Verify the Translation Service URL and your network connection.");
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
