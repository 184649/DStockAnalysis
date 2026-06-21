using DStockAnalysis.Models;

namespace DStockAnalysis.Tests;

/// <summary>テスト用の銘柄ファクトリ。</summary>
internal static class TestData
{
    /// <summary>健全・優良寄りの基準銘柄。</summary>
    public static Stock Good() => new()
    {
        Code = "0001", Name = "テスト優良", Market = "東証プライム", Sector = "情報・通信業", Scale = "大型",
        Theme = "AI", Description = "テスト",
        Price = 3000, MarketCap = 1000000, PER = 12, PBR = 1.2, ROE = 15, MixFactor = 14.4,
        EPS = 250, BPS = 2500, OperatingMargin = 18, OrdinaryProfitMargin = 18, NetProfitMargin = 12,
        DividendYield = 3.2, PayoutRatio = 40, Dividend = 100, ConsecutiveDividendYears = 10,
        DividendCutCount = 0, NonDividendCutYears = 12, DividendRemainingYears = 8, BuybackAmount = 50000,
        CumulativeDividend = true, DoeAdopted = false,
        EquityRatio = 60, InterestBearingDebtRatio = 15,
        RevenueGrowth1Y = 8, RevenueGrowth3Y = 10, RevenueGrowth5Y = 9, RevenueGrowth10Y = 8,
        RevenueGrowthRate = 8, AverageRevenueGrowth3Y = 10, OperatingProfitGrowthRate = 12,
        OrdinaryProfitGrowthRate = 11, NetProfitGrowthRate = 11, EpsGrowthRate = 11,
        OperatingCF = 200000, InvestingCF = -80000, FinancingCF = -50000, FreeCashFlow = 120000,
        OperatingCashFlowMargin = 16,
        StockPriceChange3M = 5, AverageStockPriceChange3M = 4,
        HasShareholderBenefit = true, ShareholderBenefit = "QUOカード", BenefitContent = "QUOカード1000円",
        BenefitCategory = "QUOカード", BenefitRightsMonth = "3月", RequiredSharesForBenefit = 100,
        BenefitValue = 1000, BenefitYield = 0.3, TotalYield = 3.5, HasLongTermBenefit = true,
        LongTermBenefitCondition = "1年以上"
    };

    /// <summary>業績不振・高配当性向の注意銘柄。</summary>
    public static Stock Weak() => new()
    {
        Code = "0002", Name = "テスト不振", Market = "東証スタンダード", Sector = "小売業", Scale = "中型",
        Price = 500, MarketCap = 50000, PER = 40, PBR = 0.6, ROE = 2, MixFactor = 24,
        EPS = 12, OperatingMargin = 2, NetProfitMargin = 1.5,
        DividendYield = 5.0, PayoutRatio = 95, Dividend = 11, ConsecutiveDividendYears = 0,
        DividendCutCount = 2, NonDividendCutYears = 1, DividendRemainingYears = 0,
        EquityRatio = 25, InterestBearingDebtRatio = 80,
        RevenueGrowth1Y = -3, RevenueGrowth3Y = -2, RevenueGrowthRate = -3,
        OperatingProfitGrowthRate = -10, NetProfitGrowthRate = -12, EpsGrowthRate = -12,
        OperatingCF = 3000, InvestingCF = -1000, FinancingCF = -2000, FreeCashFlow = -2000,
        OperatingCashFlowMargin = 2, StockPriceChange3M = -15,
        HasShareholderBenefit = true, BenefitYield = 3.0, TotalYield = 8.0
    };
}
