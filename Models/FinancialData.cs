using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FinancialAnalyst.Models
{
    public class FinancialData
    {
        [JsonPropertyName("revenue")]
        public decimal? Revenue { get; set; }

        [JsonPropertyName("net_income")]
        public decimal? NetIncome { get; set; }

        [JsonPropertyName("assets")]
        public decimal? Assets { get; set; }

        [JsonPropertyName("liabilities")]
        public decimal? Liabilities { get; set; }

        [JsonPropertyName("equity")]
        public decimal? Equity { get; set; }

        [JsonPropertyName("net_interest_income")]
        public decimal? NetInterestIncome { get; set; }

        [JsonPropertyName("fee_income")]
        public decimal? FeeIncome { get; set; }

        [JsonPropertyName("operating_expenses")]
        public decimal? OperatingExpenses { get; set; }

        public string Currency { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        public List<string> SourceFields { get; set; } = new();

        public bool HasAnyData => Revenue.HasValue || NetIncome.HasValue || Assets.HasValue
            || Liabilities.HasValue || Equity.HasValue || NetInterestIncome.HasValue
            || FeeIncome.HasValue || OperatingExpenses.HasValue;

        public List<ExtractedMetric> ToMetrics()
        {
            var metrics = new List<ExtractedMetric>();

            if (Revenue.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Выручка", Value = FormatValue(Revenue.Value), Category = "Доходы" });
            if (NetIncome.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Чистая прибыль", Value = FormatValue(NetIncome.Value), Category = "Прибыль" });
            if (Assets.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Всего активы", Value = FormatValue(Assets.Value), Category = "Баланс" });
            if (Liabilities.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Всего обязательства", Value = FormatValue(Liabilities.Value), Category = "Баланс" });
            if (Equity.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Капитал", Value = FormatValue(Equity.Value), Category = "Баланс" });
            if (NetInterestIncome.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Чистый процентный доход", Value = FormatValue(NetInterestIncome.Value), Category = "Доходы" });
            if (FeeIncome.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Комиссионный доход", Value = FormatValue(FeeIncome.Value), Category = "Доходы" });
            if (OperatingExpenses.HasValue)
                metrics.Add(new ExtractedMetric { Label = "Операционные расходы", Value = FormatValue(OperatingExpenses.Value), Category = "Расходы" });

            return metrics;
        }

        private string FormatValue(decimal value)
        {
            if (value >= 1_000_000_000_000) return $"{value / 1_000_000_000_000:F2} трлн";
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000:F2} млрд";
            if (value >= 1_000_000) return $"{value / 1_000_000:F2} млн";
            if (value >= 1_000) return $"{value / 1_000:F2} тыс";
            return value.ToString("N2");
        }
    }
}
