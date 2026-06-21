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

    /// <summary>
    /// バフェットスコア(100点満点)。
    /// 事業理解・長期需要15 / 競争優位・参入障壁15 / 収益性15 / キャッシュ創出力15 /
    /// 財務健全性15 / 株主還元の質10 / 割安性・価格妥当性10 / 暴落時保有適性5。
    /// バフェットチェックの回答(はい加点/いいえ減点)も反映する。
    /// </summary>
    public double CalcBuffett(Stock s)
    {
        var b = s.BuffettCheck;
        double v = 0;

        // 事業理解・長期需要 (15)
        v += Avg(YesScore(b.UnderstandBusiness), YesScore(b.CanExplainEarnings), YesScore(b.DemandIn10Years)) / 100.0 * 15;

        // 競争優位性・参入障壁 (15)
        v += Avg(YesScore(b.HasCompetitiveAdvantage), YesScore(b.HasEntryBarrier)) / 100.0 * 15;

        // 収益性 (15) — 営業利益率・ROE 中心
        double profit = Scale(s.OperatingMargin, 0, 25, 8) + Scale(s.ROE, 0, 20, 7);
        if (b.HighMargin == YesNoUnknown.Yes) profit = Math.Min(15, profit + 2);
        if (b.HighMargin == YesNoUnknown.No) profit = Math.Max(0, profit - 2);
        v += Math.Min(profit, 15);

        // キャッシュ創出力 (15) — 営業CF/フリーCFの安定黒字
        double cash = (s.OperatingCF > 0 ? 6 : 0) + (s.FreeCashFlow > 0 ? 6 : 0) + Scale(s.OperatingCashFlowMargin, 0, 25, 3);
        if (b.StablePositiveOperatingCf == YesNoUnknown.Yes) cash = Math.Min(15, cash + 1);
        if (b.StablePositiveFreeCf == YesNoUnknown.Yes) cash = Math.Min(15, cash + 1);
        v += Math.Min(cash, 15);

        // 財務健全性 (15) — 自己資本比率・有利子負債比率
        double fin = Scale(s.EquityRatio, 20, 80, 9) + ScaleInverse(s.InterestBearingDebtRatio, 0, 100, 6);
        if (b.SoundFinance == YesNoUnknown.No) fin = Math.Max(0, fin - 3);
        v += Math.Min(fin, 15);

        // 株主還元の質 (10) — 配当の持続性・性向の安全性。優待は小さく加点のみ。
        double ret = PayoutQuality(s.PayoutRatio) * 5 + Scale(s.ConsecutiveDividendYears, 0, 20, 3);
        if (s.HasShareholderBenefit && s.FreeCashFlow > 0 && s.PayoutRatio < 80) ret += 1; // 優待は小さく
        if (b.SustainableReturn == YesNoUnknown.No) ret = Math.Max(0, ret - 2);
        v += Math.Min(ret, 10);

        // 割安性・価格妥当性 (10) — PER/PBR/MIX
        double val = ScaleInverse(s.PER, 8, 35, 4) + ScaleInverse(s.PBR, 0.7, 4, 3) + ScaleInverse(s.MixFactor, 6, 40, 3);
        if (b.NotOverpriced == YesNoUnknown.No) val = Math.Max(0, val - 2);
        v += Math.Min(val, 10);

        // 暴落時保有適性 (5)
        v += Avg(YesScore(b.WantToBuyOnCrash), YesScore(b.CanWrite10YearReason)) / 100.0 * 5;

        return Math.Round(Math.Clamp(v, 0, 100), 0);
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
