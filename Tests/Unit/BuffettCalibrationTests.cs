using DStockAnalysis.Models;
using DStockAnalysis.Services;
using Xunit;
using Xunit.Abstractions;

namespace DStockAnalysis.Tests.Unit;

/// <summary>
/// 教師データ付き重み最適化(BuffettScoreWeightOptimizer / CalibrationSet)の検証。外部アクセスなし。
/// </summary>
public class BuffettCalibrationTests
{
    private readonly ITestOutputHelper _out;
    private readonly BuffettScoreCalculator _calc = new();
    public BuffettCalibrationTests(ITestOutputHelper o) => _out = o;

    private static BuffettResult Score(Stock s) => new BuffettScoreCalculator().Calculate(s);

    [Fact] // テスト1: 教師データ数とカテゴリ分散
    public void CalibrationSet_HasEnoughSamplesAcrossCategories()
    {
        var all = BuffettScoreCalibrationSet.All;
        Assert.True(all.Count >= 30, $"count={all.Count}");
        var cats = all.GroupBy(x => x.Category).ToList();
        Assert.True(cats.Count >= 8, $"categories={cats.Count}");
        Assert.True(cats.All(g => g.Count() <= all.Count / 2)); // 1カテゴリに偏りすぎない
    }

    [Fact] // テスト2: 教師データ必須項目
    public void CalibrationSet_AllSamplesHaveRequiredFields()
    {
        foreach (var s in BuffettScoreCalibrationSet.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.Category));
            Assert.NotNull(s.Stock);
            Assert.True(s.MinExpectedScore <= s.ExpectedScore && s.ExpectedScore <= s.MaxExpectedScore);
            Assert.NotEmpty(s.AllowedGrades);
            Assert.False(string.IsNullOrWhiteSpace(s.Rationale));
        }
    }

    [Fact] // テスト3: DefaultWeights が制約を満たす
    public void DefaultWeights_SatisfyConstraints()
    {
        var w = BuffettScoreWeightOptimizer.DefaultWeights;
        _out.WriteLine($"Weights: biz={w.BusinessDurabilityWeight} prof={w.ProfitabilityWeight} safe={w.SafetyWeight} growth={w.GrowthStabilityWeight} capital={w.CapitalAllocationWeight} val={w.ValuationWeight}");
        Assert.True(w.IsValid());
        Assert.True(w.BusinessDurabilityWeight + w.SafetyWeight >= 0.35);
        Assert.True(w.ValuationWeight <= 0.20);
    }

    [Fact] // テスト4: CalibrationSet への適合(TotalLoss が一定以下。全B評価では通らない設計)
    public void DefaultWeights_FitCalibrationSet()
    {
        var opt = new BuffettScoreWeightOptimizer();
        var res = opt.Optimize();
        var byCat = res.Evals.GroupBy(e => e.Sample.Category)
            .Select(g => $"{g.Key}: avgLoss={g.Average(e => e.Loss):0.0} n={g.Count()}");
        foreach (var line in byCat) _out.WriteLine(line);
        _out.WriteLine($"TotalLoss={res.TotalLoss:0.0}");
        // 39 程度に収まる。150 は「雑に全B」では到達できない水準(S→S・危険→E・商社→B/A 等が必要)。
        Assert.True(res.TotalLoss <= 150, $"TotalLoss={res.TotalLoss}");
    }

    [Fact] // テスト5: S の厳格化
    public void SRank_OnlyForTrueSuperior()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All)
        {
            var r = Score(smp.Stock);
            if (smp.Category == "S超優良")
                Assert.Equal("S", r.OverallGrade);
            if (smp.ProhibitS)
                Assert.NotEqual("S", r.OverallGrade);
            if (smp.Danger)
                Assert.True(r.BuffettScore < 70, $"{smp.Name} danger score={r.BuffettScore}");
        }
    }

    [Fact] // テスト6: 総合商社パターンは 70〜82・B/A・S禁止
    public void TradingHouse_IsBtoA()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All.Where(x => x.Category == "総合商社"))
        {
            var r = Score(smp.Stock);
            Assert.Equal("TradingCompany", r.Profile);
            Assert.InRange(r.BuffettScore, 70, 82);
            Assert.Contains(r.OverallGrade, new[] { "B", "A" });
        }
    }

    [Fact] // テスト7: 低PER低PBRだけでは高評価にならない(<70)
    public void LowPerLowPbr_NotHighlyRated()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All.Where(x => x.Category == "低PER低収益"))
            Assert.True(Score(smp.Stock).BuffettScore < 70, $"{smp.Name}");
    }

    [Fact] // テスト8: 高配当でも配当性向100%超は <=70
    public void HighYield_DangerousPayout_Capped()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All.Where(x => x.Category == "高配当危険"))
            Assert.True(Score(smp.Stock).BuffettScore <= 70, $"{smp.Name}");
    }

    [Fact] // テスト9: 金融業は金融業用の財務安全性で評価(営業CF/FCF=0でも過度に減点しない)
    public void Financial_UsesFinancialProfile()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All.Where(x => x.Category == "金融"))
        {
            var r = Score(smp.Stock);
            Assert.Equal("FinancialCompany", r.Profile);
            Assert.True(r.SafetyScore >= 50, $"{smp.Name} safe={r.SafetyScore}"); // CF0でも崩れない
            Assert.Contains(r.OverallGrade, new[] { "B", "A", "C" });
        }
    }

    [Fact] // テスト5: ランクと点数の整合性(92/A や 88/S が出ない)
    public void Rank_And_Score_AreConsistent()
    {
        foreach (var smp in BuffettScoreCalibrationSet.All)
        {
            var r = Score(smp.Stock);
            (double lo, double hi) = r.OverallGrade switch
            {
                "S" => (90, 100), "A" => (80, 89), "B" => (70, 79),
                "C" => (60, 69), "D" => (50, 59), _ => (0, 49),
            };
            Assert.InRange(r.BuffettScore, lo, hi);
        }
    }

    [Fact] // テスト6: S不適格は計算上90+でも89以下に補正される
    public void SIneligible_CappedTo89()
    {
        // 高ROE・高収益だが長期成長(10年)が無い → S不適格
        var s = new Stock
        {
            Code = "X", PER = 14, PBR = 2.2, ROE = 25, ROA = 14, EPS = 200, BPS = 1500, MarketCap = 800000,
            OperatingMargin = 30, NetProfitMargin = 18, OperatingCashFlowMargin = 24, EquityRatio = 65,
            RevenueGrowth5Y = 7, RevenueGrowth10Y = 0, OperatingProfitGrowthRate = 9, EpsGrowthRate = 10,
            NetProfitGrowthRate = 10, OrdinaryProfitMargin = 30, OperatingCF = 900000, FreeCashFlow = 700000,
            DividendYield = 2.5, PayoutRatio = 35, ConsecutiveDividendYears = 10, NonDividendCutYears = 12,
            DividendGrowth5Y = 9, BuybackAmount = 50000, TotalYield = 2.5, IndicatorsFetched = true,
        };
        var r = Score(s);
        Assert.True(r.BuffettScore <= 89, $"score={r.BuffettScore}");
        Assert.NotEqual("S", r.OverallGrade);
    }

    [Fact] // テスト11: 順位制約を満たす
    public void RankingConstraints_AreSatisfied()
    {
        var scores = BuffettScoreCalibrationSet.All.ToDictionary(x => x.Name, x => Score(x.Stock).BuffettScore);
        foreach (var c in BuffettScoreCalibrationSet.RankingConstraints)
            Assert.True(scores[c.BetterSampleName] >= scores[c.WorseSampleName],
                $"{c.BetterSampleName}({scores[c.BetterSampleName]}) >= {c.WorseSampleName}({scores[c.WorseSampleName]})");
        Assert.True(BuffettScoreCalibrationSet.RankingConstraints.Count >= 8);
    }

    [Fact] // 評価理由が生成される
    public void Reasons_AreGenerated()
    {
        var r = Score(BuffettScoreCalibrationSet.All.First(x => x.Category == "総合商社").Stock);
        Assert.False(string.IsNullOrWhiteSpace(r.HighScoreReasons));
        Assert.False(string.IsNullOrWhiteSpace(r.PenaltyReasons));
        Assert.False(string.IsNullOrWhiteSpace(r.RankDecisionReasons));
    }
}
