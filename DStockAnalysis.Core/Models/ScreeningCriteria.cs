namespace DStockAnalysis.Models;

/// <summary>1つの数値レンジ条件(最小・最大)。null は無指定。</summary>
public class RangeFilter
{
    public double? Min { get; set; }
    public double? Max { get; set; }

    public bool IsEmpty => Min is null && Max is null;

    public bool Matches(double value)
    {
        if (Min.HasValue && value < Min.Value) return false;
        if (Max.HasValue && value > Max.Value) return false;
        return true;
    }
}

/// <summary>
/// スクリーニング条件。ローカル保存・プリセットからの自動入力に対応する。
/// </summary>
public class ScreeningCriteria
{
    // テキスト系
    public string? Sector { get; set; }            // 業種
    public string? Market { get; set; }            // 市場(完全一致・任意)
    public string? Scale { get; set; }             // 規模(完全一致・任意)
    public string? BenefitCategory { get; set; }   // 優待カテゴリ
    public string? BenefitRightsMonth { get; set; }// 優待権利確定月

    // 市場トグル(東証/東証PR/東証GR/東証ST)。null=未指定
    public string? MarketToken { get; set; }
    // 規模トグル(小型/中型/大型)。null=未指定
    public string? ScaleToken { get; set; }

    // フラグ系
    public bool BenefitOnly { get; set; }          // 株主優待ありのみ
    public bool LongTermBenefitOnly { get; set; }  // 長期保有優遇ありのみ
    public bool HasBenefitRiskMemo { get; set; }   // 優待廃止リスクメモあり
    public bool NoDividendCut { get; set; }        // 減配なし(減配回数0)
    public bool CumulativeOnly { get; set; }       // 累進配当のみ
    public bool DoeOnly { get; set; }              // DOE採用のみ

    // 数値レンジ
    public RangeFilter MarketCap { get; set; } = new();
    public RangeFilter Price { get; set; } = new();
    public RangeFilter PER { get; set; } = new();
    public RangeFilter PBR { get; set; } = new();
    public RangeFilter ROE { get; set; } = new();
    public RangeFilter DividendYield { get; set; } = new();
    public RangeFilter PayoutRatio { get; set; } = new();
    public RangeFilter ConsecutiveDividendYears { get; set; } = new();
    public RangeFilter EquityRatio { get; set; } = new();
    public RangeFilter InterestBearingDebtRatio { get; set; } = new();
    public RangeFilter RevenueGrowth1Y { get; set; } = new();
    public RangeFilter RevenueGrowth3Y { get; set; } = new();
    public RangeFilter RevenueGrowth5Y { get; set; } = new();
    public RangeFilter RevenueGrowth10Y { get; set; } = new();
    public RangeFilter OperatingCashFlowMargin { get; set; } = new();
    public RangeFilter OperatingMargin { get; set; } = new();
    public RangeFilter NetProfitMargin { get; set; } = new();
    public RangeFilter EPS { get; set; } = new();
    public RangeFilter RevenueGrowthRate { get; set; } = new();
    public RangeFilter AverageRevenueGrowth3Y { get; set; } = new();
    public RangeFilter OperatingProfitGrowthRate { get; set; } = new();
    public RangeFilter OrdinaryProfitGrowthRate { get; set; } = new();
    public RangeFilter NetProfitGrowthRate { get; set; } = new();
    public RangeFilter StockPriceChange3M { get; set; } = new();
    public RangeFilter AverageStockPriceChange3M { get; set; } = new();
    public RangeFilter BenefitYield { get; set; } = new();
    public RangeFilter TotalYield { get; set; } = new();
    public RangeFilter RequiredShares { get; set; } = new();
    public RangeFilter BuffettScore { get; set; } = new();

    public bool Matches(Stock s)
    {
        if (!string.IsNullOrEmpty(Sector) && s.Sector != Sector) return false;
        if (!string.IsNullOrEmpty(Market) && s.Market != Market) return false;
        if (!string.IsNullOrEmpty(Scale) && s.Scale != Scale) return false;
        if (!string.IsNullOrEmpty(BenefitCategory) && s.BenefitCategory != BenefitCategory) return false;
        if (!string.IsNullOrEmpty(BenefitRightsMonth) && s.BenefitRightsMonth != BenefitRightsMonth) return false;

        if (!MatchesMarketToken(s.Market)) return false;
        if (!string.IsNullOrEmpty(ScaleToken) && s.Scale != ScaleToken) return false;

        if (BenefitOnly && !s.HasShareholderBenefit) return false;
        if (LongTermBenefitOnly && !s.HasLongTermBenefit) return false;
        if (HasBenefitRiskMemo && string.IsNullOrWhiteSpace(s.BenefitRiskMemo)) return false;
        if (NoDividendCut && s.DividendCutCount > 0) return false;
        if (CumulativeOnly && !s.CumulativeDividend) return false;
        if (DoeOnly && !s.DoeAdopted) return false;

        if (!MarketCap.Matches(s.MarketCap)) return false;
        if (!Price.Matches(s.Price)) return false;
        if (!PER.Matches(s.PER)) return false;
        if (!PBR.Matches(s.PBR)) return false;
        if (!ROE.Matches(s.ROE)) return false;
        if (!DividendYield.Matches(s.DividendYield)) return false;
        if (!PayoutRatio.Matches(s.PayoutRatio)) return false;
        if (!ConsecutiveDividendYears.Matches(s.ConsecutiveDividendYears)) return false;
        if (!EquityRatio.Matches(s.EquityRatio)) return false;
        if (!InterestBearingDebtRatio.Matches(s.InterestBearingDebtRatio)) return false;
        if (!RevenueGrowth1Y.Matches(s.RevenueGrowth1Y)) return false;
        if (!RevenueGrowth3Y.Matches(s.RevenueGrowth3Y)) return false;
        if (!RevenueGrowth5Y.Matches(s.RevenueGrowth5Y)) return false;
        if (!RevenueGrowth10Y.Matches(s.RevenueGrowth10Y)) return false;
        if (!OperatingCashFlowMargin.Matches(s.OperatingCashFlowMargin)) return false;
        if (!OperatingMargin.Matches(s.OperatingMargin)) return false;
        if (!NetProfitMargin.Matches(s.NetProfitMargin)) return false;
        if (!EPS.Matches(s.EPS)) return false;
        if (!RevenueGrowthRate.Matches(s.RevenueGrowthRate)) return false;
        if (!AverageRevenueGrowth3Y.Matches(s.AverageRevenueGrowth3Y)) return false;
        if (!OperatingProfitGrowthRate.Matches(s.OperatingProfitGrowthRate)) return false;
        if (!OrdinaryProfitGrowthRate.Matches(s.OrdinaryProfitGrowthRate)) return false;
        if (!NetProfitGrowthRate.Matches(s.NetProfitGrowthRate)) return false;
        if (!StockPriceChange3M.Matches(s.StockPriceChange3M)) return false;
        if (!AverageStockPriceChange3M.Matches(s.AverageStockPriceChange3M)) return false;
        if (!BenefitYield.Matches(s.BenefitYield)) return false;
        if (!TotalYield.Matches(s.TotalYield)) return false;
        if (!RequiredShares.Matches(s.RequiredSharesForBenefit)) return false;
        if (!BuffettScore.Matches(s.BuffettScore)) return false;
        return true;
    }

    /// <summary>市場トグルとの一致判定。"東証"は東証全体、PR/GR/ST は各市場区分。</summary>
    private bool MatchesMarketToken(string market)
    {
        if (string.IsNullOrEmpty(MarketToken)) return true;
        market ??= "";
        return MarketToken switch
        {
            "東証" => market.Contains("東証"),
            "東証PR" => market.Contains("プライム") || market.Contains("PR"),
            "東証GR" => market.Contains("グロース") || market.Contains("GR"),
            "東証ST" => market.Contains("スタンダード") || market.Contains("ST"),
            _ => true
        };
    }
}
