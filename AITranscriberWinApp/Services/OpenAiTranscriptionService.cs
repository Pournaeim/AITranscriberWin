using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AITranscriberWinApp.Services
{
    public class OpenAiTranscriptionService
    {
        private const string DefaultModel = "gpt-4o-mini-transcribe";
        private const string InstructionText = "Transcribe the provided audio in English and provide a natural Persian translation. Return a JSON object that contains the fields 'transcript' and 'translation'.";
        private static readonly Uri ResponsesEndpoint = new Uri("https://api.openai.com/v1/responses");
        private readonly HttpClient _httpClient;
        private static readonly IReadOnlyDictionary<string, string> AudioFormats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".wav"] = "wav",
            [".mp3"] = "mp3",
            [".m4a"] = "m4a",
            [".aac"] = "aac"
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

            using (var fileStream = new FileStream(audioPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                return await TranscribeAsync(fileStream, Path.GetFileName(audioPath), apiKey, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<TranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, string apiKey, CancellationToken cancellationToken)
        {
            if (audioStream == null)
            {
                throw new ArgumentNullException(nameof(audioStream));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name is required.", nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));
            }

            var audioFormat = GetAudioFormat(fileName);
            var base64Audio = await EncodeStreamToBase64Async(audioStream, cancellationToken).ConfigureAwait(false);
            var payload = BuildRequestPayload(base64Audio, audioFormat);

            using (var request = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

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

                    return ParseResponse(body);
                }
            }
        }

        private static JObject BuildRequestPayload(string base64Audio, string audioFormat)
        {
            if (string.IsNullOrWhiteSpace(base64Audio))
            {
                throw new ArgumentException("A valid audio payload is required.", nameof(base64Audio));
            }

            if (string.IsNullOrWhiteSpace(audioFormat))
            {
                throw new ArgumentException("A valid audio format is required.", nameof(audioFormat));
            }

            var schema = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["transcript"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "English transcription of the supplied audio."
                    },
                    ["translation"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Persian translation of the audio content."
                    }
                },
                ["required"] = new JArray("transcript", "translation")
            };

            var responseFormat = new JObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JObject
                {
                    ["name"] = "transcription_translation",
                    ["schema"] = schema
                }
            };

            var contentArray = new JArray
            {
                new JObject
                {
                    ["type"] = "input_text",
                    ["text"] = InstructionText
                },
                new JObject
                {
                    ["type"] = "input_audio",
                    ["audio"] = new JObject
                    {
                        ["format"] = audioFormat,
                        ["data"] = base64Audio
                    }
                }
            };

            var input = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = contentArray
                }
            };

            return new JObject
            {
                ["model"] = DefaultModel,
                ["input"] = input,
                ["temperature"] = 0,
                ["text"] = new JObject
                {
                    ["format"] = responseFormat
                }
            };
        }

        private static async Task<string> EncodeStreamToBase64Async(Stream audioStream, CancellationToken cancellationToken)
        {
            if (audioStream == null)
            {
                throw new ArgumentNullException(nameof(audioStream));
            }

            if (audioStream.CanSeek)
            {
                audioStream.Position = 0;

                if (audioStream is MemoryStream memoryStream)
                {
                    return Convert.ToBase64String(memoryStream.ToArray());
                }
            }

            using (var buffer = new MemoryStream())
            {
                await audioStream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
                return Convert.ToBase64String(buffer.ToArray());
            }
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
                return json.SelectToken("error.message")?.ToString()
                    ?? json.SelectToken("error.code")?.ToString()
                    ?? json.ToString(Formatting.None);
            }
            catch
            {
                return responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody;
            }
        }

        private static TranscriptionResult ParseResponse(string responseBody)
        {
            var json = JObject.Parse(responseBody);
            var textPayload = ExtractOutputText(json);

            if (string.IsNullOrWhiteSpace(textPayload))
            {
                throw new InvalidOperationException("OpenAI response did not include textual output.");
            }

            try
            {
                var parsed = JObject.Parse(textPayload);
                return new TranscriptionResult
                {
                    Text = parsed.Value<string>("transcript") ?? string.Empty,
                    Translation = parsed.Value<string>("translation") ?? string.Empty
                };
            }
            catch (JsonReaderException ex)
            {
                var preview = textPayload.Length > 500 ? textPayload.Substring(0, 500) : textPayload;
                throw new InvalidOperationException($"OpenAI response returned malformed JSON payload: {preview}", ex);
            }
        }

        private static string ExtractOutputText(JObject json)
        {
            if (json == null)
            {
                return string.Empty;
            }

            var outputArray = json["output"] as JArray;
            var text = outputArray?
                .SelectMany(output => output["content"] as JArray ?? new JArray())
                .FirstOrDefault(content => string.Equals(content.Value<string>("type"), "output_text", StringComparison.OrdinalIgnoreCase))?
                .Value<string>("text");

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            text = json.SelectToken("output_text")?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var contentArray = json["content"] as JArray;
            return contentArray?
                .Where(item => string.Equals(item.Value<string>("type"), "output_text", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Value<string>("text"))
                .FirstOrDefault(textValue => !string.IsNullOrWhiteSpace(textValue))
                ?? string.Empty;
        }

        private static string GetAudioFormat(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(extension) && AudioFormats.TryGetValue(extension, out var format))
            {
                return format;
            }

            return "wav";
        }

    }
}
