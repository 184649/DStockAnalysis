using DStockAnalysis.Web.Services;
using Xunit;

namespace DStockAnalysis.Web.Tests.Unit;

/// <summary>
/// Yahoo! ファイナンス quoteSummary JSON のパース単体テスト(ネットワークアクセスなし)。
/// 株価は現在値・利回り/性向/ROE は % 換算されることを検証する。
/// </summary>
public class YahooFinanceClientTests
{
    private const string Sample = @"{""quoteSummary"":{""result"":[{
        ""price"":{""regularMarketPrice"":{""raw"":2706.0},""marketCap"":{""raw"":32041888382976}},
        ""summaryDetail"":{""trailingPE"":{""raw"":9.1648},""dividendYield"":{""raw"":0.0365},""payoutRatio"":{""raw"":0.3218},""trailingAnnualDividendRate"":{""raw"":95.0}},
        ""defaultKeyStatistics"":{""priceToBook"":{""raw"":0.8835},""trailingEps"":{""raw"":295.26},""bookValue"":{""raw"":3062.816}},
        ""financialData"":{""returnOnEquity"":{""raw"":0.10233},""profitMargins"":{""raw"":0.07592},""revenueGrowth"":{""raw"":0.019},""earningsGrowth"":{""raw"":0.232},""debtToEquity"":{""raw"":107.061},""operatingCashflow"":{""raw"":5472919748608},""freeCashflow"":{""raw"":-1204836106240},""totalRevenue"":{""raw"":50684951003136}}
    }],""error"":null}}";

    [Fact]
    public void ParseQuoteSummary_ExtractsFinancialExtrasOnly()
    {
        // Yahoo からは収益性・財務・CF のみ採用。バリュエーション(PER/PBR/利回り/EPS)は株探(会社予想)を使う。
        var d = YahooFinanceClient.ParseQuoteSummary(Sample);
        Assert.Equal("2706", d["Price"]);          // 株探不通時の予備として保持
        Assert.Equal("10.23", d["ROE"]);           // 0.10233 → %
        Assert.Equal("7.59", d["NetProfitMargin"]);// 0.07592 → %
        Assert.Equal("107.06", d["InterestBearingDebtRatio"]);
        Assert.Equal("5472920", d["OperatingCF"]); // 円→百万円
        Assert.Equal("-1204836", d["FreeCashFlow"]);
        Assert.Equal("10.8", d["OperatingCashFlowMargin"]); // opCF/revenue
        // バリュエーション系・成長率・時価総額・営業利益率は Yahoo から採らない
        foreach (var k in new[] { "PER", "PBR", "EPS", "BPS", "DividendYield", "PayoutRatio",
                                  "Dividend", "RevenueGrowthRate", "NetProfitGrowthRate", "MarketCap", "OperatingMargin" })
            Assert.False(d.ContainsKey(k), $"{k} は Yahoo から採用しない");
    }

    [Fact]
    public void ParseQuotes_ExtractsCodeToPrice()
    {
        var json = @"{""quoteResponse"":{""result"":[
            {""symbol"":""8001.T"",""regularMarketPrice"":1814.0},
            {""symbol"":""7203.T"",""regularMarketPrice"":2700.5},
            {""symbol"":""6861.T"",""regularMarketPrice"":77370.0},
            {""symbol"":""9999.T""}
        ],""error"":null}}";
        var d = YahooFinanceClient.ParseQuotes(json);
        Assert.Equal(1814.0, d["8001"]);   // ".T" は除去
        Assert.Equal(2700.5, d["7203"]);
        Assert.Equal(77370.0, d["6861"]);
        Assert.False(d.ContainsKey("9999")); // 価格欠落は含めない
    }

    [Fact]
    public void ParseQuoteSummary_NoFinancials_ReturnsEmpty()
    {
        // 価格も財務も無ければ空(株探等から取得する)
        var json = @"{""quoteSummary"":{""result"":[{""summaryDetail"":{}}]}}";
        var d = YahooFinanceClient.ParseQuoteSummary(json);
        Assert.Empty(d);
    }

    [Fact]
    public void ParseQuoteSummary_EmptyResult_ReturnsEmpty()
    {
        var d = YahooFinanceClient.ParseQuoteSummary(@"{""quoteSummary"":{""result"":[],""error"":null}}");
        Assert.Empty(d);
    }
}
