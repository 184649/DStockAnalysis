using System.Globalization;
using DStockAnalysis.Web.Services;
using Xunit;

namespace DStockAnalysis.Web.Tests.Unit;

/// <summary>
/// IR BANK 配当ページのパーサ(ParseIrBankDividend)の検証(ネットワークアクセスなし)。
/// 「自己株式の取得」(自社株買い)の直近額を百万円へ換算できることを確認する。
/// </summary>
public class IrBankParseTests
{
    [Fact]
    public void ParseIrBankDividend_LatestBuyback_InMillionYen()
    {
        var html =
            "<div id=\"g_5\"><h2>自己株式の取得</h2><dl class=\"gdl\">" +
            "<dt>2023年3月</dt><dd><span class=\"ratio\"></span><span class=\"text\">500億</span></dd>" +
            "<dt>2024年3月</dt><dd><span class=\"ratio\"></span><span class=\"text\">1006億9700万</span></dd>" +
            "</dl></div>";
        var d = IndicatorFetchService.ParseIrBankDividend(html, "8001");
        Assert.True(d.ContainsKey("BuybackAmount"));
        // 1006億9700万円 = 100,697 百万円(直近年度)
        Assert.Equal(100697, double.Parse(d["BuybackAmount"], CultureInfo.InvariantCulture), 0);
    }

    [Fact]
    public void ParseIrBankDividend_NoBlock_ReturnsEmpty()
    {
        var d = IndicatorFetchService.ParseIrBankDividend("<div>無関係</div>", "9999");
        Assert.False(d.ContainsKey("BuybackAmount"));
    }
}
