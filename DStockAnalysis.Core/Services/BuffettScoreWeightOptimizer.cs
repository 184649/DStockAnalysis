using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// 教師データ(CalibrationSet)に適合するよう、6サブスコアの合成重みを最適化する。
/// 説明可能な「教師データ付き重み最適化エンジン」。外部MLライブラリは使わず、
/// 制約を満たす重み候補をグリッド探索し、TotalLoss(スコア誤差²＋ランク不一致＋禁止ペナルティ)が
/// 最小になる重みを選ぶ。選ばれた重みは <see cref="DefaultWeights"/> として通常採点に使う(初回のみ計算しキャッシュ)。
/// </summary>
public sealed class BuffettScoreWeightOptimizer
{
    public sealed record SampleEval(BuffettScoreTrainingSample Sample, double ActualScore, string ActualGrade,
        double ScoreLoss, double GradeLoss, double Penalty)
    {
        public double Loss => ScoreLoss + GradeLoss + Penalty;
    }

    public sealed record Result(BuffettScoreWeights Best, double TotalLoss, IReadOnlyList<SampleEval> Evals);

    private static readonly Lazy<BuffettScoreWeights> _default =
        new(() => new BuffettScoreWeightOptimizer().Optimize().Best, isThreadSafe: true);

    /// <summary>教師データへ最適化済みの既定重み(初回アクセス時に1度だけ計算しキャッシュ)。</summary>
    public static BuffettScoreWeights DefaultWeights => _default.Value;

    /// <summary>教師データ(スコア誤差・ランク・順位制約)に対して TotalLoss 最小の重みを探索する。</summary>
    public Result Optimize(IReadOnlyList<BuffettScoreTrainingSample>? set = null,
        IReadOnlyList<BuffettScoreRankingConstraint>? constraints = null)
    {
        var samples = set ?? BuffettScoreCalibrationSet.All;
        var rank = constraints ?? BuffettScoreCalibrationSet.RankingConstraints;
        var calc = new BuffettScoreCalculator();

        BuffettScoreWeights best = Fallback();
        double bestLoss = double.MaxValue;
        foreach (var w in Candidates())
        {
            double tot = 0;
            var scores = new Dictionary<string, double>();
            foreach (var smp in samples) { var ev = Eval(calc, smp, w); tot += ev.Loss; scores[smp.Name] = ev.ActualScore; }
            tot += RankingLoss(rank, scores);
            if (tot < bestLoss) { bestLoss = tot; best = w; }
        }

        var evals = Evaluate(samples, best);
        var bestScores = evals.ToDictionary(e => e.Sample.Name, e => e.ActualScore);
        double total = evals.Sum(e => e.Loss) + RankingLoss(rank, bestScores);
        return new Result(best, total, evals);
    }

    /// <summary>順位制約の Loss(BetterScore が WorseScore + Margin 未満なら不足分の2乗)。</summary>
    public static double RankingLoss(IReadOnlyList<BuffettScoreRankingConstraint> constraints, IReadOnlyDictionary<string, double> scores)
    {
        double loss = 0;
        foreach (var c in constraints)
            if (scores.TryGetValue(c.BetterSampleName, out var b) && scores.TryGetValue(c.WorseSampleName, out var w))
                loss += Math.Pow(Math.Max(0, c.Margin - (b - w)), 2);
        return loss;
    }

    /// <summary>指定重みで各教師データを評価した明細を返す(レポート/テスト用)。</summary>
    public IReadOnlyList<SampleEval> Evaluate(IReadOnlyList<BuffettScoreTrainingSample> samples, BuffettScoreWeights w)
    {
        var calc = new BuffettScoreCalculator();
        return samples.Select(smp => Eval(calc, smp, w)).ToList();
    }

    private static SampleEval Eval(BuffettScoreCalculator calc, BuffettScoreTrainingSample smp, BuffettScoreWeights w)
    {
        var r = calc.Calculate(smp.Stock, w);
        double scoreLoss = r.BuffettScore < smp.MinExpectedScore ? Math.Pow(smp.MinExpectedScore - r.BuffettScore, 2)
                         : r.BuffettScore > smp.MaxExpectedScore ? Math.Pow(r.BuffettScore - smp.MaxExpectedScore, 2)
                         : 0;
        double gradeLoss = smp.AllowedGrades.Contains(r.OverallGrade) ? 0 : 200;
        double penalty = 0;
        if (smp.ProhibitS && (r.OverallGrade == "S" || r.BuffettScore >= 90)) penalty += 1000;
        if (smp.Danger && (r.BuffettScore >= 70 || r.OverallGrade is "A" or "S")) penalty += 1000;
        return new SampleEval(smp, r.BuffettScore, r.OverallGrade, scoreLoss, gradeLoss, penalty);
    }

    /// <summary>制約を満たす重み候補をグリッド探索(0.05刻み)で列挙する。</summary>
    private static IEnumerable<BuffettScoreWeights> Candidates()
    {
        double[] B = { 0.20, 0.25, 0.30, 0.35 };
        double[] P = { 0.12, 0.15, 0.20, 0.25 };
        double[] Sa = { 0.12, 0.15, 0.20, 0.25 };
        double[] G = { 0.08, 0.10, 0.15, 0.20 };
        double[] C = { 0.05, 0.10, 0.15 };
        foreach (var b in B)
            foreach (var p in P)
                foreach (var sa in Sa)
                    foreach (var g in G)
                        foreach (var c in C)
                        {
                            double val = Math.Round(1.0 - (b + p + sa + g + c), 4);
                            var w = new BuffettScoreWeights
                            {
                                BusinessDurabilityWeight = b, ProfitabilityWeight = p, SafetyWeight = sa,
                                GrowthStabilityWeight = g, CapitalAllocationWeight = c, ValuationWeight = val,
                            };
                            if (w.IsValid()) yield return w;
                        }
    }

    private static BuffettScoreWeights Fallback() => new()
    {
        BusinessDurabilityWeight = 0.25, ProfitabilityWeight = 0.20, SafetyWeight = 0.15,
        GrowthStabilityWeight = 0.15, CapitalAllocationWeight = 0.10, ValuationWeight = 0.15,
    };
}
