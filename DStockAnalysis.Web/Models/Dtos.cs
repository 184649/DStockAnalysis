using DStockAnalysis.Models;

namespace DStockAnalysis.Web.Models;

/// <summary>一覧(スクリーニング・比較)用の軽量プロジェクション。</summary>
public class StockSummaryDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Scale { get; set; } = "";
    public string Theme { get; set; } = "";
    public double Price { get; set; }
    public double MarketCap { get; set; }
    public double PER { get; set; }
    public double PBR { get; set; }
    public double ROE { get; set; }
    public double MixFactor { get; set; }
    public double EPS { get; set; }
    public double DividendYield { get; set; }
    public double PayoutRatio { get; set; }
    public int ConsecutiveDividendYears { get; set; }
    public bool CumulativeDividend { get; set; }
    public bool DoeAdopted { get; set; }
    public bool HasShareholderBenefit { get; set; }
    public bool BenefitUnknown { get; set; }
    public string BenefitCategory { get; set; } = "";
    public string BenefitRightsMonth { get; set; } = "";
    public double BenefitYield { get; set; }
    public double TotalYield { get; set; }
    public bool HasLongTermBenefit { get; set; }
    public double EquityRatio { get; set; }
    public double InterestBearingDebtRatio { get; set; }
    public double OperatingMargin { get; set; }
    public double NetProfitMargin { get; set; }
    public double OperatingCashFlowMargin { get; set; }
    public double RevenueGrowth3Y { get; set; }
    public double OperatingProfitGrowthRate { get; set; }
    public double FreeCashFlow { get; set; }
    public double StockPriceChange3M { get; set; }
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
    public bool IsSampleIndicators { get; set; }

    public static StockSummaryDto From(Stock s) => new()
    {
        Code = s.Code, Name = s.Name, Market = s.Market, Sector = s.Sector, Scale = s.Scale, Theme = s.Theme,
        Price = s.Price, MarketCap = s.MarketCap, PER = s.PER, PBR = s.PBR, ROE = s.ROE, MixFactor = s.MixFactor,
        EPS = s.EPS, DividendYield = s.DividendYield, PayoutRatio = s.PayoutRatio,
        ConsecutiveDividendYears = s.ConsecutiveDividendYears, CumulativeDividend = s.CumulativeDividend,
        DoeAdopted = s.DoeAdopted, HasShareholderBenefit = s.HasShareholderBenefit,
        BenefitUnknown = s.BenefitUnknown,
        BenefitCategory = s.BenefitCategory, BenefitRightsMonth = s.BenefitRightsMonth,
        BenefitYield = s.BenefitYield, TotalYield = s.TotalYield, HasLongTermBenefit = s.HasLongTermBenefit,
        EquityRatio = s.EquityRatio, InterestBearingDebtRatio = s.InterestBearingDebtRatio,
        OperatingMargin = s.OperatingMargin, NetProfitMargin = s.NetProfitMargin,
        OperatingCashFlowMargin = s.OperatingCashFlowMargin, RevenueGrowth3Y = s.RevenueGrowth3Y,
        OperatingProfitGrowthRate = s.OperatingProfitGrowthRate, FreeCashFlow = s.FreeCashFlow,
        StockPriceChange3M = s.StockPriceChange3M,
        SafetyScore = s.SafetyScore, GrowthScore = s.GrowthScore, ProfitabilityScore = s.ProfitabilityScore,
        ReturnScore = s.ReturnScore, EfficiencyScore = s.EfficiencyScore, ValuationScore = s.ValuationScore,
        LongTermScore = s.LongTermScore, RevaluationScore = s.RevaluationScore, BuffettScore = s.BuffettScore,
        WantToBuyScore = s.WantToBuyScore, OverallScore = s.OverallScore, OverallGrade = s.OverallGrade,
        JudgementText = s.JudgementText, IsSampleIndicators = s.IsSampleIndicators
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
    public int SampleCount { get; set; }
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
