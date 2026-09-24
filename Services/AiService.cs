using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FinancialAnalyst.Models;

namespace FinancialAnalyst.Services
{
    public class AiService : IDisposable
    {
        private const int MaxRetries = 3;
        private const int MaxChunkSize = 200_000;
        private const int ChunkOverlap = 5_000;

        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly Action<string>? _debugLog;

        public AiService(AppSettings settings, Action<string>? debugLog = null)
        {
            _settings = settings;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _debugLog = debugLog;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (_settings.AiMode == AiMode.Ollama)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.OllamaUrl}/api/tags");
                    request.Headers.Add("ngrok-skip-browser-warning", "true");
                    var response = await _httpClient.SendAsync(request);
                    return response.IsSuccessStatusCode;
                }
                else if (_settings.AiMode == AiMode.Api)
                {
                    if (string.IsNullOrEmpty(_settings.ApiUrl)) return false;

                    var model = string.IsNullOrEmpty(_settings.ApiModel) ? "gpt-3.5-turbo" : _settings.ApiModel;
                    var requestBody = new
                    {
                        messages = new[] { new { role = "user", content = "test" } },
                        model = model,
                        max_tokens = 1
                    };
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
                    _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

                    var response = await _httpClient.PostAsync(_settings.ApiUrl, content);
                    return response.IsSuccessStatusCode;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FinancialData> ExtractFinancialDataAsync(string text, Action<string>? progressCallback = null, string? ragContext = null)
        {
            progressCallback?.Invoke("LLM: извлечение финансовых данных...");
            Log($"LLM: текст {text.Length} символов" + (ragContext != null ? $", RAG-контекст {ragContext.Length} символов" : ""));

            if (text.Length > MaxChunkSize)
            {
                Log($"LLM: текст большой ({text.Length} символов), применяю разбиение на чанки");
                return await ExtractFromChunksAsync(text, progressCallback, ragContext);
            }

            Log("LLM: текст вписывается в контекст, обрабатываю целиком");
            return await ExtractFromSinglePromptAsync(text, progressCallback, ragContext);
        }

        private async Task<FinancialData> ExtractFromChunksAsync(string text, Action<string>? progressCallback, string? ragContext = null)
        {
            var chunks = SplitIntoChunks(text, MaxChunkSize, ChunkOverlap);
            Log($"LLM: разбито на {chunks.Count} чанков");

            var mergedData = new FinancialData();
            int processed = 0;

            foreach (var chunk in chunks)
            {
                processed++;
                progressCallback?.Invoke($"LLM: чанк {processed}/{chunks.Count}...");
                Log($"LLM: обработка чанка {processed}/{chunks.Count} ({chunk.Length} символов)");

                var chunkData = await ExtractFromSinglePromptAsync(chunk, null, ragContext);
                mergedData = MergeFinancialData(mergedData, chunkData);

                Log($"LLM: чанк {processed} - итого полей: {mergedData.SourceFields.Count}");
            }

            Log($"LLM: все чанки обработаны, итого полей: {mergedData.SourceFields.Count}");
            return mergedData;
        }

        private List<string> SplitIntoChunks(string text, int maxChunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (text.Length <= maxChunkSize)
            {
                chunks.Add(text);
                return chunks;
            }

            int start = 0;
            while (start < text.Length)
            {
                int length = Math.Min(maxChunkSize, text.Length - start);
                chunks.Add(text.Substring(start, length));

                if (start + length < text.Length)
                {
                    start += length - overlap;
                }
                else
                {
                    break;
                }
            }

            return chunks;
        }

        private async Task<FinancialData> ExtractFromSinglePromptAsync(string text, Action<string>? progressCallback, string? ragContext = null)
        {
            progressCallback?.Invoke("LLM: извлечение финансовых данных...");
            Log("LLM: отправка текста на извлечение финансовых данных");

            var prompt = BuildExtractionPrompt(text, ragContext);
            Log($"LLM: промпт {prompt.Length} символов");

            FinancialData data = new();
            int attempt = 0;

            while (attempt < MaxRetries)
            {
                attempt++;
                Log($"LLM: попытка {attempt}/{MaxRetries}");

                try
                {
                    var response = await CallAiAsync(prompt);
                    Log($"LLM: ответ {response.Length} символов");

                    var (parsed, isValid) = TryParseFinancialJson(response);
                    if (isValid)
                    {
                        Log($"LLM: извлечено полей: {parsed.SourceFields.Count} (попытка {attempt})");
                        return parsed;
                    }

                    Log($"LLM: попытка {attempt} - JSON валиден, но данных мало ({parsed.SourceFields.Count} полей)");
                    data = MergeFinancialData(data, parsed);
                }
                catch (Exception ex)
                {
                    Log($"LLM: ошибка попытки {attempt}: {ex.Message}");
                }

                if (attempt < MaxRetries)
                {
                    Log($"LLM: повторный запрос через 1с...");
                    await Task.Delay(1000);
                    prompt = BuildExtractionPromptWithHint(text, attempt);
                }
            }

            Log($"LLM: все {MaxRetries} попыток завершены, итого полей: {data.SourceFields.Count}");
            return data;
        }

        public async Task<List<AiFinding>> GenerateTypedFindingsAsync(
            FinancialData data,
            FinancialRatios ratios,
            Action<string>? progressCallback = null)
        {
            if (!data.HasAnyData)
            {
                Log("AI: нет данных для анализа, пропускаю");
                return GenerateBasicFindings(data, ratios);
            }

            progressCallback?.Invoke("AI: генерация аналитических выводов...");
            Log("AI: генерация выводов на основе рассчитанных метрик");

            var prompt = BuildAnalysisPrompt(data, ratios);
            Log($"AI: промпт {prompt.Length} символов");

            int attempt = 0;
            while (attempt < MaxRetries)
            {
                attempt++;
                Log($"AI: попытка {attempt}/{MaxRetries}");

                try
                {
                    var response = await CallAiAsync(prompt);
                    Log($"AI: ответ {response.Length} символов");

                    var findings = ParseFindingsFromJson(response);
                    if (findings.Count > 0)
                    {
                        Log($"AI: распознано выводов: {findings.Count} (попытка {attempt})");
                        return findings;
                    }

                    Log($"AI: попытка {attempt} - не удалось распознать findings");
                }
                catch (Exception ex)
                {
                    Log($"AI: ошибка попытки {attempt}: {ex.Message}");
                }

                if (attempt < MaxRetries)
                {
                    Log($"AI: повторный запрос через 1с...");
                    await Task.Delay(1000);
                }
            }

            Log("AI: все попытки исчерпаны, использую fallback");
            return GenerateBasicFindings(data, ratios);
        }

        public async Task<List<AiFinding>> GenerateComparisonFindingsAsync(
            FinancialData data1, FinancialRatios ratios1, string name1,
            FinancialData data2, FinancialRatios ratios2, string name2,
            string? ragContext = null,
            Action<string>? progressCallback = null)
        {
            progressCallback?.Invoke("AI: сравнение документов...");
            Log("AI: генерация сравнительного анализа");

            var prompt = BuildComparisonPrompt(data1, ratios1, name1, data2, ratios2, name2, ragContext);
            Log($"AI: промпт сравнения {prompt.Length} символов");

            int attempt = 0;
            while (attempt < MaxRetries)
            {
                attempt++;
                Log($"AI: попытка {attempt}/{MaxRetries}");

                try
                {
                    var response = await CallAiAsync(prompt);
                    Log($"AI: ответ {response.Length} символов");

                    var findings = ParseFindingsFromJson(response);
                    if (findings.Count > 0)
                    {
                        Log($"AI: распознано выводов сравнения: {findings.Count}");
                        return findings;
                    }
                    Log($"AI: попытка {attempt} - не удалось распознать findings");
                }
                catch (Exception ex)
                {
                    Log($"AI: ошибка попытки {attempt}: {ex.Message}");
                }

                if (attempt < MaxRetries)
                {
                    Log("AI: повторный запрос через 1с...");
                    await Task.Delay(1000);
                }
            }

            Log("AI: все попытки исчерпаны, использую fallback для сравнения");
            return GenerateBasicComparisonFindings(data1, ratios1, name1, data2, ratios2, name2);
        }

        private string BuildComparisonPrompt(
            FinancialData d1, FinancialRatios r1, string n1,
            FinancialData d2, FinancialRatios r2, string n2,
            string? ragContext = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Ты - старший финансовый аналитик. ВСЕ твои ответы должны быть СТРОГО НА РУССКОМ ЯЗЫКЕ.");

            if (!string.IsNullOrEmpty(ragContext))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("Контекст из связанных финансовых документов (используй для дополнительной аналитики и сравнения с рыночными данными):");
                sb.AppendLine("--- НАЧАЛО КОНТЕКСТА ---");
                sb.AppendLine(ragContext.Trim());
                sb.AppendLine("--- КОНЕЦ КОНТЕКСТА ---");
                sb.AppendLine();
                sb.AppendLine("Используй этот контекст для бенчмаркинга, но основной анализ делай на основе данных двух сравниваемых организаций.");
                sb.AppendLine();
            }
            sb.AppendLine(); sb.AppendLine();
            sb.Append("Проведи сравнительный анализ двух компаний: «");
            sb.Append(n1);
            sb.Append("» и «");
            sb.Append(n2);
            sb.Append("». Каждый вывод ОБЯЗАН содержать конкретные цифры обеих компаний и указывать, кто лидирует. Используй ТОЛЬКО реальные названия компаний (не пиши «Компания 1» или «Компания 2»).");
            sb.AppendLine();
            sb.AppendLine();

            sb.Append("=== ");
            sb.Append(n1);
            sb.Append(" ===");
            sb.AppendLine();
            AppendLine(sb, "Выручка: {0}", Fmt(d1.Revenue));
            AppendLine(sb, "Чистая прибыль: {0}", Fmt(d1.NetIncome));
            AppendLine(sb, "Активы: {0}", Fmt(d1.Assets));
            AppendLine(sb, "Обязательства: {0}", Fmt(d1.Liabilities));
            AppendLine(sb, "Капитал: {0}", Fmt(d1.Equity));
            AppendLine(sb, "ROE: {0}%", FmtPct(r1.ROE));
            AppendLine(sb, "ROA: {0}%", FmtPct(r1.ROA));
            AppendLine(sb, "D/E: {0}", FmtPct(r1.DebtToEquity));
            AppendLine(sb, "NIM: {0}%", FmtPct(r1.NIM));
            AppendLine(sb, "C/I: {0}%", FmtPct(r1.CostToIncome));
            sb.AppendLine();
            sb.Append("=== ");
            sb.Append(n2);
            sb.Append(" ===");
            sb.AppendLine();
            AppendLine(sb, "Выручка: {0}", Fmt(d2.Revenue));
            AppendLine(sb, "Чистая прибыль: {0}", Fmt(d2.NetIncome));
            AppendLine(sb, "Активы: {0}", Fmt(d2.Assets));
            AppendLine(sb, "Обязательства: {0}", Fmt(d2.Liabilities));
            AppendLine(sb, "Капитал: {0}", Fmt(d2.Equity));
            AppendLine(sb, "ROE: {0}%", FmtPct(r2.ROE));
            AppendLine(sb, "ROA: {0}%", FmtPct(r2.ROA));
            AppendLine(sb, "D/E: {0}", FmtPct(r2.DebtToEquity));
            AppendLine(sb, "NIM: {0}%", FmtPct(r2.NIM));
            AppendLine(sb, "C/I: {0}%", FmtPct(r2.CostToIncome));
            sb.AppendLine();

            sb.AppendLine("СТРОГИЕ ТРЕБОВАНИЯ:");
            sb.AppendLine("1. Каждый вывод содержит конкретные цифры обеих организаций и их разницу.");
            sb.AppendLine("2. Четко указывай, у какой организации показатель лучше и на сколько.");
            sb.AppendLine("3. ЗАПРЕЩЕНЫ фразы без цифр.");
            sb.AppendLine("4. Для D/E и C/I меньше = лучше - учитывай это при сравнении.");
            sb.AppendLine("5. Используй пороги: ROE >15% отлично, 8-15% норма, <8% низко.");
            sb.AppendLine("6. ГЛАВНОЕ: анализируй, какие факторы могли привести к различиям - структура активов, эффективность управления, долговая нагрузка, операционная эффективность.");
            sb.Append("7. ВАЖНО: используй ТОЛЬКО названия организаций («");
            sb.Append(n1);
            sb.Append("», «");
            sb.Append(n2);
            sb.Append("»). НЕ используй «Компания 1», «Компания 2», «первая организация», «вторая организация» и т.п.");
            sb.AppendLine();
            sb.AppendLine("8. Каждый вывод должен содержать не только констатацию различия, но и анализ возможных причин.");
            sb.AppendLine();
            sb.Append("Пример: {\"type\": \"positive\", \"title\": \"«");
            sb.Append(n1);
            sb.Append("» опережает «");
            sb.Append(n2);
            sb.Append("» по ROE\", \"description\": \"ROE «");
            sb.Append(n1);
            sb.Append("» выше на 5%, что связано с более эффективным управлением капиталом.\"}");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Верни ТОЛЬКО JSON-массив выводов (3-7 штук).");
            sb.AppendLine("Допустимые типы: positive, problem, risk, recommendation, observation.");
            sb.AppendLine("ВСЕ заголовки и описания - ТОЛЬКО НА РУССКОМ ЯЗЫКЕ. Верни ТОЛЬКО JSON-массив.");
            return sb.ToString();
        }

        private static void AppendLine(System.Text.StringBuilder sb, string format, string value)
        {
            sb.AppendFormat(format, value);
            sb.AppendLine();
        }

        private static string Fmt(decimal? value)
        {
            return value?.ToString("N0") ?? "Н/Д";
        }

        private static string FmtPct(decimal? value)
        {
            return value?.ToString("F2") ?? "Н/Д";
        }

        private List<AiFinding> GenerateBasicComparisonFindings(
            FinancialData d1, FinancialRatios r1, string n1,
            FinancialData d2, FinancialRatios r2, string n2)
        {
            var findings = new List<AiFinding>();

            if (d1.Revenue.HasValue && d2.Revenue.HasValue)
            {
                var better = d1.Revenue > d2.Revenue ? n1 : n2;
                findings.Add(new AiFinding
                {
                    Type = FindingType.Observation,
                    Title = "Сравнение выручки",
                    Description = $"«{better}» имеет большую выручку: {FormatNumber(Math.Max(d1.Revenue.Value, d2.Revenue.Value))} против {FormatNumber(Math.Min(d1.Revenue.Value, d2.Revenue.Value))}"
                });
            }

            if (r1.ROE.HasValue && r2.ROE.HasValue)
            {
                var better = r1.ROE > r2.ROE ? n1 : n2;
                findings.Add(new AiFinding
                {
                    Type = FindingType.Observation,
                    Title = "Рентабельность капитала (ROE)",
                    Description = $"«{better}» показывает более эффективное использование капитала: {Math.Max(r1.ROE.Value, r2.ROE.Value):F2}% против {Math.Min(r1.ROE.Value, r2.ROE.Value):F2}%"
                });
            }

            if (r1.DebtToEquity.HasValue && r2.DebtToEquity.HasValue)
            {
                var better = r1.DebtToEquity < r2.DebtToEquity ? n1 : n2;
                findings.Add(new AiFinding
                {
                    Type = FindingType.Risk,
                    Title = "Долговая нагрузка",
                    Description = $"«{better}» имеет более консервативную структуру капитала (D/E: {Math.Min(r1.DebtToEquity.Value, r2.DebtToEquity.Value):F2} против {Math.Max(r1.DebtToEquity.Value, r2.DebtToEquity.Value):F2})"
                });
            }

            if (findings.Count == 0)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Сравнение выполнено", Description = $"Сравнение «{n1}» и «{n2}» завершено. Доступны метрики обоих документов." });

            return findings;
        }

        private string FormatNumber(decimal value)
        {
            if (value >= 1_000_000_000_000) return $"{value / 1_000_000_000_000:F2} трлн";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000:F2} млрд";
            if (value >= 1_000_000) return $"{value / 1_000_000:F2} млн";
            return value.ToString("N2");
        }

        private string BuildExtractionPrompt(string text, string? ragContext = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ты - система извлечения финансовых данных. ВСЕ твои ответы должны быть СТРОГО НА РУССКОМ ЯЗЫКЕ. Английский язык запрещён.");
            sb.AppendLine();
            sb.AppendLine("Твоя задача: извлечь числовые значения из финансового текста ниже.");
            sb.AppendLine();
            sb.AppendLine("ВАЖНО: Все финансовые показатели в тексте указаны в рублях РФ (RUB). НЕ конвертируй в доллары, евро или другие валюты. Извлекай значения как есть.");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(ragContext))
            {
                sb.AppendLine("Контекст из похожих документов (используй для сверки и подтверждения значений):");
                sb.AppendLine("--- НАЧАЛО КОНТЕКСТА ---");
                sb.AppendLine(ragContext.Trim());
                sb.AppendLine("--- КОНЕЦ КОНТЕКСТА ---");
                sb.AppendLine();
                sb.AppendLine("ВАЖНО: извлекай значения ТОЛЬКО из основного финансового текста ниже. Контекст дан для справки.");
                sb.AppendLine();
            }

            sb.AppendLine("Верни ТОЛЬКО валидный JSON-объект. Никакого текста до или после JSON. Никакого markdown. Никаких блоков кода. Никаких комментариев.");
            sb.AppendLine();
            sb.AppendLine("JSON должен содержать строго эти поля (используй null, если не найдено):");
            sb.AppendLine("{");
            sb.AppendLine("  \"revenue\": 0,");
            sb.AppendLine("  \"net_income\": 0,");
            sb.AppendLine("  \"assets\": 0,");
            sb.AppendLine("  \"liabilities\": 0,");
            sb.AppendLine("  \"equity\": 0,");
            sb.AppendLine("  \"net_interest_income\": 0,");
            sb.AppendLine("  \"fee_income\": 0,");
            sb.AppendLine("  \"operating_expenses\": 0");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("СТРОГИЕ ПРАВИЛА:");
            sb.AppendLine("1. Верни ТОЛЬКО JSON-объект, начинающийся с { и заканчивающийся }");
            sb.AppendLine("2. Все значения - простые числа (без строк, без запятых, без пробелов в числах)");
            sb.AppendLine("3. Преобразуй слова в числа: \"1,5 миллиарда\" = 1500000000, \"300 миллионов\" = 300000000");
            sb.AppendLine("4. Если значение отсутствует в тексте, используй null (не 0, не \"null\", просто null)");
            sb.AppendLine("5. НЕ выдумывай и НЕ предполагай значения");
            sb.AppendLine("6. НЕ включай никакой пояснительный текст");
            sb.AppendLine("7. ВСЕ комментарии и описания - ТОЛЬКО НА РУССКОМ");
            sb.AppendLine();
            sb.AppendLine("Финансовый текст:");
            sb.AppendLine(text);
            return sb.ToString();
        }

        private string BuildExtractionPromptWithHint(string text, int retryAttempt)
        {
            string hint = retryAttempt switch
            {
                2 => "\n\nНАПОМИНАНИЕ: предыдущий ответ не был валидным JSON. Верни ТОЛЬКО JSON-объект с числовыми значениями. Начни с {{ и закончи }}.",
                3 => "\n\nПОСЛЕДНЯЯ ПОПЫТКА: Верни ТОЛЬКО JSON-объект. Пример: {{\"revenue\":1000000,\"net_income\":null,\"assets\":5000000,\"liabilities\":null,\"equity\":null,\"net_interest_income\":null,\"fee_income\":null,\"operating_expenses\":null}}",
                _ => ""
            };
            return BuildExtractionPrompt(text) + hint;
        }

        private (FinancialData data, bool isValid) TryParseFinancialJson(string response)
        {
            var data = new FinancialData();

            var jsonStr = ExtractJsonFromResponse(response);
            if (string.IsNullOrEmpty(jsonStr))
            {
                Log("Не найден JSON в ответе LLM");
                return (data, false);
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    Log("JSON корневой элемент - не объект");
                    return (data, false);
                }

                bool anyExtracted = false;

                if (TryGetDecimal(root, "revenue", out var v))
                { data.Revenue = v; data.SourceFields.Add("revenue"); anyExtracted = true; }
                if (TryGetDecimal(root, "net_income", out v))
                { data.NetIncome = v; data.SourceFields.Add("net_income"); anyExtracted = true; }
                if (TryGetDecimal(root, "assets", out v))
                { data.Assets = v; data.SourceFields.Add("assets"); anyExtracted = true; }
                if (TryGetDecimal(root, "liabilities", out v))
                { data.Liabilities = v; data.SourceFields.Add("liabilities"); anyExtracted = true; }
                if (TryGetDecimal(root, "equity", out v))
                { data.Equity = v; data.SourceFields.Add("equity"); anyExtracted = true; }
                if (TryGetDecimal(root, "net_interest_income", out v))
                { data.NetInterestIncome = v; data.SourceFields.Add("net_interest_income"); anyExtracted = true; }
                if (TryGetDecimal(root, "fee_income", out v))
                { data.FeeIncome = v; data.SourceFields.Add("fee_income"); anyExtracted = true; }
                if (TryGetDecimal(root, "operating_expenses", out v))
                { data.OperatingExpenses = v; data.SourceFields.Add("operating_expenses"); anyExtracted = true; }

                return (data, anyExtracted);
            }
            catch (Exception ex)
            {
                Log($"Ошибка парсинга JSON: {ex.Message}");
                return (data, false);
            }
        }

        private FinancialData MergeFinancialData(FinancialData existing, FinancialData newData)
        {
            if (!existing.Revenue.HasValue && newData.Revenue.HasValue) existing.Revenue = newData.Revenue;
            if (!existing.NetIncome.HasValue && newData.NetIncome.HasValue) existing.NetIncome = newData.NetIncome;
            if (!existing.Assets.HasValue && newData.Assets.HasValue) existing.Assets = newData.Assets;
            if (!existing.Liabilities.HasValue && newData.Liabilities.HasValue) existing.Liabilities = newData.Liabilities;
            if (!existing.Equity.HasValue && newData.Equity.HasValue) existing.Equity = newData.Equity;
            if (!existing.NetInterestIncome.HasValue && newData.NetInterestIncome.HasValue) existing.NetInterestIncome = newData.NetInterestIncome;
            if (!existing.FeeIncome.HasValue && newData.FeeIncome.HasValue) existing.FeeIncome = newData.FeeIncome;
            if (!existing.OperatingExpenses.HasValue && newData.OperatingExpenses.HasValue) existing.OperatingExpenses = newData.OperatingExpenses;

            existing.SourceFields.Clear();
            if (existing.Revenue.HasValue) existing.SourceFields.Add("revenue");
            if (existing.NetIncome.HasValue) existing.SourceFields.Add("net_income");
            if (existing.Assets.HasValue) existing.SourceFields.Add("assets");
            if (existing.Liabilities.HasValue) existing.SourceFields.Add("liabilities");
            if (existing.Equity.HasValue) existing.SourceFields.Add("equity");
            if (existing.NetInterestIncome.HasValue) existing.SourceFields.Add("net_interest_income");
            if (existing.FeeIncome.HasValue) existing.SourceFields.Add("fee_income");
            if (existing.OperatingExpenses.HasValue) existing.SourceFields.Add("operating_expenses");

            return existing;
        }

        private string ExtractJsonFromResponse(string response)
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
                return response.Substring(jsonStart, jsonEnd - jsonStart + 1);

            return response;
        }

        private bool TryGetDecimal(JsonElement element, string name, out decimal value)
        {
            value = 0;
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                value = prop.GetDecimal();
                return true;
            }
            return false;
        }

        private string BuildAnalysisPrompt(FinancialData data, FinancialRatios ratios)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ты - старший финансовый аналитик. ВСЕ твои ответы должны быть СТРОГО НА РУССКОМ ЯЗЫКЕ. Английский язык запрещён.");
            sb.AppendLine("Проведи предметный анализ финансовых показателей и верни выводы в формате JSON-массива.");
            sb.AppendLine();
            sb.AppendLine("ВАЖНО: Все суммы указаны в рублях РФ (RUB). Не используй доллары или другие валюты.");
            sb.AppendLine();
            sb.AppendLine("Финансовые данные (в рублях):");
            sb.AppendLine($"  Выручка: {data.Revenue}");
            sb.AppendLine($"  Чистая прибыль: {data.NetIncome}");
            sb.AppendLine($"  Активы: {data.Assets}");
            sb.AppendLine($"  Обязательства: {data.Liabilities}");
            sb.AppendLine($"  Капитал: {data.Equity}");
            sb.AppendLine($"  Чистый процентный доход: {data.NetInterestIncome}");
            sb.AppendLine($"  Комиссионный доход: {data.FeeIncome}");
            sb.AppendLine($"  Операционные расходы: {data.OperatingExpenses}");
            sb.AppendLine();
            sb.AppendLine("Рассчитанные коэффициенты:");
            sb.AppendLine($"  ROE (Рентабельность капитала): {ratios.ROE:F2}%");
            sb.AppendLine($"  ROA (Рентабельность активов): {ratios.ROA:F2}%");
            sb.AppendLine($"  Долг/Капитал: {ratios.DebtToEquity:F2}");
            sb.AppendLine($"  NIM (Чистая процентная маржа): {ratios.NIM:F2}%");
            sb.AppendLine($"  Затраты/Доходы: {ratios.CostToIncome:F2}%");
            sb.AppendLine();
            sb.AppendLine("СТРОГИЕ ТРЕБОВАНИЯ К КАЖДОМУ ВЫВОДУ:");
            sb.AppendLine("1. Каждый вывод ОБЯЗАН ссылаться на конкретные числовые значения из данных выше.");
            sb.AppendLine("2. ЗАПРЕЩЕНЫ общие фразы без цифр: «компания демонстрирует», «показывает рост», «имеет показатели».");
            sb.AppendLine("3. Сравнивай с пороговыми значениями: ROE >15% отлично, 8-15% нормально, <8% низко.");
            sb.AppendLine("4. Для D/E: <1 - консервативно, 1-2 - умеренно, >2 - высокий риск.");
            sb.AppendLine("5. Для C/I: <50% - эффективно, 50-70% - приемлемо, >70% - неэффективно.");
            sb.AppendLine("6. Указывай конкретные суммы и проценты в описании.");
            sb.AppendLine();
            sb.AppendLine("Пример ХОРОШЕГО вывода:");
            sb.AppendLine("  {\"type\": \"positive\", \"title\": \"Рентабельность капитала 18.5%\", \"description\": \"ROE составляет 18.5%, что выше порога 15%. Капитал используется эффективно: при капитале 2.5 млрд компания заработала 462 млн чистой прибыли.\"}");
            sb.AppendLine();
            sb.AppendLine("Пример ПЛОХОГО вывода (ЗАПРЕЩЁН):");
            sb.AppendLine("  {\"type\": \"positive\", \"title\": \"Хорошая рентабельность\", \"description\": \"Компания демонстрирует хорошую рентабельность капитала.\"}");
            sb.AppendLine();
            sb.AppendLine("Верни ТОЛЬКО JSON-массив из 3-7 выводов.");
            sb.AppendLine("Допустимые типы: \"positive\", \"problem\", \"risk\", \"recommendation\", \"observation\".");
            sb.AppendLine("ВСЕ заголовки и описания - ТОЛЬКО НА РУССКОМ ЯЗЫКЕ.");
            sb.AppendLine("Верни ТОЛЬКО JSON-массив. Никакого текста до или после.");

            return sb.ToString();
        }

        private List<AiFinding> ParseFindingsFromJson(string response)
        {
            var findings = new List<AiFinding>();

            var jsonStr = ExtractJsonArrayFromResponse(response);
            if (string.IsNullOrEmpty(jsonStr))
            {
                Log("Не найден JSON array в ответе AI");
                return findings;
            }

            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    Log("JSON корневой элемент - не массив");
                    return findings;
                }

                foreach (var item in root.EnumerateArray())
                {
                    var typeStr = item.GetProperty("type").GetString()?.Trim().ToLowerInvariant() ?? "";
                    var title = item.GetProperty("title").GetString()?.Trim() ?? "";
                    var description = item.GetProperty("description").GetString()?.Trim() ?? "";

                    if (string.IsNullOrEmpty(title)) continue;

                    var type = typeStr switch
                    {
                        "problem" => FindingType.Problem,
                        "risk" => FindingType.Risk,
                        "positive" => FindingType.Positive,
                        "recommendation" => FindingType.Recommendation,
                        "observation" => FindingType.Observation,
                        _ => FindingType.Observation
                    };

                    findings.Add(new AiFinding
                    {
                        Type = type,
                        Title = title,
                        Description = description
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка парсинга findings JSON: {ex.Message}");
            }

            return findings;
        }

        private string ExtractJsonArrayFromResponse(string response)
        {
            var arrStart = response.IndexOf('[');
            var arrEnd = response.LastIndexOf(']');

            if (arrStart >= 0 && arrEnd > arrStart)
                return response.Substring(arrStart, arrEnd - arrStart + 1);

            var objStart = response.IndexOf('{');
            var objEnd = response.LastIndexOf('}');
            if (objStart >= 0 && objEnd > objStart)
                return response.Substring(objStart, objEnd - objStart + 1);

            return response;
        }

        private List<AiFinding> GenerateBasicFindings(FinancialData data, FinancialRatios ratios)
        {
            var findings = new List<AiFinding>();

            if (data.Revenue.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Positive, Title = "Выручка обнаружена", Description = $"Общая выручка компании: {FormatNumber(data.Revenue.Value)}" });
            if (data.NetIncome.HasValue)
            {
                if (data.NetIncome.Value > 0)
                    findings.Add(new AiFinding { Type = FindingType.Positive, Title = "Чистая прибыль", Description = $"Компания получила чистую прибыль: {FormatNumber(data.NetIncome.Value)}" });
                else
                    findings.Add(new AiFinding { Type = FindingType.Problem, Title = "Чистый убыток", Description = $"Компания понесла чистый убыток: {FormatNumber(data.NetIncome.Value)}" });
            }
            if (ratios.ROE.HasValue)
            {
                var roeStatus = ratios.ROE > 15m ? "высокий" : ratios.ROE > 8m ? "умеренный" : "низкий";
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Рентабельность капитала (ROE)", Description = $"ROE составляет {ratios.ROE:F2}% - {roeStatus} уровень" });
            }
            if (ratios.ROA.HasValue)
            {
                var roaStatus = ratios.ROA > 5m ? "хороший" : ratios.ROA > 2m ? "умеренный" : "низкий";
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Рентабельность активов (ROA)", Description = $"ROA составляет {ratios.ROA:F2}% - {roaStatus} уровень" });
            }
            if (ratios.DebtToEquity.HasValue)
            {
                var deStatus = ratios.DebtToEquity < 1m ? "консервативная" : ratios.DebtToEquity < 2m ? "умеренная" : "высокая";
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Долговая нагрузка", Description = $"Отношение долга к капиталу: {ratios.DebtToEquity:F2} - {deStatus} структура" });
            }
            if (ratios.NIM.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Чистая процентная маржа (NIM)", Description = $"NIM составляет {ratios.NIM:F2}%" });
            if (ratios.CostToIncome.HasValue)
            {
                var ciStatus = ratios.CostToIncome < 50m ? "эффективная" : ratios.CostToIncome < 70m ? "приемлемая" : "неэффективная";
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Соотношение затрат и доходов", Description = $"C/I ratio: {ratios.CostToIncome:F2}% - {ciStatus} модель" });
            }

            if (findings.Count == 0)
                findings.Add(new AiFinding { Type = FindingType.Problem, Title = "Недостаточно данных", Description = "Не удалось извлечь финансовые данные для анализа" });

            return findings;
        }

        private async Task<string> CallAiAsync(string prompt)
        {
            if (_settings.AiMode == AiMode.Ollama)
                return await CallOllamaAsync(prompt);
            else if (_settings.AiMode == AiMode.Api)
                return await CallApiAsync(prompt);

            throw new InvalidOperationException("AI mode not configured.");
        }

        private async Task<string> CallOllamaAsync(string prompt)
        {
            var requestBody = new
            {
                model = _settings.OllamaModel,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{_settings.OllamaUrl}/api/generate";
            Log($"Ollama → POST {url}, model: {_settings.OllamaModel}");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;
            request.Headers.Add("ngrok-skip-browser-warning", "true");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Ollama error {(int)response.StatusCode}: {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            Log($"Ollama ← response: {responseJson.Length} bytes");

            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        }

        private async Task<string> CallApiAsync(string prompt)
        {
            var model = string.IsNullOrEmpty(_settings.ApiModel) ? "gpt-3.5-turbo" : _settings.ApiModel;
            var requestBody = new
            {
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                model = model,
                max_tokens = 2000
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            _httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
            Log($"API → POST {_settings.ApiUrl} (model: {model})");

            var response = await _httpClient.PostAsync(_settings.ApiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message");
                    return message.GetProperty("content").GetString() ?? string.Empty;
                }
            }
            catch
            {
                return responseJson;
            }

            return responseJson;
        }

        private void Log(string message)
        {
            _debugLog?.Invoke(message);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
