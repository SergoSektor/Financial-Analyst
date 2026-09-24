namespace Diplom_1.Models
{
    public enum RatioStatus
    {
        Good,
        Normal,
        Bad
    }

    public class FinancialRatios
    {
        public decimal? ROE { get; set; }
        public decimal? ROA { get; set; }
        public decimal? DebtToEquity { get; set; }
        public decimal? NIM { get; set; }
        public decimal? CostToIncome { get; set; }

        public RatioStatus RoeStatus => GetStatus(ROE, 15m, 8m, invert: false);
        public RatioStatus RoaStatus => GetStatus(ROA, 5m, 2m, invert: false);
        public RatioStatus DebtEquityStatus => GetStatus(DebtToEquity, 1m, 2m, invert: true);
        public RatioStatus NimStatus => GetStatus(NIM, 3m, 1.5m, invert: false);
        public RatioStatus CostIncomeStatus => GetStatus(CostToIncome, 50m, 70m, invert: true);

        private RatioStatus GetStatus(decimal? value, decimal goodThreshold, decimal normalThreshold, bool invert)
        {
            if (!value.HasValue) return RatioStatus.Normal;
            var v = value.Value;

            if (invert)
            {
                if (v < goodThreshold) return RatioStatus.Good;
                if (v < normalThreshold) return RatioStatus.Normal;
                return RatioStatus.Bad;
            }

            if (v > goodThreshold) return RatioStatus.Good;
            if (v > normalThreshold) return RatioStatus.Normal;
            return RatioStatus.Bad;
        }
    }
}
