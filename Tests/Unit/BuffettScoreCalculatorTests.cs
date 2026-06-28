using DStockAnalysis.Models;
using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

/// <summary>
/// バフェット採点(BuffettScoreCalculator)の単体テスト。外部アクセスなし。
/// 仕様の受け入れ条件(高品質≥80、赤字≤65、サンプル≤40、財務危険≤60、無理な高配当≤70、欠損で例外なし、金融業判定)を検証する。
/// </summary>
public class BuffettScoreCalculatorTests
{
    private readonly BuffettScoreCalculator _calc = new();

    private static Stock HighQuality() => new()
    {
        Code = "1001", Sector = "情報・通信業",
        PER = 15, PBR = 2.0, MixFactor = 30, ROE = 18, ROA = 10, EPS = 200, BPS = 1500, MarketCap = 1_000_000,
        OperatingMargin = 20, NetProfitMargin = 14, OperatingCashFlowMargin = 18,
        EquityRatio = 60, InterestBearingDebtRatio = 10,
        RevenueGrowth3Y = 7, RevenueGrowth5Y = 7, RevenueGrowth10Y = 7,
        OperatingProfitGrowthRate = 8, NetProfitGrowthRate = 8, EpsGrowthRate = 8,
        OperatingCF = 200_000, FreeCashFlow = 150_000,
        DividendYield = 2.5, PayoutRatio = 40, Dividend = 50, DividendGrowth5Y = 8,
        ConsecutiveDividendYears = 10, NonDividendCutYears = 10, DividendCutCount = 0,
        TotalYield = 2.5, BuybackAmount = 10_000,
    };

    [Fact] // 1. 高品質企業は 80点以上・A以上
    public void HighQuality_ScoresAtLeast80_GradeA()
    {
        var r = _calc.Calculate(HighQuality());
        Assert.True(r.BuffettScore >= 80, $"score={r.BuffettScore} grade={r.OverallGrade}");
        Assert.Contains(r.OverallGrade, new[] { "A", "S" });
        // 6サブスコアと信頼度が算出される
        foreach (var v in new[] { r.BusinessDurabilityScore, r.ProfitabilityScore, r.SafetyScore,
                                  r.GrowthStabilityScore, r.CapitalAllocationScore, r.ValuationScore })
            Assert.InRange(v, 0, 100);
        Assert.Equal(100, r.DataConfidence, 0);
    }

    [Fact] // 2. 赤字企業は高得点にならない(≤65)
    public void LossMaking_IsCapped()
    {
        var s = new Stock
        {
            Code = "2002", Sector = "サービス業",
            PER = -5, EPS = -10, PBR = 1.5, ROE = -8, ROA = -4, OperatingMargin = -3, NetProfitMargin = -5,
            EquityRatio = 40, OperatingCF = -5_000, FreeCashFlow = -8_000, MarketCap = 50_000,
            RevenueGrowth5Y = -3, OperatingProfitGrowthRate = -10,
        };
        var r = _calc.Calculate(s);
        Assert.True(r.BuffettScore <= 65, $"score={r.BuffettScore}");
    }

    [Fact] // 3. サンプル指標はどれだけ良くても 40点以下
    public void SampleIndicators_CappedAt40()
    {
        var s = HighQuality();
        s.IsSampleIndicators = true;
        var r = _calc.Calculate(s);
        Assert.True(r.BuffettScore <= 40, $"score={r.BuffettScore}");
    }

    [Fact] // 4. 非金融業で自己資本比率20%未満は 60点以下
    public void WeakBalanceSheet_NonFinancial_CappedAt60()
    {
        var s = new Stock
        {
            Code = "4004", Sector = "サービス業",
            PER = 12, EPS = 100, PBR = 1.5, ROE = 15, ROA = 6, OperatingMargin = 15, NetProfitMargin = 8,
            EquityRatio = 15, InterestBearingDebtRatio = 150, OperatingCF = 5_000, FreeCashFlow = 3_000,
            PayoutRatio = 40, DividendYield = 2, MarketCap = 50_000, RevenueGrowth5Y = 5, OperatingProfitGrowthRate = 5,
        };
        var r = _calc.Calculate(s);
        Assert.True(r.BuffettScore <= 60, $"score={r.BuffettScore}");
    }

    [Fact] // 5. 無理な高配当(配当性向>100% かつ 配当利回り≥4%)は 70点以下
    public void UnsustainableHighYield_CappedAt70()
    {
        var s = new Stock
        {
            Code = "5005", Sector = "サービス業",
            PER = 12, EPS = 100, PBR = 1.5, ROE = 12, ROA = 6, OperatingMargin = 12, NetProfitMargin = 7,
            EquityRatio = 50, OperatingCF = 5_000, FreeCashFlow = 3_000,
            PayoutRatio = 120, DividendYield = 5, MarketCap = 50_000, RevenueGrowth5Y = 4, OperatingProfitGrowthRate = 4,
        };
        var r = _calc.Calculate(s);
        Assert.True(r.BuffettScore <= 70, $"score={r.BuffettScore}");
    }

    [Fact] // 6. 欠損があっても例外を出さず、データ信頼度が下がる(0点扱いしない)
    public void MissingValues_NoException_LowerConfidence()
    {
        var s = new Stock { Code = "6006", PER = 12, ROE = 10 }; // ほとんど未取得
        var r = _calc.Calculate(s);
        Assert.True(r.DataConfidence < 100);
        Assert.InRange(r.BuffettScore, 0, 100); // 例外なく算出
    }

    [Fact] // 7. 金融業は金融業用の財務安全性で評価される(営業CF/有利子負債/FCFを使わない)
    public void Financial_UsesFinancialSafety()
    {
        Stock Make(string sector) => new()
        {
            Code = "8306", Sector = sector,
            EquityRatio = 5, ROE = 8, OrdinaryProfitGrowthRate = 5, NetProfitGrowthRate = 5,
            PayoutRatio = 35, DividendCutCount = 0, NonDividendCutYears = 10,
            OperatingCF = -1_000, FreeCashFlow = -2_000, PER = 10, EPS = 80, PBR = 0.6,
        };
        var bank = _calc.Calculate(Make("銀行業"));
        var nonFin = _calc.Calculate(Make("サービス業"));
        // 金融業は CF/有利子負債で減点されないため、同じ数値でも財務安全性が高くなる
        Assert.True(bank.SafetyScore > nonFin.SafetyScore, $"bank={bank.SafetyScore} nonFin={nonFin.SafetyScore}");
    }
}
