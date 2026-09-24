using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FinancialAnalyst.Models;
using FinancialAnalyst.Services;
using FinancialAnalyst.Views;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace FinancialAnalyst.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DocumentParserService _docParser;
        private readonly FinancialExtractorService _extractor;
        private readonly FinancialCalculatorService _calculator;
        private readonly StorageService _storageService;
        private AppSettings _settings;

        private ObservableCollection<DocumentInfo> _documents = new();
        private DocumentInfo? _selectedDocument;
        private ObservableCollection<ExtractedMetric> _extractedMetrics = new();
        private ObservableCollection<AiFinding> _aiFindings = new();
        private FinancialData _financialData = new();
        private FinancialRatios _financialRatios = new();
        private int _aiProgress;
        private string _aiStatusText = "";
        private bool _isAiProcessing;
        private string _documentSummary = "";
        private ObservableCollection<string> _debugLogs = new();

        private ISeries[] _capitalStructureSeries = Array.Empty<ISeries>();
        private ISeries[] _incomeDistributionSeries = Array.Empty<ISeries>();
        private bool _showComparisonView;

        private ObservableCollection<DocumentComparison> _comparisons = new();
        private DocumentComparison? _selectedComparison;

        private bool _hasComparableDocs;
        private bool _hasCapitalStructure;
        private bool _hasIncomeData;
        private bool _hasChartData;

        public bool HasCapitalStructure
        {
            get => _hasCapitalStructure;
            set
            {
                if (SetField(ref _hasCapitalStructure, value))
                    HasChartData = HasCapitalStructure || HasIncomeData;
            }
        }

        public bool HasIncomeData
        {
            get => _hasIncomeData;
            set
            {
                if (SetField(ref _hasIncomeData, value))
                    HasChartData = HasCapitalStructure || HasIncomeData;
            }
        }

        public bool HasChartData
        {
            get => _hasChartData;
            set => SetField(ref _hasChartData, value);
        }

        public ObservableCollection<DocumentInfo> Documents
        {
            get => _documents;
            set => SetField(ref _documents, value);
        }

        public DocumentInfo? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (SetField(ref _selectedDocument, value))
                {
                    if (value != null)
                    {
                        SelectedComparison = null;
                        ShowComparisonView = false;
                        OnDocumentSelected(value);
                    }
                }
            }
        }

        public ObservableCollection<ExtractedMetric> ExtractedMetrics
        {
            get => _extractedMetrics;
            set => SetField(ref _extractedMetrics, value);
        }

        public ObservableCollection<AiFinding> AiFindings
        {
            get => _aiFindings;
            set => SetField(ref _aiFindings, value);
        }

        public FinancialData FinancialData
        {
            get => _financialData;
            set => SetField(ref _financialData, value);
        }

        public FinancialRatios FinancialRatios
        {
            get => _financialRatios;
            set => SetField(ref _financialRatios, value);
        }

        public string DocumentSummary
        {
            get => _documentSummary;
            set => SetField(ref _documentSummary, value);
        }

        public int AiProgress
        {
            get => _aiProgress;
            set => SetField(ref _aiProgress, value);
        }

        public string AiStatusText
        {
            get => _aiStatusText;
            set => SetField(ref _aiStatusText, value);
        }

        public bool IsAiProcessing
        {
            get => _isAiProcessing;
            set
            {
                if (SetField(ref _isAiProcessing, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<string> DebugLogs
        {
            get => _debugLogs;
        }

        public bool HasDebugLogs => _settings.DebugMode && _debugLogs.Count > 0;

        public ISeries[] CapitalStructureSeries
        {
            get => _capitalStructureSeries;
            set => SetField(ref _capitalStructureSeries, value);
        }

        public ISeries[] IncomeDistributionSeries
        {
            get => _incomeDistributionSeries;
            set => SetField(ref _incomeDistributionSeries, value);
        }

        public bool ShowComparisonView
        {
            get => _showComparisonView;
            set => SetField(ref _showComparisonView, value);
        }

        public ObservableCollection<DocumentComparison> Comparisons
        {
            get => _comparisons;
            set => SetField(ref _comparisons, value);
        }

        public DocumentComparison? SelectedComparison
        {
            get => _selectedComparison;
            set
            {
                if (SetField(ref _selectedComparison, value))
                {
                    if (value != null)
                    {
                        ShowComparisonView = true;
                        SelectedDocument = null;
                        LoadComparisonView(value);
                    }
                }
            }
        }

        public bool HasComparableDocs
        {
            get => _hasComparableDocs;
            set => SetField(ref _hasComparableDocs, value);
        }

        private bool _hasComparisons;
        public bool HasComparisons
        {
            get => _hasComparisons;
            set => SetField(ref _hasComparisons, value);
        }

        private bool _isIndexing;
        public bool IsIndexing
        {
            get => _isIndexing;
            set => SetField(ref _isIndexing, value);
        }

        private string _indexingStatus = "";
        public string IndexingStatus
        {
            get => _indexingStatus;
            set => SetField(ref _indexingStatus, value);
        }

        private int _indexingProgress;
        public int IndexingProgress
        {
            get => _indexingProgress;
            set => SetField(ref _indexingProgress, value);
        }

        private int _indexingTotal;
        public int IndexingTotal
        {
            get => _indexingTotal;
            set => SetField(ref _indexingTotal, value);
        }

        public static SKColor AccentColor => new(0, 120, 212);
        public static SKColor GreenColor => new(46, 125, 50);
        public static SKColor RedColor => new(198, 40, 40);
        public static SKColor OrangeColor => new(249, 168, 37);
        public static SKColor PurpleColor => new(123, 31, 162);
        public static SKColor GrayColor => new(158, 158, 158);

        public ICommand LoadDocumentCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand ExportReportCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenHelpCommand { get; }
        public ICommand DeleteDocumentCommand { get; }
        public ICommand ClearDebugCommand { get; }
        public ICommand CopyLogsCommand { get; }
        public ICommand NewComparisonCommand { get; }
        public ICommand DeleteComparisonCommand { get; }
        public MainViewModel()
        {
            _docParser = new DocumentParserService();
            _extractor = new FinancialExtractorService();
            _calculator = new FinancialCalculatorService();
            _storageService = new StorageService();
            _settings = AppSettings.Load();

            _storageService.Initialize();

            LoadDocumentCommand = new RelayCommand(LoadDocument);
            AnalyzeCommand = new RelayCommand(async _ => await AnalyzeAsync(), _ => Documents.Count > 0 && !IsAiProcessing);
            ExportReportCommand = new RelayCommand(ExportReport);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenHelpCommand = new RelayCommand(OpenHelp);
            DeleteDocumentCommand = new RelayCommand(DeleteDocument, _ => SelectedDocument != null);
            ClearDebugCommand = new RelayCommand(ClearDebug);
            CopyLogsCommand = new RelayCommand(CopyLogs);
            _comparisons.CollectionChanged += (_, _) =>
            {
                HasComparisons = _comparisons.Count > 0;
            };
            _debugLogs.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasDebugLogs));
            };
            NewComparisonCommand = new RelayCommand(async _ => await StartNewComparisonAsync(), _ => HasComparableDocs && !IsAiProcessing);
            DeleteComparisonCommand = new RelayCommand(DeleteComparison, _ => SelectedComparison != null);

            LoadSavedDocuments();

            _ = EnsureEmbeddingModelAsync();
        }

        private async Task EnsureEmbeddingModelAsync()
        {
            try
            {
                using var embedder = new EmbeddingService(_settings, Log);
                await embedder.EnsureModelExistsAsync();
            }
            catch { }
        }

        private void LoadSavedDocuments()
        {
            var storedDocs = _storageService.LoadDocuments();

            foreach (var stored in storedDocs)
            {
                var doc = new DocumentInfo
                {
                    FileName = stored.FileName,
                    FilePath = stored.FilePath,
                    Type = Enum.TryParse<DocumentType>(stored.DocumentType, out var type) ? type : DocumentType.Pdf,
                    FileSize = stored.FileSize,
                    DateAdded = stored.DateAdded,
                    Status = Enum.TryParse<DocumentStatus>(stored.Status, out var status) ? status : DocumentStatus.NotProcessed,
                    ExtractedText = string.Empty,
                    StoredId = stored.Id
                };

                if (doc.Status == DocumentStatus.Completed)
                {
                    var result = _storageService.LoadAnalysis(stored.Id);
                    if (result.data != null)
                    {
                        doc.CachedFinancialData = result.data;
                        doc.CachedFinancialRatios = _calculator.CalculateRatios(result.data);
                        doc.CachedFindings = result.findings;
                    }
                }

                Documents.Add(doc);
            }

            var savedComparisons = _storageService.LoadComparisons();
            foreach (var comp in savedComparisons)
            {
                if (string.IsNullOrEmpty(comp.Doc1Name) || string.IsNullOrEmpty(comp.Doc2Name))
                {
                    var doc1 = Documents.FirstOrDefault(d => d.StoredId == comp.Doc1Id);
                    var doc2 = Documents.FirstOrDefault(d => d.StoredId == comp.Doc2Id);
                    if (doc1 != null)
                        comp.Doc1Name = doc1.CachedFinancialData?.CompanyName
                            ?? Path.GetFileNameWithoutExtension(doc1.FileName);
                    if (doc2 != null)
                        comp.Doc2Name = doc2.CachedFinancialData?.CompanyName
                            ?? Path.GetFileNameWithoutExtension(doc2.FileName);
                }
                Comparisons.Add(comp);
            }

            if (Documents.Count > 0)
            {
                var lastAnalyzed = Documents.FirstOrDefault(d => d.Status == DocumentStatus.Completed);
                SelectedDocument = lastAnalyzed ?? Documents.First();
            }

            UpdateComparableFlag();
        }

        private void OnDocumentSelected(DocumentInfo doc)
        {

            if (doc.CachedFinancialData != null)
            {
                FinancialData = doc.CachedFinancialData;
                FinancialRatios = doc.CachedFinancialRatios ?? new FinancialRatios();
                ExtractedMetrics = new ObservableCollection<ExtractedMetric>(doc.CachedFinancialData.ToMetrics());
                AiFindings = new ObservableCollection<AiFinding>(doc.CachedFindings ?? new List<AiFinding>());

                var metricsCount = doc.CachedFinancialData.SourceFields.Count;
                var findingsCount = doc.CachedFindings?.Count ?? 0;
                var ratiosCount = (doc.CachedFinancialRatios?.ROE.HasValue == true ? 1 : 0)
                    + (doc.CachedFinancialRatios?.ROA.HasValue == true ? 1 : 0)
                    + (doc.CachedFinancialRatios?.DebtToEquity.HasValue == true ? 1 : 0)
                    + (doc.CachedFinancialRatios?.NIM.HasValue == true ? 1 : 0)
                    + (doc.CachedFinancialRatios?.CostToIncome.HasValue == true ? 1 : 0);

                var parts = new List<string>();
                if (!string.IsNullOrEmpty(doc.CachedFinancialData.CompanyName))
                    parts.Add(doc.CachedFinancialData.CompanyName);
                if (metricsCount > 0) parts.Add($"метрик: {metricsCount}");
                if (ratiosCount > 0) parts.Add($"коэф.: {ratiosCount}");
                if (findingsCount > 0) parts.Add($"выводов: {findingsCount}");
                DocumentSummary = string.Join(" • ", parts);
                if (string.IsNullOrEmpty(DocumentSummary))
                    DocumentSummary = doc.FileName;

                UpdatePieCharts(doc.CachedFinancialData, doc.CachedFinancialRatios);
            }
            else
            {
                FinancialData = new FinancialData();
                FinancialRatios = new FinancialRatios();
                ExtractedMetrics = new ObservableCollection<ExtractedMetric>();
                AiFindings = new ObservableCollection<AiFinding>();
                var statusText = doc.Status switch
                {
                    DocumentStatus.NotProcessed => "Не обработан",
                    DocumentStatus.Processing => "В обработке...",
                    _ => ""
                };
                DocumentSummary = $"{doc.FileName} - {statusText}";
                CapitalStructureSeries = Array.Empty<ISeries>();
                IncomeDistributionSeries = Array.Empty<ISeries>();
                HasCapitalStructure = false;
                HasIncomeData = false;
            }
        }

        private void LoadDocument(object? parameter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Документы|*.pdf;*.doc;*.docx|PDF-файлы|*.pdf|Word-файлы|*.doc;*.docx",
                Title = "Выберите документ",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        var existing = Documents.FirstOrDefault(d =>
                            d.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            SelectedDocument = existing;
                            continue;
                        }

                        var doc = _docParser.LoadDocument(file);
                        Documents.Add(doc);

                        var stored = new StoredDocument
                        {
                            FileName = doc.FileName,
                            FilePath = doc.FilePath,
                            DocumentType = doc.Type.ToString(),
                            FileSize = doc.FileSize,
                            DateAdded = doc.DateAdded,
                            Status = doc.Status.ToString()
                        };

                        var savedId = _storageService.SaveDocument(stored);
                        doc.StoredId = savedId;
                        SelectedDocument = doc;

                        _ = IndexDocumentAsync(doc);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                UpdateComparableFlag();
            }
        }

        private Dispatcher? _dispatcher;
        private Dispatcher Dispatcher => _dispatcher ??= Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        private async Task IndexDocumentAsync(DocumentInfo doc)
        {
            if (doc.StoredId == null || string.IsNullOrEmpty(doc.ExtractedText) || doc.ExtractedText.Length < 100)
                return;

            // проверяем, не индексирован ли уже документ
            try
            {
                var existing = _storageService.LoadChunks(doc.StoredId.Value);
                if (existing.Count > 0)
                {
                    doc.ClearExtractedText();
                    Log($"Индексация «{doc.FileName}» пропущена: {existing.Count} чанков уже есть");
                    return;
                }
            }
            catch { }

            await Dispatcher.InvokeAsync(() =>
            {
                IsIndexing = true;
                IndexingStatus = $"Индексация «{doc.FileName}»...";
            });

            try
            {
                using var embedder = new EmbeddingService(_settings, Log);
                var chunks = EmbeddingService.SplitIntoChunks(doc.ExtractedText, 1000, 100, maxChunks: 500);

                if (chunks.Count == 0)
                {
                    await Dispatcher.InvokeAsync(() => IsIndexing = false);
                    return;
                }

                if (chunks.Count >= 500)
                    Log($"Внимание: документ «{doc.FileName}» слишком большой, индексируется только 500 из ~{doc.ExtractedText.Length / 900} чанков");

                await Dispatcher.InvokeAsync(() =>
                {
                    IndexingTotal = chunks.Count;
                    IndexingProgress = 0;
                });

                var chunkData = new List<(int Index, string Content, string EmbeddingBase64)>();
                for (int i = 0; i < chunks.Count; i++)
                {
                    var emb = await embedder.GenerateEmbeddingAsync(chunks[i]);
                    var b64 = EmbeddingService.Normalize(emb);
                    chunkData.Add((i, chunks[i], b64));

                    if (i % 5 == 0 || i == chunks.Count - 1)
                    {
                        var idx = i + 1;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            IndexingProgress = idx;
                            IndexingStatus = $"Индексация «{doc.FileName}»: {idx}/{chunks.Count}";
                        });
                    }
                }

                _storageService.SaveChunks(doc.StoredId.Value, chunkData);
                Log($"Индексация «{doc.FileName}» завершена: {chunks.Count} чанков");

                // очищаем текст документа - для RAG он больше не нужен (хранится в чанках),
                // а для анализа будет переизвлечён из файла на следующем шаге
                doc.ClearExtractedText();
            }
            catch (Exception ex)
            {
                Log($"Ошибка индексации «{doc.FileName}»: {ex.Message}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    IsIndexing = false;
                    IndexingStatus = "";
                    IndexingProgress = 0;
                    IndexingTotal = 0;
                });
            }
        }

        private async Task AnalyzeAsync()
        {
            try
            {
                if (Documents.Count == 0) return;

                _settings = AppSettings.Load();

                IsAiProcessing = true;
                AiProgress = 0;
                AiStatusText = "Начало анализа...";
                _debugLogs.Clear();

                Log("=== Анализ начат ===");
                Log($"Документов: {Documents.Count}");

                var toAnalyze = new List<DocumentInfo>();
                foreach (var doc in Documents)
                {
                    if (doc.IsComparison) continue;

                    if (doc.Status == DocumentStatus.Completed && doc.CachedFinancialData != null)
                    {
                        Log($"Пропускаю «{doc.FileName}» - уже проанализирован");
                        continue;
                    }

                    if (doc.Status == DocumentStatus.Completed && doc.StoredId.HasValue)
                    {
                        var hasAnalysis = _storageService.HasAnalysis(doc.StoredId.Value);
                        if (hasAnalysis)
                        {
                            var existingAnalysis = _storageService.LoadAnalysis(doc.StoredId.Value);
                            if (existingAnalysis.data != null)
                            {
                                doc.CachedFinancialData = existingAnalysis.data;
                                doc.CachedFinancialRatios = _calculator.CalculateRatios(existingAnalysis.data);
                                doc.CachedFindings = existingAnalysis.findings;
                                Log($"Загрузил сохранённый анализ «{doc.FileName}»");
                                continue;
                            }
                        }
                    }

                    toAnalyze.Add(doc);
                }

            if (toAnalyze.Count == 0)
            {
                Log("Все документы уже проанализированы");
                AiStatusText = "Все документы уже проанализированы";
                AiProgress = 100;

                if (SelectedDocument != null)
                    OnDocumentSelected(SelectedDocument);

                IsAiProcessing = false;
                return;
            }

            foreach (var doc in toAnalyze)
            {
                doc.Status = DocumentStatus.Processing;
                if (doc.StoredId.HasValue)
                    _storageService.UpdateDocumentStatus(doc.StoredId.Value, "Processing");
            }

            var totalSteps = toAnalyze.Count * 4;
            var currentStep = 0;

            foreach (var doc in toAnalyze)
            {
                Log($"--- Обработка документа: {doc.FileName} ---");

                AiStatusText = $"Этап 1: Извлечение текста - {doc.FileName}";

                if (string.IsNullOrEmpty(doc.ExtractedText) || doc.ExtractedText.Length < 20)
                {
                    Log($"Извлечение текста из файла: {doc.FileName}");
                    doc.ExtractedText = _docParser.LoadDocument(doc.FilePath).ExtractedText;
                    Log($"Извлечено: {doc.ExtractedText.Length} символов");
                }

                currentStep++;
                AiProgress = currentStep * 100 / Math.Max(totalSteps, 1);

                if (doc.ExtractedText.Length < 20)
                {
                    Log("Текст слишком короткий, пропускаю документ");
                    doc.Status = DocumentStatus.NotProcessed;
                    if (doc.StoredId.HasValue)
                        _storageService.UpdateDocumentStatus(doc.StoredId.Value, "NotProcessed");
                    currentStep += 3;
                    AiProgress = currentStep * 100 / Math.Max(totalSteps, 1);
                    continue;
                }

                AiStatusText = $"Этап 2: AI-извлечение - {doc.FileName}";
                using var aiService = new AiService(_settings, Log);

                string? ragContext = null;
                if (doc.StoredId.HasValue)
                {
                    try
                    {
                        var queryText = doc.ExtractedText.Length > 1000
                            ? doc.ExtractedText.Substring(0, 1000) : doc.ExtractedText;
                        using var embedder = new EmbeddingService(_settings, Log);
                        var queryEmb = await embedder.GenerateEmbeddingAsync(queryText);
                        var similar = _storageService.SearchSimilarChunks(queryEmb, doc.StoredId.Value, 3);
                        if (similar.Count > 0)
                        {
                            ragContext = string.Join("\n---\n", similar.Select(s => s.Chunk.Content));
                            Log($"RAG: найдено {similar.Count} похожих чанков из других документов");
                        }
                    }
                    catch (Exception ragEx)
                    {
                        Log($"RAG: ошибка поиска: {ragEx.Message}");
                    }
                }

                var llmData = await aiService.ExtractFinancialDataAsync(doc.ExtractedText, (p) =>
                {
                    AiStatusText = $"{doc.FileName}: {p}";
                }, ragContext);

                Log($"Извлечение LLM: {llmData.SourceFields.Count} полей");

                if (!llmData.HasAnyData)
                {
                    Log("LLM не вернул данных, резервное извлечение через regex...");
                    var regexData = _extractor.ExtractAsFinancialData(doc.ExtractedText);
                    Log($"Извлечение regex: {regexData.SourceFields.Count} полей");
                    llmData = FinancialExtractorService.MergeFinancialData(llmData, regexData);
                    Log($"Объединённый результат: {llmData.SourceFields.Count} полей");
                }

                doc.ClearExtractedText();
                Log($"Очистил текст «{doc.FileName}» из памяти");

                currentStep++;
                AiProgress = currentStep * 100 / Math.Max(totalSteps, 1);

                AiStatusText = $"Этап 3: Расчёт коэффициентов - {doc.FileName}";
                var docRatios = _calculator.CalculateRatios(llmData);

                currentStep++;
                AiProgress = currentStep * 100 / Math.Max(totalSteps, 1);

                List<AiFinding> docFindings;
                if (!llmData.HasAnyData)
                {
                    Log("Нет данных - базовые выводы");
                    docFindings = GenerateBasicFindingsFor(llmData, docRatios);
                }
                else
                {
                    AiStatusText = $"Этап 4: AI-анализ - {doc.FileName}";
                    try
                    {
                        docFindings = await aiService.GenerateTypedFindingsAsync(llmData, docRatios, (p) =>
                        {
                            AiStatusText = $"{doc.FileName}: {p}";
                        });
                    }
                    catch (Exception ex)
                    {
                        Log($"Ошибка AI: {ex.Message}");
                        docFindings = GenerateBasicFindingsFor(llmData, docRatios);
                    }
                }

                doc.CachedFinancialData = llmData;
                doc.CachedFinancialRatios = docRatios;
                doc.CachedFindings = docFindings;
                doc.Status = DocumentStatus.Completed;

                if (doc.StoredId.HasValue)
                {
                    _storageService.UpdateDocumentStatus(doc.StoredId.Value, "Completed");
                    _storageService.SaveAnalysis(doc.StoredId.Value, llmData, docFindings);
                }

                currentStep++;
                AiProgress = currentStep * 100 / Math.Max(totalSteps, 1);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                Log($"--- Завершён: {doc.FileName} ---");
            }

            AiProgress = 100;
            AiStatusText = "Анализ завершён";

            if (SelectedDocument != null)
                OnDocumentSelected(SelectedDocument);

            UpdateComparableFlag();

            Log("Очистка памяти...");
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var dbSizeBefore = _storageService.GetDatabaseSize();
            Log($"Размер БД до оптимизации: {dbSizeBefore / 1024} КБ");
            _storageService.VacuumDatabase();
            var dbSizeAfter = _storageService.GetDatabaseSize();
            Log($"Размер БД после оптимизации: {dbSizeAfter / 1024} КБ");

            Log("=== Анализ завершён ===");
            await Task.Delay(3000);
            AiStatusText = "";
            AiProgress = 0;
            IsAiProcessing = false;
            }
            catch (Exception ex)
            {
                try { Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}"); } catch { }
                try { Log(ex.StackTrace ?? ""); } catch { }
                AiStatusText = $"Ошибка: {ex.Message}";
                AiProgress = 0;
                IsAiProcessing = false;

                try
                {
                    MessageBox.Show($"Произошла ошибка при анализе:\n\n{ex.Message}\n\nПодробности в журнале отладки.",
                        "Ошибка анализа", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }

        private async Task StartNewComparisonAsync()
        {
            var completed = Documents
                .Where(d => d.Status == DocumentStatus.Completed && d.CachedFinancialData != null && !d.IsComparison)
                .ToList();

            if (completed.Count < 2)
            {
                MessageBox.Show("Требуется минимум 2 проанализированных документа для сравнения.",
                    "Сравнение недоступно", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new CompareSelectionWindow(completed)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || !dialog.Confirmed) return;

            var doc1 = dialog.SelectedDoc1!;
            var doc2 = dialog.SelectedDoc2!;
            var name1 = !string.IsNullOrEmpty(doc1.CachedFinancialData?.CompanyName)
                ? doc1.CachedFinancialData.CompanyName : doc1.FileName;
            var name2 = !string.IsNullOrEmpty(doc2.CachedFinancialData?.CompanyName)
                ? doc2.CachedFinancialData.CompanyName : doc2.FileName;

            Log($"=== Сравнение: {doc1.FileName} vs {doc2.FileName} ===");

            var comparison = new DocumentComparison
            {
                Doc1Id = doc1.StoredId ?? 0,
                Doc2Id = doc2.StoredId ?? 0,
                Doc1Name = name1,
                Doc2Name = name2,
                Data1 = doc1.CachedFinancialData,
                Data2 = doc2.CachedFinancialData,
                Ratios1 = doc1.CachedFinancialRatios,
                Ratios2 = doc2.CachedFinancialRatios,
                Metrics = BuildComparisonMetrics(doc1, doc2)
            };

            Comparisons.Add(comparison);
            SelectedComparison = comparison;

            Log($"Сравнение создано: {comparison.Label}");

            _settings = AppSettings.Load();
            IsAiProcessing = true;
            AiProgress = 0;
            AiStatusText = "Поиск контекста для сравнения...";

            string? ragContext = null;
            try
            {
                var queryText = "";
                if (doc1.CachedFinancialData != null && !string.IsNullOrEmpty(doc1.CachedFinancialData.CompanyName))
                    queryText = doc1.CachedFinancialData.CompanyName;

                if (!string.IsNullOrEmpty(doc1.ExtractedText))
                {
                    queryText = doc1.ExtractedText.Length > 1000
                        ? doc1.ExtractedText.Substring(0, 1000) : doc1.ExtractedText;
                }
                else if (!string.IsNullOrEmpty(doc2.ExtractedText))
                {
                    queryText = doc2.ExtractedText.Length > 1000
                        ? doc2.ExtractedText.Substring(0, 1000) : doc2.ExtractedText;
                }

                if (!string.IsNullOrEmpty(queryText))
                {
                    using var embedder = new EmbeddingService(_settings, Log);
                    var queryEmb = await embedder.GenerateEmbeddingAsync(queryText);
                    var similar = _storageService.SearchSimilarChunks(queryEmb, excludeDocId: 0, topK: 5);
                    if (similar.Count > 0)
                    {
                        ragContext = string.Join("\n---\n", similar.Select(s => s.Chunk.Content));
                        Log($"RAG сравнения: найдено {similar.Count} похожих чанков");
                    }
                }
            }
            catch (Exception ragEx)
            {
                Log($"RAG сравнения: ошибка поиска: {ragEx.Message}");
            }

            AiStatusText = "Генерация сравнения...";

            using var aiService = new AiService(_settings, Log);
            try
            {
                AiStatusText = "AI-сравнение документов...";
                AiProgress = 30;
                var aiFindings = await aiService.GenerateComparisonFindingsAsync(
                    doc1.CachedFinancialData!, doc1.CachedFinancialRatios!, name1,
                    doc2.CachedFinancialData!, doc2.CachedFinancialRatios!, name2,
                    ragContext,
                    (p) => { AiStatusText = $"Сравнение: {p}"; });
                comparison.Findings.AddRange(aiFindings);
                Log($"AI-сравнение: получено {aiFindings.Count} выводов");

                var savedId = _storageService.SaveComparison(comparison);
                comparison.Id = savedId;
                Log($"Сравнение сохранено в БД (Id={savedId})");

                AiProgress = 100;
                AiStatusText = "Сравнение завершено";

                if (SelectedComparison == comparison)
                    LoadComparisonView(comparison);
            }
            catch (Exception ex)
            {
                Log($"Ошибка AI-сравнения: {ex.Message}");
                AiStatusText = "Ошибка при сравнении";
            }
            finally
            {
                await Task.Delay(2000);
                AiStatusText = "";
                AiProgress = 0;
                IsAiProcessing = false;
            }
        }

        private List<ExtractedMetric> BuildComparisonMetrics(DocumentInfo doc1, DocumentInfo doc2)
        {
            var d1 = doc1.CachedFinancialData!;
            var d2 = doc2.CachedFinancialData!;
            var r1 = doc1.CachedFinancialRatios!;
            var r2 = doc2.CachedFinancialRatios!;
            var metrics = new List<ExtractedMetric>();

            void AddPair(string label, decimal? v1, decimal? v2, bool inverted = false)
            {
                if (!v1.HasValue && !v2.HasValue)
                {
                    metrics.Add(new ExtractedMetric
                    { Label = label, Value = "Н/Д - Н/Д", Category = "Сравнение", RawText = "equal" });
                    return;
                }

                string op, rawText;
                if (!v1.HasValue) { op = "-"; rawText = "right"; }
                else if (!v2.HasValue) { op = "-"; rawText = "left"; }
                else
                {
                    var cmp = v1.Value.CompareTo(v2.Value);
                    if (inverted) cmp = -cmp;
                    if (cmp > 0) { op = ">"; rawText = "left"; }
                    else if (cmp < 0) { op = "<"; rawText = "right"; }
                    else { op = "="; rawText = "equal"; }
                }

                metrics.Add(new ExtractedMetric
                {
                    Label = label,
                    Value = $"{(v1.HasValue ? FormatNumber(v1.Value) : "Н/Д")} {op} {(v2.HasValue ? FormatNumber(v2.Value) : "Н/Д")}",
                    Category = "Сравнение",
                    RawText = rawText
                });
            }

            AddPair("Выручка", d1.Revenue, d2.Revenue);
            AddPair("Чистая прибыль", d1.NetIncome, d2.NetIncome);
            AddPair("Активы", d1.Assets, d2.Assets);
            AddPair("Обязательства", d1.Liabilities, d2.Liabilities);
            AddPair("Капитал", d1.Equity, d2.Equity);
            AddPair("ROE", r1.ROE, r2.ROE);
            AddPair("ROA", r1.ROA, r2.ROA);
            AddPair("Долг/Капитал", r1.DebtToEquity, r2.DebtToEquity, inverted: true);
            AddPair("NIM", r1.NIM, r2.NIM);
            AddPair("C/I", r1.CostToIncome, r2.CostToIncome, inverted: true);
            return metrics;
        }

        private void LoadComparisonView(DocumentComparison comparison)
        {
            FinancialData = new FinancialData();
            FinancialRatios = new FinancialRatios();
            ExtractedMetrics = new ObservableCollection<ExtractedMetric>(comparison.Metrics);
            AiFindings = new ObservableCollection<AiFinding>(comparison.Findings);

            var parts = new List<string> { $"метрик: {comparison.Metrics.Count}" };
            if (comparison.Findings.Count > 0)
                parts.Add($"выводов: {comparison.Findings.Count}");
            DocumentSummary = string.Join(" • ", parts);

            CapitalStructureSeries = Array.Empty<ISeries>();
            IncomeDistributionSeries = Array.Empty<ISeries>();
            HasCapitalStructure = false;
            HasIncomeData = false;

            SelectedComparison = comparison;
            ShowComparisonView = true;
        }

        private void DeleteComparison(object? parameter)
        {
            if (parameter is DocumentComparison comp)
            {
                var result = MessageBox.Show($"Удалить сравнение «{comp.Label}»?",
                    "Удаление сравнения", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                if (comp.Id > 0)
                    _storageService.DeleteComparison(comp.Id);

                Comparisons.Remove(comp);
                if (SelectedComparison == comp)
                {
                    SelectedComparison = null;
                    ShowComparisonView = false;
                }
                if (SelectedDocument != null)
                    OnDocumentSelected(SelectedDocument);

                UpdateComparableFlag();
            }
        }

        private void UpdateComparableFlag()
        {
            var completed = Documents.Count(d =>
                d.Status == DocumentStatus.Completed && d.CachedFinancialData != null);
            HasComparableDocs = completed >= 2;
        }

        private List<AiFinding> GenerateBasicFindingsFor(FinancialData data, FinancialRatios ratios)
        {
            var findings = new List<AiFinding>();

            if (data.Revenue.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Выручка", Description = $"Общая выручка: {FormatNumber(data.Revenue.Value)}" });
            if (data.NetIncome.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Чистая прибыль", Description = $"Чистая прибыль: {FormatNumber(data.NetIncome.Value)}" });
            if (ratios.ROE.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "ROE", Description = $"Рентабельность капитала: {ratios.ROE:F2}%" });
            if (ratios.ROA.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "ROA", Description = $"Рентабельность активов: {ratios.ROA:F2}%" });
            if (ratios.DebtToEquity.HasValue)
                findings.Add(new AiFinding { Type = FindingType.Observation, Title = "Долг / Капитал", Description = $"Отношение долга к капиталу: {ratios.DebtToEquity:F2}" });

            if (findings.Count == 0)
                findings.Add(new AiFinding { Type = FindingType.Problem, Title = "Недостаточно данных", Description = "Не удалось извлечь финансовые данные из документа" });

            return findings;
        }

        private void UpdatePieCharts(FinancialData data, FinancialRatios? ratios)
        {
            var hasEquity = data.Equity.HasValue && data.Equity.Value > 0;
            var hasLiabilities = data.Liabilities.HasValue && data.Liabilities.Value > 0;
            var hasRevenue = data.Revenue.HasValue && data.Revenue.Value > 0;
            var hasNetIncome = data.NetIncome.HasValue;
            var hasExpenses = data.OperatingExpenses.HasValue && data.OperatingExpenses.Value > 0;

            HasCapitalStructure = hasEquity || hasLiabilities;
            HasIncomeData = hasRevenue || hasNetIncome || hasExpenses;

            if (hasEquity || hasLiabilities)
            {
                var series = new List<ISeries>();
                if (hasLiabilities)
                    series.Add(new PieSeries<double>
                    {
                        Name = "Обязательства",
                        Values = new double[] { (double)data.Liabilities!.Value },
                        Fill = new SolidColorPaint(RedColor),
                        Stroke = null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                        DataLabelsSize = 12,
                        DataLabelsFormatter = (p) => FormatNumber((decimal)p.Coordinate.PrimaryValue),
                        ToolTipLabelFormatter = (p) => $"{FormatNumber((decimal)p.Coordinate.PrimaryValue)}"
                    });
                if (hasEquity)
                    series.Add(new PieSeries<double>
                    {
                        Name = "Капитал",
                        Values = new double[] { (double)data.Equity!.Value },
                        Fill = new SolidColorPaint(GreenColor),
                        Stroke = null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                        DataLabelsSize = 12,
                        DataLabelsFormatter = (p) => FormatNumber((decimal)p.Coordinate.PrimaryValue),
                        ToolTipLabelFormatter = (p) => $"{FormatNumber((decimal)p.Coordinate.PrimaryValue)}"
                    });
                CapitalStructureSeries = series.ToArray();
            }
            else
            {
                CapitalStructureSeries = Array.Empty<ISeries>();
            }

            if (hasRevenue || hasNetIncome || hasExpenses)
            {
                var series = new List<ISeries>();
                if (hasRevenue)
                    series.Add(new PieSeries<double>
                    {
                        Name = "Выручка",
                        Values = new double[] { (double)data.Revenue!.Value },
                        Fill = new SolidColorPaint(AccentColor),
                        Stroke = null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                        DataLabelsSize = 12,
                        DataLabelsFormatter = (p) => FormatNumber((decimal)p.Coordinate.PrimaryValue),
                        ToolTipLabelFormatter = (p) => $"{FormatNumber((decimal)p.Coordinate.PrimaryValue)}"
                    });
                if (hasExpenses)
                    series.Add(new PieSeries<double>
                    {
                        Name = "Операционные расходы",
                        Values = new double[] { (double)data.OperatingExpenses!.Value },
                        Fill = new SolidColorPaint(RedColor),
                        Stroke = null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                        DataLabelsSize = 12,
                        DataLabelsFormatter = (p) => FormatNumber((decimal)p.Coordinate.PrimaryValue),
                        ToolTipLabelFormatter = (p) => $"{FormatNumber((decimal)p.Coordinate.PrimaryValue)}"
                    });
                if (hasNetIncome)
                    series.Add(new PieSeries<double>
                    {
                        Name = "Чистая прибыль",
                        Values = new double[] { (double)data.NetIncome!.Value },
                        Fill = new SolidColorPaint(GreenColor),
                        Stroke = null,
                        DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                        DataLabelsSize = 12,
                        DataLabelsFormatter = (p) => FormatNumber((decimal)p.Coordinate.PrimaryValue),
                        ToolTipLabelFormatter = (p) => $"{FormatNumber((decimal)p.Coordinate.PrimaryValue)}"
                    });
                IncomeDistributionSeries = series.ToArray();
            }
            else
            {
                IncomeDistributionSeries = Array.Empty<ISeries>();
            }
        }

        private void ExportReport(object? parameter)
        {
            var format = (parameter as string ?? "txt").ToLower();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт отчёта"
            };

            switch (format)
            {
                case "pdf":
                    dialog.Filter = "PDF файл|*.pdf";
                    break;
                case "word":
                    dialog.Filter = "Word документ|*.docx";
                    break;
                case "csv":
                    dialog.Filter = "Excel (CSV)|*.csv";
                    break;
                default:
                    dialog.Filter = "Текстовый файл|*.txt";
                    break;
            }

            if (dialog.ShowDialog() == true)
            {
                switch (format)
                {
                    case "pdf": ExportPdf(dialog.FileName); break;
                    case "word": ExportWord(dialog.FileName); break;
                    case "csv": ExportCsv(dialog.FileName); break;
                    default: ExportTxt(dialog.FileName); break;
                }
            }
        }

        private string BuildExportHeader()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== ФИНАНСОВЫЙ ОТЧЁТ ===");
            sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy}");
            sb.AppendLine();

            if (ShowComparisonView && SelectedComparison != null)
            {
                sb.AppendLine($"Сравнение: {SelectedComparison.Doc1Name} vs {SelectedComparison.Doc2Name}");
                sb.AppendLine();
            }
            else if (SelectedDocument?.IsComparison == true && SelectedDocument.ComparisonReference != null)
            {
                sb.AppendLine($"Сравнение: {SelectedDocument.ComparisonReference.Doc1Name} vs {SelectedDocument.ComparisonReference.Doc2Name}");
                sb.AppendLine();
            }
            else if (SelectedDocument != null)
            {
                sb.AppendLine($"Документ: {SelectedDocument.FileName}");
                sb.AppendLine();
            }

            sb.AppendLine("ИЗВЛЕЧЁННЫЕ МЕТРИКИ:");
            foreach (var m in ExtractedMetrics)
                sb.AppendLine($"  {m.Label}: {m.Value}");
            sb.AppendLine();

            return sb.ToString();
        }

        private void ExportTxt(string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(BuildExportHeader());

            sb.AppendLine("ВЫВОДЫ AI:");
            foreach (var f in AiFindings)
                sb.AppendLine($"  [{f.Type}] {f.Title}: {f.Description}");

            File.WriteAllText(path, sb.ToString());
            MessageBox.Show("Отчёт экспортирован в TXT.", "Экспорт",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportPdf(string path)
        {
            try
            {
                var content = BuildExportHeader();
                content += "ВЫВОДЫ AI:\n";
                foreach (var f in AiFindings)
                    content += $"  [{f.Type}] {f.Title}: {f.Description}\n";

                var lines = content.Split(Environment.NewLine, StringSplitOptions.None);
                float margin = 50, pageWidth = 495, pageHeight = 782, y = margin;
                const float headerSize = 16f, normalSize = 11f, lineSpacing = 1.5f;

                using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
                using var document = SkiaSharp.SKDocument.CreatePdf(stream);
                using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Arial"), normalSize);
                using var boldFont = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Arial"), headerSize);
                using var paint = new SkiaSharp.SKPaint { IsAntialias = true, Color = SkiaSharp.SKColors.Black };

                for (int idx = 0; idx < lines.Length;)
                {
                    using var canvas = document.BeginPage(pageWidth + 100, pageHeight + 60);
                    y = margin;

                    while (idx < lines.Length)
                    {
                        var line = lines[idx].TrimEnd();
                        bool isHeader = line.StartsWith("===") || line.StartsWith("ИЗВЛЕЧ") || line.StartsWith("ВЫВОДЫ");

                        var currentFont = isHeader ? boldFont : font;
                        var metrics = currentFont.Metrics;
                        float lineHeight = (-metrics.Ascent + metrics.Descent + metrics.Leading) * lineSpacing;

                        if (y + lineHeight > pageHeight)
                            break;

                        if (!string.IsNullOrWhiteSpace(line))
                            canvas.DrawText(line, margin, y, currentFont, paint);

                        y += lineHeight;
                        idx++;
                    }

                    document.EndPage();
                }
                document.Close();
                stream.Close();

                MessageBox.Show("Отчёт экспортирован в PDF.", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                if (MessageBox.Show($"PDF: {ex.GetType().Name}: {msg}\n\nПопробовать сохранить как Word?", "Ошибка",
                    MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    ExportWord(Path.ChangeExtension(path, ".docx"));
                }
            }
        }

        private void ExportWord(string path)
        {
            try
            {
                using var doc = new Spire.Doc.Document();
                var section = doc.AddSection();

                var header = BuildExportHeader();
                foreach (var line in header.Split(Environment.NewLine))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var clean = line.TrimStart();
                    bool isBold = clean.StartsWith("===") || clean.StartsWith("ИЗВЛЕЧ") || clean.StartsWith("ВЫВОДЫ");
                    var para = section.AddParagraph();
                    para.AppendText(clean);
                    if (isBold)
                        para.ApplyStyle(Spire.Doc.Documents.BuiltinStyle.Heading3);
                }

                var findingsPara = section.AddParagraph();
                findingsPara.AppendText("ВЫВОДЫ AI:");
                findingsPara.ApplyStyle(Spire.Doc.Documents.BuiltinStyle.Heading3);

                foreach (var f in AiFindings)
                {
                    var p = section.AddParagraph();
                    p.AppendText($"  [{f.Type}] {f.Title}: {f.Description}");
                }

                doc.SaveToFile(path, Spire.Doc.FileFormat.Docx);
                MessageBox.Show("Отчёт экспортирован в Word.", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта Word: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv(string path)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Показатель,Значение");
                foreach (var m in ExtractedMetrics)
                {
                    var label = m.Label.Replace("\"", "\"\"");
                    var value = m.Value?.Replace("\"", "\"\"") ?? "";
                    sb.AppendLine($"\"{label}\",\"{value}\"");
                }

                if (SelectedDocument?.CachedFinancialRatios != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("Коэффициент,Значение,Статус");
                    var r = SelectedDocument.CachedFinancialRatios;
                    AddRatioCsv(sb, "ROE", r.ROE, r.RoeStatus);
                    AddRatioCsv(sb, "ROA", r.ROA, r.RoaStatus);
                    AddRatioCsv(sb, "D/E", r.DebtToEquity, r.DebtEquityStatus);
                    AddRatioCsv(sb, "NIM", r.NIM, r.NimStatus);
                    AddRatioCsv(sb, "C/I", r.CostToIncome, r.CostIncomeStatus);
                }
                else if (FinancialRatios != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("Коэффициент,Значение,Статус");
                    AddRatioCsv(sb, "ROE", FinancialRatios.ROE, FinancialRatios.RoeStatus);
                    AddRatioCsv(sb, "ROA", FinancialRatios.ROA, FinancialRatios.RoaStatus);
                    AddRatioCsv(sb, "D/E", FinancialRatios.DebtToEquity, FinancialRatios.DebtEquityStatus);
                    AddRatioCsv(sb, "NIM", FinancialRatios.NIM, FinancialRatios.NimStatus);
                    AddRatioCsv(sb, "C/I", FinancialRatios.CostToIncome, FinancialRatios.CostIncomeStatus);
                }

                File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show("Отчёт экспортирован в Excel (CSV).", "Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта CSV: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void AddRatioCsv(System.Text.StringBuilder sb, string name, decimal? value, Models.RatioStatus status)
        {
            var val = value.HasValue ? $"{value:F2}" : "N/A";
            sb.AppendLine($"\"{name}\",\"{val}\",\"{status}\"");
        }

        private void OpenSettings(object? parameter)
        {
            var window = new Views.SettingsWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();
            _settings.Reload();
            if (!_settings.DebugMode)
                _debugLogs.Clear();
            OnPropertyChanged(nameof(HasDebugLogs));
        }

        private void OpenHelp(object? parameter)
        {
            MessageBox.Show(
                "Финансовый Аналитик\n\n" +
                "1. Загрузите PDF или Word документ\n" +
                "2. Нажмите «Анализ» для извлечения данных и расчёта коэффициентов\n" +
                "3. Просмотрите метрики, коэффициенты и выводы AI\n" +
                "4. Используйте «Новое сравнение» для сравнения двух проанализированных документов\n" +
                "5. Экспортируйте отчёт\n\n" +
                "Настройки AI и режим отладки - через «Настройки».",
                "Справка", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteDocument(object? parameter)
        {
            var doc = parameter as DocumentInfo ?? SelectedDocument;
            if (doc == null) return;

            var result = MessageBox.Show(
                $"Удалить «{doc.FileName}»?\nВсе данные анализа будут удалены.",
                "Удаление документа",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            var docId = doc.StoredId ?? 0;
            if (docId > 0)
            {
                _storageService.DeleteChunksByDocument(docId);
                _storageService.DeleteComparisonsByDocument(docId);
                _storageService.DeleteDocument(docId);
            }

            Documents.Remove(doc);

            if (Documents.Count == 0)
            {
                SelectedDocument = null;
                FinancialData = new FinancialData();
                FinancialRatios = new FinancialRatios();
                ExtractedMetrics = new ObservableCollection<ExtractedMetric>();
                AiFindings = new ObservableCollection<AiFinding>();
                DocumentSummary = "";
                AiStatusText = "";
                CapitalStructureSeries = Array.Empty<ISeries>();
                IncomeDistributionSeries = Array.Empty<ISeries>();
                HasCapitalStructure = false;
                HasIncomeData = false;
            }
            else
            {
                SelectedDocument = Documents.First();
            }

            UpdateComparableFlag();
            CommandManager.InvalidateRequerySuggested();
        }

        private string FormatNumber(decimal value)
        {
            if (value >= 1_000_000_000_000) return $"{value / 1_000_000_000_000:F2} трлн";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000:F2} млрд";
            if (value >= 1_000_000) return $"{value / 1_000_000:F2} млн";
            return value.ToString("N2");
        }

        private void CopyLogs(object? parameter)
        {
            var allLogs = string.Join("\n", _debugLogs);
            if (string.IsNullOrEmpty(allLogs))
            {
                MessageBox.Show("Нет записей для копирования.", "Копирование журнала",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Clipboard.SetText(allLogs);
            MessageBox.Show("Журнал скопирован в буфер обмена.", "Копирование журнала",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearDebug(object? parameter) => _debugLogs.Clear();

        private void Log(string message)
        {
            if (!_settings.DebugMode) return;
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _debugLogs.Add(entry);
        }
    }
}
