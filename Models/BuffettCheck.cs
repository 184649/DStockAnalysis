namespace DStockAnalysis.Models;

/// <summary>
/// バフェット型チェックリスト。各項目を はい/いいえ/不明 で管理する。
/// チェック結果はバフェットスコアにも反映される。
/// </summary>
public class BuffettCheck
{
    public YesNoUnknown CanExplainEarnings { get; set; }        // この会社は何で稼いでいるか説明できる
    public YesNoUnknown UnderstandBusiness { get; set; }        // 事業内容を理解できる
    public YesNoUnknown DemandIn10Years { get; set; }           // 10年後も需要がある
    public YesNoUnknown HasCompetitiveAdvantage { get; set; }   // 競争優位性がある
    public YesNoUnknown HasEntryBarrier { get; set; }           // 参入障壁がある
    public YesNoUnknown HighMargin { get; set; }                // 高い利益率を維持している
    public YesNoUnknown StableHighRoe { get; set; }             // ROEが安定して高い
    public YesNoUnknown StablePositiveOperatingCf { get; set; } // 営業CFが安定して黒字
    public YesNoUnknown StablePositiveFreeCf { get; set; }      // フリーCFが安定して黒字
    public YesNoUnknown SoundFinance { get; set; }              // 財務が健全
    public YesNoUnknown SustainableReturn { get; set; }         // 配当や自社株買いに無理がない
    public YesNoUnknown TrustManagement { get; set; }           // 経営者の説明に納得できる
    public YesNoUnknown NotOverpriced { get; set; }             // 割高すぎない
    public YesNoUnknown WantToBuyOnCrash { get; set; }          // 暴落時に買い増ししたい
    public YesNoUnknown CanWrite10YearReason { get; set; }      // 10年保有する理由を書ける

    /// <summary>全項目を列挙する（スコア計算・UIバインド用）。</summary>
    public IEnumerable<YesNoUnknown> AllAnswers()
    {
        yield return CanExplainEarnings;
        yield return UnderstandBusiness;
        yield return DemandIn10Years;
        yield return HasCompetitiveAdvantage;
        yield return HasEntryBarrier;
        yield return HighMargin;
        yield return StableHighRoe;
        yield return StablePositiveOperatingCf;
        yield return StablePositiveFreeCf;
        yield return SoundFinance;
        yield return SustainableReturn;
        yield return TrustManagement;
        yield return NotOverpriced;
        yield return WantToBuyOnCrash;
        yield return CanWrite10YearReason;
    }
}
