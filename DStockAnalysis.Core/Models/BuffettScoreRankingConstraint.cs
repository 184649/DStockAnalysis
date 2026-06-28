namespace DStockAnalysis.Models;

/// <summary>
/// 教師データ間の順位制約。BetterSample のスコアが WorseSample より Margin 以上高くあるべき、という関係。
/// 短期高成長銘柄が長期耐久性の高い銘柄を不当に上回らないようにするために用いる。
/// </summary>
public sealed class BuffettScoreRankingConstraint
{
    public string BetterSampleName { get; init; } = "";
    public string WorseSampleName { get; init; } = "";
    public double Margin { get; init; } = 3.0;
    public string Rationale { get; init; } = "";
}
