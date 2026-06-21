using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// サンプル銘柄データを生成する。CSV が無い初回起動時のデモ用。
/// 時系列データ(History)も併せて生成し、グラフ表示を確認できるようにする。
/// </summary>
public class SampleDataService
{
    public List<Stock> CreateSampleStocks()
    {
        var list = new List<Stock>
        {
            Make("7203", "トヨタ自動車", "東証プライム", "輸送用機器", "大型", "EV・自動運転",
                "世界最大級の自動車メーカー。HV・EV・水素まで幅広く展開。",
                price: 3050, cap: 49000000, per: 10.5, pbr: 1.1, roe: 11.5, dy: 2.6, payout: 28,
                equity: 38, debt: 55, om: 9.8, npm: 8.2, rg1: 4.2, rg3: 6.1, rg5: 5.0,
                fcf: 1800000, cfm: 9.0, ocf: 3700000, icf: -2500000, fcfin: -900000,
                conYears: 3, eps: 290, fiscal: "3月", ir: "https://global.toyota/jp/ir/",
                benefit: false),

            Make("8058", "三菱商事", "東証プライム", "卸売業", "大型", "資源・脱炭素",
                "総合商社最大手。資源から食品・インフラまで多角化。連続増配と自社株買いに積極的。",
                price: 2900, cap: 11800000, per: 11.0, pbr: 1.2, roe: 13.0, dy: 3.4, payout: 35,
                equity: 36, debt: 60, om: 6.5, npm: 6.0, rg1: 3.0, rg3: 7.5, rg5: 6.0,
                fcf: 600000, cfm: 8.0, ocf: 900000, icf: -300000, fcfin: -400000,
                conYears: 9, eps: 260, fiscal: "3月", ir: "https://www.mitsubishicorp.com/jp/ja/ir/",
                buyback: 300000, cumulative: true,
                benefit: false),

            Make("9433", "KDDI", "東証プライム", "情報・通信業", "大型", "5G・DX",
                "総合通信大手。通信に加え金融・DXへ拡大。23期連続増配の代表的累進配当銘柄。",
                price: 4800, cap: 10500000, per: 14.5, pbr: 1.9, roe: 13.5, dy: 3.0, payout: 44,
                equity: 45, debt: 35, om: 18.5, npm: 11.5, rg1: 3.5, rg3: 4.0, rg5: 4.2,
                fcf: 500000, cfm: 16.0, ocf: 1300000, icf: -700000, fcfin: -600000,
                conYears: 22, eps: 330, fiscal: "3月", ir: "https://www.kddi.com/corporate/ir/",
                buyback: 250000, cumulative: true, doe: false,
                benefit: true, benefitName: "auPAYギフト", benefitCat: "ポイント", benefitMonth: "3月",
                reqShares: 100, benefitValue: 1000, benefitYield: 0.2, longTerm: true,
                longTermCond: "5年以上", longTermContent: "5年以上保有で増額",
                benefitRisk: "通信主力で優待依存度は低く廃止リスクは小さい"),

            Make("2914", "日本たばこ産業", "東証プライム", "食料品", "大型", "高配当",
                "JT。たばこを中心に医薬・加工食品。高水準の配当利回りが特徴。",
                price: 4100, cap: 8200000, per: 13.0, pbr: 1.7, roe: 13.0, dy: 4.8, payout: 75,
                equity: 48, debt: 30, om: 22.0, npm: 14.0, rg1: 6.0, rg3: 5.0, rg5: 2.0,
                fcf: 400000, cfm: 15.0, ocf: 700000, icf: -200000, fcfin: -450000,
                conYears: 1, eps: 315, fiscal: "12月", ir: "https://www.jt.com/investors/",
                benefit: false, dividendCut: 1, nonCut: 2),

            Make("6098", "リクルートHD", "東証プライム", "サービス業", "大型", "HRテック・AI",
                "人材・販促プラットフォーム。Indeedを核にグローバル展開する高ROE企業。",
                price: 9200, cap: 14500000, per: 28.0, pbr: 5.5, roe: 20.5, dy: 0.8, payout: 22,
                equity: 60, debt: 10, om: 14.0, npm: 10.0, rg1: 8.0, rg3: 12.0, rg5: 13.0,
                fcf: 350000, cfm: 13.0, ocf: 450000, icf: -100000, fcfin: -200000,
                conYears: 4, eps: 330, fiscal: "3月", ir: "https://recruit-holdings.com/ja/ir/",
                buyback: 200000,
                benefit: false),

            Make("6758", "ソニーグループ", "東証プライム", "電気機器", "大型", "エンタメ・半導体",
                "ゲーム・音楽・映画・イメージセンサー・金融を持つ複合企業。",
                price: 13500, cap: 17000000, per: 18.5, pbr: 2.3, roe: 13.0, dy: 0.6, payout: 11,
                equity: 30, debt: 40, om: 11.5, npm: 8.5, rg1: 9.0, rg3: 8.0, rg5: 9.5,
                fcf: 600000, cfm: 7.0, ocf: 1500000, icf: -800000, fcfin: -100000,
                conYears: 3, eps: 730, fiscal: "3月", ir: "https://www.sony.com/ja/SonyInfo/IR/",
                benefit: false),

            Make("8035", "東京エレクトロン", "東証プライム", "電気機器", "大型", "半導体製造装置・AI",
                "半導体製造装置の世界大手。AI向け需要を背景に高成長・高利益率。再評価候補。",
                price: 28000, cap: 13200000, per: 26.0, pbr: 7.0, roe: 28.0, dy: 1.8, payout: 50,
                equity: 65, debt: 5, om: 27.0, npm: 20.0, rg1: 12.0, rg3: 14.0, rg5: 16.0,
                fcf: 350000, cfm: 22.0, ocf: 450000, icf: -120000, fcfin: -150000,
                conYears: 2, eps: 1080, fiscal: "3月", ir: "https://www.tel.co.jp/ir/",
                benefit: false),

            Make("285A", "キオクシアHD", "東証プライム", "電気機器", "大型", "半導体メモリ・AI",
                "NAND型フラッシュメモリ大手。市況回復とAI需要で業績が再評価された代表例。",
                price: 1850, cap: 990000, per: 9.0, pbr: 1.3, roe: 16.0, dy: 0.0, payout: 0,
                equity: 42, debt: 70, om: 14.0, npm: 9.0, rg1: 35.0, rg3: 8.0, rg5: 6.0,
                fcf: 120000, cfm: 11.0, ocf: 380000, icf: -250000, fcfin: -50000,
                conYears: 0, eps: 205, fiscal: "3月", ir: "https://www.kioxia.com/ja-jp/about/ir.html",
                benefit: false),

            Make("3382", "セブン&アイHD", "東証プライム", "小売業", "大型", "コンビニ・小売再編",
                "セブン-イレブンを核とする小売最大手。事業再編とグローバルCVS成長が焦点。",
                price: 2200, cap: 5700000, per: 22.0, pbr: 1.3, roe: 7.0, dy: 2.2, payout: 45,
                equity: 38, debt: 55, om: 4.5, npm: 2.8, rg1: 5.0, rg3: 6.0, rg5: 7.0,
                fcf: 250000, cfm: 3.5, ocf: 700000, icf: -400000, fcfin: -50000,
                conYears: 3, eps: 100, fiscal: "2月", ir: "https://www.7andi.com/ir.html",
                benefit: false),

            Make("9831", "ヤマダHD", "東証プライム", "小売業", "中型", "住建・優待",
                "家電量販最大手。住宅・金融へ多角化。買物優待券が人気の優待銘柄。",
                price: 480, cap: 420000, per: 11.5, pbr: 0.6, roe: 5.5, dy: 3.2, payout: 38,
                equity: 52, debt: 28, om: 4.0, npm: 3.0, rg1: 1.0, rg3: 0.5, rg5: 1.5,
                fcf: 40000, cfm: 3.0, ocf: 70000, icf: -20000, fcfin: -30000,
                conYears: 0, eps: 42, fiscal: "3月", ir: "https://www.yamada-holdings.jp/ir/",
                benefit: true, benefitName: "買物優待券", benefitCat: "買物券", benefitMonth: "3月・9月",
                reqShares: 100, benefitValue: 1000, benefitYield: 2.1, longTerm: true,
                longTermCond: "1年以上", longTermContent: "1年以上で枚数増加",
                benefitRisk: "業績軟調。利回りの優待依存が大きく改悪余地あり", dividendCut: 1, nonCut: 4),

            Make("3048", "ビックカメラ", "東証プライム", "小売業", "中型", "優待・インバウンド",
                "家電量販大手。インバウンド需要が追い風。買物券優待が人気。",
                price: 1500, cap: 280000, per: 15.0, pbr: 1.4, roe: 9.5, dy: 1.6, payout: 25,
                equity: 35, debt: 45, om: 3.8, npm: 2.5, rg1: 7.0, rg3: 4.0, rg5: 2.0,
                fcf: 20000, cfm: 2.5, ocf: 45000, icf: -15000, fcfin: -20000,
                conYears: 2, eps: 100, fiscal: "8月", ir: "https://www.biccamera.co.jp/ir/",
                benefit: true, benefitName: "買物優待券", benefitCat: "買物券", benefitMonth: "2月・8月",
                reqShares: 100, benefitValue: 3000, benefitYield: 2.0, longTerm: true,
                longTermCond: "1年以上・2年以上", longTermContent: "保有期間で枚数増加",
                benefitRisk: "優待人気が高く改悪インパクト大"),

            Make("8591", "オリックス", "東証プライム", "その他金融業", "大型", "総合金融・優待",
                "リース発祥の総合金融。事業投資・不動産・環境まで多角化。優待は2024年に終了。",
                price: 3300, cap: 4000000, per: 11.0, pbr: 1.1, roe: 10.0, dy: 3.6, payout: 39,
                equity: 26, debt: 75, om: 12.0, npm: 9.0, rg1: 4.0, rg3: 5.5, rg5: 4.0,
                fcf: 300000, cfm: 10.0, ocf: 800000, icf: -400000, fcfin: -100000,
                conYears: 11, eps: 300, fiscal: "3月", ir: "https://www.orix.co.jp/grp/ir/",
                cumulative: true, buyback: 100000,
                benefit: false, benefitRisk: "優待は2024年3月で廃止済"),

            Make("4661", "オリエンタルランド", "東証プライム", "サービス業", "大型", "レジャー・体験消費",
                "東京ディズニーリゾート運営。圧倒的ブランドと参入障壁を持つが株価評価は高い。",
                price: 3600, cap: 6500000, per: 45.0, pbr: 6.0, roe: 14.0, dy: 0.3, payout: 14,
                equity: 70, debt: 8, om: 25.0, npm: 18.0, rg1: 10.0, rg3: 9.0, rg5: 3.0,
                fcf: 80000, cfm: 18.0, ocf: 150000, icf: -120000, fcfin: -10000,
                conYears: 2, eps: 80, fiscal: "3月", ir: "https://www.olc.co.jp/ja/ir.html",
                benefit: true, benefitName: "1デーパスポート", benefitCat: "自社サービス", benefitMonth: "3月",
                reqShares: 100, benefitValue: 8400, benefitYield: 0.2, longTerm: false,
                benefitRisk: "ブランド力高く優待依存は低い"),

            Make("2502", "アサヒグループHD", "東証プライム", "食料品", "大型", "飲料・グローバル",
                "ビール・飲料の世界的大手。安定したCFと連続増配が魅力。",
                price: 1900, cap: 9600000, per: 14.0, pbr: 1.4, roe: 10.5, dy: 2.5, payout: 35,
                equity: 42, debt: 50, om: 12.0, npm: 8.0, rg1: 4.0, rg3: 5.0, rg5: 4.5,
                fcf: 250000, cfm: 9.5, ocf: 400000, icf: -120000, fcfin: -150000,
                conYears: 4, eps: 135, fiscal: "12月", ir: "https://www.asahigroup-holdings.com/ir/",
                cumulative: true,
                benefit: true, benefitName: "自社製品詰合せ", benefitCat: "飲料", benefitMonth: "12月",
                reqShares: 100, benefitValue: 1000, benefitYield: 0.5, longTerm: false,
                benefitRisk: "本業堅調で優待依存は低い"),
        };

        var scorer = new ScoreService();
        foreach (var s in list)
        {
            if (s.TotalYield == 0) s.TotalYield = Math.Round(s.DividendYield + s.BenefitYield, 2);
            if (s.MixFactor == 0 && s.PER > 0 && s.PBR > 0) s.MixFactor = Math.Round(s.PER * s.PBR, 1);
            s.History = BuildHistory(s);
            scorer.Recalculate(s);
        }
        return list;
    }

    private static List<TimeSeriesPoint> BuildHistory(Stock s)
    {
        var hist = new List<TimeSeriesPoint>();
        int startYear = DateTime.Today.Year - 9;
        // 直近の売上を時価総額×推定からざっくり生成
        double baseRevenue = s.MarketCap > 0 ? s.MarketCap / 1.2 : 100000;
        double g = Math.Max(-0.05, s.RevenueGrowth3Y / 100.0);
        double rev0 = baseRevenue / Math.Pow(1 + g, 9);

        var rnd = new Random(s.Code.GetHashCode());
        for (int i = 0; i < 10; i++)
        {
            double noise = 1 + (rnd.NextDouble() - 0.5) * 0.06;
            double rev = rev0 * Math.Pow(1 + g, i) * noise;
            double op = rev * (s.OperatingMargin / 100.0);
            double ni = rev * (s.NetProfitMargin / 100.0);
            double eps = s.EPS > 0 ? s.EPS * Math.Pow(1 + g, i - 9) : 0;
            double div = s.Dividend > 0 ? s.Dividend * Math.Pow(1.05, i - 9)
                                        : eps * (s.PayoutRatio / 100.0);
            double payout = eps > 0 ? Math.Round(div / eps * 100, 1) : s.PayoutRatio;
            double assets = rev * (s.EquityRatio / 100.0);
            double liab = rev * (1 - s.EquityRatio / 100.0) * 0.8;
            double ocf = op * 1.2 + ni * 0.3;
            double icf = -op * 0.5 * noise;
            double fcf = ocf + icf;
            double fin = -(div * 0.4) - (s.BuybackAmount > 0 ? s.BuybackAmount / 10 : 0);

            hist.Add(new TimeSeriesPoint
            {
                FiscalYear = startYear + i,
                Revenue = Math.Round(rev),
                OperatingProfit = Math.Round(op),
                NetIncome = Math.Round(ni),
                EPS = Math.Round(eps, 1),
                Dividend = Math.Round(div, 1),
                PayoutRatio = payout,
                NetAssets = Math.Round(assets),
                Liabilities = Math.Round(liab),
                EquityRatio = s.EquityRatio,
                OperatingCF = Math.Round(ocf),
                InvestingCF = Math.Round(icf),
                FinancingCF = Math.Round(fin),
                FreeCF = Math.Round(fcf),
                BuybackAmount = s.BuybackAmount > 0 ? Math.Round(s.BuybackAmount / 10 * noise) : 0
            });
        }
        return hist;
    }

    // 大量の引数を1か所に集約するファクトリ。
    private static Stock Make(
        string code, string name, string market, string sector, string scale, string theme,
        string description, double price, double cap, double per, double pbr, double roe,
        double dy, double payout, double equity, double debt, double om, double npm,
        double rg1, double rg3, double rg5, double fcf, double cfm, double ocf, double icf, double fcfin,
        int conYears, double eps, string fiscal, string ir,
        bool benefit, string benefitName = "", string benefitCat = "", string benefitMonth = "",
        int reqShares = 0, double benefitValue = 0, double benefitYield = 0, bool longTerm = false,
        string longTermCond = "", string longTermContent = "", string benefitRisk = "",
        double buyback = 0, bool cumulative = false, bool doe = false,
        int dividendCut = 0, int nonCut = 0)
    {
        double rg10 = rg5 * 0.9;
        return new Stock
        {
            Code = code, Name = name, Market = market, Sector = sector, Scale = scale, Theme = theme,
            Description = description, FiscalMonth = fiscal, IRUrl = ir, DataUpdated = DateTime.Today,
            Price = price, MarketCap = cap, PER = per, PBR = pbr, ROE = roe,
            MixFactor = Math.Round(per * pbr, 1), EPS = eps, BPS = pbr > 0 ? Math.Round(price / pbr) : 0,
            OperatingMargin = om, OrdinaryProfitMargin = Math.Round(om * 1.02, 1), NetProfitMargin = npm,
            DividendYield = dy, PayoutRatio = payout, Dividend = Math.Round(eps * payout / 100, 1),
            DividendTrend = conYears >= 5 ? "連続増配" : (dividendCut > 0 ? "減配実績あり" : "安定"),
            CumulativeDividend = cumulative, DoeAdopted = doe,
            ConsecutiveDividendYears = conYears, DividendCutCount = dividendCut,
            NonDividendCutYears = nonCut > 0 ? nonCut : (conYears > 0 ? conYears + 2 : 5),
            DividendRemainingYears = fcf > 0 ? Math.Round(fcf / Math.Max(1, cap * dy / 100), 1) : 0,
            BuybackAmount = buyback, ShareholderReturnPolicy = cumulative ? "累進配当・自社株買い" : "配当性向目安",
            DividendGrowth1Y = conYears > 0 ? 5 : 0, DividendGrowth3Y = conYears > 0 ? 6 : 0,
            DividendGrowth5Y = conYears > 0 ? 7 : 0, DividendGrowth10Y = conYears > 0 ? 6 : 0,
            HasShareholderBenefit = benefit, ShareholderBenefit = benefit ? benefitName : "",
            BenefitContent = benefit ? $"{benefitName}（{benefitValue:N0}円相当 / {reqShares}株以上）" : "",
            BenefitCategory = benefitCat, BenefitRightsMonth = benefitMonth,
            RequiredSharesForBenefit = reqShares, BenefitValue = benefitValue, BenefitYield = benefitYield,
            TotalYield = Math.Round(dy + benefitYield, 2),
            HasLongTermBenefit = longTerm, LongTermBenefitCondition = longTermCond,
            LongTermBenefitContent = longTermContent, BenefitRiskMemo = benefitRisk,
            EquityRatio = equity, InterestBearingDebtRatio = debt,
            RevenueGrowth1Y = rg1, RevenueGrowth3Y = rg3, RevenueGrowth5Y = rg5, RevenueGrowth10Y = rg10,
            RevenueGrowthRate = rg1, AverageRevenueGrowth3Y = rg3,
            OperatingProfitGrowthRate = Math.Round(rg3 * 1.2, 1), OrdinaryProfitGrowthRate = Math.Round(rg3 * 1.15, 1),
            NetProfitGrowthRate = Math.Round(rg3 * 1.1, 1), EpsGrowthRate = Math.Round(rg3 * 1.1, 1),
            OperatingCF = ocf, InvestingCF = icf, FinancingCF = fcfin, FreeCashFlow = fcf,
            OperatingCashFlowMargin = cfm,
            StockPriceChange3M = Math.Round((new Random(code.GetHashCode()).NextDouble() - 0.4) * 30, 1),
            AverageStockPriceChange3M = Math.Round((new Random(code.GetHashCode() + 1).NextDouble() - 0.4) * 25, 1),
            AveragePrice3M = Math.Round(price * 0.97),
            PriceChange3M = Math.Round((new Random(code.GetHashCode() + 2).NextDouble() - 0.4) * 30, 1),
            PriceChangeAverage3M = Math.Round((new Random(code.GetHashCode() + 3).NextDouble() - 0.4) * 25, 1),
        };
    }
}
