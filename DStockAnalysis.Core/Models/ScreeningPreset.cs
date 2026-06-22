namespace DStockAnalysis.Models;

/// <summary>
/// 上部に並べるプリセット条件ボタン。押すと条件パネルへ値が自動入力される。
/// </summary>
public class ScreeningPreset
{
    public string Name { get; set; } = "";
    public Func<ScreeningCriteria> Build { get; set; } = () => new ScreeningCriteria();

    public ScreeningPreset() { }

    public ScreeningPreset(string name, Func<ScreeningCriteria> build)
    {
        Name = name;
        Build = build;
    }
}
