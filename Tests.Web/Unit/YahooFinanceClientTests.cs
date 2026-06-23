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
    public void ParseQuoteSummary_ExtractsAndConvertsMetrics()
    {
        var d = YahooFinanceClient.ParseQuoteSummary(Sample);
        Assert.Equal("2706", d["Price"]);          // 現在値(前日終値ではない)
        Assert.Equal("9.16", d["PER"]);
        Assert.Equal("0.88", d["PBR"]);
        Assert.Equal("295.26", d["EPS"]);
        Assert.Equal("3063", d["BPS"]);            // bookValue 丸め
        Assert.Equal("95", d["Dividend"]);         // 1株配当
        Assert.Equal("3.65", d["DividendYield"]);  // 0.0365 → %
        Assert.Equal("32.18", d["PayoutRatio"]);   // 0.3218 → %
        Assert.Equal("10.23", d["ROE"]);           // 0.10233 → %
        Assert.Equal("7.59", d["NetProfitMargin"]);// 0.07592 → %
        Assert.Equal("1.9", d["RevenueGrowthRate"]);// 0.019 → %
        Assert.Equal("23.2", d["NetProfitGrowthRate"]);// 0.232 → %
        Assert.Equal("107.06", d["InterestBearingDebtRatio"]);
        Assert.Equal("5472920", d["OperatingCF"]); // 円→百万円
        Assert.Equal("-1204836", d["FreeCashFlow"]);
        Assert.Equal("10.8", d["OperatingCashFlowMargin"]); // opCF/revenue
        // 時価総額・営業利益率は採用しない(過少・不正確のため)
        Assert.False(d.ContainsKey("MarketCap"));
        Assert.False(d.ContainsKey("OperatingMargin"));
    }

    [Fact]
    public void ParseQuoteSummary_NoDividend_YieldAndPayoutZero()
    {
        var json = @"{""quoteSummary"":{""result"":[{
            ""price"":{""regularMarketPrice"":{""raw"":1850.0}},
            ""summaryDetail"":{""trailingPE"":{""raw"":9.0}},
            ""defaultKeyStatistics"":{""priceToBook"":{""raw"":1.3},""trailingEps"":{""raw"":205.0}}
        }]}}";
        var d = YahooFinanceClient.ParseQuoteSummary(json);
        Assert.Equal("1850", d["Price"]);
        Assert.Equal("0", d["DividendYield"]); // 無配は 0
        Assert.Equal("0", d["PayoutRatio"]);
    }

    [Fact]
    public void ParseQuoteSummary_NoPrice_ReturnsEmpty()
    {
        var json = @"{""quoteSummary"":{""result"":[{""summaryDetail"":{""trailingPE"":{""raw"":10.0}}}]}}";
        var d = YahooFinanceClient.ParseQuoteSummary(json);
        Assert.Empty(d); // 株価が取れなければ信頼できないので何も返さない
    }

    [Fact]
    public void ParseQuoteSummary_EmptyResult_ReturnsEmpty()
    {
        var d = YahooFinanceClient.ParseQuoteSummary(@"{""quoteSummary"":{""result"":[],""error"":null}}");
        Assert.Empty(d);
    }
}
