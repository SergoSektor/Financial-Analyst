using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using FinancialAnalyst.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalyst.Services
{
    public class StorageService
    {
        private readonly string _dbPath;

        public StorageService()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FinancialAnalyst");
            if (!Directory.Exists(appData))
                Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "data.db");
        }

        public void Initialize()
        {
            var needsReset = false;

            using (var checkCtx = new AppDbContext(_dbPath))
            {
                needsReset = !HasCorrectSchema(checkCtx);
            }

            if (needsReset)
            {
                using var delCtx = new AppDbContext(_dbPath);
                delCtx.Database.EnsureDeleted();
            }

            using var createCtx = new AppDbContext(_dbPath);
            createCtx.Database.EnsureCreated();
            CreateComparisonsTableIfMissing();
            CreateChunksTableIfMissing();

            RunPragma("PRAGMA journal_mode=WAL;");
            RunPragma("PRAGMA wal_autocheckpoint=1000;");
            RunPragma("PRAGMA synchronous=NORMAL;");
            RunPragma("PRAGMA cache_size=-2000;");
        }

        private void RunPragma(string sql)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                using var cmd = new SqliteCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void CreateComparisonsTableIfMissing()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                using var cmd = new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS Comparisons (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Doc1Id INTEGER NOT NULL,
                        Doc2Id INTEGER NOT NULL,
                        Doc1Name TEXT NOT NULL DEFAULT '',
                        Doc2Name TEXT NOT NULL DEFAULT '',
                        Data1Json TEXT NOT NULL DEFAULT '',
                        Data2Json TEXT NOT NULL DEFAULT '',
                        Ratios1Json TEXT NOT NULL DEFAULT '',
                        Ratios2Json TEXT NOT NULL DEFAULT '',
                        MetricsJson TEXT NOT NULL DEFAULT '',
                        FindingsJson TEXT NOT NULL DEFAULT '',
                        CreatedAt TEXT NOT NULL
                    );", conn);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private bool HasCorrectSchema(AppDbContext ctx)
        {
            try
            {
                ctx.Analyses.FirstOrDefault();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<StoredDocument> LoadDocuments()
        {
            using var ctx = new AppDbContext(_dbPath);
            return ctx.Documents.AsNoTracking().ToList();
        }

        public bool HasAnalysis(int documentId)
        {
            using var ctx = new AppDbContext(_dbPath);
            return ctx.Analyses.AsNoTracking().Any(a => a.DocumentId == documentId);
        }

        public int SaveDocument(StoredDocument doc)
        {
            using var ctx = new AppDbContext(_dbPath);
            ctx.Documents.Add(doc);
            ctx.SaveChanges();
            return doc.Id;
        }

        public void UpdateDocumentStatus(int docId, string status)
        {
            using var ctx = new AppDbContext(_dbPath);
            var doc = ctx.Documents.Find(docId);
            if (doc != null)
            {
                doc.Status = status;
                ctx.SaveChanges();
            }
        }

        public void SaveAnalysis(int documentId, FinancialData data, List<AiFinding> findings)
        {
            using var ctx = new AppDbContext(_dbPath);

            var existing = ctx.Analyses.FirstOrDefault(a => a.DocumentId == documentId);
            if (existing != null)
                ctx.Analyses.Remove(existing);

            var analysis = new StoredAnalysis
            {
                DocumentId = documentId,
                FinancialDataJson = JsonSerializer.Serialize(data),
                FindingsJson = JsonSerializer.Serialize(findings),
                AnalyzedAt = DateTime.Now
            };

            ctx.Analyses.Add(analysis);
            ctx.SaveChanges();
        }

        public (FinancialData? data, List<AiFinding>? findings) LoadAnalysis(int documentId)
        {
            using var ctx = new AppDbContext(_dbPath);
            var analysis = ctx.Analyses.AsNoTracking().FirstOrDefault(a => a.DocumentId == documentId);

            if (analysis == null)
                return (null, null);

            FinancialData? data = null;
            List<AiFinding>? findings = null;

            if (!string.IsNullOrEmpty(analysis.FinancialDataJson))
                data = JsonSerializer.Deserialize<FinancialData>(analysis.FinancialDataJson);

            if (!string.IsNullOrEmpty(analysis.FindingsJson))
                findings = JsonSerializer.Deserialize<List<AiFinding>>(analysis.FindingsJson);

            return (data, findings);
        }

        public void DeleteDocument(int docId)
        {
            using var ctx = new AppDbContext(_dbPath);
            var doc = ctx.Documents.Find(docId);
            if (doc != null)
            {
                ctx.Documents.Remove(doc);
                var analyses = ctx.Analyses.Where(a => a.DocumentId == docId).ToList();
                ctx.Analyses.RemoveRange(analyses);
                ctx.SaveChanges();
            }
        }

        public int SaveComparison(DocumentComparison comparison)
        {
            using var ctx = new AppDbContext(_dbPath);
            var data1Json = comparison.Data1 != null ? JsonSerializer.Serialize(comparison.Data1) : "";
            var data2Json = comparison.Data2 != null ? JsonSerializer.Serialize(comparison.Data2) : "";
            var ratios1Json = comparison.Ratios1 != null ? JsonSerializer.Serialize(comparison.Ratios1) : "";
            var ratios2Json = comparison.Ratios2 != null ? JsonSerializer.Serialize(comparison.Ratios2) : "";
            var metricsJson = JsonSerializer.Serialize(comparison.Metrics);
            var findingsJson = JsonSerializer.Serialize(comparison.Findings);

            var stored = new StoredComparison
            {
                Doc1Id = comparison.Doc1Id,
                Doc2Id = comparison.Doc2Id,
                Doc1Name = comparison.Doc1Name,
                Doc2Name = comparison.Doc2Name,
                Data1Json = data1Json,
                Data2Json = data2Json,
                Ratios1Json = ratios1Json,
                Ratios2Json = ratios2Json,
                MetricsJson = metricsJson,
                FindingsJson = findingsJson,
                CreatedAt = DateTime.Now
            };

            ctx.Comparisons.Add(stored);
            ctx.SaveChanges();
            return stored.Id;
        }

        public List<DocumentComparison> LoadComparisons()
        {
            using var ctx = new AppDbContext(_dbPath);
            var storedList = ctx.Comparisons.AsNoTracking().ToList();
            var result = new List<DocumentComparison>();

            foreach (var stored in storedList)
            {
                var comp = new DocumentComparison
                {
                    Id = stored.Id,
                    Doc1Id = stored.Doc1Id,
                    Doc2Id = stored.Doc2Id,
                    Doc1Name = stored.Doc1Name,
                    Doc2Name = stored.Doc2Name,
                };

                if (!string.IsNullOrEmpty(stored.Data1Json))
                    comp.Data1 = JsonSerializer.Deserialize<FinancialData>(stored.Data1Json);
                if (!string.IsNullOrEmpty(stored.Data2Json))
                    comp.Data2 = JsonSerializer.Deserialize<FinancialData>(stored.Data2Json);
                if (!string.IsNullOrEmpty(stored.Ratios1Json))
                    comp.Ratios1 = JsonSerializer.Deserialize<FinancialRatios>(stored.Ratios1Json);
                if (!string.IsNullOrEmpty(stored.Ratios2Json))
                    comp.Ratios2 = JsonSerializer.Deserialize<FinancialRatios>(stored.Ratios2Json);
                if (!string.IsNullOrEmpty(stored.MetricsJson))
                    comp.Metrics = JsonSerializer.Deserialize<List<ExtractedMetric>>(stored.MetricsJson) ?? new();
                if (!string.IsNullOrEmpty(stored.FindingsJson))
                    comp.Findings = JsonSerializer.Deserialize<List<AiFinding>>(stored.FindingsJson) ?? new();

                comp.CreatedAt = stored.CreatedAt;
                result.Add(comp);
            }

            return result;
        }

        public void DeleteComparison(int comparisonId)
        {
            using var ctx = new AppDbContext(_dbPath);
            var stored = ctx.Comparisons.Find(comparisonId);
            if (stored != null)
            {
                ctx.Comparisons.Remove(stored);
                ctx.SaveChanges();
            }
        }

        public void DeleteComparisonsByDocument(int docId)
        {
            using var ctx = new AppDbContext(_dbPath);
            var toRemove = ctx.Comparisons.Where(c => c.Doc1Id == docId || c.Doc2Id == docId).ToList();
            if (toRemove.Count > 0)
            {
                ctx.Comparisons.RemoveRange(toRemove);
                ctx.SaveChanges();
            }
        }

        private void CreateChunksTableIfMissing()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                using var cmd = new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS Chunks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DocumentId INTEGER NOT NULL,
                        ChunkIndex INTEGER NOT NULL,
                        Content TEXT NOT NULL DEFAULT '',
                        Embedding TEXT NOT NULL DEFAULT ''
                    );", conn);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public void SaveChunks(int documentId, List<(int Index, string Content, string EmbeddingBase64)> chunks)
        {
            using var ctx = new AppDbContext(_dbPath);
            var existing = ctx.Chunks.Where(c => c.DocumentId == documentId).ToList();
            ctx.Chunks.RemoveRange(existing);

            foreach (var (index, content, embedding) in chunks)
            {
                ctx.Chunks.Add(new DocumentChunk
                {
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Content = content,
                    Embedding = embedding
                });
            }
            ctx.SaveChanges();
        }

        public List<DocumentChunk> LoadChunks(int documentId)
        {
            using var ctx = new AppDbContext(_dbPath);
            return ctx.Chunks
                .AsNoTracking()
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToList();
        }

        public void DeleteChunksByDocument(int documentId)
        {
            using var ctx = new AppDbContext(_dbPath);
            var chunks = ctx.Chunks.Where(c => c.DocumentId == documentId).ToList();
            if (chunks.Count > 0)
            {
                ctx.Chunks.RemoveRange(chunks);
                ctx.SaveChanges();
            }
        }

        public List<(DocumentChunk Chunk, double Score)> SearchSimilarChunks(
            float[] queryEmbedding, int excludeDocId = 0, int topK = 5)
        {
            using var ctx = new AppDbContext(_dbPath);
            var allChunks = ctx.Chunks.AsNoTracking().ToList();
            var query = queryEmbedding;
            var scored = new List<(DocumentChunk, double)>();

            foreach (var chunk in allChunks)
            {
                if (excludeDocId > 0 && chunk.DocumentId == excludeDocId) continue;
                if (string.IsNullOrEmpty(chunk.Embedding)) continue;

                try
                {
                    var emb = EmbeddingService.Denormalize(chunk.Embedding);
                    var sim = EmbeddingService.CosineSimilarity(query, emb);
                    if (sim > 0.3)
                        scored.Add((chunk, sim));
                }
                catch { }
            }

            scored.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return scored.GetRange(0, Math.Min(topK, scored.Count));
        }

        public void VacuumDatabase()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                conn.Open();
                using var cmd = new SqliteCommand("PRAGMA wal_checkpoint(TRUNCATE); VACUUM;", conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Vacuum failed: {ex.Message}");
            }
        }

        public long GetDatabaseSize()
        {
            if (File.Exists(_dbPath))
            {
                var dbSize = new FileInfo(_dbPath).Length;
                var walPath = _dbPath + "-wal";
                if (File.Exists(walPath))
                    dbSize += new FileInfo(walPath).Length;
                return dbSize;
            }
            return 0;
        }
    }
}
