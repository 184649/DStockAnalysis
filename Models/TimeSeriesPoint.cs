namespace DStockAnalysis.Models;

/// <summary>
/// 年度単位の時系列データ。業績・配当・財務・キャッシュフローのグラフ表示に使用する。
/// </summary>
public class TimeSeriesPoint
{
    public int FiscalYear { get; set; }

    // 業績
    public double Revenue { get; set; }          // 売上高
    public double OperatingProfit { get; set; }  // 営業利益
    public double NetIncome { get; set; }        // 純利益

    // 配当
    public double EPS { get; set; }              // 1株利益
    public double Dividend { get; set; }         // 1株配当
    public double PayoutRatio { get; set; }      // 配当性向(%)

    // 財務
    public double NetAssets { get; set; }        // 純資産
    public double Liabilities { get; set; }      // 負債
    public double EquityRatio { get; set; }      // 自己資本比率(%)

    // キャッシュフロー
    public double OperatingCF { get; set; }      // 営業CF
    public double InvestingCF { get; set; }      // 投資CF
    public double FinancingCF { get; set; }      // 財務CF
    public double FreeCF { get; set; }           // フリーCF

    // 自己株式
    public double BuybackAmount { get; set; }    // 自社株買い額
}
