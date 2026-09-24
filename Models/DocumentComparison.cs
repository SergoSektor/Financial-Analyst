using System;
using System.Collections.Generic;

namespace FinancialAnalyst.Models
{
    public class DocumentComparison
    {
        public int Id { get; set; }
        public int Doc1Id { get; set; }
        public int Doc2Id { get; set; }
        public string Doc1Name { get; set; } = string.Empty;
        public string Doc2Name { get; set; } = string.Empty;
        public FinancialData? Data1 { get; set; }
        public FinancialData? Data2 { get; set; }
        public FinancialRatios? Ratios1 { get; set; }
        public FinancialRatios? Ratios2 { get; set; }
        public List<ExtractedMetric> Metrics { get; set; } = new();
        public List<AiFinding> Findings { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Label => $"{Doc1Name} vs {Doc2Name}";
    }
}
