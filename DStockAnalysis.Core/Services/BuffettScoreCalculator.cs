using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// バフェット型の総合採点(100点満点)を算出する。低PER/低PBRランキングではなく、
/// 事業の質(長期で稼ぐ力・競争優位・財務安全・利益とCFの安定・資本効率・株主還元・価格妥当性)を総合評価する。
///
/// 設計方針:
///  - 外部アクセスは一切しない。Stock に既にある指標(CSV取込/自動取得済み)のみを使う。
///  - 欠損指標は 0点扱いせず「評価対象から除外」し、残った指標で重みを再配分する(値が無いだけで低評価にしない)。
///    本モデルは数値型(null不可)のため、値が 0 のものを「欠損」とみなす(成長率・CF等の負値は実値として採点)。
///    ただし有利子負債比率の 0 は「無借金=満点」、配当継続性の 0 は仕様どおり下限点として扱う。
///  - 重要指標の取得率(DataConfidence)が低いほど最終スコアに上限をかける。
///  - 赤字・財務危険・無理な高配当・サンプル指標などには強制上限を適用する。
///
/// 重み: 事業耐久力0.25 / 収益力0.20 / 財務安全0.15 / 成長安定0.15 / 資本配分0.10 / 割安性0.15。
/// </summary>
public class BuffettScoreCalculator
{
    public enum Profile { Standard, Financial, Trading }

    /// <summary>既定の重み(教師データへ最適化した値)で採点する。</summary>
    public BuffettResult Calculate(Stock s) => Calculate(s, BuffettScoreWeightOptimizer.DefaultWeights);

    public BuffettResult Calculate(Stock s, BuffettScoreWeights w)
    {
        var profile = DetectProfile(s);
        bool fin = profile == Profile.Financial;
        bool trading = profile == Profile.Trading;

        // 業種プロファイル別の採点。総合商社・卸売業(Trading)は営業利益率の絶対値に依存せず、
        // ROE・CF・財務・株主還元・バリュエーションを重視する。
        double biz = fin ? FinancialBusinessDurability(s) : trading ? TradingBusinessDurability(s) : BusinessDurability(s);
        double prof = fin ? FinancialProfitability(s) : trading ? TradingProfitability(s) : Profitability(s, fin);
        double safe = fin ? FinancialSafety(s) : Safety(s);
        double growth = fin ? FinancialGrowthStability(s) : trading ? TradingGrowthStability(s) : GrowthStability(s);
        double capital = CapitalAllocation(s);
        double val = Valuation(s);

        // Trading: 妥当価格(PER低・PBR低・高ROE・性向健全)なら割安性を少し加点
        if (trading && s.PER > 0 && s.PER <= 15 && s.PBR > 0 && s.PBR <= 2.0 && s.ROE >= 12 && s.PayoutRatio > 0 && s.PayoutRatio <= 50)
            val = Math.Min(100, val + 8);
        // Trading: 健全な配当(性向25-45%・利回り>0・ROE>=10・PER<=18)なら資本配分を不当に下げない
        if (trading && s.PayoutRatio >= 25 && s.PayoutRatio <= 45 && s.DividendYield > 0 && s.ROE >= 10 && s.PER > 0 && s.PER <= 18)
            capital = Math.Max(capital, 70);

        double raw = biz * w.BusinessDurabilityWeight + prof * w.ProfitabilityWeight + safe * w.SafetyWeight
                   + growth * w.GrowthStabilityWeight + capital * w.CapitalAllocationWeight + val * w.ValuationWeight;
        double conf = DataConfidence(s);

        // 上限(複数該当時は最小値を採用)
        double cap = 100;
        if (conf < 40) cap = Math.Min(cap, 60);
        else if (conf < 60) cap = Math.Min(cap, 75);
        else if (conf < 80) cap = Math.Min(cap, 85);

        if (s.IsSampleIndicators) cap = Math.Min(cap, 40);
        if (s.PER <= 0 || s.EPS <= 0) cap = Math.Min(cap, 65);
        if (!fin && s.OperatingCF < 0 && s.FreeCashFlow < 0) cap = Math.Min(cap, 60);
        if (!fin && s.EquityRatio > 0 && s.EquityRatio < 20) cap = Math.Min(cap, 60);
        if (s.PayoutRatio > 100 && s.DividendYield >= 4) cap = Math.Min(cap, 70);
        if (s.RevenueGrowth5Y < 0 && s.OperatingProfitGrowthRate < 0) cap = Math.Min(cap, 65);
        if (s.ROE < 5 && s.OperatingMargin < 5) cap = Math.Min(cap, 60);

        double final = Math.Clamp(Math.Min(raw, cap), 0, 100);

        // 優良・妥当価格の下限補正: 明確な弱点(赤字/債務超過/CF大幅赤字/過大配当/サンプル/低信頼度)が無く、
        // PER妥当・ROE高め・財務許容・配当性向健全・データ信頼度が高い銘柄を D評価/50点台に沈めない。
        bool qualifies = conf >= 80 && s.PER > 0 && s.PER <= 18 && s.PBR > 0 && s.PBR <= 2.5
            && s.ROE >= 12 && s.EquityRatio >= 30 && s.PayoutRatio >= 20 && s.PayoutRatio <= 60
            && !s.IsSampleIndicators;
        bool disqualified = s.EPS <= 0 || s.PER <= 0 || s.ROE <= 0 || s.EquityRatio < 20
            || (!fin && s.OperatingCF < 0 && s.FreeCashFlow < 0) || s.PayoutRatio > 100
            || s.IsSampleIndicators || conf < 80;
        if (qualifies && !disqualified) final = Math.Max(final, 70); // 最低70=B以上

        final = Math.Round(final, 0);

        var r = new BuffettResult
        {
            BuffettScore = final,
            BusinessDurabilityScore = Math.Round(biz, 0),
            ProfitabilityScore = Math.Round(prof, 0),
            SafetyScore = Math.Round(safe, 0),
            GrowthStabilityScore = Math.Round(growth, 0),
            CapitalAllocationScore = Math.Round(capital, 0),
            ValuationScore = Math.Round(val, 0),
            DataConfidence = Math.Round(conf, 0),
            Profile = profile switch { Profile.Financial => "FinancialCompany", Profile.Trading => "TradingCompany", _ => "StandardCompany" },
        };
        // ランクは点数だけで決めない。S は厳格条件を満たす場合のみ許可(満たさなければ A 止まり)。
        string grade = Grade(final);
        bool sAllowed = AllowS(s, r, profile, conf);
        if (grade == "S" && !sAllowed) grade = "A";
        r.OverallGrade = grade;

        r.JudgementText = Judgement(r, profile);
        BuildReasons(s, r, profile, sAllowed);
        return r;
    }

    /// <summary>S ランクの厳格条件。点数(90+)に加え、質・財務・CF・長期成長・データ信頼度をすべて満たす場合のみ。</summary>
    private static bool AllowS(Stock s, BuffettResult r, Profile profile, double conf)
    {
        if (profile == Profile.Trading) return false;                 // 商社はSにしない
        if (IsCyclical(s.Sector)) return false;                       // 景気敏感(市況)はSにしない
        if (IsHitDriven(s)) return false;                             // ヒット依存はSにしない
        return r.BuffettScore >= 90 && conf >= 85
            && r.BusinessDurabilityScore >= 75 && r.ProfitabilityScore >= 75 && r.SafetyScore >= 75
            && r.GrowthStabilityScore >= 65 && r.CapitalAllocationScore >= 60 && r.ValuationScore >= 60
            && !s.IsSampleIndicators && s.PER > 0 && s.EPS > 0 && s.ROE > 0
            && s.OperatingCF > 0 && s.FreeCashFlow >= 0 && s.PayoutRatio <= 100
            && s.RevenueGrowth10Y >= 3;                               // 長期成長の実績を要求
    }

    private static bool IsCyclical(string? sector)
    {
        if (string.IsNullOrEmpty(sector)) return false;
        foreach (var kw in new[] { "鉄鋼", "化学", "海運", "非鉄", "石油", "鉱業", "ガラス", "パルプ", "繊維" })
            if (sector.Contains(kw)) return true;
        return false;
    }

    private static bool IsHitDriven(Stock s)
    {
        // ヒット商品依存(ゲーム/アニメ等)で、長期成長(10年)が無く CF 安定が確認できない
        bool theme = false;
        foreach (var kw in new[] { "ゲーム", "アニメ", "IP", "キャラクター", "娯楽" })
            if ((s.Theme?.Contains(kw) ?? false)) { theme = true; break; }
        return theme && (s.RevenueGrowth10Y < 3 || s.FreeCashFlow <= 0);
    }

    /// <summary>採点理由(高評価要因・減点要因・ランク判定理由)を生成する。</summary>
    private static void BuildReasons(Stock s, BuffettResult r, Profile profile, bool sAllowed)
    {
        var dims = new (string name, double v)[]
        {
            ("事業耐久力", r.BusinessDurabilityScore), ("収益力", r.ProfitabilityScore),
            ("財務安全性", r.SafetyScore), ("成長安定性", r.GrowthStabilityScore),
            ("株主還元・資本配分", r.CapitalAllocationScore), ("割安性", r.ValuationScore),
        };
        var strong = dims.Where(d => d.v >= 70).OrderByDescending(d => d.v).Select(d => $"{d.name}({d.v:0})").ToList();
        var weak = dims.Where(d => d.v < 50).OrderBy(d => d.v).Select(d => $"{d.name}({d.v:0})").ToList();

        var hi = new List<string>();
        if (s.ROE >= 12) hi.Add($"ROE {s.ROE:0.#}% と高め");
        if (s.PER > 0 && s.PER <= 15) hi.Add($"PER {s.PER:0.#} は妥当");
        if (s.OperatingCF > 0 && s.FreeCashFlow > 0) hi.Add("営業CF・フリーCFとも黒字");
        if (s.PayoutRatio >= 20 && s.PayoutRatio <= 60) hi.Add($"配当性向 {s.PayoutRatio:0.#}% は健全");
        if (s.ConsecutiveDividendYears >= 5) hi.Add($"連続増配 {s.ConsecutiveDividendYears}年");
        if (s.BuybackAmount > 0) hi.Add("自社株買いあり");
        if (strong.Count > 0) hi.Add("強み: " + string.Join("・", strong));
        r.Strengths = hi.Count > 0 ? string.Join("、", hi) : "特筆すべき高評価要因は乏しい。";

        var lo = new List<string>();
        if (s.PER <= 0 || s.EPS <= 0) lo.Add("赤字(PER/EPSが0以下)");
        if (s.OperatingCF < 0 && s.FreeCashFlow < 0) lo.Add("営業CF・フリーCFが赤字");
        else if (s.FreeCashFlow < 0) lo.Add("フリーCFが赤字");
        if (s.EquityRatio > 0 && s.EquityRatio < 20) lo.Add($"自己資本比率 {s.EquityRatio:0.#}% が低い");
        if (s.PayoutRatio > 100) lo.Add($"配当性向 {s.PayoutRatio:0.#}% が過大");
        if (profile == Profile.Trading) lo.Add("商社特有の市況感応度");
        if (IsCyclical(s.Sector)) lo.Add("景気敏感(市況)による業績変動");
        if (IsHitDriven(s)) lo.Add("ヒット依存で長期実績・CF安定が不足");
        if (weak.Count > 0) lo.Add("弱み: " + string.Join("・", weak));
        if (r.DataConfidence < 80) lo.Add($"データ信頼度 {r.DataConfidence:0}%");
        r.Weaknesses = lo.Count > 0 ? string.Join("、", lo) : "明確な減点要因は少ない。";

        string rank = $"BuffettScore {r.BuffettScore:0} のため {r.OverallGrade} 評価。";
        if (r.OverallGrade != "S")
        {
            if (r.BuffettScore >= 90 && !sAllowed)
                rank += "90点以上だがS厳格条件(質・財務・長期成長・データ信頼度)に未達のためS不可。";
            else rank += "S条件(90点以上＋各サブ高水準)には未達。";
        }
        r.RankReason = rank;
    }

    // ========== 業種プロファイル判定 ==========
    private static Profile DetectProfile(Stock s)
    {
        if (IsFinancial(s.Sector)) return Profile.Financial;
        if (IsTrading(s)) return Profile.Trading;
        return Profile.Standard;
    }

    private static bool IsTrading(Stock s)
    {
        if (!string.IsNullOrEmpty(s.Sector) && s.Sector.Contains("卸売")) return true;
        foreach (var kw in new[] { "総合商社", "商社", "卸売" })
            if ((s.Name?.Contains(kw) ?? false) || (s.Theme?.Contains(kw) ?? false) || (s.Description?.Contains(kw) ?? false))
                return true;
        return false;
    }

    // ========== Trading: 事業耐久力 ==========
    private static double TradingBusinessDurability(Stock s) => Weighted(
        (RoeQualityScore(s.ROE), 0.25),
        (ProfitAndCFStabilityScore(s), 0.25),
        (BalanceSheetResilienceScore(s.EquityRatio), 0.20),
        (ShareholderReturnStabilityScore(s), 0.15),
        (ValuationReasonablenessScore(s), 0.15));

    // ========== Trading: 収益力(営業利益率の絶対値に依存しない) ==========
    private static double TradingProfitability(Stock s) => Weighted(
        (RoeQualityScore(s.ROE), 0.45),
        (NetProfitMarginScore(s.NetProfitMargin), 0.15),
        (OrdinaryMarginScore(s.OrdinaryProfitMargin), 0.15),
        (TradingOperatingCFScore(s), 0.15),
        (RoaScore(s.ROA), 0.10));

    // ========== Trading: 成長安定性(売上ぶれに引きずられない。利益・配当の安定を重視) ==========
    private static double TradingGrowthStability(Stock s) => Weighted(
        (GrowthScore(s.EpsGrowthRate), 0.25),
        (GrowthScore(s.NetProfitGrowthRate), 0.25),
        (GrowthScore(s.OrdinaryProfitGrowthRate), 0.20),
        (DividendGrowthScore(s), 0.15),
        (DownsideResilienceScore(s), 0.15));

    private static double? RoeQualityScore(double roe)
        => roe == 0 ? (double?)null : roe >= 18 ? 100 : roe >= 15 ? 85 : roe >= 12 ? 70 : roe >= 10 ? 55 : roe >= 8 ? 40 : roe > 0 ? 20 : 0;

    private static double? ProfitAndCFStabilityScore(Stock s)
    {
        double basePart;
        if (s.OperatingCF > 0 && s.FreeCashFlow > 0) basePart = 90;
        else if (s.OperatingCF > 0) basePart = 70;
        else if (s.OperatingCF < 0 && s.FreeCashFlow < 0) basePart = 20;
        else if (s.OperatingCF == 0 && s.FreeCashFlow == 0) return null; // 両方欠損は除外
        else basePart = 50;
        double g = Pick(s.NetProfitGrowthRate, s.OrdinaryProfitGrowthRate, s.EpsGrowthRate);
        if (g > 0) basePart = Math.Min(100, basePart + 10); // 利益成長プラスは加点(欠損は減点しない)
        return basePart;
    }

    private static double? BalanceSheetResilienceScore(double e)
        => e == 0 ? (double?)null : e >= 50 ? 100 : e >= 40 ? 80 : e >= 35 ? 70 : e >= 30 ? 60 : e >= 20 ? 40 : Math.Clamp(e, 0, 20);

    private static double? ShareholderReturnStabilityScore(Stock s)
    {
        if (s.PayoutRatio == 0 && s.DividendYield == 0 && s.BuybackAmount == 0) return null;
        double p = s.PayoutRatio;
        double sc = p <= 0 ? 50
            : (p >= 25 && p <= 45) ? 100
            : (p >= 20 && p <= 60) ? 80
            : (p >= 10 && p <= 70) ? 60
            : p <= 100 ? 30
            : Math.Clamp(20 - (p - 100) / 10, 0, 20);
        if (s.DividendCutCount == 0 && s.NonDividendCutYears >= 10) sc += 5;
        if (s.BuybackAmount > 0) sc += 5;
        if (s.DividendGrowth5Y > 0 || s.DividendGrowth10Y > 0) sc += 5;
        return Math.Min(100, sc);
    }

    private static double? ValuationReasonablenessScore(Stock s)
    {
        double? perSc = s.PER == 0 ? (double?)null
            : s.PER <= 10 ? 100 : s.PER <= 15 ? 80 : s.PER <= 18 ? 65 : s.PER <= 22 ? 45 : s.PER <= 30 ? 20 : 0;
        double? pbrSc = null;
        if (s.PBR != 0)
        {
            double v = s.PBR <= 1.0 ? 100 : s.PBR <= 1.5 ? 80 : s.PBR <= 2.0 ? 65 : s.PBR <= 3.0 ? 40 : Math.Clamp(20 - (s.PBR - 3) * 5, 0, 20);
            if (s.ROE >= 15 && s.PBR <= 3.0) v += 15; else if (s.ROE >= 12 && s.PBR <= 2.5) v += 10;
            pbrSc = Math.Min(100, v);
        }
        return Weighted((perSc, 0.5), (pbrSc, 0.5));
    }

    private static double? OrdinaryMarginScore(double v)
        => v == 0 ? (double?)null : v >= 15 ? 100 : v >= 10 ? 80 : v >= 7 ? 60 : v >= 5 ? 40 : v > 0 ? 20 : 0;

    private static double? TradingOperatingCFScore(Stock s)
    {
        if (s.OperatingCF == 0 && s.FreeCashFlow == 0) return null;
        if (s.OperatingCF > 0) return s.FreeCashFlow > 0 ? 100 : 80;
        return s.FreeCashFlow > 0 ? 40 : 0;
    }

    // ========== 1. 事業耐久力(25%) ==========
    private static double BusinessDurability(Stock s) => Weighted(
        (OperatingMarginScore(s.OperatingMargin), 0.30),
        (LongTermRevenueScore(s), 0.20),
        (ProfitStabilityScore(s.OperatingProfitGrowthRate), 0.20),
        (OperatingCFStabilityScore(s), 0.15),
        (MoatProxyScore(s), 0.15));

    // ========== 2. 収益力(20%) ==========
    private static double Profitability(Stock s, bool fin) => Weighted(
        (RoeScore(s, fin), 0.35),
        (RoaScore(s.ROA), 0.20),
        (OperatingMarginScore(s.OperatingMargin), 0.20),
        (NetProfitMarginScore(s.NetProfitMargin), 0.15),
        (OperatingCFMarginScore(s.OperatingCashFlowMargin), 0.10));

    // ========== 3a. 財務安全性(通常企業, 15%) ==========
    private static double Safety(Stock s) => Weighted(
        (EquityRatioScore(s.EquityRatio), 0.35),
        (DebtRatioScore(s.InterestBearingDebtRatio), 0.25),
        (FcfStabilityScore(s.FreeCashFlow), 0.20),
        (PayoutSafetyScore(s.PayoutRatio), 0.10),
        (CashGenerationScore(s.OperatingCF), 0.10));

    // ========== 3b. 財務安全性(金融業, 15%) ==========
    //  営業CF・有利子負債比率・FCF は使わない。自己資本比率は金融業向けの基準で評価(低めが正常)。
    private static double FinancialSafety(Stock s) => Weighted(
        (FinancialEquityScore(s.EquityRatio), 0.25),
        (RoeStabilityScore(s.ROE), 0.25),
        (FinancialProfitStabilityScore(s), 0.25),
        (PayoutSafetyScore(s.PayoutRatio), 0.15),
        (DividendResilienceScore(s), 0.10));

    // ========== 金融業の事業耐久力/収益力/成長(営業利益率・ROA・CFに依存しない) ==========
    private static double FinancialBusinessDurability(Stock s) => Weighted(
        (RoeQualityScore(s.ROE), 0.35),
        (FinancialProfitStabilityScore(s), 0.25),
        (DividendResilienceScore(s), 0.20),
        (ValuationReasonablenessScore(s), 0.20));

    private static double FinancialProfitability(Stock s) => Weighted(
        (RoeQualityScore(s.ROE), 0.55),
        (FinancialProfitStabilityScore(s), 0.25),
        (ValuationReasonablenessScore(s), 0.20));

    private static double FinancialGrowthStability(Stock s) => Weighted(
        (GrowthScore(s.NetProfitGrowthRate), 0.40),
        (GrowthScore(s.OrdinaryProfitGrowthRate), 0.30),
        (DownsideResilienceScore(s), 0.30));

    /// <summary>金融業向け自己資本比率(規制上 5〜12% が正常)。</summary>
    private static double? FinancialEquityScore(double e)
        => e == 0 ? (double?)null : e >= 12 ? 100 : e >= 10 ? 90 : e >= 8 ? 80 : e >= 5 ? 60 : e >= 3 ? 40 : 20;

    // ========== 4. 成長安定性(15%) ==========
    private static double GrowthStability(Stock s) => Weighted(
        (GrowthScore(Pick(s.RevenueGrowth3Y, s.RevenueGrowth5Y, s.RevenueGrowth1Y)), 0.25),
        (GrowthScore(s.OperatingProfitGrowthRate), 0.25),
        (GrowthScore(s.EpsGrowthRate), 0.25),
        (LongTermRevenueScore(s), 0.15),
        (DownsideResilienceScore(s), 0.10));

    // ========== 5. 株主還元・資本配分(10%) ==========
    private static double CapitalAllocation(Stock s)
    {
        bool floor = (s.Dividend == 0 || s.DividendYield == 0)
                     && s.ROE >= 15 && s.RevenueGrowth5Y >= 5 && s.FreeCashFlow > 0; // 無配でも高ROA成長企業は下限50
        double? F(double? v) => v.HasValue ? (floor ? Math.Max(50, v.Value) : v) : (floor ? 50 : (double?)null);
        return Weighted(
            (F(DividendContinuityScore(s.ConsecutiveDividendYears)), 0.20),
            (F(DividendGrowthScore(s)), 0.20),
            (F(PayoutSafetyScore(s.PayoutRatio)), 0.20),
            (BuybackScore(s.BuybackAmount), 0.25),
            (F(TotalYieldScore(s.TotalYield)), 0.15));
    }

    // ========== 6. 割安性(15%) ==========
    private static double Valuation(Stock s)
    {
        bool hq = s.ROE >= 15 && s.RevenueGrowth5Y >= 5 && s.OperatingMargin >= 15;
        double? per = PerScore(s.PER);
        if (per.HasValue && hq) per = Math.Min(100, per.Value + 10);
        return Weighted(
            (per, 0.25),
            (PbrScore(s.PBR, s.ROE), 0.15),
            (MixFactorScore(s), 0.20),
            (FcfYieldScore(s), 0.25),
            (TotalYieldScore(s.TotalYield), 0.15));
    }

    // ========== サブ指標(各 0..100、欠損は null=除外) ==========

    private static double? OperatingMarginScore(double m)
        => m == 0 ? (double?)null : m >= 25 ? 100 : m >= 15 ? 75 : m >= 10 ? 50 : m >= 5 ? 25 : m > 0 ? 10 : 0;

    private static double? LongTermRevenueScore(Stock s)
    {
        double v = Pick(s.RevenueGrowth10Y, s.RevenueGrowth5Y, s.RevenueGrowth3Y);
        if (v == 0) return null;
        return v >= 8 ? 100 : v >= 5 ? 80 : v >= 3 ? 60 : v >= 0 ? 40 : Math.Clamp(30 + v, 0, 30);
    }

    private static double? ProfitStabilityScore(double v)
        => v == 0 ? (double?)null : v >= 10 ? 100 : v >= 7 ? 85 : v >= 5 ? 70 : v >= 3 ? 55 : v >= 0 ? 35 : Math.Clamp(25 + v, 0, 25);

    private static double? OperatingCFStabilityScore(Stock s)
    {
        if (s.OperatingCF > 0 && s.FreeCashFlow > 0) return 100;
        if (s.OperatingCF > 0 && s.FreeCashFlow <= 0) return 60;
        if (s.OperatingCF <= 0 && s.FreeCashFlow > 0) return 40;
        if (s.OperatingCF == 0 && s.FreeCashFlow == 0) return null; // 両方欠損は除外
        return 10; // 両方マイナス
    }

    private static double? MoatProxyScore(Stock s)
    {
        double v = Pick(s.RevenueGrowth10Y, s.RevenueGrowth5Y);
        double m = 0;
        if (s.OperatingMargin >= 15) m += 30;
        if (s.ROE >= 15) m += 25;
        if (v >= 3) m += 20;
        if (s.NonDividendCutYears >= 10) m += 15;
        if (s.DividendCutCount == 0) m += 10;
        return Math.Min(100, m);
    }

    private static double? RoeScore(Stock s, bool fin)
    {
        if (s.ROE == 0) return null;
        double sc = s.ROE >= 20 ? 100 : s.ROE >= 15 ? 80 : s.ROE >= 10 ? 60 : s.ROE >= 5 ? 30 : s.ROE > 0 ? 10 : 0;
        if (!fin && s.EquityRatio > 0 && s.EquityRatio < 30 && s.ROE >= 20) sc *= 0.8; // レバレッジ起因の高ROEを割引
        return sc;
    }

    private static double? RoaScore(double v)
        => v == 0 ? (double?)null : v >= 10 ? 100 : v >= 7 ? 80 : v >= 5 ? 60 : v >= 3 ? 40 : v > 0 ? 20 : 0;

    private static double? NetProfitMarginScore(double v)
        => v == 0 ? (double?)null : v >= 15 ? 100 : v >= 10 ? 80 : v >= 7 ? 60 : v >= 5 ? 40 : v > 0 ? 20 : 0;

    private static double? OperatingCFMarginScore(double v)
        => v == 0 ? (double?)null : v >= 20 ? 100 : v >= 15 ? 80 : v >= 10 ? 60 : v >= 5 ? 40 : v > 0 ? 20 : 0;

    private static double? EquityRatioScore(double e)
        => e == 0 ? (double?)null : e >= 70 ? 100 : e >= 50 ? 80 : e >= 40 ? 65 : e >= 30 ? 50 : e >= 20 ? 30 : Math.Clamp(e, 0, 20);

    private static double? DebtRatioScore(double d)
        => d <= 0 ? 100 : d <= 30 ? 80 : d <= 50 ? 60 : d <= 100 ? 30 : Math.Clamp(20 - (d - 100) / 10, 0, 20);

    private static double? FcfStabilityScore(double fcf)
        => fcf == 0 ? (double?)null : fcf > 0 ? 100 : 0;

    private static double? PayoutSafetyScore(double p)
    {
        if (p == 0) return null;            // 欠損は除外
        if (p > 100) return 0;
        if (p >= 30 && p <= 50) return 100;
        if (p >= 20 && p <= 60) return 80;
        if (p >= 10 && p <= 70) return 60;
        if (p > 80) return 20;              // 80<p<=100
        return 40;                          // 0<p<=80 のその他
    }

    private static double? CashGenerationScore(double ocf)
        => ocf == 0 ? (double?)null : ocf > 0 ? 100 : 0;

    private static double? GrowthScore(double v)
        => v == 0 ? (double?)null : v >= 10 ? 100 : v >= 7 ? 85 : v >= 5 ? 70 : v >= 3 ? 55 : v >= 0 ? 35 : Math.Clamp(25 + v, 0, 25);

    private static double? DownsideResilienceScore(Stock s)
    {
        if (s.DividendCutCount == 0 && s.NonDividendCutYears >= 10) return 100;
        if (s.DividendCutCount == 0 && s.NonDividendCutYears >= 5) return 80;
        if (s.DividendCutCount <= 1) return 60;
        if (s.DividendCutCount <= 2) return 40;
        return Math.Clamp(20 - (s.DividendCutCount - 3) * 5, 0, 20);
    }

    private static double? DividendContinuityScore(int years)
        => years >= 20 ? 100 : years >= 10 ? 80 : years >= 5 ? 60 : years >= 1 ? 40 : 20;

    private static double? DividendGrowthScore(Stock s)
    {
        double v = Pick(s.DividendGrowth10Y, s.DividendGrowth5Y, s.DividendGrowth3Y, s.DividendGrowth1Y);
        if (v == 0) return null;
        return v >= 10 ? 100 : v >= 7 ? 85 : v >= 5 ? 70 : v >= 3 ? 55 : v >= 0 ? 35 : Math.Clamp(25 + v, 0, 25);
    }

    private static double? BuybackScore(double amount)
        => amount > 0 ? 100 : (double?)null; // 0/欠損は区別不能のため評価対象から除外

    private static double? TotalYieldScore(double y)
        => y == 0 ? (double?)null : y >= 5 ? 100 : y >= 4 ? 80 : y >= 3 ? 60 : y >= 2 ? 40 : y > 0 ? 20 : 0;

    private static double? PerScore(double per)
        => per <= 0 ? 0 : per <= 10 ? 100 : per <= 15 ? 80 : per <= 20 ? 60 : per <= 25 ? 40 : per <= 30 ? 20 : Math.Clamp(10 - (per - 30) / 5, 0, 10);

    private static double? PbrScore(double pbr, double roe)
    {
        if (pbr <= 0) return null;
        double sc = pbr <= 1.0 ? 100 : pbr <= 1.5 ? 80 : pbr <= 2.0 ? 60 : pbr <= 3.0 ? 40 : pbr <= 5.0 ? 20 : Math.Clamp(10 - (pbr - 5) * 2, 0, 10);
        if (roe >= 25) sc += 15; else if (roe >= 20) sc += 10; // 高ROEはPBRが高くなりやすい補正
        return Math.Min(100, sc);
    }

    private static double? MixFactorScore(Stock s)
    {
        double mix = s.MixFactor != 0 ? s.MixFactor : (s.PER > 0 && s.PBR > 0 ? s.PER * s.PBR : 0);
        if (mix <= 0) return null;
        double sc = mix <= 22.5 ? 100 : mix <= 30 ? 80 : mix <= 40 ? 60 : mix <= 50 ? 40 : mix <= 70 ? 20 : 0;
        if (s.ROE >= 25) sc += 15; else if (s.ROE >= 20) sc += 10;
        return Math.Min(100, sc);
    }

    private static double? FcfYieldScore(Stock s)
    {
        // FreeCashFlow・MarketCap はともに百万円(単位一致)。MarketCap 欠損時は評価しない。
        if (s.MarketCap <= 0 || s.FreeCashFlow == 0) return null;
        double y = s.FreeCashFlow / s.MarketCap * 100;
        return y >= 8 ? 100 : y >= 6 ? 80 : y >= 4 ? 60 : y >= 2 ? 40 : y > 0 ? 20 : 0;
    }

    // 金融業用
    private static double? RoeStabilityScore(double roe)
        => roe == 0 ? (double?)null : roe >= 12 ? 100 : roe >= 9 ? 80 : roe >= 6 ? 60 : roe >= 3 ? 40 : roe > 0 ? 20 : 0;

    private static double? FinancialProfitStabilityScore(Stock s)
    {
        double v = Pick(s.OrdinaryProfitGrowthRate, s.NetProfitGrowthRate);
        if (v == 0) return null;
        return v >= 10 ? 100 : v >= 7 ? 85 : v >= 5 ? 70 : v >= 3 ? 55 : v >= 0 ? 35 : Math.Clamp(25 + v, 0, 25);
    }

    private static double? DividendResilienceScore(Stock s)
    {
        if (s.DividendCutCount == 0 && s.NonDividendCutYears >= 10) return 100;
        if (s.DividendCutCount == 0) return 80;
        if (s.DividendCutCount <= 1) return 60;
        if (s.DividendCutCount <= 2) return 40;
        return 20;
    }

    // ========== DataConfidence ==========
    private static readonly Func<Stock, double>[] _important =
    {
        s => s.PER, s => s.PBR, s => s.ROE, s => s.EquityRatio, s => s.RevenueGrowth5Y,
        s => s.OperatingProfitGrowthRate, s => s.OperatingMargin, s => s.DividendYield,
        s => s.PayoutRatio, s => s.OperatingCF, s => s.FreeCashFlow, s => s.MarketCap,
        s => s.EPS, s => s.BPS,
    };

    private static double DataConfidence(Stock s)
    {
        int have = _important.Count(f => f(s) != 0);
        return (double)have / _important.Length * 100;
    }

    // ========== ヘルパ ==========

    /// <summary>存在する(0でない)値で重み付き平均。すべて欠損なら 0。</summary>
    private static double Weighted(params (double? sc, double w)[] parts)
    {
        double sw = 0, ss = 0;
        foreach (var (sc, w) in parts) if (sc.HasValue) { ss += sc.Value * w; sw += w; }
        return sw > 0 ? ss / sw : 0;
    }

    /// <summary>先頭から 0 でない最初の値を返す(優先順位つきフォールバック)。すべて 0 なら 0。</summary>
    private static double Pick(params double[] vals)
    {
        foreach (var v in vals) if (v != 0) return v;
        return 0;
    }

    private static bool IsFinancial(string? sector)
    {
        if (string.IsNullOrEmpty(sector)) return false;
        foreach (var kw in new[] { "銀行", "保険", "証券", "その他金融", "金融" })
            if (sector.Contains(kw)) return true;
        return false;
    }

    public static string Grade(double score)
        => score >= 90 ? "S" : score >= 80 ? "A" : score >= 70 ? "B" : score >= 60 ? "C" : score >= 50 ? "D" : "E";

    private static string Judgement(BuffettResult r, Profile profile)
    {
        // 総合商社・卸売業は市況・会計影響で売上がぶれるため、ROE・株主還元・価格水準を主眼にコメントする。
        if (profile == Profile.Trading && r.BuffettScore >= 70)
            return "バフェット型候補。ROE・株主還元・バリュエーションのバランスは良好。ただし商社特有の市況感応度と成長安定性には確認が必要。";

        string baseText = r.OverallGrade switch
        {
            "S" => "超優良。長期保有の中核候補。ただし購入価格には注意。",
            "A" => "バフェット型の有力候補。財務・収益力・成長性のバランスが良い。",
            "B" => "良い会社だが、価格・成長性・財務のいずれかに確認点あり。",
            "C" => "普通。積極的に買うには決め手が弱い。",
            "D" => "要注意。バフェット型としては弱点が多い。",
            _ => "バフェット型では原則見送り。",
        };
        var weakest = new (string name, double v)[]
        {
            ("事業耐久力", r.BusinessDurabilityScore), ("収益力", r.ProfitabilityScore),
            ("財務安全性", r.SafetyScore), ("成長安定性", r.GrowthStabilityScore),
            ("資本配分", r.CapitalAllocationScore), ("割安性", r.ValuationScore),
        }.OrderBy(x => x.v).First();
        string note = weakest.v < 50 ? $" 弱点: {weakest.name}が低め({weakest.v:0})。" : "";
        if (profile == Profile.Trading) note += " ※卸売業/総合商社は営業利益率の絶対値で評価せず、ROE・CF・資本配分・財務を重視して補正。";
        if (r.DataConfidence < 80) note += $" データ信頼度{r.DataConfidence:0}%。";
        return baseText + note;
    }
}
