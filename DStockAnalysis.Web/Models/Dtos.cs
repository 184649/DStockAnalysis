using DStockAnalysis.Models;

namespace DStockAnalysis.Web.Models;

/// <summary>
/// 一覧(スクリーニング)・比較用の銘柄プロジェクション。
/// Stock.cs の表示対象指標を漏れなく含める(画面で値を確認できるようにするため)。
/// </summary>
public class StockSummaryDto
{
    // ===== 基本情報 =====
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Scale { get; set; } = "";
    public string Theme { get; set; } = "";
    public string Description { get; set; } = "";
    public string FiscalMonth { get; set; } = "";
    public string IRUrl { get; set; } = "";
    public DateTime DataUpdated { get; set; }

    // ===== バリュエーション =====
    public double Price { get; set; }
    public double MarketCap { get; set; }
    public double PER { get; set; }
    public double PBR { get; set; }
    public double ROE { get; set; }
    public double ROA { get; set; }
    public double TotalAssetTurnover { get; set; }
    public double MixFactor { get; set; }
    public double EPS { get; set; }
    public double BPS { get; set; }
    public double OperatingMargin { get; set; }
    public double OrdinaryProfitMargin { get; set; }
    public double NetProfitMargin { get; set; }

    // ===== 配当・株主還元 =====
    public double DividendYield { get; set; }
    public double PayoutRatio { get; set; }
    public double Dividend { get; set; }
    public string DividendTrend { get; set; } = "";
    public bool CumulativeDividend { get; set; }
    public bool DoeAdopted { get; set; }
    public int ConsecutiveDividendYears { get; set; }
    public int DividendCutCount { get; set; }
    public int NonDividendCutYears { get; set; }
    public double DividendRemainingYears { get; set; }
    public double BuybackAmount { get; set; }
    public string ShareholderReturnPolicy { get; set; } = "";
    public double DividendGrowth1Y { get; set; }
    public double DividendGrowth3Y { get; set; }
    public double DividendGrowth5Y { get; set; }
    public double DividendGrowth10Y { get; set; }

    // ===== 株主優待 =====
    public bool HasShareholderBenefit { get; set; }
    public bool BenefitUnknown { get; set; }
    public string ShareholderBenefit { get; set; } = "";
    public string BenefitContent { get; set; } = "";
    public string BenefitCategory { get; set; } = "";
    public string BenefitRightsMonth { get; set; } = "";
    public int RequiredSharesForBenefit { get; set; }
    public double BenefitValue { get; set; }
    public double BenefitYield { get; set; }
    public double TotalYield { get; set; }
    public bool HasLongTermBenefit { get; set; }
    public string LongTermBenefitCondition { get; set; } = "";
    public string LongTermBenefitContent { get; set; } = "";
    public string BenefitRiskMemo { get; set; } = "";

    // ===== 財務 =====
    public double EquityRatio { get; set; }
    public double InterestBearingDebtRatio { get; set; }

    // ===== 成長性 =====
    public double RevenueGrowth1Y { get; set; }
    public double RevenueGrowth3Y { get; set; }
    public double RevenueGrowth5Y { get; set; }
    public double RevenueGrowth10Y { get; set; }
    public double RevenueGrowthRate { get; set; }
    public double AverageRevenueGrowth3Y { get; set; }
    public double OperatingProfitGrowthRate { get; set; }
    public double OrdinaryProfitGrowthRate { get; set; }
    public double NetProfitGrowthRate { get; set; }
    public double EpsGrowthRate { get; set; }

    // ===== キャッシュフロー =====
    public double OperatingCF { get; set; }
    public double InvestingCF { get; set; }
    public double FinancingCF { get; set; }
    public double FreeCashFlow { get; set; }
    public double OperatingCashFlowMargin { get; set; }

    // ===== 株価変化 =====
    public double StockPriceChange3M { get; set; }
    public double AverageStockPriceChange3M { get; set; }
    public double AveragePrice3M { get; set; }
    public double PriceChange3M { get; set; }
    public double PriceChangeAverage3M { get; set; }

    // ===== スコア =====
    public double SafetyScore { get; set; }
    public double GrowthScore { get; set; }
    public double ProfitabilityScore { get; set; }
    public double ReturnScore { get; set; }
    public double EfficiencyScore { get; set; }
    public double ValuationScore { get; set; }
    public double LongTermScore { get; set; }
    public double RevaluationScore { get; set; }
    public double BuffettScore { get; set; }
    public double WantToBuyScore { get; set; }
    public double OverallScore { get; set; }
    public string OverallGrade { get; set; } = "";
    public string JudgementText { get; set; } = "";
    public double UserInterest { get; set; }

    public bool IndicatorsFetched { get; set; }
    public bool Provisional { get; set; }

    public static StockSummaryDto From(Stock s) => new()
    {
        Code = s.Code, Name = s.Name, Market = s.Market, Sector = s.Sector, Scale = s.Scale, Theme = s.Theme,
        Description = s.Description, FiscalMonth = s.FiscalMonth, IRUrl = s.IRUrl, DataUpdated = s.DataUpdated,

        Price = s.Price, MarketCap = s.MarketCap, PER = s.PER, PBR = s.PBR, ROE = s.ROE,
        ROA = s.ROA, TotalAssetTurnover = s.TotalAssetTurnover, MixFactor = s.MixFactor,
        EPS = s.EPS, BPS = s.BPS, OperatingMargin = s.OperatingMargin, OrdinaryProfitMargin = s.OrdinaryProfitMargin,
        NetProfitMargin = s.NetProfitMargin,

        DividendYield = s.DividendYield, PayoutRatio = s.PayoutRatio, Dividend = s.Dividend,
        DividendTrend = s.DividendTrend, CumulativeDividend = s.CumulativeDividend, DoeAdopted = s.DoeAdopted,
        ConsecutiveDividendYears = s.ConsecutiveDividendYears, DividendCutCount = s.DividendCutCount,
        NonDividendCutYears = s.NonDividendCutYears, DividendRemainingYears = s.DividendRemainingYears,
        BuybackAmount = s.BuybackAmount, ShareholderReturnPolicy = s.ShareholderReturnPolicy,
        DividendGrowth1Y = s.DividendGrowth1Y, DividendGrowth3Y = s.DividendGrowth3Y,
        DividendGrowth5Y = s.DividendGrowth5Y, DividendGrowth10Y = s.DividendGrowth10Y,

        HasShareholderBenefit = s.HasShareholderBenefit, BenefitUnknown = s.BenefitUnknown,
        ShareholderBenefit = s.ShareholderBenefit, BenefitContent = s.BenefitContent,
        BenefitCategory = s.BenefitCategory, BenefitRightsMonth = s.BenefitRightsMonth,
        RequiredSharesForBenefit = s.RequiredSharesForBenefit, BenefitValue = s.BenefitValue,
        BenefitYield = s.BenefitYield, TotalYield = s.TotalYield, HasLongTermBenefit = s.HasLongTermBenefit,
        LongTermBenefitCondition = s.LongTermBenefitCondition, LongTermBenefitContent = s.LongTermBenefitContent,
        BenefitRiskMemo = s.BenefitRiskMemo,

        EquityRatio = s.EquityRatio, InterestBearingDebtRatio = s.InterestBearingDebtRatio,

        RevenueGrowth1Y = s.RevenueGrowth1Y, RevenueGrowth3Y = s.RevenueGrowth3Y,
        RevenueGrowth5Y = s.RevenueGrowth5Y, RevenueGrowth10Y = s.RevenueGrowth10Y,
        RevenueGrowthRate = s.RevenueGrowthRate, AverageRevenueGrowth3Y = s.AverageRevenueGrowth3Y,
        OperatingProfitGrowthRate = s.OperatingProfitGrowthRate, OrdinaryProfitGrowthRate = s.OrdinaryProfitGrowthRate,
        NetProfitGrowthRate = s.NetProfitGrowthRate, EpsGrowthRate = s.EpsGrowthRate,

        OperatingCF = s.OperatingCF, InvestingCF = s.InvestingCF, FinancingCF = s.FinancingCF,
        FreeCashFlow = s.FreeCashFlow, OperatingCashFlowMargin = s.OperatingCashFlowMargin,

        StockPriceChange3M = s.StockPriceChange3M, AverageStockPriceChange3M = s.AverageStockPriceChange3M,
        AveragePrice3M = s.AveragePrice3M, PriceChange3M = s.PriceChange3M, PriceChangeAverage3M = s.PriceChangeAverage3M,

        SafetyScore = s.SafetyScore, GrowthScore = s.GrowthScore, ProfitabilityScore = s.ProfitabilityScore,
        ReturnScore = s.ReturnScore, EfficiencyScore = s.EfficiencyScore, ValuationScore = s.ValuationScore,
        LongTermScore = s.LongTermScore, RevaluationScore = s.RevaluationScore, BuffettScore = s.BuffettScore,
        WantToBuyScore = s.WantToBuyScore, OverallScore = s.OverallScore, OverallGrade = s.OverallGrade,
        JudgementText = s.JudgementText, UserInterest = s.UserInterest,

        IndicatorsFetched = s.IndicatorsFetched, Provisional = s.Provisional
    };
}

/// <summary>ユーザーデータ保存リクエスト。</summary>
public class UserDataRequest
{
    public StockMemo? Memo { get; set; }
    public BuffettCheck? BuffettCheck { get; set; }
    public double? UserInterest { get; set; }
}

/// <summary>メタ情報(起動時に一括取得)。</summary>
public class MetaDto
{
    public DateTime? MasterDate { get; set; }
    public int Total { get; set; }
    public int FetchedCount { get; set; }
    public int FullyFetchedCount { get; set; }
    public int UnfetchedCount { get; set; }
    public bool MasterStale { get; set; }
    public List<string> Sectors { get; set; } = new();
    public List<string> Markets { get; set; } = new();
    public List<string> Scales { get; set; } = new();
    public List<string> BenefitCategories { get; set; } = new();
    public List<string> BenefitMonths { get; set; } = new();
    public List<PresetDto> Presets { get; set; } = new();
}

public class PresetDto
{
    public string Name { get; set; } = "";
    public ScreeningCriteria Criteria { get; set; } = new();
}
