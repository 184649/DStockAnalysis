using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// JPX 上場銘柄一覧にはコード/銘柄名/市場/業種/規模しか含まれないため、
/// 各銘柄に「もっともらしい擬似(サンプル)指標値」を決定論的に生成する。
/// 実データは CSV 取込(将来は API)で上書きする想定で、IsSampleIndicators=true を立てる。
/// ハッシュは実行ごとに変わらない安定ハッシュ(FNV-1a)を使用し、再起動しても同じ値になる。
/// </summary>
public class IndicatorSeedService
{
    /// <summary>銘柄属性から擬似指標を生成して設定する。</summary>
    public void FillIndicators(Stock s)
    {
        var rnd = new Random(StableSeed(s.Code));
        double R(double lo, double hi) => Math.Round(lo + rnd.NextDouble() * (hi - lo), 2);

        // バリュエーション
        s.Price = Math.Round(R(150, 8000));
        s.PER = R(5, 38);
        s.PBR = R(0.4, 5.0);
        s.ROE = R(-3, 24);
        s.MixFactor = Math.Round(s.PER * s.PBR, 1);
        s.EPS = Math.Round(s.PER > 0 ? s.Price / s.PER : 0, 1);
        s.BPS = Math.Round(s.PBR > 0 ? s.Price / s.PBR : 0);
        s.OperatingMargin = R(1, 28);
        s.OrdinaryProfitMargin = Math.Round(s.OperatingMargin * R(0.95, 1.08), 1);
        s.NetProfitMargin = Math.Round(s.OperatingMargin * R(0.45, 0.8), 1);

        // 規模(時価総額)。規模区分でレンジを変える。
        s.MarketCap = s.Scale switch
        {
            "大型" => Math.Round(R(500000, 15000000)),
            "中型" => Math.Round(R(100000, 800000)),
            _ => Math.Round(R(3000, 150000))
        };

        // 財務
        s.EquityRatio = R(18, 85);
        s.InterestBearingDebtRatio = R(0, 120);

        // 配当・還元
        s.DividendYield = R(0, 6);
        s.PayoutRatio = s.DividendYield > 0 ? R(10, 95) : 0;
        s.Dividend = Math.Round(s.EPS * s.PayoutRatio / 100, 1);
        s.ConsecutiveDividendYears = rnd.NextDouble() < 0.25 ? rnd.Next(3, 26) : rnd.Next(0, 3);
        s.DividendCutCount = rnd.NextDouble() < 0.2 ? rnd.Next(1, 4) : 0;
        s.NonDividendCutYears = s.ConsecutiveDividendYears > 0 ? s.ConsecutiveDividendYears + rnd.Next(0, 5) : rnd.Next(0, 8);
        s.CumulativeDividend = s.ConsecutiveDividendYears >= 5 && rnd.NextDouble() < 0.6;
        s.DoeAdopted = rnd.NextDouble() < 0.12;
        s.BuybackAmount = rnd.NextDouble() < 0.3 ? Math.Round(s.MarketCap * R(0.005, 0.03)) : 0;
        s.ShareholderReturnPolicy = s.CumulativeDividend ? "累進配当" : (s.PayoutRatio > 0 ? "配当性向目安" : "成長投資優先");
        s.DividendTrend = s.ConsecutiveDividendYears >= 5 ? "連続増配" : (s.DividendCutCount > 0 ? "減配実績あり" : "安定");
        s.DividendRemainingYears = R(0, 15);
        s.DividendGrowth1Y = s.ConsecutiveDividendYears > 0 ? R(0, 15) : 0;
        s.DividendGrowth3Y = s.ConsecutiveDividendYears > 0 ? R(0, 20) : 0;
        s.DividendGrowth5Y = s.ConsecutiveDividendYears > 0 ? R(0, 30) : 0;
        s.DividendGrowth10Y = s.ConsecutiveDividendYears > 0 ? R(0, 50) : 0;

        // 成長性
        s.RevenueGrowth1Y = R(-10, 25);
        s.RevenueGrowth3Y = R(-8, 22);
        s.RevenueGrowth5Y = R(-6, 20);
        s.RevenueGrowth10Y = R(-5, 18);
        s.RevenueGrowthRate = s.RevenueGrowth1Y;
        s.AverageRevenueGrowth3Y = s.RevenueGrowth3Y;
        s.OperatingProfitGrowthRate = R(-15, 35);
        s.OrdinaryProfitGrowthRate = R(-15, 32);
        s.NetProfitGrowthRate = R(-15, 35);
        s.EpsGrowthRate = R(-15, 35);

        // キャッシュフロー
        s.OperatingCF = Math.Round(s.MarketCap * R(0.02, 0.12) * (s.OperatingMargin > 0 ? 1 : -0.5));
        s.InvestingCF = -Math.Round(Math.Abs(s.OperatingCF) * R(0.2, 0.8));
        s.FinancingCF = -Math.Round(Math.Abs(s.OperatingCF) * R(0.1, 0.5));
        s.FreeCashFlow = s.OperatingCF + s.InvestingCF;
        s.OperatingCashFlowMargin = R(-3, 25);

        // 株価変化
        s.StockPriceChange3M = R(-25, 30);
        s.AverageStockPriceChange3M = R(-20, 25);
        s.AveragePrice3M = Math.Round(s.Price * R(0.9, 1.05));
        s.PriceChange3M = s.StockPriceChange3M;
        s.PriceChangeAverage3M = s.AverageStockPriceChange3M;

        // 株主優待(約30%)
        if (rnd.NextDouble() < 0.30)
        {
            s.HasShareholderBenefit = true;
            var cats = new[] { "QUOカード", "カタログギフト", "食品", "飲料", "外食", "買物券", "自社商品", "金券", "ポイント", "プレミアム優待倶楽部" };
            s.BenefitCategory = cats[rnd.Next(cats.Length)];
            s.ShareholderBenefit = s.BenefitCategory;
            s.RequiredSharesForBenefit = 100;
            s.BenefitValue = Math.Round(R(500, 5000));
            s.BenefitYield = Math.Round(s.Price > 0 ? s.BenefitValue / (s.Price * 100) * 100 : 0, 2);
            s.BenefitRightsMonth = new[] { "3月", "9月", "12月", "2月・8月", "3月・9月" }[rnd.Next(5)];
            s.HasLongTermBenefit = rnd.NextDouble() < 0.4;
            s.LongTermBenefitCondition = s.HasLongTermBenefit ? "1年以上" : "";
            s.LongTermBenefitContent = s.HasLongTermBenefit ? "長期保有で増額" : "";
            s.BenefitContent = $"{s.BenefitCategory}（約{s.BenefitValue:N0}円相当 / {s.RequiredSharesForBenefit}株以上）";
        }
        s.TotalYield = Math.Round(s.DividendYield + s.BenefitYield, 2);

        s.IsSampleIndicators = true;
    }

    /// <summary>選択時に遅延生成する10年分の時系列(チャート用)。</summary>
    public List<TimeSeriesPoint> BuildHistory(Stock s)
    {
        var hist = new List<TimeSeriesPoint>();
        int startYear = DateTime.Today.Year - 9;
        double baseRevenue = s.MarketCap > 0 ? s.MarketCap / 1.2 : 100000;
        double g = Math.Max(-0.05, s.RevenueGrowth3Y / 100.0);
        double rev0 = baseRevenue / Math.Pow(1 + g, 9);
        var rnd = new Random(StableSeed(s.Code) ^ 0x5f3759df);

        for (int i = 0; i < 10; i++)
        {
            double noise = 1 + (rnd.NextDouble() - 0.5) * 0.06;
            double rev = rev0 * Math.Pow(1 + g, i) * noise;
            double op = rev * (s.OperatingMargin / 100.0);
            double ni = rev * (s.NetProfitMargin / 100.0);
            double eps = s.EPS > 0 ? s.EPS * Math.Pow(1 + g, i - 9) : 0;
            double div = s.Dividend > 0 ? s.Dividend * Math.Pow(1.05, i - 9) : eps * (s.PayoutRatio / 100.0);
            double payout = eps > 0 ? Math.Round(div / eps * 100, 1) : s.PayoutRatio;
            double assets = rev * (s.EquityRatio / 100.0);
            double liab = rev * (1 - s.EquityRatio / 100.0) * 0.8;
            double ocf = op * 1.2 + ni * 0.3;
            double icf = -op * 0.5 * noise;
            double fcf = ocf + icf;
            double fin = -(div * 0.4) - (s.BuybackAmount > 0 ? s.BuybackAmount / 10 : 0);

            hist.Add(new TimeSeriesPoint
            {
                FiscalYear = startYear + i,
                Revenue = Math.Round(rev),
                OperatingProfit = Math.Round(op),
                NetIncome = Math.Round(ni),
                EPS = Math.Round(eps, 1),
                Dividend = Math.Round(div, 1),
                PayoutRatio = payout,
                NetAssets = Math.Round(assets),
                Liabilities = Math.Round(liab),
                EquityRatio = s.EquityRatio,
                OperatingCF = Math.Round(ocf),
                InvestingCF = Math.Round(icf),
                FinancingCF = Math.Round(fin),
                FreeCF = Math.Round(fcf),
                BuybackAmount = s.BuybackAmount > 0 ? Math.Round(s.BuybackAmount / 10 * noise) : 0
            });
        }
        return hist;
    }

    /// <summary>実行間で安定する FNV-1a 32bit ハッシュ。</summary>
    private static int StableSeed(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in s ?? "")
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
