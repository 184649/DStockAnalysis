namespace DStockAnalysis.Models;

/// <summary>
/// バフェット採点(100点満点)の結果。総合点・6つのサブスコア・データ信頼度・評価ランク・判定コメントを保持する。
/// 算出は <see cref="DStockAnalysis.Services.BuffettScoreCalculator"/>。外部アクセスは行わず、
/// Stock に既にある指標(CSV取込・自動取得済み)だけを使う。
/// </summary>
public class BuffettResult
{
    public double BuffettScore { get; set; }
    public double BusinessDurabilityScore { get; set; } // 事業耐久力
    public double ProfitabilityScore { get; set; }      // 収益力
    public double SafetyScore { get; set; }             // 財務安全性
    public double GrowthStabilityScore { get; set; }    // 成長安定性
    public double CapitalAllocationScore { get; set; }  // 株主還元・資本配分
    public double ValuationScore { get; set; }          // 割安性
    public double DataConfidence { get; set; }          // データ信頼度(取得済み重要指標の割合)
    public string Profile { get; set; } = "StandardCompany"; // 採点プロファイル(Standard/Financial/Trading)
    public string OverallGrade { get; set; } = "E";     // S/A/B/C/D/E
    public string JudgementText { get; set; } = "";
    public string Strengths { get; set; } = "";         // 高評価要因
    public string Weaknesses { get; set; } = "";        // 減点要因
    public string RankReason { get; set; } = "";        // ランク判定理由
}
