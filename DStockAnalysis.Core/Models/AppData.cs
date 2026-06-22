namespace DStockAnalysis.Models;

/// <summary>
/// ローカル保存される永続データのうち、銘柄に紐づくユーザー入力分。
/// 銘柄本体(Stock)とは別ファイルで保存し、CSV再取り込み時もメモ等が消えないようにする。
/// </summary>
public class StockUserData
{
    public string Code { get; set; } = "";
    public StockMemo Memo { get; set; } = new();
    public BuffettCheck BuffettCheck { get; set; } = new();
    public double UserInterest { get; set; } = 50;
}

/// <summary>アプリ設定。</summary>
public class AppSettings
{
    public string? LastCsvPath { get; set; }
    public List<string> ComparisonCodes { get; set; } = new(); // 比較対象銘柄
    public ScreeningCriteria LastCriteria { get; set; } = new();
}
