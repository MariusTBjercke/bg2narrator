using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NarratorSvc.ElevenLabs
{
    internal static class ElevenLabsClient
    {
        private static readonly HttpClient Http = new HttpClient();

        public static byte[] Synthesize(string apiKey, string text, string voiceId, string modelId, double speed)
        {
            return SynthesizeAsync(apiKey, text, voiceId, modelId, speed, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        public static async Task<byte[]> SynthesizeAsync(
            string apiKey,
            string text,
            string voiceId,
            string modelId,
            double speed,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("ElevenLabs API key is not set.");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text is empty.", nameof(text));
            }

            string voice = string.IsNullOrWhiteSpace(voiceId) ? ElevenLabsDefaults.VoiceId : voiceId;
            string model = string.IsNullOrWhiteSpace(modelId) ? ElevenLabsDefaults.ModelId : modelId;
            double clampedSpeed = Clamp(speed, ElevenLabsDefaults.MinSpeed, ElevenLabsDefaults.MaxSpeed);

            string url = "https://api.elevenlabs.io/v1/text-to-speech/" + Uri.EscapeDataString(voice)
                + "?output_format=mp3_44100_128";

            var payload = new JObject
            {
                ["text"] = text,
                ["model_id"] = model,
                ["voice_settings"] = new JObject
                {
                    ["speed"] = clampedSpeed
                }
            };

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("xi-api-key", apiKey);
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException(FormatTtsError(response.StatusCode, error));
                    }

                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static string FormatTtsError(HttpStatusCode statusCode, string responseBody)
        {
            int code = (int)statusCode;
            switch (code)
            {
                case 401:
                    return "ElevenLabs API key is invalid or expired.";
                case 402:
                    return "ElevenLabs account has insufficient credits.";
                case 429:
                    return "ElevenLabs rate limit reached.";
                default:
                    return "ElevenLabs TTS failed (" + code + "): " + Truncate(responseBody);
            }
        }

        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 300)
            {
                return text ?? "";
            }

            return text.Substring(0, 300);
        }
    }
}
