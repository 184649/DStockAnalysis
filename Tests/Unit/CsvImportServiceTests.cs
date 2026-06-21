using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class CsvImportServiceTests
{
    private readonly CsvImportService _csv = new();

    [Fact] // UT-CSV-01: 基本的なマッピング
    public void Parse_MapsBasicColumns()
    {
        var content = "Code,Name,Market,Price,PER,DividendYield\n" +
                      "7203,トヨタ,東証プライム,3000,10.5,2.6\n";
        var list = _csv.Parse(content);
        var s = Assert.Single(list);
        Assert.Equal("7203", s.Code);
        Assert.Equal("トヨタ", s.Name);
        Assert.Equal(3000, s.Price);
        Assert.Equal(10.5, s.PER);
        Assert.Equal(2.6, s.DividendYield);
    }

    [Fact] // UT-CSV-02: 列順は不問
    public void Parse_IsColumnOrderIndependent()
    {
        var content = "DividendYield,Name,PER,Code,Price\n2.6,トヨタ,10.5,7203,3000\n";
        var s = Assert.Single(_csv.Parse(content));
        Assert.Equal("7203", s.Code);
        Assert.Equal(2.6, s.DividendYield);
        Assert.Equal(3000, s.Price);
    }

    [Fact] // UT-CSV-03: 欠損列は 0 / 空文字
    public void Parse_MissingColumns_DefaultToZeroOrEmpty()
    {
        var s = Assert.Single(_csv.Parse("Code,Name\n9999,テスト\n"));
        Assert.Equal(0, s.PER);
        Assert.Equal("", s.Market);
        Assert.Equal(0, s.ROE);
    }

    [Fact] // UT-CSV-04: ダブルクォート内のカンマを保持
    public void Parse_QuotedComma_Preserved()
    {
        var content = "Code,Name,Description\n1301,極洋,\"水産物貿易・加工、買い付けが主力\"\n";
        var s = Assert.Single(_csv.Parse(content));
        Assert.Equal("水産物貿易・加工、買い付けが主力", s.Description);
    }

    [Theory] // UT-CSV-05: 各種真偽表記の解釈
    [InlineData("○", true)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("あり", true)]
    [InlineData("-", false)]
    [InlineData("0", false)]
    public void Parse_BoolVariants(string raw, bool expected)
    {
        var s = Assert.Single(_csv.Parse($"Code,CumulativeDividend\n1,{raw}\n"));
        Assert.Equal(expected, s.CumulativeDividend);
    }

    [Fact] // UT-CSV-06: FreeCF エイリアスを FreeCashFlow に取り込む
    public void Parse_FreeCfAlias()
    {
        var s = Assert.Single(_csv.Parse("Code,FreeCF\n1,123456\n"));
        Assert.Equal(123456, s.FreeCashFlow);
    }

    [Fact] // UT-CSV-07: 総合利回り未指定なら配当+優待で補完
    public void Parse_TotalYield_Complemented()
    {
        var s = Assert.Single(_csv.Parse("Code,DividendYield,BenefitYield\n1,3.0,1.5\n"));
        Assert.Equal(4.5, s.TotalYield, 3);
    }

    [Fact] // UT-CSV-08: MIX係数未指定なら PER×PBR で補完
    public void Parse_MixFactor_Complemented()
    {
        var s = Assert.Single(_csv.Parse("Code,PER,PBR\n1,10,1.5\n"));
        Assert.Equal(15.0, s.MixFactor, 3);
    }

    [Fact] // UT-CSV-09: Code 空行・空行はスキップ
    public void Parse_SkipsRowsWithoutCode()
    {
        var content = "Code,Name\n,名無し\n7203,トヨタ\n\n";
        var s = Assert.Single(_csv.Parse(content));
        Assert.Equal("7203", s.Code);
    }

    [Fact] // UT-CSV-10: カンマ区切り数値・%・円付き数値の解釈
    public void Parse_NumberWithSeparatorsAndUnits()
    {
        var s = Assert.Single(_csv.Parse("Code,MarketCap,ROE\n1,\"1,000,000\",15.5%\n"));
        Assert.Equal(1000000, s.MarketCap);
        Assert.Equal(15.5, s.ROE);
    }
}
