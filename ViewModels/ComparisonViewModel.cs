using System.Collections.ObjectModel;
using System.Windows.Input;
using DStockAnalysis.Models;

namespace DStockAnalysis.ViewModels;

/// <summary>比較テーブルの1セル。</summary>
public class ComparisonCell
{
    public string Display { get; set; } = "";
    public string Metric { get; set; } = "";   // 条件付き書式の判定キー
    public double Value { get; set; }
    public bool IsNumeric { get; set; }
}

/// <summary>比較テーブルの1行(1指標)。</summary>
public class ComparisonRow
{
    public string Label { get; set; } = "";
    public ObservableCollection<ComparisonCell> Cells { get; } = new();
}

/// <summary>銘柄比較画面の ViewModel。複数銘柄を横並びで比較する。</summary>
public class ComparisonViewModel : ViewModelBase
{
    private const double ChartHeight = 90;
    private readonly MainViewModel _main;

    public ObservableCollection<Stock> AvailableStocks { get; } = new();
    public ObservableCollection<Stock> Selected { get; } = new();
    public ObservableCollection<ComparisonRow> Rows { get; } = new();
    public ObservableCollection<ChartGroup> Charts { get; } = new();

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshAvailable(); }
    }

    private Stock? _pickStock;
    public Stock? PickStock { get => _pickStock; set => SetProperty(ref _pickStock, value); }

    private string _info = "比較したい銘柄を追加してください。";
    public string Info { get => _info; set => SetProperty(ref _info, value); }

    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ClearCommand { get; }

    public ComparisonViewModel(MainViewModel main)
    {
        _main = main;
        AddCommand = new RelayCommand(p => Add((p as Stock) ?? PickStock));
        RemoveCommand = new RelayCommand(p => Remove(p as Stock));
        ClearCommand = new RelayCommand(() => { Selected.Clear(); Rebuild(); });
    }

    public void RefreshSourceData()
    {
        RefreshAvailable();
        // 保存済み比較対象を復元
        if (Selected.Count == 0)
        {
            foreach (var s in _main.AllStocks.Take(3)) Selected.Add(s);
        }
        Rebuild();
    }

    private void RefreshAvailable()
    {
        AvailableStocks.Clear();
        IEnumerable<Stock> src = _main.AllStocks;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            src = src.Where(s => s.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in src.OrderBy(s => s.Code)) AvailableStocks.Add(s);
    }

    public void Add(Stock? s)
    {
        if (s == null || Selected.Contains(s)) return;
        if (Selected.Count >= 6) { Info = "比較は最大6銘柄までです。"; return; }
        Selected.Add(s);
        Rebuild();
    }

    public void Remove(Stock? s)
    {
        if (s == null) return;
        Selected.Remove(s);
        Rebuild();
    }

    public IEnumerable<string> SelectedCodes() => Selected.Select(s => s.Code);

    private void Rebuild()
    {
        BuildRows();
        BuildCharts();
        Info = Selected.Count == 0 ? "比較したい銘柄を追加してください。" : $"{Selected.Count} 銘柄を比較中";
    }

    private void BuildRows()
    {
        Rows.Clear();
        if (Selected.Count == 0) return;

        AddRow("株価", s => s.Price, "price", "N0", "円");
        AddRow("時価総額(百万)", s => s.MarketCap, "cap", "N0", "");
        AddRow("PER", s => s.PER, "per", "0.0", "倍");
        AddRow("PBR", s => s.PBR, "pbr", "0.00", "倍");
        AddRow("ROE", s => s.ROE, "roe", "0.0", "%");
        AddRow("配当利回り", s => s.DividendYield, "dy", "0.00", "%");
        AddRow("配当性向", s => s.PayoutRatio, "payout", "0.0", "%");
        AddRow("自己資本比率", s => s.EquityRatio, "equity", "0.0", "%");
        AddRow("有利子負債比率", s => s.InterestBearingDebtRatio, "debt", "0.0", "%");
        AddRow("増収率3年", s => s.RevenueGrowth3Y, "growth", "0.0", "%");
        AddRow("営業利益率", s => s.OperatingMargin, "margin", "0.0", "%");
        AddRow("純利益率", s => s.NetProfitMargin, "margin", "0.0", "%");
        AddRow("営業CF", s => s.OperatingCF, "cf", "N0", "");
        AddRow("フリーCF", s => s.FreeCashFlow, "cf", "N0", "");
        // 株主優待
        AddFlagRow("株主優待", s => s.HasShareholderBenefit);
        AddTextRow("優待内容", s => string.IsNullOrWhiteSpace(s.ShareholderBenefit) ? "-" : s.ShareholderBenefit);
        AddTextRow("優待カテゴリ", s => string.IsNullOrWhiteSpace(s.BenefitCategory) ? "-" : s.BenefitCategory);
        AddTextRow("優待権利確定月", s => string.IsNullOrWhiteSpace(s.BenefitRightsMonth) ? "-" : s.BenefitRightsMonth);
        AddRow("必要株数", s => s.RequiredSharesForBenefit, "neutral", "N0", "株");
        AddRow("優待価値", s => s.BenefitValue, "neutral", "N0", "円");
        AddRow("優待利回り", s => s.BenefitYield, "benefityield", "0.00", "%");
        AddRow("総合利回り", s => s.TotalYield, "totalyield", "0.00", "%");
        AddFlagRow("長期保有優遇", s => s.HasLongTermBenefit);
        // スコア
        AddRow("長期適性スコア", s => s.LongTermScore, "score", "0", "");
        AddRow("再評価期待スコア", s => s.RevaluationScore, "score", "0", "");
        AddRow("バフェットスコア", s => s.BuffettScore, "score", "0", "");
        AddRow("買いたい度スコア", s => s.WantToBuyScore, "score", "0", "");
        AddTextRow("総合判定", s => s.JudgementText);
    }

    private void AddRow(string label, Func<Stock, double> sel, string metric, string fmt, string unit)
    {
        var row = new ComparisonRow { Label = label };
        foreach (var s in Selected)
        {
            double v = sel(s);
            row.Cells.Add(new ComparisonCell
            {
                Display = v.ToString(fmt) + unit,
                Metric = metric,
                Value = v,
                IsNumeric = true
            });
        }
        Rows.Add(row);
    }

    private void AddTextRow(string label, Func<Stock, string> sel)
    {
        var row = new ComparisonRow { Label = label };
        foreach (var s in Selected)
            row.Cells.Add(new ComparisonCell { Display = sel(s), Metric = "text", IsNumeric = false });
        Rows.Add(row);
    }

    private void AddFlagRow(string label, Func<Stock, bool> sel)
    {
        var row = new ComparisonRow { Label = label };
        foreach (var s in Selected)
            row.Cells.Add(new ComparisonCell { Display = sel(s) ? "○" : "-", Metric = "flag", IsNumeric = false, Value = sel(s) ? 1 : 0 });
        Rows.Add(row);
    }

    private void BuildCharts()
    {
        Charts.Clear();
        if (Selected.Count == 0) return;

        Charts.Add(Group("スコア比較",
            ("バフェットスコア", "#FFB300", s => s.BuffettScore),
            ("長期適性", "#4FC3F7", s => s.LongTermScore),
            ("再評価期待", "#66BB6A", s => s.RevaluationScore),
            ("買いたい度", "#EC407A", s => s.WantToBuyScore)));

        Charts.Add(Group("業績・規模",
            ("時価総額", "#4FC3F7", s => s.MarketCap),
            ("営業CF", "#66BB6A", s => s.OperatingCF),
            ("フリーCF", "#FFA726", s => s.FreeCashFlow)));

        Charts.Add(Group("財務",
            ("自己資本比率", "#4FC3F7", s => s.EquityRatio),
            ("有利子負債比率", "#EF5350", s => s.InterestBearingDebtRatio)));

        Charts.Add(Group("配当・還元",
            ("配当利回り", "#4FC3F7", s => s.DividendYield),
            ("配当性向", "#FFA726", s => s.PayoutRatio),
            ("総合利回り", "#66BB6A", s => s.TotalYield)));

        ChartGroup Group(string title, params (string name, string color, Func<Stock, double> sel)[] defs)
        {
            var g = new ChartGroup { Title = title };
            foreach (var (name, color, sel) in defs)
            {
                var series = new ChartSeries { Name = name, Color = color };
                double max = Selected.Select(s => Math.Abs(sel(s))).DefaultIfEmpty(0).Max();
                if (max <= 0) max = 1;
                foreach (var s in Selected)
                {
                    double v = sel(s);
                    series.Bars.Add(new ChartBar
                    {
                        Label = s.Code,
                        Value = v,
                        Height = Math.Max(1, Math.Abs(v) / max * ChartHeight),
                        Negative = v < 0,
                        Display = Math.Abs(v) >= 1000 ? v.ToString("N0") : v.ToString("0.#"),
                        Color = v < 0 ? "#EF5350" : color
                    });
                }
                g.Series.Add(series);
            }
            return g;
        }
    }
}
