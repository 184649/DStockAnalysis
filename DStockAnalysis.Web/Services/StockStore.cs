using System.Text;
using DStockAnalysis.Models;
using DStockAnalysis.Services;

namespace DStockAnalysis.Web.Services;

/// <summary>
/// 全銘柄をサーバ側メモリに保持し、スクリーニング・個別分析・比較・CSV取込・
/// JPX マスタ更新・ユーザーデータ保存を仲介するアプリケーションサービス。
/// WPF 版の MainViewModel に相当する役割を Web 用に再構成したもの。
/// ドメインロジック(指標生成・スコア計算・CSV解析・xls解析)はすべて Core を再利用する。
/// </summary>
public class StockStore
{
    private readonly object _lock = new();
    private readonly IndicatorSeedService _seed = new();
    private readonly ScoreService _scorer = new();
    private readonly CsvImportService _csv = new();
    private readonly ReferenceLinkService _links = new();
    private readonly DataStorageService _storage;
    private readonly JpxMasterService _jpx;
    private readonly ILogger<StockStore> _log;

    private List<Stock> _stocks = new();
    private Dictionary<string, Stock> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? _masterDate;

    public StockStore(IConfiguration config, ILogger<StockStore> log)
    {
        _log = log;
        // データ保存先(JSON/マスタキャッシュ)。既定はコンテンツルート配下の data。
        var dataDir = config["DataDir"];
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _storage = new DataStorageService(dataDir);
        _jpx = new JpxMasterService(dataDir);
        DataDirectory = dataDir;
    }

    public string DataDirectory { get; }
    public DateTime? MasterDate { get { lock (_lock) return _masterDate; } }
    public int Count { get { lock (_lock) return _stocks.Count; } }
    public int FetchedCount { get { lock (_lock) return _stocks.Count(s => s.IndicatorsFetched); } }
    public int UnfetchedCount { get { lock (_lock) return _stocks.Count(s => !s.IndicatorsFetched); } }

    /// <summary>起動時の初期ロード。保存→JPX全銘柄→サンプル の優先順位。</summary>
    public void Initialize()
    {
        lock (_lock)
        {
            List<Stock> stocks;
            if (_storage.HasSavedStocks)
            {
                stocks = _storage.LoadStocks();
                // 実データ未取得の銘柄は擬似値を持たないよう一掃(旧バージョンのサンプル値の混入を防ぐ)
                foreach (var s in stocks) if (!s.IndicatorsFetched) s.ClearIndicators();
                _masterDate = stocks.Count > 0 ? stocks.Max(s => s.DataUpdated) : null;
                _log.LogInformation("保存済み銘柄を読み込みました: {Count} 件 (実データ取得済み {Fetched})",
                    stocks.Count, stocks.Count(s => s.IndicatorsFetched));
            }
            else if (_jpx.IsAvailable)
            {
                // 擬似指標は生成しない(実データ取得まで未取得のまま)
                (stocks, _masterDate) = _jpx.LoadAll(_seed, _scorer, fillIndicators: false);
                _log.LogInformation("JPX 全銘柄を読み込みました: {Count} 件 (指標は未取得・基準日 {Date:yyyy-MM-dd})", stocks.Count, _masterDate);
            }
            else
            {
                stocks = new List<Stock>();
                _log.LogWarning("JPX マスタ(Data/data_j.xls)が見つかりません。銘柄が空です。");
            }

            _storage.ApplyUserData(stocks);
            // スコアは実データ取得済みの銘柄のみ算出(未取得は0のまま=「未取得」表示)
            foreach (var s in stocks) if (s.IndicatorsFetched) _scorer.Recalculate(s);
            ReplaceInternal(stocks);
        }
    }

    private void ReplaceInternal(List<Stock> stocks)
    {
        _stocks = stocks;
        _byCode = stocks
            .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>スクリーニング条件で絞り込み、バフェット→買いたい度の降順で返す。</summary>
    public List<Stock> Screen(ScreeningCriteria criteria)
    {
        lock (_lock)
        {
            return _stocks
                .Where(criteria.Matches)
                .OrderByDescending(s => s.BuffettScore)
                .ThenByDescending(s => s.WantToBuyScore)
                .ToList();
        }
    }

    public Stock? Get(string code)
    {
        lock (_lock)
        {
            if (code != null && _byCode.TryGetValue(code, out var s))
                return s;
            return null;
        }
    }

    public IReadOnlyList<Stock> GetMany(IEnumerable<string> codes)
    {
        lock (_lock)
        {
            var list = new List<Stock>();
            foreach (var c in codes)
                if (_byCode.TryGetValue(c, out var s)) list.Add(s);
            return list;
        }
    }

    public List<ReferenceLink> Links(string code) => _links.BuildLinks(code);

    /// <summary>個別分析でメモ・バフェットチェック・興味度を保存し、スコア再計算する。</summary>
    public Stock? SaveUserData(string code, StockMemo? memo, BuffettCheck? check, double? interest)
    {
        lock (_lock)
        {
            if (!_byCode.TryGetValue(code, out var s)) return null;
            if (memo != null) s.Memo = memo;
            if (check != null) s.BuffettCheck = check;
            if (interest.HasValue) s.UserInterest = interest.Value;
            _scorer.Recalculate(s);
            _storage.SaveUserData(_stocks);
            _storage.SaveStocks(_stocks);
            return s;
        }
    }

    /// <summary>CSV(実データ)を列単位マージで取り込む。取得できた列だけ上書き。</summary>
    public (int updated, int added) ImportCsv(string content)
    {
        lock (_lock)
        {
            var (imported, columns) = _csv.ParseWithColumns(content);
            int updated = 0, added = 0;
            foreach (var imp in imported)
            {
                if (_byCode.TryGetValue(imp.Code, out var existing))
                {
                    existing.CopyIndicatorsFrom(imp, columns);
                    _scorer.Recalculate(existing);
                    updated++;
                }
                else
                {
                    // 新規コード: CSV にある列のみ実データ、その他は未取得(擬似値で埋めない)
                    imp.CopyIndicatorsFrom(imp, columns); // IndicatorsFetched=true・優待フラグを設定
                    _scorer.Recalculate(imp);
                    _stocks.Add(imp);
                    _byCode[imp.Code] = imp;
                    added++;
                }
            }
            _storage.SaveStocks(_stocks);
            return (updated, added);
        }
    }

    /// <summary>銘柄データを永続化する(取得バッチからの間引き保存用)。</summary>
    public void Persist() { lock (_lock) _storage.SaveStocks(_stocks); }

    /// <summary>取得済みの指標行(列単位)を全銘柄へ反映する。自動取得バッチから呼ばれる。
    /// save=false の場合は保存しない(呼び出し側でまとめて Persist する)。</summary>
    public int ApplyFetched(IEnumerable<(string code, Dictionary<string, string> values)> rows, bool save = true)
    {
        lock (_lock)
        {
            int updated = 0;
            foreach (var (code, values) in rows)
            {
                if (values.Count == 0) continue;
                if (!_byCode.TryGetValue(code, out var existing)) continue;
                var columns = new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase);
                // CSV と同じ解釈にするため一旦 CSV テキスト化して解析
                var src = ParseValuesToStock(code, values);
                existing.CopyIndicatorsFrom(src, columns);
                ClearUnverifiedBenefit(existing); // 優待は自動取得できないため未取得扱い(擬似優待を消す)
                _scorer.Recalculate(existing);
                updated++;
            }
            if (updated > 0 && save) _storage.SaveStocks(_stocks);
            return updated;
        }
    }

    /// <summary>自動取得では株主優待を取得できないため、擬似優待を消して「未取得」にする。
    /// 実データ優待は CSV 取込で反映する。</summary>
    private static void ClearUnverifiedBenefit(Stock s)
    {
        s.HasShareholderBenefit = false;
        s.ShareholderBenefit = "";
        s.BenefitContent = "";
        s.BenefitCategory = "";
        s.BenefitRightsMonth = "";
        s.RequiredSharesForBenefit = 0;
        s.BenefitValue = 0;
        s.BenefitYield = 0;
        s.HasLongTermBenefit = false;
        s.LongTermBenefitCondition = "";
        s.LongTermBenefitContent = "";
        s.BenefitRiskMemo = "";
        s.TotalYield = s.DividendYield; // 優待分を除く
        s.BenefitUnknown = true;
    }

    private Stock ParseValuesToStock(string code, Dictionary<string, string> values)
    {
        var sb = new StringBuilder();
        var keys = values.Keys.ToList();
        sb.Append("Code,").AppendLine(string.Join(",", keys));
        sb.Append(EscapeCsv(code)).Append(',')
          .AppendLine(string.Join(",", keys.Select(k => EscapeCsv(values[k]))));
        var (list, _) = _csv.ParseWithColumns(sb.ToString());
        return list.FirstOrDefault() ?? new Stock { Code = code };
    }

    private static string EscapeCsv(string v)
    {
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    /// <summary>記入用テンプレ CSV(全銘柄の基本情報を前埋め)を生成する。</summary>
    public string ExportTemplate()
    {
        lock (_lock)
        {
            var cols = new[]
            {
                "Code","Name","Market","Sector","Scale","Theme","Description","FiscalMonth","IRUrl",
                "Price","MarketCap","PER","PBR","ROE","EPS","BPS","OperatingMargin","NetProfitMargin",
                "DividendYield","PayoutRatio","Dividend","ConsecutiveDividendYears","EquityRatio",
                "InterestBearingDebtRatio","RevenueGrowth3Y","OperatingCF","FreeCashFlow",
                "BenefitYield","TotalYield","HasShareholderBenefit","BenefitContent","BenefitCategory"
            };
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols));
            foreach (var s in _stocks.OrderBy(s => s.Code))
            {
                sb.Append(EscapeCsv(s.Code)).Append(',')
                  .Append(EscapeCsv(s.Name)).Append(',')
                  .Append(EscapeCsv(s.Market)).Append(',')
                  .Append(EscapeCsv(s.Sector)).Append(',')
                  .Append(EscapeCsv(s.Scale)).Append(',');
                sb.Append(new string(',', cols.Length - 6)); // 残り列は空欄
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    /// <summary>JPX 全銘柄一覧を最新化して再読込する(メモ/チェックは引き継ぐ)。</summary>
    public async Task<bool> UpdateMasterAsync()
    {
        bool ok = await _jpx.UpdateAsync();
        if (!ok) return false;
        lock (_lock)
        {
            _storage.SaveUserData(_stocks);       // 既存メモを退避
            var (stocks, date) = _jpx.LoadAll(_seed, _scorer);
            _masterDate = date;
            _storage.ApplyUserData(stocks);
            foreach (var s in stocks) _scorer.Recalculate(s);
            ReplaceInternal(stocks);
            _storage.SaveStocks(_stocks);
        }
        return true;
    }

    public IReadOnlyList<string> AllCodes()
    {
        lock (_lock) return _stocks.Select(s => s.Code).ToList();
    }

    // 候補値(コンボ用)
    public List<string> Sectors() { lock (_lock) return _stocks.Select(s => s.Sector).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(); }
    public List<string> Markets() { lock (_lock) return _stocks.Select(s => s.Market).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(); }
    public List<string> Scales() { lock (_lock) return _stocks.Select(s => s.Scale).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList(); }
    public List<string> BenefitCategories() { lock (_lock) return _stocks.Where(s => s.HasShareholderBenefit).Select(s => s.BenefitCategory).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(); }
    public List<string> BenefitMonths() { lock (_lock) return _stocks.Where(s => s.HasShareholderBenefit).Select(s => s.BenefitRightsMonth).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList(); }
}
