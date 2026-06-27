namespace DStockAnalysis.Models;

/// <summary>
/// バフェットスコアの内訳1項目(配点・獲得点・根拠)。
/// 「何をもって算出しているか」を画面で説明できるようにするための明細。
/// </summary>
public class BuffettComponent
{
    public string Key { get; set; } = "";       // 機械可読キー
    public string Label { get; set; } = "";      // 表示名(日本語)
    public double Earned { get; set; }           // 獲得点
    public double Max { get; set; }              // 配点(満点)
    public string Note { get; set; } = "";       // 根拠(使った指標値や判定理由)

    public BuffettComponent() { }
    public BuffettComponent(string key, string label, double earned, double max, string note)
    {
        Key = key; Label = label;
        Earned = Math.Round(earned, 1);
        Max = max; Note = note;
    }
}
