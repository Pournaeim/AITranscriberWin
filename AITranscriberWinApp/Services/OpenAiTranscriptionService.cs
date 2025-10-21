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
        private const string TranscriptionModel = "gpt-4o-mini-transcribe";
        private const string TranslationModel = "gpt-4o-mini";
        private const string TranslationInstruction = "You are given an English transcription of an audio clip. Provide a natural Persian translation. Return a JSON object with the fields 'transcript' (the original English text) and 'translation' (the Persian translation).";
        private static readonly Uri ResponsesEndpoint = new Uri("https://api.openai.com/v1/responses");
        private static readonly Uri TranscriptionsEndpoint = new Uri("https://api.openai.com/v1/audio/transcriptions");
        private readonly HttpClient _httpClient;
        private static readonly IReadOnlyDictionary<string, string> AudioMimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

            var transcript = await RequestTranscriptionAsync(audioStream, fileName, apiKey, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new InvalidOperationException("OpenAI transcription result was empty.");
            }

            return await RequestTranslationAsync(transcript, apiKey, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> RequestTranscriptionAsync(Stream audioStream, string fileName, string apiKey, CancellationToken cancellationToken)
        {
            var audioBytes = await ReadStreamToByteArrayAsync(audioStream, cancellationToken).ConfigureAwait(false);
            var mimeType = GetMimeType(fileName);

            using (var content = new MultipartFormDataContent())
            {
                var audioContent = new ByteArrayContent(audioBytes);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                content.Add(audioContent, "file", fileName);
                content.Add(new StringContent(TranscriptionModel), "model");
                content.Add(new StringContent("json"), "response_format");

                using (var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsEndpoint))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    request.Content = content;

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
                        var text = json.Value<string>("text") ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(text))
                        {
                            throw new InvalidOperationException("OpenAI transcription response did not include text output.");
                        }

                        return text.Trim();
                    }
                }
            }
        }

        private async Task<TranscriptionResult> RequestTranslationAsync(string transcript, string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(transcript))
            {
                throw new ArgumentException("A valid transcript is required for translation.", nameof(transcript));
            }

            var payload = BuildTranslationPayload(transcript);

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
                            ? $"OpenAI translation failed ({status} {reason})."
                            : $"OpenAI translation failed ({status} {reason}): {errorDetail}";

                        throw new InvalidOperationException(message);
                    }

                    return ParseResponse(body, transcript);
                }
            }
        }

        private static JObject BuildTranslationPayload(string transcript)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["transcript"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "English transcription of the supplied audio.",
                    },
                    ["translation"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Persian translation of the audio content.",
                    }
                },
                ["required"] = new JArray("transcript", "translation")
            };

            var contentArray = new JArray
            {
                new JObject
                {
                    ["type"] = "input_text",
                    ["text"] = TranslationInstruction
                },
                new JObject
                {
                    ["type"] = "input_text",
                    ["text"] = transcript
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

            var textOptions = new JObject
            {
                ["format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JObject
                    {
                        ["name"] = "transcription_translation",
                        ["schema"] = schema
                    }
                }
            };

            return new JObject
            {
                ["model"] = TranslationModel,
                ["input"] = input,
                ["temperature"] = 0,
                ["text"] = textOptions
            };
        }

        private static async Task<byte[]> ReadStreamToByteArrayAsync(Stream audioStream, CancellationToken cancellationToken)
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
                    return memoryStream.ToArray();
                }
            }

            using (var buffer = new MemoryStream())
            {
                await audioStream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
                return buffer.ToArray();
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

        private static TranscriptionResult ParseResponse(string responseBody, string fallbackTranscript)
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
                    Text = parsed.Value<string>("transcript") ?? fallbackTranscript ?? string.Empty,
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

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(extension) && AudioMimeTypes.TryGetValue(extension, out var mimeType))
            {
                return mimeType;
            }

            return "audio/wav";
        }
    }
}
