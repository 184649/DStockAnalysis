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
    public void ParseKabutan_ValuationTableForecastBasis()
    {
        // 株探の株価指標テーブル(会社予想ベース)。実際の HTML 構造を模す。
        var html = "<table><thead><tr><th>PER</th><th>PBR</th><th>利回り</th><th>信用倍率</th></tr></thead>" +
                   "<tbody><tr>" +
                   "<td>10.6<span class=\"fs9\">倍</span></td>" +
                   "<td>0.80<span class=\"fs9\">倍</span></td>" +
                   "<td>3.72<span class=\"fs9\">％</span></td>" +
                   "<td>9.91<span class=\"fs9\">倍</span></td></tr>" +
                   "<tr><th class=\"v_zika1\">時価総額</th>" +
                   "<td class=\"v_zika2\">42<span>兆</span>4,253<span>億円</span></td></tr></tbody></table>" +
                   " 現在値 2,694 ( 19:46 06/24 )";
        var d = IndicatorFetchService.ParseKabutan(html, "7203");
        Assert.False(d.ContainsKey("Price")); // 株価は株探から取らない(Yahoo を使用)
        Assert.Equal("10.6", d["PER"]);          // 会社予想PER
        Assert.Equal("0.8", d["PBR"]);
        Assert.Equal("3.72", d["DividendYield"]);
        Assert.Equal("42425300", d["MarketCap"]); // 42兆4253億 = 42,425,300 百万円
    }

    [Fact]
    public void ParseKabutan_DashMeansMissing()
    {
        // PER が「－」の銘柄(会社予想なし)。PER は欠損、PBR/利回りは取得。
        var html = "<thead><tr><th>PER</th><th>PBR</th><th>利回り</th><th>信用倍率</th></tr></thead><tbody><tr>" +
                   "<td>－<span class=\"fs9\">倍</span></td>" +
                   "<td>2.01<span class=\"fs9\">倍</span></td>" +
                   "<td>3.13<span class=\"fs9\">％</span></td>" +
                   "<td>1.10<span class=\"fs9\">倍</span></td></tr></tbody>";
        var d = IndicatorFetchService.ParseKabutan(html, "9433");
        Assert.False(d.ContainsKey("PER"));      // 「－」は未取得
        Assert.Equal("2.01", d["PBR"]);
        Assert.Equal("3.13", d["DividendYield"]);
    }

    [Fact]
    public void ParseIrBank_EquityRatioFromBarWidthOnly()
    {
        // IR BANK からは自己資本比率(バー幅)のみ取得する(他指標は会社予想ベースで揃えるため取らない)
        var html = "<dt><a title=\"7203 トヨタ自動車 | 自己資本比率\" href=\"/E02144/safety\">株主資本比率（連）</a></dt>" +
                   "<dd><span class=\"ratio\" style=\"width:37.83%;\"></span> ROE 12.5 % PER 14.2 倍";
        var d = IndicatorFetchService.ParseIrBank(html, "7203");
        Assert.Equal("37.83", d["EquityRatio"]);
        Assert.False(d.ContainsKey("PER"));   // 株探に揃えるため取らない
        Assert.False(d.ContainsKey("ROE"));
    }

    [Fact]
    public void Derive_ComputesEpsAndDerivedMetricsFromForecastValues()
    {
        // 株探の会社予想 PER/PBR/利回り + 現在値 から EPS・BPS・配当・性向・MIX を算出
        var row = new Dictionary<string, string>
        {
            ["Price"] = "2694", ["PER"] = "10.6", ["PBR"] = "0.80", ["DividendYield"] = "3.72"
        };
        IndicatorFetchService.Derive(row);

        double D(string k) => double.Parse(row[k], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(254.2, D("EPS"), 1);        // 株価/予想PER
        Assert.Equal(3368, D("BPS"), 0);         // 株価/PBR
        Assert.Equal(8.48, D("MixFactor"), 1);   // PER×PBR
        Assert.Equal(100.2, D("Dividend"), 0);   // 株価×利回り/100
        Assert.Equal(39.4, D("PayoutRatio"), 0); // 配当/EPS×100
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
