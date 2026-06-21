using System.Collections.ObjectModel;
using System.Windows.Input;
using DStockAnalysis.Models;
using DStockAnalysis.Services;

namespace DStockAnalysis.ViewModels;

/// <summary>銘柄スクリーニング画面の ViewModel。条件パネル・プリセット・一覧テーブルを管理する。</summary>
public class ScreeningViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly PresetService _presetService = new();

    public ScreeningCriteria Criteria { get; private set; } = new();

    public ObservableCollection<Stock> FilteredStocks { get; } = new();
    public ObservableCollection<ScreeningPreset> Presets { get; } = new();

    // フィルタ用の候補リスト
    public ObservableCollection<string> Sectors { get; } = new();
    public ObservableCollection<string> Markets { get; } = new();
    public ObservableCollection<string> Scales { get; } = new();
    public ObservableCollection<string> BenefitCategories { get; } = new();
    public ObservableCollection<string> BenefitMonths { get; } = new();

    private Stock? _selectedStock;
    public Stock? SelectedStock { get => _selectedStock; set => SetProperty(ref _selectedStock, value); }

    private string _resultText = "";
    public string ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }

    // ===== 市場トグル(東証/東証PR/東証GR/東証ST) =====
    public bool MarketTSE { get => Criteria.MarketToken == "東証"; set => SetMarketToken("東証", value); }
    public bool MarketPR { get => Criteria.MarketToken == "東証PR"; set => SetMarketToken("東証PR", value); }
    public bool MarketGR { get => Criteria.MarketToken == "東証GR"; set => SetMarketToken("東証GR", value); }
    public bool MarketST { get => Criteria.MarketToken == "東証ST"; set => SetMarketToken("東証ST", value); }

    // ===== 規模トグル(小型/大型/中型) =====
    public bool ScaleSmall { get => Criteria.ScaleToken == "小型"; set => SetScaleToken("小型", value); }
    public bool ScaleLarge { get => Criteria.ScaleToken == "大型"; set => SetScaleToken("大型", value); }
    public bool ScaleMid { get => Criteria.ScaleToken == "中型"; set => SetScaleToken("中型", value); }

    // ===== フラグトグル =====
    public bool NoDividendCut { get => Criteria.NoDividendCut; set { Criteria.NoDividendCut = value; OnPropertyChanged(); ApplyFilter(); } }
    public bool BenefitFlag { get => Criteria.BenefitOnly; set { Criteria.BenefitOnly = value; OnPropertyChanged(); ApplyFilter(); } }
    public bool CumulativeFlag { get => Criteria.CumulativeOnly; set { Criteria.CumulativeOnly = value; OnPropertyChanged(); ApplyFilter(); } }
    public bool DoeFlag { get => Criteria.DoeOnly; set { Criteria.DoeOnly = value; OnPropertyChanged(); ApplyFilter(); } }

    private void SetMarketToken(string token, bool on)
    {
        Criteria.MarketToken = on ? token : (Criteria.MarketToken == token ? null : Criteria.MarketToken);
        RaiseMarketScaleFlags();
        ApplyFilter();
    }

    private void SetScaleToken(string token, bool on)
    {
        Criteria.ScaleToken = on ? token : (Criteria.ScaleToken == token ? null : Criteria.ScaleToken);
        RaiseMarketScaleFlags();
        ApplyFilter();
    }

    private void RaiseMarketScaleFlags()
    {
        OnPropertyChanged(nameof(MarketTSE)); OnPropertyChanged(nameof(MarketPR));
        OnPropertyChanged(nameof(MarketGR)); OnPropertyChanged(nameof(MarketST));
        OnPropertyChanged(nameof(ScaleSmall)); OnPropertyChanged(nameof(ScaleLarge)); OnPropertyChanged(nameof(ScaleMid));
        OnPropertyChanged(nameof(NoDividendCut)); OnPropertyChanged(nameof(BenefitFlag));
        OnPropertyChanged(nameof(CumulativeFlag)); OnPropertyChanged(nameof(DoeFlag));
    }

    public ICommand ApplyCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand OpenAnalysisCommand { get; }
    public ICommand AddToComparisonCommand { get; }

    public ScreeningViewModel(MainViewModel main)
    {
        _main = main;
        ApplyCommand = new RelayCommand(ApplyFilter);
        ClearCommand = new RelayCommand(ClearFilter);
        ApplyPresetCommand = new RelayCommand(p => ApplyPreset(p as ScreeningPreset));
        OpenAnalysisCommand = new RelayCommand(p => _main.OpenAnalysis((p as Stock) ?? SelectedStock));
        AddToComparisonCommand = new RelayCommand(p => _main.ComparisonVM.Add((p as Stock) ?? SelectedStock));

        foreach (var preset in _presetService.GetPresets()) Presets.Add(preset);
    }

    public void RefreshSourceData()
    {
        RebuildLookups();
        ApplyFilter();
    }

    private void RebuildLookups()
    {
        FillDistinct(Sectors, _main.AllStocks.Select(s => s.Sector));
        FillDistinct(Markets, _main.AllStocks.Select(s => s.Market));
        FillDistinct(Scales, _main.AllStocks.Select(s => s.Scale));
        FillDistinct(BenefitCategories, _main.AllStocks.Where(s => s.HasShareholderBenefit).Select(s => s.BenefitCategory));
        FillDistinct(BenefitMonths, _main.AllStocks.Select(s => s.BenefitRightsMonth));
    }

    private static void FillDistinct(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        target.Add(""); // 無指定
        foreach (var v in source.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x))
            target.Add(v);
    }

    public void ApplyFilter()
    {
        FilteredStocks.Clear();
        var matched = _main.AllStocks.Where(Criteria.Matches)
                                     .OrderByDescending(s => s.BuffettScore)
                                     .ThenByDescending(s => s.WantToBuyScore);
        foreach (var s in matched) FilteredStocks.Add(s);
        ResultText = $"{FilteredStocks.Count} / {_main.AllStocks.Count} 銘柄";
    }

    private void ClearFilter()
    {
        Criteria = new ScreeningCriteria();
        OnPropertyChanged(nameof(Criteria));
        RaiseMarketScaleFlags();
        ApplyFilter();
    }

    private void ApplyPreset(ScreeningPreset? preset)
    {
        if (preset == null) return;
        Criteria = preset.Build();
        OnPropertyChanged(nameof(Criteria));
        RaiseMarketScaleFlags();
        ApplyFilter();
        _main.StatusText = $"プリセット適用: {preset.Name}";
    }
}
