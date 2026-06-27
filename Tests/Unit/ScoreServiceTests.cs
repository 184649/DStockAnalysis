using DStockAnalysis.Models;
using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class ScoreServiceTests
{
    private readonly ScoreService _svc = new();

    [Fact] // UT-SC-01: 全スコアが 0..100 に収まる
    public void Recalculate_AllScores_InRange()
    {
        var s = TestData.Good();
        _svc.Recalculate(s);

        foreach (var v in new[] { s.SafetyScore, s.GrowthScore, s.ProfitabilityScore, s.ReturnScore,
                                  s.EfficiencyScore, s.ValuationScore, s.LongTermScore, s.RevaluationScore,
                                  s.BuffettScore, s.WantToBuyScore, s.OverallScore })
        {
            Assert.InRange(v, 0, 100);
        }
    }

    [Fact] // UT-SC-02: バフェットチェックが「はい」だとスコアが上がる
    public void BuffettScore_YesChecks_HigherThan_NoChecks()
    {
        var yes = TestData.Good();
        foreach (var setter in YesSetters(yes.BuffettCheck)) setter(YesNoUnknown.Yes);
        _svc.Recalculate(yes);

        var no = TestData.Good();
        foreach (var setter in YesSetters(no.BuffettCheck)) setter(YesNoUnknown.No);
        _svc.Recalculate(no);

        Assert.True(yes.BuffettScore > no.BuffettScore,
            $"yes={yes.BuffettScore} no={no.BuffettScore}");
    }

    [Fact] // UT-SC-03: 自己資本比率が高いほど安全性スコアが高い
    public void SafetyScore_Monotonic_WithEquityRatio()
    {
        var high = TestData.Good(); high.EquityRatio = 80; _svc.Recalculate(high);
        var low = TestData.Good(); low.EquityRatio = 25; _svc.Recalculate(low);
        Assert.True(high.SafetyScore > low.SafetyScore);
    }

    [Fact] // UT-SC-04: 優良銘柄のバフェットスコアは不振銘柄より高い
    public void BuffettScore_Good_Beats_Weak()
    {
        var good = TestData.Good(); _svc.Recalculate(good);
        var weak = TestData.Weak(); _svc.Recalculate(weak);
        Assert.True(good.BuffettScore > weak.BuffettScore);
    }

    [Fact] // UT-SC-05: FreeCF が赤字・配当性向過大だと還元スコアの優待加点が抑制される
    public void ReturnScore_BenefitBonus_Suppressed_WhenWeakCashflow()
    {
        var healthy = TestData.Good();
        healthy.BenefitYield = 3; healthy.FreeCashFlow = 120000; healthy.PayoutRatio = 40;
        _svc.Recalculate(healthy);

        var risky = TestData.Good();
        risky.BenefitYield = 3; risky.FreeCashFlow = -5000; risky.PayoutRatio = 95;
        _svc.Recalculate(risky);

        Assert.True(healthy.ReturnScore > risky.ReturnScore);
    }

    [Fact] // UT-SC-06: 除外分類なら総合判定は除外
    public void Judgement_Excluded_WhenClassificationExcluded()
    {
        var s = TestData.Good();
        s.Memo.Classification = StockClassification.除外;
        _svc.Recalculate(s);
        Assert.Equal(OverallJudgement.除外, s.Judgement);
    }

    [Fact] // UT-SC-07: 優良・高スコア銘柄は最重要候補に判定されうる
    public void Judgement_TopCandidate_ForStrongStock()
    {
        var s = TestData.Good();
        foreach (var setter in YesSetters(s.BuffettCheck)) setter(YesNoUnknown.Yes);
        _svc.Recalculate(s);
        Assert.NotEqual(OverallJudgement.除外, s.Judgement);
        Assert.True(s.OverallScore >= 60);
    }

    [Fact] // UT-SC-08: バフェットスコアは内訳の合計に一致する(透明性)
    public void BuffettScore_EqualsBreakdownSum()
    {
        var s = TestData.Good();
        _svc.Recalculate(s);
        var breakdown = _svc.BuffettBreakdown(s);
        var sum = Math.Round(Math.Clamp(breakdown.Sum(c => c.Earned), 0, 100), 0);
        Assert.Equal(s.BuffettScore, sum);
        // 各内訳は 0..配点 の範囲に収まる
        foreach (var c in breakdown) Assert.InRange(c.Earned, 0, c.Max);
        Assert.Equal(100, breakdown.Sum(c => c.Max)); // 満点合計100
    }

    [Fact] // UT-SC-09: 質ゲート — 安いだけの不良株は割安加点が抑制される(バリュートラップ回避)
    public void Buffett_Valuation_IsGatedByQuality()
    {
        // 同じ「割安」な PER/PBR でも、事業の質が低いと割安加点が小さくなる
        var cheapJunk = new Stock
        {
            Code = "9991", PER = 6, PBR = 0.5, MixFactor = 3,
            ROE = 2, ROA = 1, OperatingMargin = 1, NetProfitMargin = 1, EquityRatio = 18,
            InterestBearingDebtRatio = 180, OperatingCF = -1000, FreeCashFlow = -5000,
            RevenueGrowthRate = -8, NetProfitGrowthRate = -15, EpsGrowthRate = -15
        };
        var cheapQuality = new Stock
        {
            Code = "9992", PER = 6, PBR = 0.5, MixFactor = 3,
            ROE = 18, ROA = 10, OperatingMargin = 20, NetProfitMargin = 14, EquityRatio = 65,
            InterestBearingDebtRatio = 10, OperatingCF = 200000, FreeCashFlow = 150000,
            OperatingCashFlowMargin = 18, RevenueGrowthRate = 8, NetProfitGrowthRate = 10, EpsGrowthRate = 10
        };
        double Val(Stock s) => _svc.BuffettBreakdown(s).First(c => c.Key == "valuation").Earned;
        Assert.True(Val(cheapQuality) > Val(cheapJunk),
            $"quality={Val(cheapQuality)} junk={Val(cheapJunk)}");
        // 総合でも質の高い割安株が上回る
        _svc.Recalculate(cheapJunk); _svc.Recalculate(cheapQuality);
        Assert.True(cheapQuality.BuffettScore > cheapJunk.BuffettScore);
    }

    [Fact] // UT-SC-10: 同じ ROE でも自己資本比率が低い(借入依存)と資本収益力が下がる
    public void Buffett_Capital_PenalizesLeveragedRoe()
    {
        var sound = new Stock { Code = "1", ROE = 15, ROA = 8, EquityRatio = 60 };
        var levered = new Stock { Code = "2", ROE = 15, ROA = 4, EquityRatio = 20 };
        double Cap(Stock s) => _svc.BuffettBreakdown(s).First(c => c.Key == "capital").Earned;
        Assert.True(Cap(sound) > Cap(levered), $"sound={Cap(sound)} levered={Cap(levered)}");
    }

    private static List<Action<YesNoUnknown>> YesSetters(BuffettCheck b) => new()
    {
        v => b.CanExplainEarnings = v, v => b.UnderstandBusiness = v, v => b.DemandIn10Years = v,
        v => b.HasCompetitiveAdvantage = v, v => b.HasEntryBarrier = v, v => b.HighMargin = v,
        v => b.StableHighRoe = v, v => b.StablePositiveOperatingCf = v, v => b.StablePositiveFreeCf = v,
        v => b.SoundFinance = v, v => b.SustainableReturn = v, v => b.TrustManagement = v,
        v => b.NotOverpriced = v, v => b.WantToBuyOnCrash = v, v => b.CanWrite10YearReason = v,
    };
}
