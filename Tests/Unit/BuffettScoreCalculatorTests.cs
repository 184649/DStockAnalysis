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

    // 伊藤忠相当(8001をハードコードしない。卸売業の総合商社プロファイル)
    private static Stock TradingHouse() => new()
    {
        Code = "T001", Name = "テスト総合商社", Market = "東証プライム", Sector = "卸売業",
        PER = 13.5, PBR = 1.94, MixFactor = 26.2, ROE = 14.59, ROA = 5.65, EPS = 135, BPS = 945, MarketCap = 14_529_500,
        OperatingMargin = 4.7, OrdinaryProfitMargin = 8.1, NetProfitMargin = 6.1, OperatingCashFlowMargin = 7.6,
        EquityRatio = 39.4, InterestBearingDebtRatio = 72,
        RevenueGrowth1Y = 0.7, RevenueGrowth3Y = 2.1, OperatingProfitGrowthRate = 2.6, OrdinaryProfitGrowthRate = 3.8,
        NetProfitGrowthRate = 2.3, EpsGrowthRate = 4.0,
        OperatingCF = 1_131_837, FreeCashFlow = 742_965, InvestingCF = -388_872, FinancingCF = -726_477,
        DividendYield = 2.4, PayoutRatio = 32.4, Dividend = 44, ConsecutiveDividendYears = 3, NonDividendCutYears = 3,
        DividendCutCount = 0, DividendGrowth1Y = 5, DividendGrowth3Y = 14.5, TotalYield = 2.4, BuybackAmount = 170_057,
    };

    [Fact] // 8. 総合商社(卸売業)は薄利でも適正評価(B以上・各サブ基準クリア)。8001のハードコードなし。
    public void TradingHouse_NotUndervalued()
    {
        var r = _calc.Calculate(TradingHouse());
        Assert.Equal("TradingCompany", r.Profile);
        Assert.True(r.BuffettScore >= 70, $"score={r.BuffettScore}");
        Assert.Contains(r.OverallGrade, new[] { "B", "A", "S" });
        Assert.True(r.BusinessDurabilityScore >= 60, $"biz={r.BusinessDurabilityScore}");
        Assert.True(r.ProfitabilityScore >= 60, $"prof={r.ProfitabilityScore}");
        Assert.True(r.SafetyScore >= 60, $"safe={r.SafetyScore}");
        Assert.True(r.GrowthStabilityScore >= 50, $"growth={r.GrowthStabilityScore}");
        Assert.True(r.CapitalAllocationScore >= 70, $"capital={r.CapitalAllocationScore}");
        Assert.True(r.ValuationScore >= 60, $"val={r.ValuationScore}");
    }

    [Fact] // 9. 通常企業への悪影響なし(高品質StandardはA以上のまま)
    public void Standard_Profile_Unaffected()
    {
        var r = _calc.Calculate(HighQuality());
        Assert.Equal("StandardCompany", r.Profile);
        Assert.True(r.BuffettScore >= 80);
    }

    [Fact] // 10. 商社でも赤字・債務超過寸前は上限が効く(下限補正されない)
    public void TradingHouse_LossMaking_StillCapped()
    {
        var s = TradingHouse();
        s.PER = -3; s.EPS = -10; s.ROE = -5; s.OperatingCF = -1000; s.FreeCashFlow = -2000; s.EquityRatio = 12;
        var r = _calc.Calculate(s);
        Assert.True(r.BuffettScore <= 65, $"score={r.BuffettScore}");
    }

    [Fact] // 11. 商社で成長率が一部欠損でも例外なし・0点扱いにしない(重み再配分)
    public void TradingHouse_MissingGrowth_NoExceptionNoZero()
    {
        var s = TradingHouse();
        s.EpsGrowthRate = 0; s.NetProfitGrowthRate = 0; s.OrdinaryProfitGrowthRate = 0; // 成長率欠損
        var r = _calc.Calculate(s);
        Assert.InRange(r.GrowthStabilityScore, 1, 100); // 0点ではなく残った指標で算出
        Assert.True(r.BuffettScore >= 70); // 依然として下限補正でB以上
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
