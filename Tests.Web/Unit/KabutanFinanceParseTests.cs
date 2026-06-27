using System.Globalization;
using DStockAnalysis.Web.Services;
using Xunit;

namespace DStockAnalysis.Web.Tests.Unit;

/// <summary>
/// 株探 業績・財務ページのパーサ(ParseKabutanFinance)の検証。
/// 保存済み HTML(Fixtures/kabutan_finance_8001.html=実ページの4表)を使い、外部アクセスせずに
/// 利益率・ROE/ROA・各CF・自己資本比率・成長率が正しく抽出されることを確認する。
/// これにより「サイトから取得できるのにアプリで取得できていない」回帰を防ぐ。
/// </summary>
public class KabutanFinanceParseTests
{
    private static Dictionary<string, string> Parse()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "kabutan_finance_8001.html");
        var html = File.ReadAllText(path);
        return IndicatorFetchService.ParseKabutanFinance(html, "8001");
    }

    private static double D(Dictionary<string, string> d, string key)
    {
        Assert.True(d.ContainsKey(key), $"{key} が取得できていない");
        return double.Parse(d[key], CultureInfo.InvariantCulture);
    }

    [Fact] // 収益性表: 売上営業利益率/ROE/ROA/総資産回転率(最新実績 2026.03)
    public void Profitability_FromProfitTable()
    {
        var d = Parse();
        Assert.Equal(4.74, D(d, "OperatingMargin"), 2);
        Assert.Equal(14.59, D(d, "ROE"), 2);
        Assert.Equal(5.65, D(d, "ROA"), 2);
        Assert.Equal(0.93, D(d, "TotalAssetTurnover"), 2);
    }

    [Fact] // 通期業績表: 経常/純利益率は売上で按分して算出
    public void Margins_ComputedFromResultTable()
    {
        var d = Parse();
        Assert.Equal(1_199_466.0 / 14_823_087.0 * 100, D(d, "OrdinaryProfitMargin"), 1);
        Assert.Equal(900_283.0 / 14_823_087.0 * 100, D(d, "NetProfitMargin"), 1);
    }

    [Fact] // 成長率: 直近2期(2026.03 対 2025.03)の前年比
    public void GrowthRates_YoY()
    {
        var d = Parse();
        Assert.Equal((14_823_087.0 - 14_724_234.0) / 14_724_234.0 * 100, D(d, "RevenueGrowthRate"), 1);
        Assert.Equal((900_283.0 - 880_251.0) / 880_251.0 * 100, D(d, "NetProfitGrowthRate"), 1);
        Assert.Equal((128.0 - 123.1) / 123.1 * 100, D(d, "EpsGrowthRate"), 1);
        Assert.True(D(d, "RevenueGrowth3Y") > 0); // 3年CAGR(実績4期から)
    }

    [Fact] // CF表: フリー/営業/投資/財務CF(最新実績 2026.03・百万円)
    public void CashFlows_FromCfTable()
    {
        var d = Parse();
        Assert.Equal(742_965, D(d, "FreeCashFlow"), 0);
        Assert.Equal(1_131_837, D(d, "OperatingCF"), 0);
        Assert.Equal(-388_872, D(d, "InvestingCF"), 0);
        Assert.Equal(-726_477, D(d, "FinancingCF"), 0);
        Assert.True(D(d, "OperatingCashFlowMargin") > 0); // 営業CF/売上
    }

    [Fact] // 財務表: 自己資本比率・有利子負債比率(倍率×100)
    public void BalanceSheet_FromBsTable()
    {
        var d = Parse();
        Assert.Equal(39.4, D(d, "EquityRatio"), 1);
        Assert.Equal(72.0, D(d, "InterestBearingDebtRatio"), 1); // 0.72倍 → 72%
    }

    [Fact] // 1株配の実績系列(28→32→40→42)から 増配率・連続増配・減配回数・配当傾向 を算出
    public void DividendHistory_DerivedFromResultTable()
    {
        var d = Parse();
        Assert.Equal(5.0, D(d, "DividendGrowth1Y"), 1);                 // (42-40)/40
        Assert.Equal(3, (int)D(d, "ConsecutiveDividendYears"));         // 28<32<40<42 = 3年連続増配
        Assert.Equal(0, (int)D(d, "DividendCutCount"));                 // 減配なし
        Assert.True(D(d, "DividendGrowth3Y") > 0);                      // 3年CAGR
        Assert.Equal("連続増配", d["DividendTrend"]);
    }
}
