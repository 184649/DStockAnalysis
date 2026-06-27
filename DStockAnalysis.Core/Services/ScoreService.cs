using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// 各種スコアを 0-100 で算出する。投資判断(買い/売り)は出さず、
/// 「買いたい銘柄を見つける補助」としての相対評価のみを行う。
/// </summary>
public class ScoreService
{
    /// <summary>0..1 の度合いを 0..max にスケールするヘルパ。</summary>
    private static double Scale(double value, double low, double high, double max)
    {
        if (high <= low) return 0;
        var t = (value - low) / (high - low);
        t = Math.Clamp(t, 0, 1);
        return t * max;
    }

    /// <summary>大きいほど悪い指標を、低いほど高得点に反転スケール。</summary>
    private static double ScaleInverse(double value, double low, double high, double max)
        => max - Scale(value, low, high, max);

    public void Recalculate(Stock s)
    {
        s.SafetyScore = CalcSafety(s);
        s.GrowthScore = CalcGrowth(s);
        s.ProfitabilityScore = CalcProfitability(s);
        s.ReturnScore = CalcReturn(s);
        s.EfficiencyScore = CalcEfficiency(s);
        s.ValuationScore = CalcValuation(s);

        s.LongTermScore = CalcLongTerm(s);
        s.RevaluationScore = CalcRevaluation(s);
        s.BuffettScore = CalcBuffett(s);
        s.WantToBuyScore = CalcWantToBuy(s);

        s.OverallScore = Math.Round(
            s.LongTermScore * 0.30 +
            s.BuffettScore * 0.30 +
            s.RevaluationScore * 0.20 +
            s.WantToBuyScore * 0.20, 0);

        s.Judgement = DecideJudgement(s);
    }

    // ===== 6軸スコア(レーダー用) =====

    /// <summary>安全性: 自己資本比率・有利子負債比率・配当金残年数。</summary>
    public double CalcSafety(Stock s)
    {
        double v = 0;
        v += Scale(s.EquityRatio, 20, 80, 50);
        v += ScaleInverse(s.InterestBearingDebtRatio, 0, 100, 35);
        v += Scale(s.DividendRemainingYears, 0, 15, 15);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>成長性: 各種増収率・利益成長率。</summary>
    public double CalcGrowth(Stock s)
    {
        double v = 0;
        v += Scale(s.RevenueGrowth1Y, -5, 25, 20);
        v += Scale(s.RevenueGrowth3Y, -5, 20, 20);
        v += Scale(s.OperatingProfitGrowthRate, -10, 30, 20);
        v += Scale(s.NetProfitGrowthRate, -10, 30, 20);
        v += Scale(s.EpsGrowthRate, -10, 30, 20);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>収益性: 営業利益率・純利益率・ROE。</summary>
    public double CalcProfitability(Stock s)
    {
        double v = 0;
        v += Scale(s.OperatingMargin, 0, 25, 40);
        v += Scale(s.NetProfitMargin, 0, 18, 30);
        v += Scale(s.ROE, 0, 20, 30);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>還元性: 配当・連続増配・自社株買い・株主優待。ただし優待過大評価を避ける。</summary>
    public double CalcReturn(Stock s)
    {
        double v = 0;
        v += Scale(s.DividendYield, 0, 5, 30);
        v += Scale(s.ConsecutiveDividendYears, 0, 20, 20);
        // 配当性向は高すぎても低すぎても満点でない(30-60%が安全圏)
        v += PayoutQuality(s.PayoutRatio) * 20;
        v += (s.BuybackAmount > 0 ? 10 : 0);
        v += (s.CumulativeDividend ? 5 : 0);
        // 株主優待は加点要素(最大15)。総合利回りを軽く反映。
        double benefit = Scale(s.BenefitYield, 0, 3, 10) + (s.HasLongTermBenefit ? 3 : 0) + (s.HasShareholderBenefit ? 2 : 0);
        // 業績/CFが弱いのに優待利回りが高い場合は加点を抑制
        if (s.FreeCashFlow <= 0 || s.PayoutRatio > 90) benefit *= 0.4;
        v += Math.Min(benefit, 15);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>効率性: ROE・営業CFマージン。</summary>
    public double CalcEfficiency(Stock s)
    {
        double v = 0;
        v += Scale(s.ROE, 0, 20, 60);
        v += Scale(s.OperatingCashFlowMargin, 0, 25, 40);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>割安性: PER・PBR・MIX係数。低いほど高得点。</summary>
    public double CalcValuation(Stock s)
    {
        double v = 0;
        v += ScaleInverse(s.PER, 8, 35, 40);
        v += ScaleInverse(s.PBR, 0.7, 4, 30);
        v += ScaleInverse(s.MixFactor, 6, 40, 30);
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    // ===== 合成スコア =====

    /// <summary>長期適性: 財務健全性・収益安定性・株主還元・CF・参入障壁・10年後需要。</summary>
    public double CalcLongTerm(Stock s)
    {
        double v = 0;
        v += CalcSafety(s) * 0.25;
        v += CalcProfitability(s) * 0.20;
        v += CalcReturn(s) * 0.15;
        v += (s.FreeCashFlow > 0 ? 100 : 30) * 0.15;
        v += YesScore(s.BuffettCheck.HasEntryBarrier) * 0.125;
        v += YesScore(s.BuffettCheck.DemandIn10Years) * 0.125;
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>再評価期待: 売上/営業利益/利益率/EPS成長・上方修正(株価変化)・テーマ需要。</summary>
    public double CalcRevaluation(Stock s)
    {
        double v = 0;
        v += Scale(s.RevenueGrowthRate, -5, 25, 20);
        v += Scale(s.OperatingProfitGrowthRate, -10, 35, 20);
        v += Scale(s.NetProfitGrowthRate, -10, 35, 15);
        v += Scale(s.EpsGrowthRate, -10, 35, 15);
        v += Scale(s.StockPriceChange3M, -20, 30, 15); // 市場評価の変化
        v += (!string.IsNullOrWhiteSpace(s.Theme) ? 15 : 0); // テーマ需要
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    /// <summary>バフェットスコア(0-100)。内訳の合計。詳細は <see cref="BuffettBreakdown"/> を参照。</summary>
    public double CalcBuffett(Stock s)
        => Math.Round(Math.Clamp(BuffettBreakdown(s).Sum(c => c.Earned), 0, 100), 0);

    /// <summary>
    /// バフェットスコアの内訳(配点・獲得点・根拠)を返す。スコアの透明性を確保するための明細。
    ///
    /// ウォーレン・バフェットの投資原則(Berkshire 株主への手紙 /「The Essays of Warren Buffett」/
    /// Buffettology)に基づき、以下を重視して採点する。安さより「質」を優先し、
    /// バフェットが嫌う特徴(過度なレバレッジで嵩上げした ROE、薄利・コモディティ事業、
    /// CFを生まない事業、財務が脆弱なのに高配当、業績悪化中で見かけ上だけ割安=バリュートラップ)
    /// に高得点が付かないようにする。
    ///
    /// スコアは「現在株価での“バフェット流の投資”としての魅力」を表す。事業の質と価格の両方を見るため、
    /// 優れた事業でも割高なら満点にはならず、資本効率の低い事業は割安でも中位に留まる。
    ///
    /// 配点(計100):
    ///  1. 資本収益力 20  — ROA を主・ROE を従(自己資本比率が低い=借入で嵩上げした ROE は割引)。無借金の高ROAを高評価。
    ///  2. 利益率・モート 12 — 営業利益率・純利益率(価格決定力=堀)。薄利は減点するが致命傷にはしない。
    ///  3. キャッシュ創出力 14 — 営業CF/フリーCFの黒字・営業CFマージン(オーナー利益)。
    ///  4. 財務健全性 14 — 自己資本比率・有利子負債比率(低負債を高評価)。
    ///  5. 利益の継続・成長 8 — 増収率・純利益/EPS 成長率(高成長より着実さ)。
    ///  6. 株主還元 or 再投資の質 12 — 高還元と「高ROAでの内部再投資(低配当でも)」の良い方で評価。
    ///  7. 割安性・安全余裕 14 — PER/PBR/MIX。質を犠牲にしない割安。経営不振(赤字/FCF赤字/債務超過寸前)時のみ割引。
    ///  8. 事業の理解・経営(定性) 6 — バフェットチェックの回答(理解/堀/経営の信頼/10年需要)。自動取得不可。
    ///
    /// バフェットチェックの回答は各データ項目に小さな ±補正としても反映する(自身の定性判断を上乗せ)。
    /// </summary>
    public List<BuffettComponent> BuffettBreakdown(Stock s)
    {
        var b = s.BuffettCheck;
        string Pct(double v) => v.ToString("0.#") + "%";
        double Adj(YesNoUnknown a, double yes, double no) => a == YesNoUnknown.Yes ? yes : a == YesNoUnknown.No ? no : 0;

        var list = new List<BuffettComponent>();

        // 1. 資本収益力 (20) — ROA を主、ROE を従(レバレッジで嵩上げした ROE は割引)。
        //    無借金で高 ROA(=高い実質的な資本効率)をバフェットは最も好む。
        double roaCredit = Scale(s.ROA, 2, 12, 12);
        double levFactor = s.EquityRatio > 0 && s.EquityRatio < 35 ? Math.Clamp(0.6 + 0.4 * s.EquityRatio / 35.0, 0.6, 1) : 1;
        double roeCredit = Scale(s.ROE, 6, 18, 8) * levFactor;
        double capital = Math.Clamp(roaCredit + roeCredit + Adj(b.StableHighRoe, 1, -1), 0, 20);
        list.Add(new("capital", "資本収益力(ROA・ROE)", capital, 20,
            $"ROA {Pct(s.ROA)} / ROE {Pct(s.ROE)}（レバレッジ調整×{levFactor:0.00}）。無借金の高ROAを高評価"));

        // 2. 利益率・モート (12) — 薄利は減点するが致命傷にはしない(商社等の薄利事業を過度に切らない)。
        double margins = Math.Clamp(Scale(s.OperatingMargin, 3, 25, 7) + Scale(s.NetProfitMargin, 2, 15, 5) + Adj(b.HighMargin, 1, -1), 0, 12);
        list.Add(new("margins", "利益率・モート", margins, 12,
            $"営業利益率 {Pct(s.OperatingMargin)} / 純利益率 {Pct(s.NetProfitMargin)}。高い利益率は価格決定力(堀)の証拠"));

        // 3. キャッシュ創出力 (14) — オーナー利益。バフェットが最重視する一つ。
        double cash = Math.Clamp((s.OperatingCF > 0 ? 5 : 0) + (s.FreeCashFlow > 0 ? 5 : 0) + Scale(s.OperatingCashFlowMargin, 3, 22, 4)
                      + Adj(b.StablePositiveOperatingCf, 0.5, 0) + Adj(b.StablePositiveFreeCf, 0.5, 0), 0, 14);
        list.Add(new("cash", "キャッシュ創出力", cash, 14,
            $"営業CF {(s.OperatingCF > 0 ? "黒字" : "未取得/赤字")} / フリーCF {(s.FreeCashFlow > 0 ? "黒字" : "未取得/赤字")} / 営業CFマージン {Pct(s.OperatingCashFlowMargin)}"));

        // 4. 財務健全性 (14)
        double fin = Math.Clamp(Scale(s.EquityRatio, 20, 60, 9) + ScaleInverse(s.InterestBearingDebtRatio, 30, 200, 5) + Adj(b.SoundFinance, 0, -2), 0, 14);
        list.Add(new("finance", "財務健全性", fin, 14,
            $"自己資本比率 {Pct(s.EquityRatio)} / 有利子負債比率 {Pct(s.InterestBearingDebtRatio)}。低負債・厚い自己資本を高評価"));

        // 5. 利益の継続・成長 (8) — 高成長より着実さ。マイナス成長は減点。
        double g = Math.Clamp(Scale(s.RevenueGrowthRate != 0 ? s.RevenueGrowthRate : s.RevenueGrowth1Y, -5, 15, 3)
                   + Scale(s.NetProfitGrowthRate, -10, 20, 3) + Scale(s.EpsGrowthRate, -10, 20, 2), 0, 8);
        list.Add(new("growth", "利益の継続・成長", g, 8,
            $"増収率 {Pct(s.RevenueGrowthRate != 0 ? s.RevenueGrowthRate : s.RevenueGrowth1Y)} / 純利益成長 {Pct(s.NetProfitGrowthRate)} / EPS成長 {Pct(s.EpsGrowthRate)}"));

        // 6. 株主還元 or 再投資の質 (12) — 高還元と「高ROAで再投資」のどちらか良い方で評価。
        //    バフェットは高ROICでの内部再投資(=低配当でも)を最も評価するため、低配当を一律に減点しない。
        double shareholder = Scale(s.DividendYield, 0, 4, 6) + PayoutQuality(s.PayoutRatio) * 3
                             + (s.BuybackAmount > 0 ? 2 : 0) + Scale(s.ConsecutiveDividendYears, 0, 20, 2) + (s.CumulativeDividend ? 1 : 0);
        double retention = s.PayoutRatio > 0 ? Math.Clamp((100 - s.PayoutRatio) / 100.0, 0, 1) : 0.6;
        double reinvest = Scale(s.ROA, 4, 13, 8) * (0.5 + 0.5 * retention) + Scale(s.ROE, 10, 20, 4);
        double ret = Math.Clamp(Math.Max(shareholder, reinvest) + Adj(b.SustainableReturn, 0, -1.5), 0, 12);
        if (s.FreeCashFlow < 0 || s.PayoutRatio > 100) ret = Math.Min(ret, 6); // 無理な還元は頭打ち
        list.Add(new("return", "株主還元/再投資の質", ret, 12,
            $"配当利回り {Pct(s.DividendYield)} / 配当性向 {Pct(s.PayoutRatio)} / 自社株買い {(s.BuybackAmount > 0 ? "あり" : "なし")}。低配当でも高ROAでの再投資は評価"));

        // 7. 割安性・安全余裕 (14) — バフェットの主要因。質ゲートは「経営不振」(赤字/FCF赤字/債務超過寸前)のみに限定。
        double valRaw = ScaleInverse(s.PER, 8, 45, 6) + ScaleInverse(s.PBR, 0.7, 6, 5) + ScaleInverse(s.MixFactor, 6, 50, 3);
        bool distressed = s.FreeCashFlow < 0 || s.NetProfitMargin < 0 || (s.EquityRatio > 0 && s.EquityRatio < 15);
        double val = Math.Clamp(valRaw * (distressed ? 0.3 : 1) + Adj(b.NotOverpriced, 0, -1.5), 0, 14);
        list.Add(new("valuation", "割安性・安全余裕", val, 14,
            $"PER {s.PER:0.#} / PBR {s.PBR:0.##} / MIX {s.MixFactor:0.#}{(distressed ? "（業績不振のため割引）" : "")}。安全余裕(質を犠牲にしない割安)"));

        // 8. 事業の理解・経営(定性) (6)
        double qual = Avg(
            YesScore(b.UnderstandBusiness), YesScore(b.CanExplainEarnings), YesScore(b.DemandIn10Years),
            YesScore(b.HasCompetitiveAdvantage), YesScore(b.HasEntryBarrier), YesScore(b.TrustManagement),
            YesScore(b.WantToBuyOnCrash), YesScore(b.CanWrite10YearReason)) / 100.0 * 6;
        list.Add(new("qualitative", "事業の理解・経営(定性)", qual, 6,
            "事業理解 / 競争優位 / 参入障壁 / 経営の信頼 / 10年後需要(バフェットチェックの回答)"));

        return list;
    }

    /// <summary>買いたい度: 長期適性・再評価期待・バフェット適性・割高感・興味・事業理解・分類。</summary>
    public double CalcWantToBuy(Stock s)
    {
        double v = 0;
        v += s.LongTermScore * 0.25;
        v += s.RevaluationScore * 0.15;
        v += s.BuffettScore * 0.25;
        v += s.ValuationScore * 0.10;            // 割高感(割安なら加点)
        v += s.UserInterest * 0.10;              // 自分の興味
        v += YesScore(s.BuffettCheck.UnderstandBusiness) * 0.05;
        v += ClassificationBonus(s.Memo.Classification) * 0.10;
        return Math.Round(Math.Clamp(v, 0, 100), 0);
    }

    // ===== 補助 =====

    private static double YesScore(YesNoUnknown a) => a switch
    {
        YesNoUnknown.Yes => 100,
        YesNoUnknown.No => 0,
        _ => 50
    };

    private static double Avg(params double[] xs) => xs.Length == 0 ? 0 : xs.Average();

    /// <summary>配当性向の安全度を 0..1 で返す。30-55% を満点、超高/超低を減点。</summary>
    private static double PayoutQuality(double payout)
    {
        if (payout <= 0) return 0.3;
        if (payout < 20) return 0.5 + payout / 20.0 * 0.3;     // 0.5..0.8
        if (payout <= 55) return 1.0;
        if (payout <= 80) return 1.0 - (payout - 55) / 25.0 * 0.5; // 1.0..0.5
        if (payout <= 100) return 0.5 - (payout - 80) / 20.0 * 0.4; // 0.5..0.1
        return 0.1;
    }

    private static double ClassificationBonus(StockClassification c) => c switch
    {
        StockClassification.最重要候補 => 100,
        StockClassification.長期優良株候補 => 80,
        StockClassification.第二のキオクシア候補 => 80,
        StockClassification.再評価候補 => 70,
        StockClassification.決算確認待ち => 55,
        StockClassification.保留 => 30,
        StockClassification.除外 => 0,
        _ => 50
    };

    private OverallJudgement DecideJudgement(Stock s)
    {
        if (s.Memo.Classification == StockClassification.除外) return OverallJudgement.除外;
        if (s.Memo.Classification == StockClassification.保留) return OverallJudgement.保留;

        if (s.BuffettScore >= 80 && s.LongTermScore >= 75 && s.WantToBuyScore >= 75)
            return OverallJudgement.最重要候補;
        if (s.LongTermScore >= 75 && s.SafetyScore >= 70)
            return OverallJudgement.長期優良株候補;
        if (s.RevaluationScore >= 75 && !string.IsNullOrWhiteSpace(s.Theme) && s.GrowthScore >= 70)
            return OverallJudgement.第二のキオクシア候補;
        if (s.RevaluationScore >= 70)
            return OverallJudgement.再評価候補;
        if (s.TotalYield >= 4 && s.ReturnScore >= 70)
            return OverallJudgement.高配当_還元候補;
        if (!string.IsNullOrWhiteSpace(s.Theme) && s.GrowthScore >= 65)
            return OverallJudgement.テーマ候補;
        if (s.Memo.Classification == StockClassification.決算確認待ち)
            return OverallJudgement.決算確認候補;
        return OverallJudgement.調査中;
    }
}
