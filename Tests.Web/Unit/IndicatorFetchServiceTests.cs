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
        // 自己資本比率は数値テキストではなくバー幅(style="width:..%")で表現される実構造を模す
        var html = "<html>ROE 12.5 % PER 14.2 倍 PBR 1.30 倍 配当利回り 3.1 % EPS 210.5 " +
                   "<a title=\"7203 自己資本比率\">株主資本比率</a></dt><dd><span class=\"ratio\" style=\"width:55.0%;\"></span> " +
                   "時価総額 1兆2000億</html>";
        var d = IndicatorFetchService.ParseIrBank(html, "7203");
        Assert.Equal("12.5", d["ROE"]);
        Assert.Equal("14.2", d["PER"]);
        Assert.Equal("1.3", d["PBR"]);
        Assert.Equal("3.1", d["DividendYield"]);
        Assert.Equal("210.5", d["EPS"]);
        Assert.Equal("55", d["EquityRatio"]);   // バー幅 55.0% から取得
        // 1兆2000億 -> 1,200,000 百万円
        Assert.Equal("1200000", d["MarketCap"]);
        // 配当性向はどのサイトにも数値が無いため、ここでは取得しない(Derive で算出)
        Assert.False(d.ContainsKey("PayoutRatio"));
    }

    [Fact]
    public void ParseIrBank_EquityRatioFromBarWidth()
    {
        var html = "<dt><a title=\"7203 トヨタ自動車 | 自己資本比率\" href=\"/E02144/safety\">株主資本比率（連）</a></dt>" +
                   "<dd><span class=\"ratio\" style=\"width:37.83%;\"></span>";
        var d = IndicatorFetchService.ParseIrBank(html, "7203");
        Assert.Equal("37.83", d["EquityRatio"]);
    }

    [Fact]
    public void ParseIrBank_MarketCapInOku_ConvertedToMillionYen()
    {
        var d = IndicatorFetchService.ParseIrBank("時価総額 850 億", "9999");
        Assert.Equal("85000", d["MarketCap"]); // 850 億 = 85,000 百万円
    }

    [Fact]
    public void ParseIrBank_MarketCapTrillionAndOku_Combined()
    {
        var d = IndicatorFetchService.ParseIrBank("時価総額 43兆8626億 PER", "7203");
        Assert.Equal("43862600", d["MarketCap"]); // 43兆8626億 = 43,862,600 百万円
    }

    [Fact]
    public void ParseMinkabu_PriceFromJsonLdAndRatios()
    {
        var html = "{\"url\":\"https://minkabu.jp/stock/7203\",\"offers\":{\"@type\":\"Offer\",\"price\":\"2741.5\"}} " +
                   "<div>PER (調整後) 10.5 倍 PBR 1.1 倍 配当利回り 2.6 % 時価総額 43,301,958百万円</div>";
        var d = IndicatorFetchService.ParseMinkabu(html, "7203");
        Assert.Equal("2741.5", d["Price"]);            // JSON-LD の当該銘柄 offers.price
        Assert.Equal("10.5", d["PER"]);
        Assert.Equal("1.1", d["PBR"]);
        Assert.Equal("2.6", d["DividendYield"]);
        Assert.Equal("43301958", d["MarketCap"]);       // 百万円の正確値
    }

    [Fact]
    public void Parse_NoMatch_ReturnsEmpty()
    {
        var d = IndicatorFetchService.ParseMinkabu("<html>関係ないページ</html>", "7203");
        Assert.Empty(d);
    }

    [Fact]
    public void Derive_ComputesPayoutAndDerivedMetricsFromRealValues()
    {
        var row = new Dictionary<string, string>
        {
            ["Price"] = "2741.5", ["DividendYield"] = "3.6", ["EPS"] = "295.25",
            ["PER"] = "12.06", ["PBR"] = "0.91"
        };
        IndicatorFetchService.Derive(row);

        double D(string k) => double.Parse(row[k], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(10.97, D("MixFactor"), 1);                 // PER×PBR
        Assert.Equal(3013, D("BPS"), 0);                        // Price/PBR
        Assert.Equal(98.7, D("Dividend"), 0);                   // Price×DY/100
        Assert.Equal(33.4, D("PayoutRatio"), 0);                // Dividend/EPS×100(実値から算出)
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
