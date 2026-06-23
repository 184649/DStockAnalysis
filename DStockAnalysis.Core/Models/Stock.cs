using DStockAnalysis.Common;

namespace DStockAnalysis.Models;

/// <summary>
/// 1 銘柄の全データを保持する中心モデル。
/// 基本情報・バリュエーション・配当/株主還元・財務・成長性・キャッシュフロー・株価変化・
/// 株主優待・スコア・時系列・メモ・バフェットチェックを含む。
/// </summary>
public class Stock : ObservableObject
{
    // ===== 基本情報 =====
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = "";        // 市場
    public string Sector { get; set; } = "";        // 業種
    public string Scale { get; set; } = "";         // 規模
    public string Theme { get; set; } = "";         // テーマ
    public string Description { get; set; } = "";   // 企業概要
    public string FiscalMonth { get; set; } = "";   // 決算月
    public string IRUrl { get; set; } = "";         // IRリンク
    public DateTime DataUpdated { get; set; } = DateTime.Today; // データ更新日

    // ===== バリュエーション =====
    public double Price { get; set; }               // 株価
    public double MarketCap { get; set; }           // 時価総額(百万円)
    public double PER { get; set; }
    public double PBR { get; set; }
    public double ROE { get; set; }                 // %
    public double MixFactor { get; set; }           // MIX係数 (PER×PBR)
    public double EPS { get; set; }
    public double BPS { get; set; }
    public double OperatingMargin { get; set; }     // 営業利益率 %
    public double OrdinaryProfitMargin { get; set; }// 経常利益率 %
    public double NetProfitMargin { get; set; }     // 純利益率 %

    // ===== 配当・株主還元 =====
    public double DividendYield { get; set; }       // 配当利回り %
    public double PayoutRatio { get; set; }         // 配当性向 %
    public double Dividend { get; set; }            // 1株配当
    public string DividendTrend { get; set; } = ""; // 配当傾向
    public bool CumulativeDividend { get; set; }    // 累進配当
    public bool DoeAdopted { get; set; }            // DOE採用
    public int ConsecutiveDividendYears { get; set; } // 連続増配年数
    public int DividendCutCount { get; set; }       // 減配回数
    public int NonDividendCutYears { get; set; }    // 非減配年数
    public double DividendRemainingYears { get; set; } // 配当金残年数
    public double BuybackAmount { get; set; }        // 自社株買い額(百万円)
    public string ShareholderReturnPolicy { get; set; } = ""; // 株主還元方針

    // 増配率
    public double DividendGrowth1Y { get; set; }
    public double DividendGrowth3Y { get; set; }
    public double DividendGrowth5Y { get; set; }
    public double DividendGrowth10Y { get; set; }

    // ===== 株主優待 =====
    public bool HasShareholderBenefit { get; set; }                // 株主優待の有無
    public string ShareholderBenefit { get; set; } = "";           // 株主優待(短縮表記)
    public string BenefitContent { get; set; } = "";               // 優待内容(全文)
    public string BenefitCategory { get; set; } = "";              // 優待カテゴリ
    public string BenefitRightsMonth { get; set; } = "";           // 優待権利確定月
    public int RequiredSharesForBenefit { get; set; }              // 必要株数
    public double BenefitValue { get; set; }                       // 優待価値(円)
    public double BenefitYield { get; set; }                       // 優待利回り %
    public double TotalYield { get; set; }                         // 総合利回り %(配当+優待)
    public bool HasLongTermBenefit { get; set; }                   // 長期保有優遇の有無
    public string LongTermBenefitCondition { get; set; } = "";     // 継続保有年数条件
    public string LongTermBenefitContent { get; set; } = "";       // 長期保有優遇内容
    public string BenefitRiskMemo { get; set; } = "";              // 優待廃止リスクメモ

    // ===== 財務 =====
    public double EquityRatio { get; set; }                  // 自己資本比率 %
    public double InterestBearingDebtRatio { get; set; }     // 有利子負債比率 %

    // ===== 成長性 =====
    public double RevenueGrowth1Y { get; set; }       // 増収率 1年
    public double RevenueGrowth3Y { get; set; }       // 増収率 3年
    public double RevenueGrowth5Y { get; set; }       // 増収率 5年
    public double RevenueGrowth10Y { get; set; }      // 増収率 10年
    public double RevenueGrowthRate { get; set; }     // 売上高成長率
    public double AverageRevenueGrowth3Y { get; set; }// 過去3年平均売上高成長率
    public double OperatingProfitGrowthRate { get; set; } // 営業利益成長率
    public double OrdinaryProfitGrowthRate { get; set; }  // 経常利益成長率
    public double NetProfitGrowthRate { get; set; }   // 純利益成長率
    public double EpsGrowthRate { get; set; }         // EPS成長率

    // ===== キャッシュフロー =====
    public double OperatingCF { get; set; }           // 営業CF
    public double InvestingCF { get; set; }           // 投資CF
    public double FinancingCF { get; set; }           // 財務CF
    public double FreeCashFlow { get; set; }          // フリーCF
    public double OperatingCashFlowMargin { get; set; } // 営業CFマージン %

    // ===== 株価変化 =====
    public double StockPriceChange3M { get; set; }        // 直近3ヶ月株価変化率 %
    public double AverageStockPriceChange3M { get; set; } // 直近3ヶ月平均株価変化率 %
    public double AveragePrice3M { get; set; }            // 直近3ヶ月平均株価
    public double PriceChange3M { get; set; }             // 直近3ヶ月株価変化率(別系列) %
    public double PriceChangeAverage3M { get; set; }      // 直近3ヶ月平均株価変化率(別系列) %

    // ===== スコア(0-100) =====
    private double _safetyScore;
    public double SafetyScore { get => _safetyScore; set => SetProperty(ref _safetyScore, value); }

    private double _growthScore;
    public double GrowthScore { get => _growthScore; set => SetProperty(ref _growthScore, value); }

    private double _profitabilityScore;
    public double ProfitabilityScore { get => _profitabilityScore; set => SetProperty(ref _profitabilityScore, value); }

    private double _returnScore;
    public double ReturnScore { get => _returnScore; set => SetProperty(ref _returnScore, value); }

    private double _efficiencyScore;
    public double EfficiencyScore { get => _efficiencyScore; set => SetProperty(ref _efficiencyScore, value); }

    private double _valuationScore;
    public double ValuationScore { get => _valuationScore; set => SetProperty(ref _valuationScore, value); }

    private double _longTermScore;
    public double LongTermScore { get => _longTermScore; set => SetProperty(ref _longTermScore, value); }

    private double _revaluationScore;
    public double RevaluationScore { get => _revaluationScore; set => SetProperty(ref _revaluationScore, value); }

    private double _buffettScore;
    public double BuffettScore { get => _buffettScore; set => SetProperty(ref _buffettScore, value); }

    private double _wantToBuyScore;
    public double WantToBuyScore { get => _wantToBuyScore; set => SetProperty(ref _wantToBuyScore, value); }

    private double _overallScore;
    public double OverallScore { get => _overallScore; set => SetProperty(ref _overallScore, value); }

    private OverallJudgement _judgement;
    public OverallJudgement Judgement { get => _judgement; set => SetProperty(ref _judgement, value); }

    /// <summary>指標値が擬似(サンプル)生成かどうか。CSV/API で実データに置換すると false。
    /// (Web 版では擬似生成を行わないため常に false。WPF 版のローカル単独動作用)</summary>
    public bool IsSampleIndicators { get; set; }

    /// <summary>実データ(株価・財務指標)を取得済みかどうか。Web 版では未取得時 false。
    /// CSV 取込・自動取得で実データを入れると true。未取得の銘柄は画面で「未取得」と表示する。</summary>
    public bool IndicatorsFetched { get; set; }

    /// <summary>株主優待情報が未取得(自動取得では取得できないため不明)かどうか。
    /// 実データ取得時に擬似優待を消して true にする。CSV で優待列を取り込むと false。</summary>
    public bool BenefitUnknown { get; set; }

    private double _userInterest = 50; // 自分の興味(0-100)。買いたい度に影響。
    public double UserInterest { get => _userInterest; set { if (SetProperty(ref _userInterest, value)) OnPropertyChanged(nameof(JudgementText)); } }

    // ===== ぶら下がりデータ =====
    public List<TimeSeriesPoint> History { get; set; } = new();
    public StockMemo Memo { get; set; } = new();
    public BuffettCheck BuffettCheck { get; set; } = new();

    /// <summary>
    /// CSV 取込銘柄(src)の属性・指標を本インスタンスへ取り込む。
    /// cols を指定すると、その列名に対応する項目のみ上書きする(部分CSV対応)。null なら全項目。
    /// Memo / BuffettCheck / UserInterest / History / スコアは引き継がない(別管理・再計算)。
    /// </summary>
    public void CopyIndicatorsFrom(Stock src, ISet<string>? cols = null)
    {
        bool H(params string[] names) => cols == null || names.Any(cols.Contains);

        if (H("Name")) Name = src.Name;
        if (H("Market")) Market = src.Market;
        if (H("Sector")) Sector = src.Sector;
        if (H("Scale")) Scale = src.Scale;
        if (H("Theme")) Theme = src.Theme;
        if (H("Description")) Description = src.Description;
        if (H("FiscalMonth")) FiscalMonth = src.FiscalMonth;
        if (H("IRUrl")) IRUrl = src.IRUrl;
        if (H("DataUpdated")) DataUpdated = src.DataUpdated;
        if (H("Price")) Price = src.Price;
        if (H("MarketCap")) MarketCap = src.MarketCap;
        if (H("PER")) PER = src.PER;
        if (H("PBR")) PBR = src.PBR;
        if (H("ROE")) ROE = src.ROE;
        if (H("MixFactor")) MixFactor = src.MixFactor;
        if (H("EPS")) EPS = src.EPS;
        if (H("BPS")) BPS = src.BPS;
        if (H("OperatingMargin")) OperatingMargin = src.OperatingMargin;
        if (H("OrdinaryProfitMargin")) OrdinaryProfitMargin = src.OrdinaryProfitMargin;
        if (H("NetProfitMargin")) NetProfitMargin = src.NetProfitMargin;
        if (H("DividendYield")) DividendYield = src.DividendYield;
        if (H("PayoutRatio")) PayoutRatio = src.PayoutRatio;
        if (H("Dividend")) Dividend = src.Dividend;
        if (H("DividendTrend")) DividendTrend = src.DividendTrend;
        if (H("CumulativeDividend")) CumulativeDividend = src.CumulativeDividend;
        if (H("DoeAdopted")) DoeAdopted = src.DoeAdopted;
        if (H("ConsecutiveDividendYears")) ConsecutiveDividendYears = src.ConsecutiveDividendYears;
        if (H("DividendCutCount")) DividendCutCount = src.DividendCutCount;
        if (H("NonDividendCutYears")) NonDividendCutYears = src.NonDividendCutYears;
        if (H("DividendRemainingYears")) DividendRemainingYears = src.DividendRemainingYears;
        if (H("BuybackAmount")) BuybackAmount = src.BuybackAmount;
        if (H("ShareholderReturnPolicy")) ShareholderReturnPolicy = src.ShareholderReturnPolicy;
        if (H("DividendGrowth1Y")) DividendGrowth1Y = src.DividendGrowth1Y;
        if (H("DividendGrowth3Y")) DividendGrowth3Y = src.DividendGrowth3Y;
        if (H("DividendGrowth5Y")) DividendGrowth5Y = src.DividendGrowth5Y;
        if (H("DividendGrowth10Y")) DividendGrowth10Y = src.DividendGrowth10Y;
        bool benefitProvided = H("HasShareholderBenefit", "ShareholderBenefit", "BenefitContent", "BenefitCategory", "BenefitYield", "BenefitValue");
        if (H("HasShareholderBenefit", "ShareholderBenefit", "BenefitContent")) HasShareholderBenefit = src.HasShareholderBenefit;
        if (H("ShareholderBenefit")) ShareholderBenefit = src.ShareholderBenefit;
        if (H("BenefitContent")) BenefitContent = src.BenefitContent;
        if (H("BenefitCategory")) BenefitCategory = src.BenefitCategory;
        if (H("BenefitRightsMonth")) BenefitRightsMonth = src.BenefitRightsMonth;
        if (H("RequiredSharesForBenefit")) RequiredSharesForBenefit = src.RequiredSharesForBenefit;
        if (H("BenefitValue")) BenefitValue = src.BenefitValue;
        if (H("BenefitYield")) BenefitYield = src.BenefitYield;
        if (H("TotalYield")) TotalYield = src.TotalYield;
        if (H("HasLongTermBenefit")) HasLongTermBenefit = src.HasLongTermBenefit;
        if (H("LongTermBenefitCondition")) LongTermBenefitCondition = src.LongTermBenefitCondition;
        if (H("LongTermBenefitContent")) LongTermBenefitContent = src.LongTermBenefitContent;
        if (H("BenefitRiskMemo")) BenefitRiskMemo = src.BenefitRiskMemo;
        if (H("EquityRatio")) EquityRatio = src.EquityRatio;
        if (H("InterestBearingDebtRatio")) InterestBearingDebtRatio = src.InterestBearingDebtRatio;
        if (H("RevenueGrowth1Y")) RevenueGrowth1Y = src.RevenueGrowth1Y;
        if (H("RevenueGrowth3Y")) RevenueGrowth3Y = src.RevenueGrowth3Y;
        if (H("RevenueGrowth5Y")) RevenueGrowth5Y = src.RevenueGrowth5Y;
        if (H("RevenueGrowth10Y")) RevenueGrowth10Y = src.RevenueGrowth10Y;
        if (H("RevenueGrowthRate")) RevenueGrowthRate = src.RevenueGrowthRate;
        if (H("AverageRevenueGrowth3Y")) AverageRevenueGrowth3Y = src.AverageRevenueGrowth3Y;
        if (H("OperatingProfitGrowthRate")) OperatingProfitGrowthRate = src.OperatingProfitGrowthRate;
        if (H("OrdinaryProfitGrowthRate")) OrdinaryProfitGrowthRate = src.OrdinaryProfitGrowthRate;
        if (H("NetProfitGrowthRate")) NetProfitGrowthRate = src.NetProfitGrowthRate;
        if (H("EpsGrowthRate")) EpsGrowthRate = src.EpsGrowthRate;
        if (H("OperatingCF")) OperatingCF = src.OperatingCF;
        if (H("InvestingCF")) InvestingCF = src.InvestingCF;
        if (H("FinancingCF")) FinancingCF = src.FinancingCF;
        if (H("FreeCashFlow", "FreeCF")) FreeCashFlow = src.FreeCashFlow;
        if (H("OperatingCashFlowMargin")) OperatingCashFlowMargin = src.OperatingCashFlowMargin;
        if (H("StockPriceChange3M")) StockPriceChange3M = src.StockPriceChange3M;
        if (H("AverageStockPriceChange3M")) AverageStockPriceChange3M = src.AverageStockPriceChange3M;
        if (H("AveragePrice3M", "AverageStockPrice3M")) AveragePrice3M = src.AveragePrice3M;
        if (H("PriceChange3M")) PriceChange3M = src.PriceChange3M;
        if (H("PriceChangeAverage3M")) PriceChangeAverage3M = src.PriceChangeAverage3M;

        IsSampleIndicators = false; // 実データで上書き
        IndicatorsFetched = true;   // 実データ取得済み
        if (benefitProvided) BenefitUnknown = false; // CSV で優待列が来たら実データ優待
        History.Clear();           // 時系列は選択時に再生成
    }

    /// <summary>指標(数値)・株主優待・時系列・スコアをすべて未取得状態(0/空)に戻す。
    /// 擬似値を持たない「未取得」状態を作るために使う。基本情報(コード・銘柄名・市場等)は保持。</summary>
    public void ClearIndicators()
    {
        Price = MarketCap = PER = PBR = ROE = MixFactor = EPS = BPS = 0;
        OperatingMargin = OrdinaryProfitMargin = NetProfitMargin = 0;
        DividendYield = PayoutRatio = Dividend = DividendRemainingYears = BuybackAmount = 0;
        DividendTrend = ShareholderReturnPolicy = "";
        CumulativeDividend = DoeAdopted = false;
        ConsecutiveDividendYears = DividendCutCount = NonDividendCutYears = 0;
        DividendGrowth1Y = DividendGrowth3Y = DividendGrowth5Y = DividendGrowth10Y = 0;
        EquityRatio = InterestBearingDebtRatio = 0;
        RevenueGrowth1Y = RevenueGrowth3Y = RevenueGrowth5Y = RevenueGrowth10Y = 0;
        RevenueGrowthRate = AverageRevenueGrowth3Y = 0;
        OperatingProfitGrowthRate = OrdinaryProfitGrowthRate = NetProfitGrowthRate = EpsGrowthRate = 0;
        OperatingCF = InvestingCF = FinancingCF = FreeCashFlow = OperatingCashFlowMargin = 0;
        StockPriceChange3M = AverageStockPriceChange3M = AveragePrice3M = PriceChange3M = PriceChangeAverage3M = 0;
        SafetyScore = GrowthScore = ProfitabilityScore = ReturnScore = EfficiencyScore = ValuationScore = 0;
        LongTermScore = RevaluationScore = BuffettScore = WantToBuyScore = OverallScore = 0;
        Judgement = OverallJudgement.調査中;
        // 株主優待(擬似)も消す
        HasShareholderBenefit = HasLongTermBenefit = false;
        ShareholderBenefit = BenefitContent = BenefitCategory = BenefitRightsMonth = "";
        LongTermBenefitCondition = LongTermBenefitContent = BenefitRiskMemo = "";
        RequiredSharesForBenefit = 0;
        BenefitValue = BenefitYield = TotalYield = 0;
        IsSampleIndicators = false;
        IndicatorsFetched = false;
        BenefitUnknown = true;
        History.Clear();
    }

    /// <summary>総合判定の表示文字列。</summary>
    public string JudgementText => Judgement.ToString().Replace('_', '・');

    /// <summary>総合評価のレターグレード(S/A/B/C/D)。</summary>
    public string OverallGrade => Common.Grades.Letter(OverallScore);

    /// <summary>各レーダー軸のレターグレード。</summary>
    public string SafetyGrade => Common.Grades.Letter(SafetyScore);
    public string GrowthGrade => Common.Grades.Letter(GrowthScore);
    public string ProfitabilityGrade => Common.Grades.Letter(ProfitabilityScore);
    public string ReturnGrade => Common.Grades.Letter(ReturnScore);
    public string EfficiencyGrade => Common.Grades.Letter(EfficiencyScore);
    public string ValuationGrade => Common.Grades.Letter(ValuationScore);
}
