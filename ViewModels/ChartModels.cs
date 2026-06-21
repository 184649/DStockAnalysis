using System.Collections.ObjectModel;

namespace DStockAnalysis.ViewModels;

/// <summary>簡易バーチャート用のデータ構造。LiveCharts2 等への差し替えを想定し View から分離。</summary>
public class ChartBar
{
    public string Label { get; set; } = "";   // 年度など
    public double Value { get; set; }          // 実数値
    public double Height { get; set; }         // ピクセル高さ(正の値)
    public bool Negative { get; set; }         // 負値か
    public string Display { get; set; } = "";  // 表示文字列
    public string Color { get; set; } = "#4FC3F7"; // バー色(負値は赤系)
}

/// <summary>1系列(売上高など)のバー集合。</summary>
public class ChartSeries
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4FC3F7";
    public ObservableCollection<ChartBar> Bars { get; } = new();
}

/// <summary>複数系列をまとめたチャート(業績推移など)。</summary>
public class ChartGroup
{
    public string Title { get; set; } = "";
    public ObservableCollection<ChartSeries> Series { get; } = new();
}

/// <summary>レーダー(横バーゲージ)の1軸。</summary>
public class GaugeItem
{
    public string Label { get; set; } = "";
    public double Score { get; set; }            // 0-100
    public string Grade { get; set; } = "";      // S/A/B/C/D
    public double BarWidth { get; set; }         // ピクセル幅
    public string Color { get; set; } = "#4FC3F7";
}
