using System.Collections.ObjectModel;
using System.Windows.Input;
using DStockAnalysis.Models;
using DStockAnalysis.Services;

namespace DStockAnalysis.ViewModels;

/// <summary>
/// アプリ全体を統括する ViewModel。全銘柄(JPX一覧)を保持し、3画面の切替・更新・CSV取込・保存を管理する。
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly DataStorageService _storage = new();
    private readonly CsvImportService _csv = new();
    private readonly SampleDataService _sample = new();
    private readonly ScoreService _scorer = new();
    private readonly JpxMasterService _jpx = new();
    private readonly IndicatorSeedService _seed = new();

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

    public string ActiveScreen =>
        CurrentViewModel == ScreeningVM ? "Screening" :
        CurrentViewModel == AnalysisVM ? "Analysis" :
        CurrentViewModel == ComparisonVM ? "Comparison" : "";

    private string _statusText = "";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private DateTime? _masterDate;

    public string DataUpdatedText
    {
        get
        {
            if (AllStocks.Count == 0) return "データ更新日: -";
            var d = _masterDate ?? AllStocks.Max(s => s.DataUpdated);
            // JPX一覧は毎月更新。35日以上経過していれば更新を促す。
            var stale = (DateTime.Today - d.Date).TotalDays > 35 ? " (更新を推奨)" : "";
            return $"データ更新日: {d:yyyy/MM/dd}{stale}";
        }
    }

    /// <summary>擬似指標が含まれるかどうかの注意表示。</summary>
    public string IndicatorNotice
    {
        get
        {
            int sample = AllStocks.Count(s => s.IsSampleIndicators);
            if (sample == 0) return $"全 {AllStocks.Count} 銘柄(実データ取込済)";
            return $"全 {AllStocks.Count} 銘柄 / うち {sample} 件は指標がサンプル(擬似)値。CSV取込で実データに置換できます。";
        }
    }

    public ICommand ShowScreeningCommand { get; }
    public ICommand ShowAnalysisCommand { get; }
    public ICommand ShowComparisonCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand UpdateMasterCommand { get; }
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
        UpdateMasterCommand = new RelayCommand(UpdateMaster);
        LoadSampleCommand = new RelayCommand(LoadSample);
        SaveCommand = new RelayCommand(SaveAll);
        RecalculateCommand = new RelayCommand(RecalculateAll);

        LoadInitialData();
        CurrentViewModel = ScreeningVM;
    }

    private void LoadInitialData()
    {
        if (_storage.HasSavedStocks)
        {
            var saved = _storage.LoadStocks();
            if (saved.Count > 0)
            {
                _storage.ApplyUserData(saved);
                foreach (var s in saved) _scorer.Recalculate(s);
                _masterDate = saved.Max(s => s.DataUpdated);
                ReplaceStocks(saved);
                StatusText = $"保存済みデータ({saved.Count} 銘柄)を読み込みました。";
                return;
            }
        }

        if (_jpx.IsAvailable)
        {
            var (universe, date) = _jpx.LoadAll(_seed, _scorer);
            if (universe.Count > 0)
            {
                _masterDate = date;
                _storage.ApplyUserData(universe);
                ReplaceStocks(universe);
                StatusText = $"JPX上場銘柄一覧から全 {universe.Count} 銘柄を読み込みました(指標はサンプル値)。";
                return;
            }
        }

        // フォールバック: 同梱マスタが見つからない場合はサンプル14銘柄
        var sample = _sample.CreateSampleStocks();
        _storage.ApplyUserData(sample);
        foreach (var s in sample) _scorer.Recalculate(s);
        ReplaceStocks(sample);
        StatusText = "JPXマスタが見つからないためサンプルデータを表示しています。";
    }

    private void ImportCsv()
    {
        var path = FilePicker?.Invoke();
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var imported = _csv.ImportFromFile(path);
            if (imported.Count == 0)
            {
                StatusText = "CSVから銘柄を読み込めませんでした。ヘッダー行とCode列を確認してください。";
                return;
            }

            var byCode = AllStocks.ToDictionary(s => s.Code, s => s);
            int updated = 0, added = 0;
            foreach (var imp in imported)
            {
                if (byCode.TryGetValue(imp.Code, out var existing))
                {
                    existing.CopyIndicatorsFrom(imp);
                    _scorer.Recalculate(existing);
                    updated++;
                }
                else
                {
                    imp.IsSampleIndicators = false;
                    _storage.ApplyUserData(new[] { imp });
                    _scorer.Recalculate(imp);
                    AllStocks.Add(imp);
                    added++;
                }
            }

            RefreshAllViews();
            _storage.SaveStocks(AllStocks);
            StatusText = $"CSV取込: {updated} 件更新 / {added} 件追加 ({System.IO.Path.GetFileName(path)})";
        }
        catch (Exception ex)
        {
            StatusText = "CSV取込エラー: " + ex.Message;
        }
    }

    private async void UpdateMaster()
    {
        StatusText = "JPXから最新の上場銘柄一覧をダウンロード中...";
        try
        {
            bool ok = await _jpx.UpdateAsync();
            if (!ok) { StatusText = "JPXマスタの更新に失敗しました(ダウンロード内容が不正)。"; return; }

            var (universe, date) = _jpx.LoadAll(_seed, _scorer);
            if (universe.Count == 0) { StatusText = "更新後の一覧を読み込めませんでした。"; return; }

            // 既存のメモ/チェックを引き継ぐ
            _storage.SaveUserData(AllStocks);
            _storage.ApplyUserData(universe);
            _masterDate = date;
            ReplaceStocks(universe);
            StatusText = $"JPXマスタを更新しました(基準日 {date:yyyy/MM/dd}、全 {universe.Count} 銘柄)。";
        }
        catch (Exception ex)
        {
            StatusText = "JPXマスタ更新エラー: " + ex.Message;
        }
    }

    private void LoadSample()
    {
        var stocks = _sample.CreateSampleStocks();
        _storage.ApplyUserData(stocks);
        foreach (var s in stocks) _scorer.Recalculate(s);
        _masterDate = stocks.Max(s => s.DataUpdated);
        ReplaceStocks(stocks);
        StatusText = "サンプルデータ(14銘柄・実データ風)を読み込みました。";
    }

    private void ReplaceStocks(IEnumerable<Stock> stocks)
    {
        AllStocks.Clear();
        foreach (var s in stocks.OrderBy(x => x.Code, StringComparer.Ordinal)) AllStocks.Add(s);
        RefreshAllViews();
    }

    private void RefreshAllViews()
    {
        OnPropertyChanged(nameof(DataUpdatedText));
        OnPropertyChanged(nameof(IndicatorNotice));
        ScreeningVM.RefreshSourceData();
        AnalysisVM.RefreshSourceData();
        ComparisonVM.RefreshSourceData();
    }

    public void RecalculateAll()
    {
        foreach (var s in AllStocks) _scorer.Recalculate(s);
        ScreeningVM.ApplyFilter();
        StatusText = "全銘柄のスコアを再計算しました。";
    }

    /// <summary>単一銘柄のスコア再計算(チェック/メモ変更時)。</summary>
    public void Recalculate(Stock s) => _scorer.Recalculate(s);

    /// <summary>選択銘柄の時系列を遅延生成する(チャート表示用)。</summary>
    public void EnsureHistory(Stock s)
    {
        if (s.History.Count == 0) s.History = _seed.BuildHistory(s);
    }

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
