namespace DStockAnalysis.Models;

/// <summary>
/// 銘柄ごとのユーザーメモ。ローカル保存される。
/// </summary>
public class StockMemo
{
    public string DiscoveryReason { get; set; } = "";       // この銘柄を見つけた理由
    public string InterestingNumbers { get; set; } = "";    // 気になった数値
    public string GoodPoints { get; set; } = "";            // 良い点
    public string BadPoints { get; set; } = "";             // 悪い点
    public string LongTermEvaluation { get; set; } = "";    // 長期優良株としての評価
    public string RevaluationEvaluation { get; set; } = ""; // 再評価候補としての評価
    public string KioxiaReason { get; set; } = "";          // 第二のキオクシア候補として見る理由
    public string NextToCheck { get; set; } = "";           // 次に確認する情報

    public StockClassification Classification { get; set; } = StockClassification.未分類;
}
