using Diplom_1.Models;

namespace Diplom_1.Services
{
    public class FinancialCalculatorService
    {
        public FinancialRatios CalculateRatios(FinancialData data)
        {
            var ratios = new FinancialRatios();

            if (data.NetIncome.HasValue && data.Equity.HasValue && data.Equity.Value != 0)
                ratios.ROE = (data.NetIncome.Value / data.Equity.Value) * 100m;

            if (data.NetIncome.HasValue && data.Assets.HasValue && data.Assets.Value != 0)
                ratios.ROA = (data.NetIncome.Value / data.Assets.Value) * 100m;

            if (data.Liabilities.HasValue && data.Equity.HasValue && data.Equity.Value != 0)
                ratios.DebtToEquity = data.Liabilities.Value / data.Equity.Value;

            if (data.NetInterestIncome.HasValue && data.Assets.HasValue && data.Assets.Value != 0)
                ratios.NIM = (data.NetInterestIncome.Value / data.Assets.Value) * 100m;

            if (data.OperatingExpenses.HasValue && data.Revenue.HasValue && data.Revenue.Value != 0)
                ratios.CostToIncome = (data.OperatingExpenses.Value / data.Revenue.Value) * 100m;

            return ratios;
        }
    }
}
