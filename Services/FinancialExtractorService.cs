using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Diplom_1.Models;

namespace Diplom_1.Services
{
    public class FinancialExtractorService
    {
        private static readonly Dictionary<string, string> MetricPatterns = new()
        {
            { "revenue_ru", @"(?:выручка|доход|объ[её]м\s+продаж)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "revenue_en", @"(?:revenue|sales)\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
            { "profit_ru", @"(?:чистая\s+прибыль|чистый\s+доход)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "profit_en", @"(?:net\s+(?:income|profit))\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
            { "assets_ru", @"(?:активы|итого\s+активов|все\s+активы)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "assets_en", @"(?:total\s+assets)\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
            { "liabilities_ru", @"(?:обязательства|итого\s+обязательств)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "liabilities_en", @"(?:total\s+liabilities)\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
            { "equity_ru", @"(?:капитал|собственный\s+капитал|акционерный\s+капитал)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "equity_en", @"(?:total\s+equity|shareholders?\s+equity)\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
            { "opex_ru", @"(?:операционные\s+расходы|операционные\s+затраты|OPEX)\s*[:\-]?\s*([\d\s]+(?:\.\d+)?)\s*(?:млрд|млн|тыс)?\s*(?:руб|₽|RUB)?" },
            { "opex_en", @"(?:operating\s+expenses)\s*[:\-]?\s*\$?\s*([\d,]+(?:\.\d+)?)\s*(?:billion|million|thousand)?" },
        };

        public FinancialData ExtractAsFinancialData(string text)
        {
            var data = new FinancialData();

            foreach (var (key, pattern) in MetricPatterns)
            {
                var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        var rawValue = match.Groups[1].Value.Replace(" ", "").Replace(",", ".");
                        if (decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value))
                        {
                            var absoluteValue = value * GetMultiplier(match.Value);

                            if (!data.Revenue.HasValue && key is "revenue_ru" or "revenue_en")
                            { data.Revenue = absoluteValue; data.SourceFields.Add("revenue"); }
                            else if (!data.NetIncome.HasValue && key is "profit_ru" or "profit_en")
                            { data.NetIncome = absoluteValue; data.SourceFields.Add("net_income"); }
                            else if (!data.Assets.HasValue && key is "assets_ru" or "assets_en")
                            { data.Assets = absoluteValue; data.SourceFields.Add("assets"); }
                            else if (!data.Liabilities.HasValue && key is "liabilities_ru" or "liabilities_en")
                            { data.Liabilities = absoluteValue; data.SourceFields.Add("liabilities"); }
                            else if (!data.Equity.HasValue && key is "equity_ru" or "equity_en")
                            { data.Equity = absoluteValue; data.SourceFields.Add("equity"); }
                            else if (!data.OperatingExpenses.HasValue && key is "opex_ru" or "opex_en")
                            { data.OperatingExpenses = absoluteValue; data.SourceFields.Add("operating_expenses"); }
                        }
                    }
                }
            }

            return data;
        }

        public static FinancialData MergeFinancialData(FinancialData llmData, FinancialData regexData)
        {
            var merged = new FinancialData
            {
                Revenue = llmData.Revenue ?? regexData.Revenue,
                NetIncome = llmData.NetIncome ?? regexData.NetIncome,
                Assets = llmData.Assets ?? regexData.Assets,
                Liabilities = llmData.Liabilities ?? regexData.Liabilities,
                Equity = llmData.Equity ?? regexData.Equity,
                NetInterestIncome = llmData.NetInterestIncome ?? regexData.NetInterestIncome,
                FeeIncome = llmData.FeeIncome ?? regexData.FeeIncome,
                OperatingExpenses = llmData.OperatingExpenses ?? regexData.OperatingExpenses
            };

            if (merged.Revenue.HasValue) merged.SourceFields.Add("revenue");
            if (merged.NetIncome.HasValue) merged.SourceFields.Add("net_income");
            if (merged.Assets.HasValue) merged.SourceFields.Add("assets");
            if (merged.Liabilities.HasValue) merged.SourceFields.Add("liabilities");
            if (merged.Equity.HasValue) merged.SourceFields.Add("equity");
            if (merged.NetInterestIncome.HasValue) merged.SourceFields.Add("net_interest_income");
            if (merged.FeeIncome.HasValue) merged.SourceFields.Add("fee_income");
            if (merged.OperatingExpenses.HasValue) merged.SourceFields.Add("operating_expenses");

            return merged;
        }

        private static decimal GetMultiplier(string text)
        {
            if (Regex.IsMatch(text, @"млрд|billion", RegexOptions.IgnoreCase)) return 1_000_000_000m;
            if (Regex.IsMatch(text, @"млн|million", RegexOptions.IgnoreCase)) return 1_000_000m;
            if (Regex.IsMatch(text, @"тыс|thousand", RegexOptions.IgnoreCase)) return 1_000m;
            return 1m;
        }
    }
}
