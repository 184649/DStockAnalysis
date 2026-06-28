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

    private static List<Action<YesNoUnknown>> YesSetters(BuffettCheck b) => new()
    {
        v => b.CanExplainEarnings = v, v => b.UnderstandBusiness = v, v => b.DemandIn10Years = v,
        v => b.HasCompetitiveAdvantage = v, v => b.HasEntryBarrier = v, v => b.HighMargin = v,
        v => b.StableHighRoe = v, v => b.StablePositiveOperatingCf = v, v => b.StablePositiveFreeCf = v,
        v => b.SoundFinance = v, v => b.SustainableReturn = v, v => b.TrustManagement = v,
        v => b.NotOverpriced = v, v => b.WantToBuyOnCrash = v, v => b.CanWrite10YearReason = v,
    };
}
