using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Integration;

public class SampleDataIntegrationTests
{
    private readonly SampleDataService _sample = new();
    private readonly ScoreService _scorer = new();

    [Fact] // IT-SD-01: サンプルは複数銘柄を生成し、各銘柄に10年分の時系列を持つ
    public void CreateSampleStocks_HasStocksAndHistory()
    {
        var stocks = _sample.CreateSampleStocks();
        Assert.True(stocks.Count >= 10);
        Assert.All(stocks, s => Assert.Equal(10, s.History.Count));
    }

    [Fact] // IT-SD-02: 生成時にスコアが算出され、全て 0..100 に収まる
    public void CreateSampleStocks_ScoresComputedInRange()
    {
        var stocks = _sample.CreateSampleStocks();
        Assert.All(stocks, s =>
        {
            Assert.InRange(s.BuffettScore, 0, 100);
            Assert.InRange(s.OverallScore, 0, 100);
            Assert.InRange(s.LongTermScore, 0, 100);
        });
        Assert.Contains(stocks, s => s.OverallScore > 0);
    }

    [Fact] // IT-SD-03: 優待銘柄の総合利回り = 配当利回り + 優待利回り
    public void CreateSampleStocks_BenefitStock_TotalYieldConsistent()
    {
        var stocks = _sample.CreateSampleStocks();
        var benefit = stocks.Find(s => s.HasShareholderBenefit && s.BenefitYield > 0);
        Assert.NotNull(benefit);
        Assert.Equal(benefit!.DividendYield + benefit.BenefitYield, benefit.TotalYield, 2);
    }

    [Fact] // IT-SD-04: 再計算しても値が安定(冪等)
    public void Recalculate_IsIdempotent()
    {
        var stocks = _sample.CreateSampleStocks();
        var s = stocks[0];
        var before = s.BuffettScore;
        _scorer.Recalculate(s);
        _scorer.Recalculate(s);
        Assert.Equal(before, s.BuffettScore);
    }
}
