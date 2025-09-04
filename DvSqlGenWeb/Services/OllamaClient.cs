using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DvSqlGenWeb.Services
{
    /// <summary>
    /// Клиент к локальному Ollama (http://localhost:11434).
    /// Поведение полностью совпадает с Gpt4AllClient:
    /// - Проверяем наличие модели в /api/tags.
    /// - Сначала /api/chat (со stop), затем /api/chat (без stop), затем /api/generate.
    /// - Sanitize: убираем <think>, возвращаем первый ```sql```; если нет — сырой текст.
    /// </summary>
    public class OllamaClient
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


        public OllamaClient(string baseUrl = "http://localhost:11434",
                            string model = "gpt-oss:20b",
                            double temperature = 0.1,
                            TimeSpan? timeout = null)
        {
            _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = timeout ?? TimeSpan.FromMinutes(5) };
            _model = model;
            _temperature = temperature;
        }

        public async Task<string> ChatAsync(string systemPrompt, string userPrompt, string context = "", CancellationToken ct = default)
        {
            var available = await GetModelsAsync(ct);
            if (!available.Contains(_model, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Model not found in Ollama: " + _model +
                    Environment.NewLine + "Available: " + string.Join(", ", available));


            var contentCombined = string.IsNullOrWhiteSpace(context)
                ? userPrompt
                : "Вопрос: " + userPrompt + "\nКонтекст:\n" + context;

            var chatPayload1 = new
            {
                model = _model,
                stream = false,
                temperature = _temperature,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = contentCombined }
                },
                stop = new[] { "<think>", "</think>" }
            };
            var (ok1, text1, err1) = await TryPostAsync("/api/chat", chatPayload1, ct);
            if (ok1)
                return Sanitize(ParseOllamaChatText(text1));

            return null;
        }


        private async Task<string[]> GetModelsAsync(CancellationToken ct)
        {
            using var resp = await _http.GetAsync("/api/tags", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                          .Select(m =>
                          {
                              if (m.TryGetProperty("name", out var n)) return n.GetString();
                              if (m.TryGetProperty("model", out var id)) return id.GetString();
                              return null;
                          })
                          .Where(s => !string.IsNullOrWhiteSpace(s))
                          .ToArray();
            }
            return Array.Empty<string>();
        }

        private async Task<(bool ok, string text, string err)> TryPostAsync(string path, object payload, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
            return (true, body, null);
        }

        private static string ParseOllamaChatText(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var c))
                return c.GetString() ?? "";
            return "";
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // убрать <think>...</think>
            text = Regex.Replace(text, @"(?is)<think[\s\S]*?</think>", "");

            // вернуть первый ```sql ... ```
            var m = Regex.Match(text, @"```sql[\s\S]*?```", RegexOptions.IgnoreCase);
            if (m.Success) return m.Value.Trim();

            // если блока нет — вернуть очищенный текст (Trim)
            return text.Trim();
        }
    }
}
