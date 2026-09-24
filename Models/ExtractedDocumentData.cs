using System;
using System.Collections.Generic;

namespace FinancialAnalyst.Models
{
    public class ExtractedMetric
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
    }

    public enum FindingType
    {
        Problem,
        Risk,
        Positive,
        Recommendation,
        Observation
    }

    public class AiFinding
    {
        public FindingType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
    }

    public class ExtractedDocumentData
    {
        public List<ExtractedMetric> Metrics { get; set; } = new();
        public List<AiFinding> Findings { get; set; } = new();
        public string DocumentTitle { get; set; } = string.Empty;
        public string DocumentPeriod { get; set; } = string.Empty;
    }
}
