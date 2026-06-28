namespace DStockAnalysis.Models;

/// <summary>
/// バフェット採点の教師データ1件。バフェット型投資の考え方に基づく「あるべき評価」を表す。
/// 実在の銘柄コードに依存せず、銘柄パターンとして定義する(特定コードの特別扱いはしない)。
/// </summary>
public sealed class BuffettScoreTrainingSample
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public Stock Stock { get; init; } = new();
    public double ExpectedScore { get; init; }
    public double MinExpectedScore { get; init; }
    public double MaxExpectedScore { get; init; }
    public string[] AllowedGrades { get; init; } = System.Array.Empty<string>();
    public bool ProhibitS { get; init; }
    /// <summary>赤字・CF悪化・財務危険など「高評価にしてはいけない」サンプル。70点以上で強いペナルティ。</summary>
    public bool Danger { get; init; }
    public string Rationale { get; init; } = "";
}
