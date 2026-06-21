using DStockAnalysis.Models;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class ScreeningCriteriaTests
{
    [Fact] // UT-CR-01: 空の RangeFilter は全て一致
    public void RangeFilter_Empty_MatchesAll()
    {
        var f = new RangeFilter();
        Assert.True(f.IsEmpty);
        Assert.True(f.Matches(-100));
        Assert.True(f.Matches(0));
        Assert.True(f.Matches(99999));
    }

    [Theory] // UT-CR-02: 最小・最大の境界
    [InlineData(10, 20, 9.99, false)]
    [InlineData(10, 20, 10, true)]
    [InlineData(10, 20, 20, true)]
    [InlineData(10, 20, 20.01, false)]
    public void RangeFilter_Bounds(double min, double max, double value, bool expected)
    {
        var f = new RangeFilter { Min = min, Max = max };
        Assert.Equal(expected, f.Matches(value));
    }

    [Fact] // UT-CR-03: 数値レンジで銘柄を絞り込む
    public void Criteria_NumericRange_Filters()
    {
        var c = new ScreeningCriteria { PER = new RangeFilter { Max = 15 } };
        Assert.True(c.Matches(new Stock { Code = "1", PER = 12 }));
        Assert.False(c.Matches(new Stock { Code = "2", PER = 40 }));
    }

    [Fact] // UT-CR-04: 市場トークン(東証PR は東証プライムに一致)
    public void Criteria_MarketToken_Matches()
    {
        var c = new ScreeningCriteria { MarketToken = "東証PR" };
        Assert.True(c.Matches(new Stock { Code = "1", Market = "東証プライム" }));
        Assert.False(c.Matches(new Stock { Code = "2", Market = "東証グロース" }));
    }

    [Fact] // UT-CR-05: 規模トークン(完全一致)
    public void Criteria_ScaleToken_Matches()
    {
        var c = new ScreeningCriteria { ScaleToken = "大型" };
        Assert.True(c.Matches(new Stock { Code = "1", Scale = "大型" }));
        Assert.False(c.Matches(new Stock { Code = "2", Scale = "小型" }));
    }

    [Fact] // UT-CR-06: 減配なしフラグ
    public void Criteria_NoDividendCut_Filters()
    {
        var c = new ScreeningCriteria { NoDividendCut = true };
        Assert.True(c.Matches(new Stock { Code = "1", DividendCutCount = 0 }));
        Assert.False(c.Matches(new Stock { Code = "2", DividendCutCount = 1 }));
    }

    [Fact] // UT-CR-07: 株主優待ありのみ
    public void Criteria_BenefitOnly_Filters()
    {
        var c = new ScreeningCriteria { BenefitOnly = true };
        Assert.True(c.Matches(new Stock { Code = "1", HasShareholderBenefit = true }));
        Assert.False(c.Matches(new Stock { Code = "2", HasShareholderBenefit = false }));
    }

    [Fact] // UT-CR-08: 複合条件(業種 + 配当利回り)
    public void Criteria_Combined_Filters()
    {
        var c = new ScreeningCriteria
        {
            Sector = "情報・通信業",
            DividendYield = new RangeFilter { Min = 3 }
        };
        Assert.True(c.Matches(new Stock { Code = "1", Sector = "情報・通信業", DividendYield = 3.5 }));
        Assert.False(c.Matches(new Stock { Code = "2", Sector = "情報・通信業", DividendYield = 1.0 }));
        Assert.False(c.Matches(new Stock { Code = "3", Sector = "小売業", DividendYield = 3.5 }));
    }
}
