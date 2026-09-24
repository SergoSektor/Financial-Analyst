using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Diplom_1.Models;

namespace Diplom_1.Services
{
    public class EmbeddingService : IDisposable
    {
        private const string EmbedModel = "nomic-embed-text";
        private const int EmbeddingDim = 768;

        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly Action<string>? _debugLog;

        public EmbeddingService(AppSettings settings, Action<string>? debugLog = null)
        {
            _settings = settings;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _debugLog = debugLog;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var url = $"{_settings.OllamaUrl}/api/embed";

            var requestBody = new
            {
                model = EmbedModel,
                input = text
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;
            request.Headers.Add("ngrok-skip-browser-warning", "true");

            Log($"Embedding → POST {url}, text: {text.Length} chars");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Embedding error {(int)response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var embeddings = doc.RootElement.GetProperty("embeddings");
            if (embeddings.GetArrayLength() == 0)
                throw new InvalidOperationException("Empty embedding response");

            var arr = embeddings[0];
            var result = new float[arr.GetArrayLength()];
            for (int i = 0; i < result.Length; i++)
                result[i] = arr[i].GetSingle();

            Log($"Embedding ← {result.Length} floats");
            return result;
        }

        public static string Normalize(float[] vec)
        {
            var bytes = new byte[vec.Length * 4];
            Buffer.BlockCopy(vec, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        public static float[] Denormalize(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        public static double CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na < 1e-10 || nb < 1e-10) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        public static List<(int Index, double Score)> Search(
            float[] queryEmbedding, List<(int ChunkId, float[] Embedding)> chunks, int topK = 5)
        {
            var scored = new List<(int, double)>();
            foreach (var (id, emb) in chunks)
                scored.Add((id, CosineSimilarity(queryEmbedding, emb)));
            scored.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return scored.GetRange(0, Math.Min(topK, scored.Count));
        }

        public static List<string> SplitIntoChunks(string text, int maxSize = 1000, int overlap = 100, int maxChunks = 500)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            int start = 0;
            while (start < text.Length && chunks.Count < maxChunks)
            {
                int end = Math.Min(start + maxSize, text.Length);
                if (end < text.Length)
                {
                    var boundary = text.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, end, maxSize - overlap);
                    if (boundary > start + maxSize / 2)
                        end = boundary + 1;
                }
                chunks.Add(text.Substring(start, end - start).Trim());

                if (end >= text.Length) break;

                start = end - overlap;
                if (start <= 0) { start = end; break; }
            }

            return chunks;
        }

        private void Log(string message)
        {
            _debugLog?.Invoke(message);
        }

        public async Task EnsureModelExistsAsync()
        {
            try
            {
                var tagsUrl = $"{_settings.OllamaUrl}/api/tags";
                using var tagsRequest = new HttpRequestMessage(HttpMethod.Get, tagsUrl);
                tagsRequest.Headers.Add("ngrok-skip-browser-warning", "true");
                var tagsResponse = await _httpClient.SendAsync(tagsRequest);

                if (tagsResponse.IsSuccessStatusCode)
                {
                    var tagsJson = await tagsResponse.Content.ReadAsStringAsync();
                    using var tagsDoc = JsonDocument.Parse(tagsJson);
                    if (tagsDoc.RootElement.TryGetProperty("models", out var models))
                    {
                        foreach (var model in models.EnumerateArray())
                        {
                            var name = model.GetProperty("name").GetString() ?? "";
                            if (name == EmbedModel || name.StartsWith(EmbedModel + ":"))
                            {
                                Log($"Модель эмбеддингов {EmbedModel} уже установлена");
                                return;
                            }
                        }
                    }
                }

                Log($"Модель {EmbedModel} не найдена, загрузка...");
                var pullUrl = $"{_settings.OllamaUrl}/api/pull";
                var pullBody = new { model = EmbedModel, stream = false };
                var pullJson = JsonSerializer.Serialize(pullBody);
                var pullContent = new StringContent(pullJson, Encoding.UTF8, "application/json");

                using var pullRequest = new HttpRequestMessage(HttpMethod.Post, pullUrl);
                pullRequest.Content = pullContent;
                pullRequest.Headers.Add("ngrok-skip-browser-warning", "true");

                var pullResponse = await _httpClient.SendAsync(pullRequest);
                if (pullResponse.IsSuccessStatusCode)
                    Log($"Модель {EmbedModel} успешно загружена");
                else
                    Log($"Не удалось загрузить {EmbedModel}: {pullResponse.StatusCode}");
            }
            catch (Exception ex)
            {
                Log($"Ошибка проверки модели эмбеддингов: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
