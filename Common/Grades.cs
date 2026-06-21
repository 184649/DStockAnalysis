namespace DStockAnalysis.Common;

/// <summary>0-100 スコアを S/A/B/C/D のレターグレードへ変換する。</summary>
public static class Grades
{
    public static string Letter(double score) => score switch
    {
        >= 85 => "S",
        >= 75 => "A",
        >= 60 => "B",
        >= 45 => "C",
        _ => "D"
    };
}
