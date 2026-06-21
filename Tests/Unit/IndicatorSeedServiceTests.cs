using DStockAnalysis.Models;
using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class IndicatorSeedServiceTests
{
    private readonly IndicatorSeedService _seed = new();

    private static Stock Bare(string code) => new() { Code = code, Name = "X", Market = "東証プライム", Sector = "情報・通信業", Scale = "大型" };

    [Fact] // UT-IS-01: 同じコードなら毎回同じ指標(決定論的・再起動耐性)
    public void Fill_IsDeterministic_ByCode()
    {
        var a = Bare("7203"); _seed.FillIndicators(a);
        var b = Bare("7203"); _seed.FillIndicators(b);
        Assert.Equal(a.PER, b.PER);
        Assert.Equal(a.ROE, b.ROE);
        Assert.Equal(a.DividendYield, b.DividendYield);
        Assert.Equal(a.MarketCap, b.MarketCap);
    }

    [Fact] // UT-IS-02: 異なるコードは異なる傾向(分散がある)
    public void Fill_VariesAcrossCodes()
    {
        var pers = new List<double>();
        foreach (var c in new[] { "1000", "2000", "3000", "4000", "5000" })
        {
            var s = Bare(c); _seed.FillIndicators(s); pers.Add(s.PER);
        }
        Assert.True(pers.Distinct().Count() >= 4);
    }

    [Fact] // UT-IS-03: 擬似フラグと整合した派生値
    public void Fill_SetsSampleFlagAndDerived()
    {
        var s = Bare("9999"); _seed.FillIndicators(s);
        Assert.True(s.IsSampleIndicators);
        Assert.True(s.Price > 0);
        Assert.Equal(Math.Round(s.PER * s.PBR, 1), s.MixFactor);
        Assert.Equal(Math.Round(s.DividendYield + s.BenefitYield, 2), s.TotalYield);
    }

    [Fact] // UT-IS-04: 規模で時価総額レンジが異なる
    public void Fill_MarketCap_DependsOnScale()
    {
        var large = new Stock { Code = "1111", Scale = "大型" }; _seed.FillIndicators(large);
        var small = new Stock { Code = "1111", Scale = "小型" }; _seed.FillIndicators(small);
        // 同一コードでも規模区分で時価総額レンジが変わる
        Assert.True(large.MarketCap > small.MarketCap);
    }

    [Fact] // UT-IS-05: 時系列は10年分生成される
    public void BuildHistory_Returns10Years()
    {
        var s = Bare("6758"); _seed.FillIndicators(s);
        var hist = _seed.BuildHistory(s);
        Assert.Equal(10, hist.Count);
        Assert.All(hist, p => Assert.True(p.FiscalYear > 2000));
    }
}
