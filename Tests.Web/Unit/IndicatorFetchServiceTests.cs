using DStockAnalysis.Web.Services;
using Xunit;

namespace DStockAnalysis.Web.Tests.Unit;

/// <summary>
/// 指標スクレイパ(IndicatorFetchService)の純粋関数部分の単体テスト。
/// HTML 整形・サイト別抽出・robots.txt 解釈を検証する(ネットワークアクセスなし)。
/// </summary>
public class IndicatorFetchServiceTests
{
    [Fact]
    public void Strip_RemovesTagsAndCollapsesWhitespace()
    {
        var html = "<div>  PER <b>14.2</b>\n 倍 </div>";
        var t = IndicatorFetchService.Strip(html);
        Assert.Equal(" PER 14.2 倍 ", t);
    }

    [Fact]
    public void ParseIrBank_ExtractsCoreMetrics()
    {
        var html = "<html>ROE 12.5 % PER 14.2 倍 PBR 1.30 倍 配当利回り 3.1 % " +
                   "EPS 210.5 自己資本比率 55.0 % 配当性向 35.0 % 時価総額 1.2 兆</html>";
        var d = IndicatorFetchService.ParseIrBank(html);
        Assert.Equal("12.5", d["ROE"]);
        Assert.Equal("14.2", d["PER"]);
        Assert.Equal("1.3", d["PBR"]);
        Assert.Equal("3.1", d["DividendYield"]);
        Assert.Equal("210.5", d["EPS"]);
        Assert.Equal("55", d["EquityRatio"]);
        Assert.Equal("35", d["PayoutRatio"]);
        // 1.2 兆 -> 1,200,000 百万円
        Assert.Equal("1200000", d["MarketCap"]);
    }

    [Fact]
    public void ParseIrBank_MarketCapInOku_ConvertedToMillionYen()
    {
        var html = "時価総額 850 億";
        var d = IndicatorFetchService.ParseIrBank(html);
        Assert.Equal("85000", d["MarketCap"]); // 850 億 = 85,000 百万円
    }

    [Fact]
    public void ParseMinkabu_ExtractsPriceAndRatios()
    {
        var html = "現在値 3,050 PER 10.5 倍 PBR 1.1 倍 配当利回り 2.6 %";
        var d = IndicatorFetchService.ParseMinkabu(html);
        Assert.Equal("3050", d["Price"]);
        Assert.Equal("10.5", d["PER"]);
        Assert.Equal("1.1", d["PBR"]);
        Assert.Equal("2.6", d["DividendYield"]);
    }

    [Fact]
    public void Parse_NoMatch_ReturnsEmpty()
    {
        var d = IndicatorFetchService.ParseMinkabu("<html>関係ないページ</html>");
        Assert.Empty(d);
    }

    [Fact]
    public void Robots_DisallowBlocksMatchingPath()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /private\nAllow: /private/ok\nCrawl-delay: 3");
        Assert.False(rules.IsAllowed("/private/page"));
        Assert.True(rules.IsAllowed("/public"));
        Assert.Equal(3, rules.CrawlDelay);
    }

    [Fact]
    public void Robots_LongestMatchWins()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /a\nAllow: /a/b");
        Assert.True(rules.IsAllowed("/a/b/c"));  // より長い Allow が優先
        Assert.False(rules.IsAllowed("/a/x"));   // Disallow に該当
    }

    [Fact]
    public void Robots_EmptyMeansAllowAll()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow:");
        Assert.True(rules.IsAllowed("/anything"));
    }
}
