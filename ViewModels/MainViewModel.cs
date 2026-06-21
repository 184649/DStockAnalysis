using System.Collections.ObjectModel;
using System.Windows.Input;
using DStockAnalysis.Models;
using DStockAnalysis.Services;

namespace DStockAnalysis.ViewModels;

/// <summary>
/// アプリ全体を統括する ViewModel。銘柄データを保持し、3画面の切替・CSV取込・保存を管理する。
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly DataStorageService _storage = new();
    private readonly CsvImportService _csv = new();
    private readonly SampleDataService _sample = new();
    private readonly ScoreService _scorer = new();

    /// <summary>CSV ファイル選択ダイアログ。View(MainWindow)から注入する。</summary>
    public Func<string?>? FilePicker { get; set; }

    /// <summary>全銘柄。3画面で共有する。</summary>
    public ObservableCollection<Stock> AllStocks { get; } = new();

    public ScreeningViewModel ScreeningVM { get; }
    public StockAnalysisViewModel AnalysisVM { get; }
    public ComparisonViewModel ComparisonVM { get; }

    private object? _currentViewModel;
    public object? CurrentViewModel
    {
        get => _currentViewModel;
        set { if (SetProperty(ref _currentViewModel, value)) OnPropertyChanged(nameof(ActiveScreen)); }
    }

    /// <summary>現在表示中の画面識別子(上部ボタンのハイライト用)。</summary>
    public string ActiveScreen =>
        CurrentViewModel == ScreeningVM ? "Screening" :
        CurrentViewModel == AnalysisVM ? "Analysis" :
        CurrentViewModel == ComparisonVM ? "Comparison" : "";

    private string _statusText = "";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public string DataUpdatedText
    {
        get
        {
            if (AllStocks.Count == 0) return "データ更新日: -";
            var d = AllStocks.Max(s => s.DataUpdated);
            return $"データ更新日: {d:yyyy/MM/dd}";
        }
    }

    public ICommand ShowScreeningCommand { get; }
    public ICommand ShowAnalysisCommand { get; }
    public ICommand ShowComparisonCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RecalculateCommand { get; }

    public MainViewModel()
    {
        ScreeningVM = new ScreeningViewModel(this);
        AnalysisVM = new StockAnalysisViewModel(this);
        ComparisonVM = new ComparisonViewModel(this);

        ShowScreeningCommand = new RelayCommand(() => CurrentViewModel = ScreeningVM);
        ShowAnalysisCommand = new RelayCommand(() => CurrentViewModel = AnalysisVM);
        ShowComparisonCommand = new RelayCommand(() => CurrentViewModel = ComparisonVM);
        ImportCsvCommand = new RelayCommand(ImportCsv);
        LoadSampleCommand = new RelayCommand(LoadSample);
        SaveCommand = new RelayCommand(SaveAll);
        RecalculateCommand = new RelayCommand(RecalculateAll);

        LoadInitialData();
        CurrentViewModel = ScreeningVM;
    }

    private void LoadInitialData()
    {
        List<Stock> stocks;
        if (_storage.HasSavedStocks)
        {
            stocks = _storage.LoadStocks();
            if (stocks.Count == 0) stocks = _sample.CreateSampleStocks();
            StatusText = "保存済みデータを読み込みました。";
        }
        else
        {
            stocks = _sample.CreateSampleStocks();
            StatusText = "サンプルデータを表示しています。CSV取込で実データに置換できます。";
        }

        _storage.ApplyUserData(stocks);
        foreach (var s in stocks) _scorer.Recalculate(s);
        ReplaceStocks(stocks);
    }

    private void ImportCsv()
    {
        var path = FilePicker?.Invoke();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var stocks = _csv.ImportFromFile(path);
            if (stocks.Count == 0)
            {
                StatusText = "CSVから銘柄を読み込めませんでした。ヘッダー行とCode列を確認してください。";
                return;
            }
            _storage.ApplyUserData(stocks);
            foreach (var s in stocks) _scorer.Recalculate(s);
            ReplaceStocks(stocks);
            _storage.SaveStocks(AllStocks);
            StatusText = $"CSVから {stocks.Count} 銘柄を取り込みました。({System.IO.Path.GetFileName(path)})";
        }
        catch (Exception ex)
        {
            StatusText = "CSV取込エラー: " + ex.Message;
        }
    }

    private void LoadSample()
    {
        var stocks = _sample.CreateSampleStocks();
        _storage.ApplyUserData(stocks);
        foreach (var s in stocks) _scorer.Recalculate(s);
        ReplaceStocks(stocks);
        StatusText = "サンプルデータを再読み込みしました。";
    }

    private void ReplaceStocks(IEnumerable<Stock> stocks)
    {
        AllStocks.Clear();
        foreach (var s in stocks.OrderBy(x => x.Code)) AllStocks.Add(s);
        OnPropertyChanged(nameof(DataUpdatedText));
        ScreeningVM.RefreshSourceData();
        AnalysisVM.RefreshSourceData();
        ComparisonVM.RefreshSourceData();
    }

    public void RecalculateAll()
    {
        foreach (var s in AllStocks) _scorer.Recalculate(s);
        StatusText = "全銘柄のスコアを再計算しました。";
    }

    /// <summary>単一銘柄のスコア再計算(チェック/メモ変更時)。</summary>
    public void Recalculate(Stock s) => _scorer.Recalculate(s);

    public void SaveAll()
    {
        _storage.SaveStocks(AllStocks);
        _storage.SaveUserData(AllStocks);
        var settings = _storage.LoadSettings();
        settings.ComparisonCodes = ComparisonVM.SelectedCodes().ToList();
        _storage.SaveSettings(settings);
        StatusText = $"保存しました。({_storage.DataDirectory})";
    }

    /// <summary>指定銘柄を個別分析画面で開く。</summary>
    public void OpenAnalysis(Stock? stock)
    {
        if (stock != null) AnalysisVM.SelectedStock = stock;
        CurrentViewModel = AnalysisVM;
    }
}
