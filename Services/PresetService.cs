using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>上部プリセットボタンの定義。押下で ScreeningCriteria を生成する。</summary>
public class PresetService
{
    public List<ScreeningPreset> GetPresets() => new()
    {
        new("高配当で好財務な銘柄", () => new ScreeningCriteria
        {
            DividendYield = new RangeFilter { Min = 3.5 },
            EquityRatio = new RangeFilter { Min = 50 },
            PayoutRatio = new RangeFilter { Max = 70 }
        }),
        new("財務健全", () => new ScreeningCriteria
        {
            EquityRatio = new RangeFilter { Min = 60 },
            InterestBearingDebtRatio = new RangeFilter { Max = 30 }
        }),
        new("高成長銘柄", () => new ScreeningCriteria
        {
            RevenueGrowth3Y = new RangeFilter { Min = 10 },
            OperatingProfitGrowthRate = new RangeFilter { Min = 10 }
        }),
        new("しげなさ投資法", () => new ScreeningCriteria
        {
            ConsecutiveDividendYears = new RangeFilter { Min = 5 },
            DividendYield = new RangeFilter { Min = 3 },
            EquityRatio = new RangeFilter { Min = 50 },
            OperatingMargin = new RangeFilter { Min = 8 }
        }),
        new("下落中の割安バリュー株", () => new ScreeningCriteria
        {
            PBR = new RangeFilter { Max = 1.0 },
            PER = new RangeFilter { Max = 12 },
            StockPriceChange3M = new RangeFilter { Max = 0 }
        }),
        new("その高配当株、減配するかも？", () => new ScreeningCriteria
        {
            DividendYield = new RangeFilter { Min = 4 },
            PayoutRatio = new RangeFilter { Min = 80 }
        }),
        new("増配×成長性 両立銘柄", () => new ScreeningCriteria
        {
            ConsecutiveDividendYears = new RangeFilter { Min = 5 },
            RevenueGrowth3Y = new RangeFilter { Min = 7 }
        }),
        new("有名・優良株 厳選100", () => new ScreeningCriteria
        {
            MarketCap = new RangeFilter { Min = 500000 },
            ROE = new RangeFilter { Min = 10 }
        }),
        new("全指標が高評価の銘柄", () => new ScreeningCriteria
        {
            ROE = new RangeFilter { Min = 12 },
            EquityRatio = new RangeFilter { Min = 50 },
            OperatingMargin = new RangeFilter { Min = 10 },
            RevenueGrowth3Y = new RangeFilter { Min = 5 }
        }),
        new("総合評価が最高評価の銘柄", () => new ScreeningCriteria
        {
            BuffettScore = new RangeFilter { Min = 75 }
        }),
        // 株主優待系
        new("株主優待あり高利回り銘柄", () => new ScreeningCriteria
        {
            BenefitOnly = true,
            BenefitYield = new RangeFilter { Min = 1.5 }
        }),
        new("長期保有優待がある銘柄", () => new ScreeningCriteria
        {
            BenefitOnly = true,
            LongTermBenefitOnly = true
        }),
        new("配当＋優待の総合利回りが高い銘柄", () => new ScreeningCriteria
        {
            BenefitOnly = true,
            TotalYield = new RangeFilter { Min = 4 }
        }),
        new("優待はあるが財務も健全な銘柄", () => new ScreeningCriteria
        {
            BenefitOnly = true,
            EquityRatio = new RangeFilter { Min = 55 },
            OperatingMargin = new RangeFilter { Min = 8 }
        }),
        new("優待廃止リスクに注意したい銘柄", () => new ScreeningCriteria
        {
            BenefitOnly = true,
            HasBenefitRiskMemo = true,
            PayoutRatio = new RangeFilter { Min = 70 }
        }),
    };
}
