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
    /// 配点(計100):
    ///  1. 資本収益力 20  — ROE(レバレッジ調整)× ROA。借入で嵩上げした ROE は減点。
    ///  2. 利益率・モート 16 — 営業利益率・純利益率(価格決定力=堀の代理指標)。
    ///  3. 利益の一貫性・成長 12 — 増収率・純利益/EPS 成長率(高成長より安定継続を重視)。
    ///  4. 財務健全性 16 — 自己資本比率・有利子負債比率(低負債を高評価)。
    ///  5. キャッシュ創出力 14 — 営業CF/フリーCFの黒字・営業CFマージン(オーナー利益)。
    ///  6. 株主還元の質 8 — 配当性向の無理のなさ・連続増配・自社株買い(CF黒字が前提)。
    ///  7. 割安性(質ゲート付き) 8 — PER/PBR/MIX。ただし上記1〜5の質に比例して減衰=安いだけの不良株は加点しない。
    ///  8. 事業の理解・経営(定性) 6 — バフェットチェックの回答(理解/堀/経営の信頼/10年需要)。
    ///
    /// バフェットチェックの回答は各データ項目に小さな ±補正としても反映する(自身の定性判断を上乗せ)。
    /// </summary>
    public List<BuffettComponent> BuffettBreakdown(Stock s)
    {
        var b = s.BuffettCheck;
        string Pct(double v) => v.ToString("0.#") + "%";
        double Adj(YesNoUnknown a, double yes, double no) => a == YesNoUnknown.Yes ? yes : a == YesNoUnknown.No ? no : 0;

        var list = new List<BuffettComponent>();

        // 1. 資本収益力 (20) — ROE はレバレッジ調整(自己資本比率が低いほど割引く)
        double roe = Scale(s.ROE, 5, 18, 12);
        double levFactor = s.EquityRatio > 0 && s.EquityRatio < 40 ? Math.Clamp(0.55 + 0.45 * s.EquityRatio / 40.0, 0.55, 1) : 1;
        double roeCredit = roe * levFactor;
        double roaCredit = Scale(s.ROA, 2, 10, 8);
        double capital = roeCredit + roaCredit + Adj(b.StableHighRoe, 1.5, -1.5);
        capital = Math.Clamp(capital, 0, 20);
        list.Add(new("capital", "資本収益力(ROE×ROA)", capital, 20,
            $"ROE {Pct(s.ROE)}（レバレッジ調整×{levFactor:0.00}）/ ROA {Pct(s.ROA)}。借入依存の高ROEは割引"));

        // 2. 利益率・モート (16)
        double margins = Scale(s.OperatingMargin, 2, 18, 9) + Scale(s.NetProfitMargin, 1, 12, 7)
                         + Adj(b.HighMargin, 1.5, -1.5);
        margins = Math.Clamp(margins, 0, 16);
        list.Add(new("margins", "利益率・モート", margins, 16,
            $"営業利益率 {Pct(s.OperatingMargin)} / 純利益率 {Pct(s.NetProfitMargin)}。高く安定した利益率は価格決定力の証拠"));

        // 3. 利益の一貫性・成長 (12) — 高すぎる成長より安定継続を重視。マイナス成長は減点
        double g = Scale(s.RevenueGrowthRate != 0 ? s.RevenueGrowthRate : s.RevenueGrowth1Y, -5, 15, 4)
                   + Scale(s.NetProfitGrowthRate, -10, 20, 4)
                   + Scale(s.EpsGrowthRate, -10, 20, 4);
        g = Math.Clamp(g, 0, 12);
        list.Add(new("growth", "利益の一貫性・成長", g, 12,
            $"増収率 {Pct(s.RevenueGrowthRate != 0 ? s.RevenueGrowthRate : s.RevenueGrowth1Y)} / 純利益成長 {Pct(s.NetProfitGrowthRate)} / EPS成長 {Pct(s.EpsGrowthRate)}"));

        // 4. 財務健全性 (16)
        double fin = Scale(s.EquityRatio, 25, 65, 10) + ScaleInverse(s.InterestBearingDebtRatio, 30, 200, 6)
                     + Adj(b.SoundFinance, 0, -2);
        fin = Math.Clamp(fin, 0, 16);
        list.Add(new("finance", "財務健全性", fin, 16,
            $"自己資本比率 {Pct(s.EquityRatio)} / 有利子負債比率 {Pct(s.InterestBearingDebtRatio)}。低負債・厚い自己資本を高評価"));

        // 5. キャッシュ創出力 (14)
        double cash = (s.OperatingCF > 0 ? 5 : 0) + (s.FreeCashFlow > 0 ? 5 : 0) + Scale(s.OperatingCashFlowMargin, 2, 20, 4)
                      + Adj(b.StablePositiveOperatingCf, 0.5, 0) + Adj(b.StablePositiveFreeCf, 0.5, 0);
        cash = Math.Clamp(cash, 0, 14);
        list.Add(new("cash", "キャッシュ創出力", cash, 14,
            $"営業CF {(s.OperatingCF > 0 ? "黒字" : "未黒字")} / フリーCF {(s.FreeCashFlow > 0 ? "黒字" : "未黒字")} / 営業CFマージン {Pct(s.OperatingCashFlowMargin)}"));

        // 6. 株主還元の質 (8) — CFが黒字であることを前提に評価(無理な還元は評価しない)
        double ret = PayoutQuality(s.PayoutRatio) * 4 + Scale(s.ConsecutiveDividendYears, 0, 20, 2);
        if (s.BuybackAmount > 0 && s.FreeCashFlow > 0) ret += 1.5; // 自社株買いはバフェットが好む(CF黒字が条件)
        if (s.CumulativeDividend) ret += 0.5;
        if (s.FreeCashFlow <= 0 || s.PayoutRatio > 90) ret *= 0.5; // CF赤字/過大性向の還元は割引
        ret += Adj(b.SustainableReturn, 0, -2);
        ret = Math.Clamp(ret, 0, 8);
        list.Add(new("return", "株主還元の質", ret, 8,
            $"配当性向 {Pct(s.PayoutRatio)} / 連続増配 {s.ConsecutiveDividendYears}年 / 自社株買い {(s.BuybackAmount > 0 ? "あり" : "なし")}"));

        // 質ゲート: 1〜5(収益性・財務・CF)の充足度。安いだけの不良株に割安加点を与えないため。
        double qualityMax = 20 + 16 + 12 + 16 + 14;
        double quality = Math.Clamp((capital + margins + g + fin + cash) / qualityMax, 0, 1);

        // 7. 割安性(質ゲート付き) (8)
        double valRaw = ScaleInverse(s.PER, 8, 30, 3.5) + ScaleInverse(s.PBR, 0.7, 4, 2.5) + ScaleInverse(s.MixFactor, 6, 35, 2);
        double val = valRaw * quality + Adj(b.NotOverpriced, 0, -1.5);
        val = Math.Clamp(val, 0, 8);
        list.Add(new("valuation", "割安性(質ゲート付き)", val, 8,
            $"PER {s.PER:0.#} / PBR {s.PBR:0.##} / MIX {s.MixFactor:0.#}。割安度を事業の質（{quality * 100:0}%）で調整"));

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
