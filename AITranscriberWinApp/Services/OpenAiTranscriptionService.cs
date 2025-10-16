using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AITranscriberWinApp.Services
{
    public class OpenAiTranscriptionService
    {
        private static readonly Uri TranscriptionEndpoint = new Uri("https://api.openai.com/v1/audio/transcriptions");
        private readonly HttpClient _httpClient;
        private static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".wav"] = "audio/wav",
            [".mp3"] = "audio/mpeg",
            [".m4a"] = "audio/mp4",
            [".aac"] = "audio/aac"
        };

        public OpenAiTranscriptionService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<TranscriptionResult> TranscribeAsync(string audioPath, string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(audioPath))
            {
                throw new ArgumentException("Audio path is required.", nameof(audioPath));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionEndpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using (var form = new MultipartFormDataContent())
                {
                    var fileStream = new FileStream(audioPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    var fileContent = new StreamContent(fileStream);
                    var mimeType = GetMimeType(audioPath);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

                    form.Add(fileContent, "file", Path.GetFileName(audioPath));
                    form.Add(new StringContent("whisper-1"), "model");
                    form.Add(new StringContent("json"), "response_format");

                    request.Content = form;

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            var errorDetail = TryExtractErrorMessage(body);
                            var status = (int)response.StatusCode;
                            var reason = response.ReasonPhrase;
                            var message = string.IsNullOrWhiteSpace(errorDetail)
                                ? $"OpenAI transcription failed ({status} {reason})."
                                : $"OpenAI transcription failed ({status} {reason}): {errorDetail}";

                            throw new InvalidOperationException(message);
                        }

                        var json = JObject.Parse(body);
                        return new TranscriptionResult
                        {
                            Text = json.Value<string>("text") ?? string.Empty
                        };
                    }
                }
            }
        }

        private static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (!string.IsNullOrWhiteSpace(extension) && MimeTypes.TryGetValue(extension, out var mime))
            {
                return mime;
            }

            return "application/octet-stream";
        }

        private static string TryExtractErrorMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            try
            {
                var json = JObject.Parse(responseBody);
                return json.SelectToken("error.message")?.ToString() ?? string.Empty;
            }
            catch
            {
                return responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody;
            }
        }
    }
}
