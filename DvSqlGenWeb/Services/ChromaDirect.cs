using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DvSqlGenWeb.Models;

namespace DvSqlGenWeb.Services
{
    public class ChromaDirect
    {
        private readonly HttpClient _http;
        private readonly string _openAiApiKey;
        private readonly string _openAiBaseUrl;
        private readonly string _embeddingModel;

        public ChromaDirect(string baseUrl = "", string openAiBaseUrl = "", string openAiApiKey = "", string embeddingModel = "", HttpClient httpClient = null)
        {
            _http = httpClient;
            _openAiBaseUrl = openAiBaseUrl;
            _openAiApiKey = openAiApiKey ?? throw new ArgumentNullException(nameof(openAiApiKey));
            _embeddingModel = string.IsNullOrWhiteSpace(embeddingModel) ? "text-embedding-3-small" : embeddingModel;
        }

        public async Task<string> EnsureCollectionAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Коллекция не найдена или пустая", nameof(name));

            var response = await _http.GetAsync("/api/v1/collections");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var c in doc.RootElement.EnumerateArray())
                {
                    if (string.Equals(c.GetProperty("name").GetString(), name, StringComparison.Ordinal))
                        return c.GetProperty("id").GetString();
                }
            }

            var payload = new { name = name };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var createResponse = await _http.PostAsync("/api/v1/collections", content);
            createResponse.EnsureSuccessStatusCode();

            var createdJson = await createResponse.Content.ReadAsStringAsync();
            using var createdDoc = JsonDocument.Parse(createdJson);
            return createdDoc.RootElement.GetProperty("id").GetString();
        }

        //public async Task UpsertAsync(string collectionId, List<(string Id, string Text)> documents)
        //{
        //    if (string.IsNullOrWhiteSpace(collectionId))
        //        throw new ArgumentException("Коллекция не найдена или пустая", nameof(collectionId));

        //    var clean = (documents ?? new List<(string Id, string Text)>())
        //        .Where(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Text))
        //        .Select(d => (Id: d.Id.Trim(), Text: d.Text.Trim()))
        //        .ToList();

        //    if (clean.Count == 0)
        //        throw new InvalidOperationException("Не валидный ид и текст");

        //    var embeddings = await GetEmbeddingsAsync(clean.Select(d => d.Text));
        //    if (embeddings == null || embeddings.Count != clean.Count || embeddings.Any(e => e == null || e.Length == 0))
        //        throw new Exception("Эмбиндинг не валидный");

        //    var payloadObj = new
        //    {
        //        ids = clean.Select(d => d.Id).ToArray(),
        //        documents = clean.Select(d => d.Text).ToArray(),
        //        embeddings = embeddings.ToArray(),
        //        metadatas = clean.Select(_ => new Dictionary<string, object>()).ToArray()
        //    };

        //    var jsonPretty = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
        //    var content = new StringContent(jsonPretty, Encoding.UTF8, "application/json");
        //    var response = await _http.PostAsync("/api/v1/collections/" + collectionId + "/upsert", content);

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        var error = await response.Content.ReadAsStringAsync();
        //        throw new Exception("Обновление не успешное: " + response.StatusCode + "\n" + error);
        //    }
        //}

        public async Task UpsertAsync(
    string collectionId,
    List<(string Id, string Text, Dictionary<string, object> Metadata)> documents)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
                throw new ArgumentException("Коллекция не найдена или пустая", nameof(collectionId));

            var clean = (documents ?? new List<(string Id, string Text, Dictionary<string, object>)>())
                .Where(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Text))
                .Select(d => (Id: d.Id.Trim(), Text: d.Text.Trim(), Metadata: d.Metadata ?? new Dictionary<string, object>()))
                .ToList();

            if (clean.Count == 0)
                throw new InvalidOperationException("Не валидный ид и текст");

            var embeddings = await GetEmbeddingsAsync(clean.Select(d => d.Text));
            if (embeddings == null || embeddings.Count != clean.Count || embeddings.Any(e => e == null || e.Length == 0))
                throw new Exception("Эмбеддинг не валидный");

            var payloadObj = new
            {
                ids = clean.Select(d => d.Id).ToArray(),
                documents = clean.Select(d => d.Text).ToArray(),
                embeddings = embeddings.ToArray(),
                metadatas = clean.Select(d => d.Metadata).ToArray()
            };

            var jsonPretty = JsonSerializer.Serialize(payloadObj, new JsonSerializerOptions { WriteIndented = true });
            var content = new StringContent(jsonPretty, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync("/api/v1/collections/" + collectionId + "/upsert", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception("Обновление не успешное: " + response.StatusCode + "\n" + error);
            }
        }


        public async Task UpsertAsync(string collectionId, List<(string Id, string Text, string CardType, string Section, string SectionId, string field)> documents)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
                throw new ArgumentException("Коллекция не найдена или пустая", nameof(collectionId));

            var clean = (documents ?? new())
                .Where(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Text))
                .Select(d => (
                            Id: d.Id.Trim(),
                            Text: d.Text.Trim(),
                            CardType: d.CardType?.Trim() ?? "",
                            Section: d.Section?.Trim() ?? "",
                            SectionId: d.SectionId?.Trim() ?? "",
                            Field: d.field?.Trim() ?? ""
                        )).ToList();

            if (clean.Count == 0)
                throw new InvalidOperationException("Нет допустимых пар (Id, Text, CardType, Section) для обновления.");

            var embeddings = await GetEmbeddingsAsync(clean.Select(d => d.Text + " " + d.Field));
            if (embeddings == null || embeddings.Count() != clean.Count || embeddings.Any(e => e == null || e.Length == 0))
                throw new Exception("Вложения недействительны.");

            var payloadObj = new
            {
                ids = clean.Select(d => d.Id).ToArray(),
                documents = clean.Select(d => d.Text + d.Field).ToArray(),
                embeddings = embeddings.ToArray(),
                metadatas = clean.Select(d => new Dictionary<string, object>
                {
                    ["cardType"] = d.CardType,
                    ["section"] = d.Section,
                    ["sectionId"] = d.SectionId,
                    ["Field"] = d.Field
                }).ToArray()
            };

            var jsonPretty = System.Text.Json.JsonSerializer.Serialize(payloadObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            using var content = new StringContent(jsonPretty, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync($"/api/v1/collections/{collectionId}/upsert", content);
            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                throw new Exception("Ошибка: " + resp.StatusCode + "\n" + error);
            }
        }

        public async Task<List<string>> QueryAsync(
                string collectionId,
                string query,
                int n = 8,
                Dictionary<string, object>? where = null,
                string mustContain1 = null,
                string mustContain2 = null)
        {
            if (string.IsNullOrWhiteSpace(collectionId))
                throw new ArgumentException("Коллекция пустая", nameof(collectionId));
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("не задан вопрос", nameof(query));

            var queryEmbedding = await GetEmbeddingsAsync(new[] { query.Trim() });
            if (queryEmbedding == null || queryEmbedding.Count != 1 || queryEmbedding[0] == null || queryEmbedding[0].Length == 0)
                throw new InvalidOperationException("Ошибка при поиске эмбиндинга.");

            object whereDocument = new Dictionary<string, object>();
            var clauses = new List<Dictionary<string, object>>();
            if (!string.IsNullOrWhiteSpace(mustContain1))
                clauses.Add(new() { { "$contains", mustContain1 } });

            if (!string.IsNullOrWhiteSpace(mustContain2))
                clauses.Add(new() { { "$contains", mustContain2 } });

            if (clauses.Count == 1)
                whereDocument = clauses[0];
            else if (clauses.Count > 1)
                whereDocument = new Dictionary<string, object> { { "$and", clauses } };

            var payload = new Dictionary<string, object>
            {
                ["query_embeddings"] = queryEmbedding,
                ["n_results"] = Math.Max(3, n),
                ["include"] = new[] { "documents", "distances", "metadatas" }
            };
            if (where != null)
                payload["where"] = where;

            if (whereDocument != null)
                payload["where_document"] = whereDocument;

            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine("=== PAYLOAD TO CHROMA ===");
            Console.WriteLine(payloadJson);
            using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");


            using var resp = await _http.PostAsync($"/api/v1/collections/{collectionId}/query", content);
            var jsonStr = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Запрос в Chroma не выполнен. Код: {(int)resp.StatusCode} {resp.ReasonPhrase}. Тело: {jsonStr}");

            var docs = new List<string>();
            using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
            if (doc.RootElement.TryGetProperty("documents", out var documents))
                foreach (var arr in documents.EnumerateArray())
                    foreach (var d in arr.EnumerateArray())
                        docs.Add(d.GetString() ?? string.Empty);

            return docs.Take(Math.Max(3, n)).ToList();
        }

        public async Task<string> GetSampleAsync(string collectionId, int limit = 1)
        {
            var payload = new Dictionary<string, object>
            {
                ["limit"] = limit,
                ["include"] = new[] { "metadatas" }
            };

            using var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync($"/api/v1/collections/{collectionId}/get", content);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Ошибка: " + resp.StatusCode + "\n" + body);
            return body;
        }


        public async Task<List<float[]>> GetEmbeddingsAsync(IEnumerable<string> texts)
        {
            var batch = (texts ?? Enumerable.Empty<string>())
                .Select(t => (t ?? string.Empty).Trim())
                .ToList();

            if (batch.Count == 0 || batch.Any(string.IsNullOrEmpty))
                throw new ArgumentException("Батч пустой");

            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
            http.DefaultRequestHeaders.Add("Accept", "application/json");
            //http.DefaultRequestHeaders.Authorization =
            //    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);
            //http.DefaultRequestHeaders.Add("Accept", "application/json");

            var payload = new { model = _embeddingModel, input = batch };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await http.PostAsync("/v1/embeddings", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Запрос от OpenAI пустой: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                throw new Exception("Не вернулся массив данных.");

            var results = new List<float[]>(data.GetArrayLength());
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("embedding", out var emb))
                    throw new Exception("OpenAI не вернул эмбиндинг.");

                var vec = new List<float>(emb.GetArrayLength());
                foreach (var v in emb.EnumerateArray())
                    vec.Add((float)v.GetDouble());

                if (vec.Count == 0)
                    throw new Exception("Пустой вектор данных.");

                results.Add(vec.ToArray());
            }

            if (results.Count != batch.Count)
                throw new Exception($"Вернулось пакетов {results.Count}, ждал {batch.Count}.");

            return results;
        }

    }
}
