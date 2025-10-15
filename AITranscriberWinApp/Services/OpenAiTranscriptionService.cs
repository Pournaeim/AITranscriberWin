using System;
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

        public OpenAiTranscriptionService()
        {
            _httpClient = new HttpClient();
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
                    var fileBytes = await File.ReadAllBytesAsync(audioPath, cancellationToken).ConfigureAwait(false);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                    form.Add(fileContent, "file", Path.GetFileName(audioPath));
                    form.Add(new StringContent("whisper-1"), "model");
                    form.Add(new StringContent("json"), "response_format");

                    request.Content = form;

                    using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var json = JObject.Parse(body);
                        return new TranscriptionResult
                        {
                            Text = json.Value<string>("text") ?? string.Empty
                        };
                    }
                }
            }
        }
    }
}
