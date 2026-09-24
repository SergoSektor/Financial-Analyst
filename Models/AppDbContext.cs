using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalyst.Models
{
    public class AppDbContext : DbContext
    {
        public DbSet<StoredDocument> Documents { get; set; } = null!;
        public DbSet<StoredAnalysis> Analyses { get; set; } = null!;
        public DbSet<StoredComparison> Comparisons { get; set; } = null!;
        public DbSet<DocumentChunk> Chunks { get; set; } = null!;

        public string DbPath { get; }

        public AppDbContext(string dbPath)
        {
            DbPath = dbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }
    }

    public class StoredDocument
    {
        [Key]
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime DateAdded { get; set; }
        public string Status { get; set; } = "NotProcessed";
    }

    public class StoredAnalysis
    {
        [Key]
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public string FinancialDataJson { get; set; } = string.Empty;
        public string FindingsJson { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
    }

    public class DocumentChunk
    {
        [Key]
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Embedding { get; set; } = string.Empty;
    }

    public class StoredComparison
    {
        [Key]
        public int Id { get; set; }
        public int Doc1Id { get; set; }
        public int Doc2Id { get; set; }
        public string Doc1Name { get; set; } = string.Empty;
        public string Doc2Name { get; set; } = string.Empty;
        public string Data1Json { get; set; } = string.Empty;
        public string Data2Json { get; set; } = string.Empty;
        public string Ratios1Json { get; set; } = string.Empty;
        public string Ratios2Json { get; set; } = string.Empty;
        public string MetricsJson { get; set; } = string.Empty;
        public string FindingsJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
