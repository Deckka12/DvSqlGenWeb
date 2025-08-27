using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DvSqlGenWeb.Services
{
    public class OpenAIClient
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly double _temperature;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public OpenAIClient(string apiKey, string baseUrl = "", string model = "", double temperature = 0.1, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Апи ключ не указан", nameof(apiKey));

            _http = httpClient;
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            _http.DefaultRequestHeaders.Add("Accept", "application/json");
            _model = model;
            _temperature = temperature;
        }


        public async Task<string> ChatAsync(string systemPrompt, string userPrompt, string context = "", CancellationToken ct = default)
        {
            var contentCombined = string.IsNullOrWhiteSpace(context)
                ? userPrompt
                : "Вопрос: " + userPrompt + "\nКонтекст:\n" + context;

            var payload = new
            {
                model = _model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = contentCombined }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"Ошибка при обращение по апи {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("choices")[0]
                                      .GetProperty("message")
                                      .GetProperty("content")
                                      .GetString() ?? string.Empty;

            return Sanitize(text);
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) 
                return string.Empty;


            text = Regex.Replace(text, @"(?is)<think[\s\S]*?</think>", "");

            var m = Regex.Match(text, @"```sql[\s\S]*?```", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Value.Trim();


            return text.Trim();
        }
    }
}
