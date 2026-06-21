using System.Collections.ObjectModel;
using System.Windows.Input;
using DStockAnalysis.Common;
using DStockAnalysis.Models;

namespace DStockAnalysis.ViewModels;

/// <summary>バフェットチェック1項目。回答変更でスコア再計算コールバックを呼ぶ。</summary>
public class CheckItem : ObservableObject
{
    private readonly Func<YesNoUnknown> _get;
    private readonly Action<YesNoUnknown> _set;
    private readonly Action _onChanged;

    public string Label { get; }

    public CheckItem(string label, Func<YesNoUnknown> get, Action<YesNoUnknown> set, Action onChanged)
    {
        Label = label; _get = get; _set = set; _onChanged = onChanged;
    }

    public YesNoUnknown Answer
    {
        get => _get();
        set
        {
            if (_get() == value) return;
            _set(value);
            OnPropertyChanged();
            _onChanged();
        }
    }
}

/// <summary>個別銘柄分析画面の ViewModel。</summary>
public class StockAnalysisViewModel : ViewModelBase
{
    private const double ChartHeight = 110;
    private const double GaugeMax = 170;

    private readonly MainViewModel _main;

    public ObservableCollection<Stock> StockList { get; } = new();
    public ObservableCollection<ChartGroup> Charts { get; } = new();
    public ObservableCollection<GaugeItem> Radar { get; } = new();
    public ObservableCollection<CheckItem> BuffettItems { get; } = new();

    public Array YesNoOptions => Enum.GetValues(typeof(YesNoUnknown));
    public Array ClassificationOptions => Enum.GetValues(typeof(StockClassification));

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshList(); }
    }

    private Stock? _selectedStock;
    public Stock? SelectedStock
    {
        get => _selectedStock;
        set
        {
            if (SetProperty(ref _selectedStock, value))
            {
                RebuildDetail();
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedStock != null;

    private readonly Services.ReferenceLinkService _refService = new();

    /// <summary>選択銘柄の外部データソース(IR BANK等)へのリンク。最新の実数値確認に使用。</summary>
    public ObservableCollection<Services.ReferenceLink> ReferenceLinks { get; } = new();

    public ICommand ApplyCommand { get; }          // チェック/メモ反映+保存
    public ICommand ShowScreeningCommand { get; }
    public ICommand ShowComparisonCommand { get; }
    public ICommand AddToComparisonCommand { get; }
    public ICommand OpenIrCommand { get; }
    public ICommand OpenLinkCommand { get; }

    public StockAnalysisViewModel(MainViewModel main)
    {
        _main = main;
        ApplyCommand = new RelayCommand(ApplyChanges);
        ShowScreeningCommand = main.ShowScreeningCommand;
        ShowComparisonCommand = main.ShowComparisonCommand;
        AddToComparisonCommand = new RelayCommand(() => _main.ComparisonVM.Add(SelectedStock));
        OpenIrCommand = new RelayCommand(OpenIr);
        OpenLinkCommand = new RelayCommand(p => OpenUrl((p as Services.ReferenceLink)?.Url));
    }

    public void RefreshSourceData()
    {
        RefreshList();
        if (SelectedStock == null || !_main.AllStocks.Contains(SelectedStock))
            SelectedStock = StockList.FirstOrDefault();
        else
            RebuildDetail();
    }

    private void RefreshList()
    {
        StockList.Clear();
        IEnumerable<Stock> src = _main.AllStocks;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            src = src.Where(s => s.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || s.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var s in src.OrderBy(s => s.Code)) StockList.Add(s);
    }

    private void ApplyChanges()
    {
        if (SelectedStock == null) return;
        _main.Recalculate(SelectedStock);
        RebuildDetail();
        _main.SaveAll();
        _main.StatusText = $"{SelectedStock.Name} のチェック・メモを保存し、スコアを再計算しました。";
    }

    private void OpenIr() => OpenUrl(SelectedStock?.IRUrl);

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* リンクが無効でも無視 */ }
    }

    private void RebuildDetail()
    {
        if (SelectedStock != null) _main.EnsureHistory(SelectedStock); // 時系列を遅延生成
        BuildBuffettItems();
        BuildCharts();
        BuildRadar();
        BuildReferenceLinks();
        OnPropertyChanged(nameof(SelectedStock));
    }

    private void BuildReferenceLinks()
    {
        ReferenceLinks.Clear();
        if (SelectedStock == null) return;
        foreach (var link in _refService.BuildLinks(SelectedStock.Code))
            ReferenceLinks.Add(link);
    }

    private void BuildBuffettItems()
    {
        BuffettItems.Clear();
        var s = SelectedStock;
        if (s == null) return;
        var b = s.BuffettCheck;
        void Add(string label, Func<YesNoUnknown> g, Action<YesNoUnknown> set)
            => BuffettItems.Add(new CheckItem(label, g, set, OnCheckChanged));

        Add("この会社は何で稼いでいるか説明できる", () => b.CanExplainEarnings, v => b.CanExplainEarnings = v);
        Add("事業内容を理解できる", () => b.UnderstandBusiness, v => b.UnderstandBusiness = v);
        Add("10年後も需要がある", () => b.DemandIn10Years, v => b.DemandIn10Years = v);
        Add("競争優位性がある", () => b.HasCompetitiveAdvantage, v => b.HasCompetitiveAdvantage = v);
        Add("参入障壁がある", () => b.HasEntryBarrier, v => b.HasEntryBarrier = v);
        Add("高い利益率を維持している", () => b.HighMargin, v => b.HighMargin = v);
        Add("ROEが安定して高い", () => b.StableHighRoe, v => b.StableHighRoe = v);
        Add("営業CFが安定して黒字", () => b.StablePositiveOperatingCf, v => b.StablePositiveOperatingCf = v);
        Add("フリーCFが安定して黒字", () => b.StablePositiveFreeCf, v => b.StablePositiveFreeCf = v);
        Add("財務が健全", () => b.SoundFinance, v => b.SoundFinance = v);
        Add("配当や自社株買いに無理がない", () => b.SustainableReturn, v => b.SustainableReturn = v);
        Add("経営者の説明に納得できる", () => b.TrustManagement, v => b.TrustManagement = v);
        Add("割高すぎない", () => b.NotOverpriced, v => b.NotOverpriced = v);
        Add("暴落時に買い増ししたい", () => b.WantToBuyOnCrash, v => b.WantToBuyOnCrash = v);
        Add("10年保有する理由を書ける", () => b.CanWrite10YearReason, v => b.CanWrite10YearReason = v);
    }

    private void OnCheckChanged()
    {
        if (SelectedStock == null) return;
        _main.Recalculate(SelectedStock);
        BuildRadar();
        OnPropertyChanged(nameof(SelectedStock));
    }

    private void BuildCharts()
    {
        Charts.Clear();
        var s = SelectedStock;
        if (s == null || s.History.Count == 0) return;
        var h = s.History;

        Charts.Add(Group("業績推移",
            ("売上高", "#4FC3F7", p => p.Revenue),
            ("営業利益", "#66BB6A", p => p.OperatingProfit),
            ("純利益", "#FFA726", p => p.NetIncome)));

        Charts.Add(Group("配当推移",
            ("配当", "#4FC3F7", p => p.Dividend),
            ("EPS", "#66BB6A", p => p.EPS),
            ("配当性向%", "#FFA726", p => p.PayoutRatio)));

        Charts.Add(Group("財務推移",
            ("純資産", "#4FC3F7", p => p.NetAssets),
            ("負債", "#EF5350", p => p.Liabilities),
            ("自己資本比率%", "#66BB6A", p => p.EquityRatio)));

        Charts.Add(Group("キャッシュフロー推移",
            ("営業CF", "#4FC3F7", p => p.OperatingCF),
            ("投資CF", "#AB47BC", p => p.InvestingCF),
            ("財務CF", "#FFA726", p => p.FinancingCF),
            ("フリーCF", "#66BB6A", p => p.FreeCF)));

        Charts.Add(Group("自己株式の取得",
            ("自社株買い額", "#26C6DA", p => p.BuybackAmount)));

        ChartGroup Group(string title, params (string name, string color, Func<TimeSeriesPoint, double> sel)[] defs)
        {
            var g = new ChartGroup { Title = title };
            foreach (var (name, color, sel) in defs)
            {
                var series = new ChartSeries { Name = name, Color = color };
                double max = h.Select(p => Math.Abs(sel(p))).DefaultIfEmpty(0).Max();
                if (max <= 0) max = 1;
                foreach (var p in h)
                {
                    double val = sel(p);
                    series.Bars.Add(new ChartBar
                    {
                        Label = p.FiscalYear.ToString(),
                        Value = val,
                        Height = Math.Max(1, Math.Abs(val) / max * ChartHeight),
                        Negative = val < 0,
                        Display = FormatNum(val),
                        Color = val < 0 ? "#EF5350" : color
                    });
                }
                g.Series.Add(series);
            }
            return g;
        }
    }

    private void BuildRadar()
    {
        Radar.Clear();
        var s = SelectedStock;
        if (s == null) return;
        void Add(string label, double score, string color)
            => Radar.Add(new GaugeItem { Label = label, Score = score, Grade = Common.Grades.Letter(score), BarWidth = Math.Clamp(score, 0, 100) / 100.0 * GaugeMax, Color = color });

        Add("安全性", s.SafetyScore, "#4FC3F7");
        Add("成長性", s.GrowthScore, "#66BB6A");
        Add("収益性", s.ProfitabilityScore, "#FFA726");
        Add("還元性", s.ReturnScore, "#26C6DA");
        Add("効率性", s.EfficiencyScore, "#AB47BC");
        Add("割安性", s.ValuationScore, "#EC407A");
    }

    private static string FormatNum(double v)
    {
        if (Math.Abs(v) >= 1000) return v.ToString("N0");
        return v.ToString("0.#");
    }
}
