using System.Globalization;
using System.IO;
using System.Text;
using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// CSV から銘柄データを取り込む。ヘッダー行の列名でマッピングするため列順は不問。
/// 存在しない列はエラーにせず、空欄(0/"")として扱う。
/// </summary>
public class CsvImportService
{
    public List<Stock> ImportFromFile(string path)
    {
        using var reader = new StreamReader(path, DetectEncoding(path));
        return Parse(reader.ReadToEnd());
    }

    public List<Stock> Parse(string content)
    {
        var rows = ParseCsv(content);
        var result = new List<Stock>();
        if (rows.Count == 0) return result;

        // ヘッダー -> 列インデックス(大文字小文字無視)
        var header = rows[0];
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++)
        {
            var key = header[i].Trim();
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = i;
        }

        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace)) continue;

            string S(string col) => map.TryGetValue(col, out var idx) && idx < row.Count ? row[idx].Trim() : "";
            double D(string col) => ParseDouble(S(col));
            int I(string col) => (int)Math.Round(ParseDouble(S(col)));
            bool B(string col) => ParseBool(S(col));

            var code = S("Code");
            if (string.IsNullOrWhiteSpace(code)) continue;

            var s = new Stock
            {
                // 基本情報
                Code = code,
                Name = S("Name"),
                Market = S("Market"),
                Sector = S("Sector"),
                Scale = S("Scale"),
                Theme = S("Theme"),
                Description = S("Description"),
                FiscalMonth = S("FiscalMonth"),
                IRUrl = S("IRUrl"),
                DataUpdated = ParseDate(S("DataUpdated")),

                // バリュエーション
                Price = D("Price"),
                MarketCap = D("MarketCap"),
                PER = D("PER"),
                PBR = D("PBR"),
                ROE = D("ROE"),
                MixFactor = D("MixFactor"),
                EPS = D("EPS"),
                BPS = D("BPS"),
                OperatingMargin = D("OperatingMargin"),
                OrdinaryProfitMargin = D("OrdinaryProfitMargin"),
                NetProfitMargin = D("NetProfitMargin"),

                // 配当・株主還元
                DividendYield = D("DividendYield"),
                PayoutRatio = D("PayoutRatio"),
                Dividend = D("Dividend"),
                DividendTrend = S("DividendTrend"),
                CumulativeDividend = B("CumulativeDividend"),
                DoeAdopted = B("DoeAdopted"),
                ConsecutiveDividendYears = I("ConsecutiveDividendYears"),
                DividendCutCount = I("DividendCutCount"),
                NonDividendCutYears = I("NonDividendCutYears"),
                DividendRemainingYears = D("DividendRemainingYears"),
                BuybackAmount = D("BuybackAmount"),
                ShareholderReturnPolicy = S("ShareholderReturnPolicy"),
                DividendGrowth1Y = D("DividendGrowth1Y"),
                DividendGrowth3Y = D("DividendGrowth3Y"),
                DividendGrowth5Y = D("DividendGrowth5Y"),
                DividendGrowth10Y = D("DividendGrowth10Y"),

                // 株主優待
                HasShareholderBenefit = map.ContainsKey("HasShareholderBenefit")
                    ? B("HasShareholderBenefit")
                    : !string.IsNullOrWhiteSpace(S("ShareholderBenefit")) || !string.IsNullOrWhiteSpace(S("BenefitContent")),
                ShareholderBenefit = S("ShareholderBenefit"),
                BenefitContent = S("BenefitContent"),
                BenefitCategory = S("BenefitCategory"),
                BenefitRightsMonth = S("BenefitRightsMonth"),
                RequiredSharesForBenefit = I("RequiredSharesForBenefit"),
                BenefitValue = D("BenefitValue"),
                BenefitYield = D("BenefitYield"),
                TotalYield = D("TotalYield"),
                HasLongTermBenefit = B("HasLongTermBenefit"),
                LongTermBenefitCondition = S("LongTermBenefitCondition"),
                LongTermBenefitContent = S("LongTermBenefitContent"),
                BenefitRiskMemo = S("BenefitRiskMemo"),

                // 財務
                EquityRatio = D("EquityRatio"),
                InterestBearingDebtRatio = D("InterestBearingDebtRatio"),

                // 成長性
                RevenueGrowth1Y = D("RevenueGrowth1Y"),
                RevenueGrowth3Y = D("RevenueGrowth3Y"),
                RevenueGrowth5Y = D("RevenueGrowth5Y"),
                RevenueGrowth10Y = D("RevenueGrowth10Y"),
                RevenueGrowthRate = D("RevenueGrowthRate"),
                AverageRevenueGrowth3Y = D("AverageRevenueGrowth3Y"),
                OperatingProfitGrowthRate = D("OperatingProfitGrowthRate"),
                OrdinaryProfitGrowthRate = D("OrdinaryProfitGrowthRate"),
                NetProfitGrowthRate = D("NetProfitGrowthRate"),
                EpsGrowthRate = D("EpsGrowthRate"),

                // キャッシュフロー
                OperatingCF = D("OperatingCF"),
                InvestingCF = D("InvestingCF"),
                FinancingCF = D("FinancingCF"),
                FreeCashFlow = map.ContainsKey("FreeCashFlow") ? D("FreeCashFlow") : D("FreeCF"),
                OperatingCashFlowMargin = D("OperatingCashFlowMargin"),

                // 株価変化
                StockPriceChange3M = D("StockPriceChange3M"),
                AverageStockPriceChange3M = D("AverageStockPriceChange3M"),
                AveragePrice3M = map.ContainsKey("AveragePrice3M") ? D("AveragePrice3M") : D("AverageStockPrice3M"),
                PriceChange3M = D("PriceChange3M"),
                PriceChangeAverage3M = D("PriceChangeAverage3M"),
            };

            // CSV にスコアが入っていれば取り込む(後で再計算で上書き可能)
            s.BuffettScore = D("BuffettScore");
            s.SafetyScore = D("SafetyScore");
            s.GrowthScore = D("GrowthScore");
            s.ProfitabilityScore = D("ProfitabilityScore");
            s.ReturnScore = D("ReturnScore");
            s.EfficiencyScore = D("EfficiencyScore");
            s.ValuationScore = D("ValuationScore");

            // 総合利回り未指定なら配当+優待で補完
            if (s.TotalYield == 0 && (s.DividendYield != 0 || s.BenefitYield != 0))
                s.TotalYield = Math.Round(s.DividendYield + s.BenefitYield, 2);

            // MIX係数未指定なら PER×PBR で補完
            if (s.MixFactor == 0 && s.PER > 0 && s.PBR > 0)
                s.MixFactor = Math.Round(s.PER * s.PBR, 1);

            result.Add(s);
        }

        return result;
    }

    private static Encoding DetectEncoding(string path)
    {
        // BOM があれば自動判定。なければ UTF-8。
        using var fs = File.OpenRead(path);
        var bom = new byte[3];
        int read = fs.Read(bom, 0, 3);
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true);
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        return new UTF8Encoding(false);
    }

    private static double ParseDouble(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        var cleaned = raw.Replace(",", "").Replace("%", "").Replace("円", "").Replace("倍", "").Trim();
        if (cleaned is "-" or "－" or "N/A" or "n/a" or "ー") return 0;
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static bool ParseBool(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim().ToLowerInvariant();
        return t is "1" or "true" or "yes" or "y" or "○" or "◯" or "あり" or "採用" or "◎";
    }

    private static DateTime ParseDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DateTime.Today;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Today;
    }

    /// <summary>RFC4180 風の CSV パーサ(ダブルクォート対応)。</summary>
    private static List<List<string>> ParseCsv(string content)
    {
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(field.ToString()); field.Clear();
                        rows.Add(row); row = new List<string>();
                        break;
                    default: field.Append(c); break;
                }
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
