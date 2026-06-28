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

    [Fact] // 評価理由が生成される
    public void Reasons_AreGenerated()
    {
        var r = Score(BuffettScoreCalibrationSet.All.First(x => x.Category == "総合商社").Stock);
        Assert.False(string.IsNullOrWhiteSpace(r.Strengths));
        Assert.False(string.IsNullOrWhiteSpace(r.Weaknesses));
        Assert.False(string.IsNullOrWhiteSpace(r.RankReason));
    }
}
