namespace DStockAnalysis.Models;

/// <summary>
/// バフェット採点の6サブスコアを総合点へ合成する重み。固定の感覚値ではなく、
/// 教師データ(CalibrationSet)へ適合するよう <see cref="DStockAnalysis.Services.BuffettScoreWeightOptimizer"/> で
/// 最適化した値を <c>Default</c>(= Optimizer.Default)として用いる。
/// 制約: 合計1.0 / 各範囲内 / BusinessDurability+Safety≥0.35 / Valuation≤0.20。
/// </summary>
public sealed class BuffettScoreWeights
{
    public double BusinessDurabilityWeight { get; init; }
    public double ProfitabilityWeight { get; init; }
    public double SafetyWeight { get; init; }
    public double GrowthStabilityWeight { get; init; }
    public double CapitalAllocationWeight { get; init; }
    public double ValuationWeight { get; init; }

    /// <summary>制約を満たすか。</summary>
    public bool IsValid()
    {
        double sum = BusinessDurabilityWeight + ProfitabilityWeight + SafetyWeight
                   + GrowthStabilityWeight + CapitalAllocationWeight + ValuationWeight;
        bool inRange =
            R(BusinessDurabilityWeight, 0.20, 0.35) && R(ProfitabilityWeight, 0.12, 0.25) &&
            R(SafetyWeight, 0.12, 0.25) && R(GrowthStabilityWeight, 0.08, 0.20) &&
            R(CapitalAllocationWeight, 0.05, 0.15) && R(ValuationWeight, 0.08, 0.20);
        return Math.Abs(sum - 1.0) < 1e-6 && inRange
            && BusinessDurabilityWeight + SafetyWeight >= 0.35 - 1e-9
            && ValuationWeight <= 0.20 + 1e-9;
        static bool R(double v, double lo, double hi) => v >= lo - 1e-9 && v <= hi + 1e-9;
    }
}
