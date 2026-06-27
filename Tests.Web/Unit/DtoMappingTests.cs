using DStockAnalysis.Models;
using DStockAnalysis.Web.Models;
using Xunit;

namespace DStockAnalysis.Web.Tests.Unit;

/// <summary>
/// StockSummaryDto.From() が Stock.cs の表示対象指標を漏れなく DTO へ反映することを検証する
/// (一覧・比較で値が画面に出ない不具合の回帰防止)。
/// </summary>
public class DtoMappingTests
{
    [Fact]
    public void From_MapsAllDisplayIndicators()
    {
        var s = new Stock
        {
            // 基本情報
            Code = "7203", Name = "トヨタ", Market = "東証プライム", Sector = "輸送用機器", Scale = "大型",
            Theme = "EV", Description = "自動車大手", FiscalMonth = "3月", IRUrl = "https://example/ir",
            // バリュエーション
            Price = 2700, MarketCap = 14000000, PER = 10.5, PBR = 1.1, ROE = 12.3, ROA = 8.4, MixFactor = 11.55,
            EPS = 250.4, BPS = 3000, OperatingMargin = 8.5, OrdinaryProfitMargin = 9.1, NetProfitMargin = 6.2,
            // 配当・還元
            DividendYield = 3.2, PayoutRatio = 35.0, Dividend = 80, DividendTrend = "安定",
            CumulativeDividend = true, DoeAdopted = true, ConsecutiveDividendYears = 5, DividendCutCount = 1,
            NonDividendCutYears = 6, DividendRemainingYears = 8, BuybackAmount = 300000, ShareholderReturnPolicy = "累進配当",
            DividendGrowth1Y = 5, DividendGrowth3Y = 8, DividendGrowth5Y = 10, DividendGrowth10Y = 12,
            // 株主優待
            HasShareholderBenefit = true, ShareholderBenefit = "QUOカード", BenefitContent = "QUOカード1000円",
            BenefitCategory = "金券", BenefitRightsMonth = "3月", RequiredSharesForBenefit = 100, BenefitValue = 1000,
            BenefitYield = 1.0, TotalYield = 4.2, HasLongTermBenefit = true, LongTermBenefitCondition = "1年以上",
            LongTermBenefitContent = "増額", BenefitRiskMemo = "注意",
            // 財務
            EquityRatio = 45.0, InterestBearingDebtRatio = 30.0,
            // 成長性
            RevenueGrowth1Y = 2.1, RevenueGrowth3Y = 6.5, RevenueGrowth5Y = 10.2, RevenueGrowth10Y = 15.0,
            RevenueGrowthRate = 2.1, AverageRevenueGrowth3Y = 6.5, OperatingProfitGrowthRate = 4,
            OrdinaryProfitGrowthRate = 5, NetProfitGrowthRate = 6, EpsGrowthRate = 7,
            // キャッシュフロー
            OperatingCF = 1000000, InvestingCF = -500000, FinancingCF = 200000, FreeCashFlow = 500000,
            OperatingCashFlowMargin = 12.5,
            // 株価変化
            StockPriceChange3M = 4.5, AverageStockPriceChange3M = 3.2, AveragePrice3M = 2800,
            PriceChange3M = 4.5, PriceChangeAverage3M = 4.1,
            // スコア・ユーザー
            UserInterest = 70, IndicatorsFetched = true,
        };

        var d = StockSummaryDto.From(s);

        // 以前 DTO に無く画面で確認できなかった項目を中心に検証
        Assert.Equal("自動車大手", d.Description);
        Assert.Equal("3月", d.FiscalMonth);
        Assert.Equal("https://example/ir", d.IRUrl);
        Assert.Equal(3000, d.BPS);
        Assert.Equal(8.4, d.ROA);
        Assert.Equal(9.1, d.OrdinaryProfitMargin);
        Assert.Equal(80, d.Dividend);
        Assert.Equal("安定", d.DividendTrend);
        Assert.Equal(1, d.DividendCutCount);
        Assert.Equal(6, d.NonDividendCutYears);
        Assert.Equal(8, d.DividendRemainingYears);
        Assert.Equal(300000, d.BuybackAmount);
        Assert.Equal("累進配当", d.ShareholderReturnPolicy);
        Assert.Equal(5, d.DividendGrowth1Y);
        Assert.Equal(12, d.DividendGrowth10Y);
        Assert.Equal("QUOカード", d.ShareholderBenefit);
        Assert.Equal("QUOカード1000円", d.BenefitContent);
        Assert.Equal(100, d.RequiredSharesForBenefit);
        Assert.Equal(1000, d.BenefitValue);
        Assert.Equal("1年以上", d.LongTermBenefitCondition);
        Assert.Equal("増額", d.LongTermBenefitContent);
        Assert.Equal("注意", d.BenefitRiskMemo);
        Assert.Equal(2.1, d.RevenueGrowth1Y);
        Assert.Equal(10.2, d.RevenueGrowth5Y);
        Assert.Equal(15.0, d.RevenueGrowth10Y);
        Assert.Equal(2.1, d.RevenueGrowthRate);
        Assert.Equal(6.5, d.AverageRevenueGrowth3Y);
        Assert.Equal(5, d.OrdinaryProfitGrowthRate);
        Assert.Equal(6, d.NetProfitGrowthRate);
        Assert.Equal(7, d.EpsGrowthRate);
        Assert.Equal(1000000, d.OperatingCF);
        Assert.Equal(-500000, d.InvestingCF);
        Assert.Equal(200000, d.FinancingCF);
        Assert.Equal(3.2, d.AverageStockPriceChange3M);
        Assert.Equal(2800, d.AveragePrice3M);
        Assert.Equal(4.5, d.PriceChange3M);
        Assert.Equal(4.1, d.PriceChangeAverage3M);
        Assert.Equal(70, d.UserInterest);
        Assert.True(d.HasLongTermBenefit);
        Assert.True(d.DoeAdopted);
    }
}
