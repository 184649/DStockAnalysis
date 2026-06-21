using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class JpxMasterServiceTests
{
    private readonly JpxMasterService _jpx = new();
    private readonly IndicatorSeedService _seed = new();
    private readonly ScoreService _scorer = new();

    [Fact] // UT-JPX-01: 同梱マスタが利用可能
    public void Master_IsAvailable()
    {
        Assert.True(_jpx.IsAvailable, "Data/data_j.xls がテスト出力に存在すること");
    }

    [Fact] // UT-JPX-02: 全銘柄(内国株式)を多数読み込める
    public void LoadAll_ReturnsManyStocks()
    {
        var (stocks, date) = _jpx.LoadAll(_seed, _scorer);
        Assert.True(stocks.Count > 1000, $"件数={stocks.Count}");
        Assert.NotNull(date);
    }

    [Fact] // UT-JPX-03: コード・銘柄名が設定され、市場は東証3区分のみ
    public void LoadAll_HasValidAttributes()
    {
        var (stocks, _) = _jpx.LoadAll(_seed, _scorer);
        Assert.All(stocks, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Code));
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.Contains(s.Market, new[] { "東証プライム", "東証スタンダード", "東証グロース" });
            Assert.Contains(s.Scale, new[] { "大型", "中型", "小型" });
        });
    }

    [Fact] // UT-JPX-04: 各市場区分が含まれる
    public void LoadAll_ContainsAllMarketSegments()
    {
        var (stocks, _) = _jpx.LoadAll(_seed, _scorer);
        Assert.Contains(stocks, s => s.Market == "東証プライム");
        Assert.Contains(stocks, s => s.Market == "東証スタンダード");
        Assert.Contains(stocks, s => s.Market == "東証グロース");
    }

    [Fact] // UT-JPX-05: 読み込み後はスコアが算出され擬似指標フラグが立つ
    public void LoadAll_FillsSampleIndicatorsAndScores()
    {
        var (stocks, _) = _jpx.LoadAll(_seed, _scorer);
        var s = stocks[0];
        Assert.True(s.IsSampleIndicators);
        Assert.InRange(s.BuffettScore, 0, 100);
        Assert.True(s.Price > 0);
    }
}
