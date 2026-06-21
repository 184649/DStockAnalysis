using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class PresetServiceTests
{
    private readonly PresetService _svc = new();

    [Fact] // UT-PS-01: プリセットが定義されている
    public void GetPresets_ReturnsNamedPresets()
    {
        var presets = _svc.GetPresets();
        Assert.True(presets.Count >= 10);
        Assert.All(presets, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    [Fact] // UT-PS-02: 主要プリセットが存在する
    public void GetPresets_ContainsKeyPresets()
    {
        var names = _svc.GetPresets().ConvertAll(p => p.Name);
        Assert.Contains("高配当で好財務な銘柄", names);
        Assert.Contains("財務健全", names);
        Assert.Contains("株主優待あり高利回り銘柄", names);
        Assert.Contains("総合評価が最高評価の銘柄", names);
    }

    [Fact] // UT-PS-03: 「総合評価が最高評価の銘柄」はバフェットスコア下限を設定
    public void Preset_TopBuffett_SetsBuffettMin()
    {
        var p = _svc.GetPresets().Find(x => x.Name == "総合評価が最高評価の銘柄")!;
        var c = p.Build();
        Assert.Equal(75, c.BuffettScore.Min);
    }

    [Fact] // UT-PS-04: 「株主優待あり高利回り銘柄」は優待ありフラグを設定
    public void Preset_BenefitHighYield_SetsBenefitOnly()
    {
        var p = _svc.GetPresets().Find(x => x.Name == "株主優待あり高利回り銘柄")!;
        var c = p.Build();
        Assert.True(c.BenefitOnly);
        Assert.NotNull(c.BenefitYield.Min);
    }
}
