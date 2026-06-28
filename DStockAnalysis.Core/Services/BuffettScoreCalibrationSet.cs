using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// バフェット採点の教師データ(Claude がバフェット型投資の考え方に基づいて作成)。
/// 実在銘柄コードに依存せず、銘柄パターンとして 10 カテゴリに分散。重み最適化(Optimizer)と
/// 回帰テストの基準に用いる。特定コード(例 8001)を特別扱いするものではない。
/// </summary>
public static class BuffettScoreCalibrationSet
{
    private static BuffettScoreTrainingSample S(string name, string cat, Stock stock,
        double min, double max, string[] grades, bool prohibitS = false, bool danger = false, string rationale = "")
        => new()
        {
            Name = name, Category = cat, Stock = stock,
            ExpectedScore = (min + max) / 2, MinExpectedScore = min, MaxExpectedScore = max,
            AllowedGrades = grades, ProhibitS = prohibitS, Danger = danger, Rationale = rationale,
        };

    // 重要指標(DataConfidence)を満たすよう主要14指標を設定するファクトリ。
    private static Stock Mk(double per, double pbr, double roe, double eq, double opm, double g5, double opg,
        double divY, double payout, double ocf, double fcf,
        double roa = 0, double npm = 0, double ocfm = 0, double eps = 120, double bps = 1500, double mktcap = 600000,
        double rev3 = 0, double rev10 = 0, double npg = 0, double epsg = 0, double ord = 0,
        int consec = 0, int noncut = 0, int cuts = 0, double divG5 = 0, double buyback = 0, double totalYield = 0,
        string sector = "", string theme = "", string name = "", string desc = "", bool sample = false)
        => new Stock
        {
            PER = per, PBR = pbr, ROE = roe, EquityRatio = eq, OperatingMargin = opm, RevenueGrowth5Y = g5,
            OperatingProfitGrowthRate = opg, DividendYield = divY, PayoutRatio = payout, OperatingCF = ocf,
            FreeCashFlow = fcf, ROA = roa, NetProfitMargin = npm, OperatingCashFlowMargin = ocfm, EPS = eps,
            BPS = bps, MarketCap = mktcap, RevenueGrowth3Y = rev3, RevenueGrowth10Y = rev10, NetProfitGrowthRate = npg,
            EpsGrowthRate = epsg, OrdinaryProfitMargin = ord, ConsecutiveDividendYears = consec, NonDividendCutYears = noncut,
            DividendCutCount = cuts, DividendGrowth5Y = divG5, BuybackAmount = buyback, TotalYield = totalYield,
            Sector = sector, Theme = theme, Name = name, Description = desc, IsSampleIndicators = sample,
            IndicatorsFetched = true,
        };

    public static IReadOnlyList<BuffettScoreTrainingSample> All { get; } = Build();

    /// <summary>順位制約: BetterSample のスコアが WorseSample より Margin 以上高くあるべき。
    /// 長期耐久性の高い銘柄が短期高成長・割安だけの銘柄に不当に下回らないようにする。</summary>
    public static IReadOnlyList<BuffettScoreRankingConstraint> RankingConstraints { get; } = new List<BuffettScoreRankingConstraint>
    {
        new() { BetterSampleName = "超優良A", WorseSampleName = "優良A", Margin = 5, Rationale = "真のS > 優良A" },
        new() { BetterSampleName = "優良A", WorseSampleName = "テスト総合商社1", Margin = 3, Rationale = "優良A > 総合商社" },
        new() { BetterSampleName = "優良A", WorseSampleName = "ヒット型1", Margin = 2, Rationale = "優良A > 一時性リスク高成長" },
        new() { BetterSampleName = "優良A", WorseSampleName = "安定低成長1", Margin = 3, Rationale = "優良A > 財務健全低成長" },
        new() { BetterSampleName = "安定低成長1", WorseSampleName = "低PER低収益1", Margin = 3, Rationale = "財務健全低成長 > 低PER低収益" },
        new() { BetterSampleName = "低PER低収益1", WorseSampleName = "赤字危険A", Margin = 5, Rationale = "低PER低収益 > 赤字・CF悪化" },
        new() { BetterSampleName = "テスト銀行2", WorseSampleName = "テスト証券1", Margin = 2, Rationale = "金融優良 > 金融低収益" },
        new() { BetterSampleName = "市況株5", WorseSampleName = "市況株1", Margin = 2, Rationale = "景気敏感優良 > 景気敏感弱" },
    };

    private static List<BuffettScoreTrainingSample> Build()
    {
        var L = new List<BuffettScoreTrainingSample>();
        string[] gS = { "S" }, gA = { "A", "B" }, gBA = { "B", "A" }, gBC = { "B", "C" }, gCD = { "C", "D" }, gDE = { "D", "E" }, gCBA = { "C", "B", "A" };

        // 1. 真のS(超優良)。長期成長(10年)とCF安定を伴う最高品質のみ。全指標を高水準に揃える。
        L.Add(S("超優良A", "S超優良", Mk(12, 1.8, 27, 66, 32, 8, 10, 2.6, 35, 950000, 800000, roa: 15, npm: 19, ocfm: 24, rev3: 9, rev10: 7, npg: 11, epsg: 12, ord: 31, consec: 14, noncut: 18, divG5: 10, buyback: 60000, totalYield: 2.6), 90, 100, gS, rationale: "高ROE・高利益率・無借金的財務・長期成長・強CF。"));
        L.Add(S("超優良B", "S超優良", Mk(13, 2.0, 26, 64, 30, 7, 9, 2.4, 33, 900000, 760000, roa: 14, npm: 18, ocfm: 23, rev3: 8, rev10: 6, npg: 10, epsg: 11, ord: 30, consec: 16, noncut: 20, divG5: 9, buyback: 70000, totalYield: 2.4), 90, 100, gS, rationale: "盤石な質と妥当価格。"));
        L.Add(S("超優良C", "S超優良", Mk(12, 1.8, 28, 68, 33, 8, 10, 2.8, 36, 980000, 820000, roa: 16, npm: 20, ocfm: 25, rev3: 9, rev10: 7, npg: 12, epsg: 12, ord: 33, consec: 20, noncut: 22, divG5: 10, buyback: 80000, totalYield: 2.8), 90, 100, gS, rationale: "圧倒的な資本効率と継続増配。"));
        L.Add(S("超優良D", "S超優良", Mk(13, 1.9, 25, 62, 29, 7, 9, 2.3, 32, 900000, 740000, roa: 14, npm: 17, ocfm: 23, rev3: 8, rev10: 6, npg: 10, epsg: 10, ord: 29, consec: 12, noncut: 14, divG5: 9, buyback: 55000, totalYield: 2.3), 90, 100, gS, rationale: "高品質・妥当価格。"));
        L.Add(S("超優良E", "S超優良", Mk(13, 2.0, 26, 63, 31, 8, 10, 2.5, 34, 930000, 780000, roa: 15, npm: 18, ocfm: 24, rev3: 9, rev10: 7, npg: 11, epsg: 11, ord: 31, consec: 15, noncut: 17, divG5: 10, buyback: 65000, totalYield: 2.5), 90, 100, gS, rationale: "総合的に最上位。"));

        // 2. 優良・妥当価格(A)。Sほど完璧でない。
        L.Add(S("優良A", "A優良", Mk(15, 2.0, 17, 56, 17, 6, 7, 2.6, 40, 450000, 320000, roa: 9, npm: 12, ocfm: 17, rev3: 6, rev10: 4, npg: 8, epsg: 8, ord: 17, consec: 10, noncut: 12, divG5: 7, buyback: 20000, totalYield: 2.6), 80, 89, gA, rationale: "良い会社・妥当価格。"));
        L.Add(S("優良B", "A優良", Mk(16, 2.4, 18, 52, 18, 6, 7, 2.2, 38, 460000, 330000, roa: 10, npm: 13, ocfm: 18, rev3: 6, rev10: 4, npg: 8, epsg: 8, ord: 18, consec: 8, noncut: 10, divG5: 6, buyback: 25000, totalYield: 2.2), 80, 89, gA, rationale: "収益力・成長のバランス良。"));
        L.Add(S("優良C", "A優良", Mk(14, 1.8, 16, 58, 16, 5, 6, 3.0, 42, 440000, 320000, roa: 9, npm: 12, ocfm: 17, rev3: 5, rev10: 4, npg: 7, epsg: 7, ord: 16, consec: 12, noncut: 14, divG5: 6, totalYield: 3.0), 80, 89, gA, rationale: "堅実・割安寄り。"));
        L.Add(S("優良D", "A優良", Mk(18, 2.8, 18, 50, 17, 6, 7, 2.0, 38, 455000, 325000, roa: 10, npm: 13, ocfm: 18, rev3: 7, rev10: 5, npg: 8, epsg: 8, ord: 17, consec: 9, noncut: 11, divG5: 7, buyback: 30000, totalYield: 2.0), 80, 89, gA, rationale: "やや割高だが質が高い。"));
        L.Add(S("優良E", "A優良", Mk(15, 2.0, 16, 54, 16, 5, 6, 2.8, 42, 445000, 315000, roa: 9, npm: 12, ocfm: 17, rev3: 6, rev10: 4, npg: 7, epsg: 7, ord: 16, consec: 11, noncut: 13, divG5: 6, totalYield: 2.8), 80, 89, gA, rationale: "バランス型優良。"));
        L.Add(S("優良F", "A優良", Mk(16, 2.3, 17, 53, 16, 6, 7, 2.4, 40, 450000, 320000, roa: 9, npm: 12, ocfm: 17, rev3: 6, rev10: 4, npg: 8, epsg: 8, ord: 16, consec: 9, noncut: 11, divG5: 6, buyback: 20000, totalYield: 2.4), 80, 89, gA, rationale: "総合的に優良。"));

        // 3. 総合商社(卸売業)。薄利でも ROE/CF/還元/価格で B〜A。S は禁止。
        for (int i = 0; i < 6; i++)
        {
            double roe = 13 + i, per = 12 + i * 0.7, pbr = 1.6 + i * 0.1;
            L.Add(S($"テスト総合商社{i + 1}", "総合商社", Mk(per, pbr, roe, 39 + i, 4.7, 1, 2, 2.4, 32, 1100000, 600000,
                roa: 5.5, npm: 6, ocfm: 7.6, ord: 8, mktcap: 12000000, rev3: 2, npg: 3, epsg: 4, consec: 3, noncut: 3, divG5: 6, buyback: 150000, totalYield: 2.4,
                sector: "卸売業", name: "テスト総合商社"), 70, 82, gBA, prohibitS: true, rationale: "商社は営業利益率でなくROE/CF/還元/価格で評価。"));
        }

        // 4. 高成長・高収益だが一時性リスク(ヒット依存)。長期実績・CF安定が不足。S 禁止。
        for (int i = 0; i < 6; i++)
        {
            L.Add(S($"ヒット型{i + 1}", "ヒット依存", Mk(7 + i * 2, 2.0 + i * 0.5, 24 + i, 50, 16 + i * 2, 0, 0, 1.0, 15, 80000, 0,
                roa: 15, npm: 18, ocfm: 18, rev3: 16 + i * 3, rev10: 0, npg: 0, epsg: 0, eps: 200, mktcap: 200000,
                theme: "ゲーム,アニメ,IP,キャラクター,娯楽,パチンコ", name: "テストヒット企業"), 70, 84, gBA, prohibitS: true, rationale: "短期は高収益だが長期実績・CF安定が不足。Sにしない。"));
        }

        // 5. 財務健全・安定配当だが低成長(B)。
        for (int i = 0; i < 5; i++)
        {
            L.Add(S($"安定低成長{i + 1}", "安定低成長", Mk(13 + i, 1.0 + i * 0.1, 7 + i * 0.5, 65 + i * 3, 8 + i, 1, 1, 3.5, 45, 150000, 90000,
                roa: 5, npm: 7, ocfm: 12, rev3: 1, rev10: 1, npg: 1, epsg: 1, ord: 9, consec: 6, noncut: 12, divG5: 2, totalYield: 3.5),
                65, 78, gBC, rationale: "守りは強いが成長弱い。"));
        }

        // 6. 低PER低PBRだが低収益・低成長(C/D)。割安だけでは高評価にしない。
        for (int i = 0; i < 6; i++)
        {
            L.Add(S($"低PER低収益{i + 1}", "低PER低収益", Mk(6 + i, 0.5 + i * 0.08, 3 + i * 0.6, 35 + i * 4, 2 + i * 0.4, -1, -1, 2.5, 30, 30000, 15000,
                roa: 2, npm: 2, ocfm: 5, rev3: -1, rev10: -1, npg: -1, epsg: -1, ord: 3, consec: 0, noncut: 2, cuts: 1, totalYield: 2.5),
                50, 69, gCD, rationale: "割安でも収益力・成長が弱く高評価にしない。"));
        }

        // 7. 赤字・CF悪化・財務危険(D/E)。強制上限で高得点にしない。
        L.Add(S("赤字危険A", "赤字危険", Mk(-5, 0.6, -8, 15, -3, -10, -20, 0, 0, -20000, -30000, roa: -4, npm: -8, ocfm: -5, eps: -30, rev3: -10, rev10: -8, npg: -25, epsg: -25), 0, 60, gDE, danger: true, prohibitS: true, rationale: "赤字・CF赤字・低自己資本。"));
        L.Add(S("赤字危険B", "赤字危険", Mk(-2, 0.4, -3, 12, -1, -5, -15, 0, 0, -5000, -12000, roa: -2, npm: -3, ocfm: -3, eps: -10, rev3: -5, rev10: -6, npg: -15, epsg: -18), 0, 60, gDE, danger: true, prohibitS: true, rationale: "債務超過寸前・赤字。"));
        L.Add(S("赤字危険C", "赤字危険", Mk(-8, 0.7, -12, 8, -5, -30, -40, 0, 0, -40000, -60000, roa: -6, npm: -12, ocfm: -8, eps: -50, rev3: -20, rev10: -10, npg: -40, epsg: -40), 0, 60, gDE, danger: true, prohibitS: true, rationale: "深刻な赤字。"));
        L.Add(S("赤字危険D", "赤字危険", Mk(0, 0.5, -1, 18, 0.5, -8, -12, 1, 0, -2000, -8000, roa: -1, npm: -1, ocfm: -2, eps: -2, rev3: -8, rev10: -7, npg: -12, epsg: -14), 0, 60, gDE, danger: true, prohibitS: true, rationale: "業績悪化・CF赤字。"));
        L.Add(S("赤字危険E", "赤字危険", Mk(-3, 0.3, -6, 10, -2, -15, -25, 0, 0, -15000, -25000, roa: -3, npm: -6, ocfm: -4, eps: -20, rev3: -12, rev10: -9, npg: -30, epsg: -30), 0, 60, gDE, danger: true, prohibitS: true, rationale: "赤字・債務超過寸前。"));

        // 8. 高配当だが配当性向が危険(C/D)。高配当だけで高評価にしない(性向100%超で上限)。
        string[] gCDE = { "C", "D", "E" };
        for (int i = 0; i < 4; i++)
        {
            L.Add(S($"高配当危険{i + 1}", "高配当危険", Mk(10 + i, 1.0 + i * 0.1, 9 + i, 40 + i * 3, 10 + i, 1, 1, 4.8 + i * 0.4, 108 + i * 10, 80000, -4000,
                roa: 5, npm: 6, ocfm: 10, rev3: 1, rev10: 0, npg: 1, epsg: 1, ord: 10, consec: 2, noncut: 3, divG5: 1, totalYield: 4.8 + i * 0.4),
                45, 70, gCDE, rationale: "配当性向100%超・FCF赤字。高配当でも高評価にしない。"));
        }

        // 9. 金融業(銀行/保険/証券)。専用の財務安全性で評価(自己資本比率・営業CF・有利子負債を使わない)。
        L.Add(S("テスト銀行1", "金融", Mk(10, 0.8, 9, 6, 0, 0, 3, 3.5, 40, 0, 0, roa: 0.6, npm: 0, ord: 0, npg: 4, eps: 200, bps: 2500, mktcap: 2000000, consec: 5, noncut: 8, divG5: 3, totalYield: 3.5, sector: "銀行業", name: "テスト銀行"), 62, 82, gCBA, rationale: "金融は専用基準。"));
        L.Add(S("テスト銀行2", "金融", Mk(12, 1.2, 11, 8, 0, 0, 5, 3.0, 35, 0, 0, roa: 0.8, npg: 5, eps: 250, bps: 2300, mktcap: 2500000, consec: 8, noncut: 10, divG5: 4, totalYield: 3.0, sector: "銀行業", name: "テスト地銀"), 62, 82, gCBA, rationale: "安定金融。"));
        L.Add(S("テスト保険1", "金融", Mk(11, 1.0, 12, 12, 0, 0, 4, 3.8, 45, 0, 0, roa: 1.2, npg: 4, eps: 300, bps: 3000, mktcap: 3000000, consec: 6, noncut: 9, divG5: 5, totalYield: 3.8, sector: "保険業", name: "テスト保険"), 62, 82, gCBA, rationale: "保険・専用基準。"));
        L.Add(S("テスト証券1", "金融", Mk(9, 0.9, 8, 15, 0, 0, 2, 3.2, 50, 0, 0, roa: 1.0, npg: 2, eps: 150, bps: 1800, mktcap: 1200000, consec: 3, noncut: 5, divG5: 2, totalYield: 3.2, sector: "証券業", name: "テスト証券"), 62, 82, gCBA, rationale: "証券・専用基準。"));

        // 10. 景気敏感・市況株(鉄鋼/化学/海運/非鉄)。短期割安・高収益でも S にしない。
        string[] cyc = { "鉄鋼", "化学", "海運業", "非鉄金属", "石油・石炭製品" };
        for (int i = 0; i < 5; i++)
        {
            L.Add(S($"市況株{i + 1}", "景気敏感", Mk(6 + i, 0.7 + i * 0.15, 12 + i * 2, 45 + i * 3, 10 + i, 1, 2, 4.0 + i * 0.5, 30 + i * 5, 120000, 30000,
                roa: 6, npm: 8, ocfm: 12, rev3: 20, rev10: 1, npg: 5, epsg: 6, ord: 11, consec: 2, noncut: 3, cuts: 1, divG5: 3, totalYield: 4.0 + i * 0.5,
                sector: cyc[i], name: "テスト市況株"), 60, 78, gBC, prohibitS: true, rationale: "市況依存。長期安定が不足するためSにしない。"));
        }

        return L;
    }
}
