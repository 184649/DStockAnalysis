namespace DStockAnalysis.Models;

/// <summary>はい / いいえ / 不明 の3値。バフェットチェックで使用。</summary>
public enum YesNoUnknown
{
    Unknown = 0,
    Yes = 1,
    No = 2
}

/// <summary>銘柄のユーザー分類。</summary>
public enum StockClassification
{
    未分類 = 0,
    最重要候補 = 1,
    長期優良株候補 = 2,
    第二のキオクシア候補 = 3,
    再評価候補 = 4,
    決算確認待ち = 5,
    保留 = 6,
    除外 = 7
}

/// <summary>総合判定の区分。買い・売りは表現しない。</summary>
public enum OverallJudgement
{
    調査中 = 0,
    最重要候補 = 1,
    長期優良株候補 = 2,
    第二のキオクシア候補 = 3,
    再評価候補 = 4,
    高配当_還元候補 = 5,
    テーマ候補 = 6,
    決算確認候補 = 7,
    保留 = 8,
    除外 = 9
}
