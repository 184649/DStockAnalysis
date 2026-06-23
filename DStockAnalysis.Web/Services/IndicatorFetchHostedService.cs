namespace DStockAnalysis.Web.Services;

/// <summary>自動取得の設定(appsettings の "Fetch" セクション)。</summary>
public class FetchOptions
{
    /// <summary>バックグラウンドの全銘柄巡回取得を有効にするか。</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>個別分析で銘柄を開いた時に、その銘柄の実値をその場で取得するか。</summary>
    public bool OnDemand { get; set; } = true;
    /// <summary>対象範囲: "all"=全銘柄 / "watchlist"=codes.txt の銘柄のみ。</summary>
    public string Scope { get; set; } = "all";
    /// <summary>リクエスト間隔の基準秒(robots の Crawl-delay と大きい方を採用)。</summary>
    public double DelaySeconds { get; set; } = 8;
    /// <summary>この日数以内に取得済みの銘柄は再取得しない(週次運用想定)。</summary>
    public int MaxAgeDays { get; set; } = 6;
    /// <summary>1巡(全銘柄走査)を終えてから次巡まで待つ時間。</summary>
    public double CycleRestHours { get; set; } = 24;
    /// <summary>株探も取得対象に含めるか。</summary>
    public bool UseKabutan { get; set; } = false;
}

/// <summary>自動取得の進捗・状態(API で参照)。</summary>
public class FetchStatus
{
    public bool Enabled { get; set; }
    public bool Running { get; set; }
    public string Scope { get; set; } = "all";
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? CurrentCode { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? LastCompletedUtc { get; set; }
    public string? LastError { get; set; }
}

/// <summary>
/// 全銘柄(または codes.txt)の主要指標を、robots 順守・低速で巡回取得し続けるバックグラウンドサービス。
/// 取得済み日時をキャッシュし、MaxAgeDays 以内の銘柄はスキップ(週次更新相当)。
/// 取得結果は列単位マージで StockStore へ反映し、JSON へ保存する。
/// 「全銘柄を自動取得する」要件をサーバ常駐で満たす。
/// </summary>
public class IndicatorFetchHostedService : BackgroundService
{
    private readonly StockStore _store;
    private readonly IIndicatorFetcher _fetcher;
    private readonly FetchCoordinator _coordinator;
    private readonly FetchOptions _opt;
    private readonly ILogger<IndicatorFetchHostedService> _log;
    private readonly FetchStatus _status = new();
    private readonly object _statusLock = new();

    public IndicatorFetchHostedService(StockStore store, IIndicatorFetcher fetcher,
        FetchCoordinator coordinator, FetchOptions opt, ILogger<IndicatorFetchHostedService> log)
    {
        _store = store;
        _fetcher = fetcher;
        _coordinator = coordinator;
        _opt = opt;
        _log = log;
        lock (_statusLock) { _status.Enabled = _opt.Enabled; _status.Scope = _opt.Scope; }
    }

    public FetchStatus Snapshot()
    {
        lock (_statusLock)
            return new FetchStatus
            {
                Enabled = _status.Enabled, Running = _status.Running, Scope = _status.Scope,
                Total = _status.Total, Processed = _status.Processed, Updated = _status.Updated,
                Skipped = _status.Skipped, CurrentCode = _status.CurrentCode,
                LastRunUtc = _status.LastRunUtc, LastCompletedUtc = _status.LastCompletedUtc,
                LastError = _status.LastError
            };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled)
        {
            _log.LogInformation("自動取得は無効です(appsettings の Fetch:Enabled を true で有効化)。");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e)
            {
                _log.LogWarning(e, "自動取得サイクルでエラー");
                lock (_statusLock) _status.LastError = e.Message;
            }

            lock (_statusLock) _status.LastCompletedUtc = DateTime.UtcNow;
            await DelaySafe(TimeSpan.FromHours(Math.Max(0.1, _opt.CycleRestHours)), stoppingToken);
        }
    }

    /// <summary>1巡実行する。MaxAgeDays 以内の銘柄はスキップ。</summary>
    public async Task RunOnceAsync(CancellationToken ct, IReadOnlyList<string>? overrideCodes = null, bool force = false)
    {
        var codes = overrideCodes ?? GetTargetCodes();
        lock (_statusLock)
        {
            _status.Running = true;
            _status.Total = codes.Count;
            _status.Processed = 0; _status.Updated = 0; _status.Skipped = 0;
            _status.LastRunUtc = DateTime.UtcNow;
            _status.LastError = null;
        }
        _log.LogInformation("自動取得を開始: {Count} 銘柄 (scope={Scope})", codes.Count, _opt.Scope);

        try
        {
            int sinceSave = 0;
            foreach (var code in codes)
            {
                ct.ThrowIfCancellationRequested();
                lock (_statusLock) _status.CurrentCode = code;

                if (!force && _coordinator.IsFresh(code))
                {
                    lock (_statusLock) { _status.Skipped++; _status.Processed++; }
                    continue;
                }

                // 取得ごとに全銘柄を保存すると重いので、まとめて間引き保存する(save:false)
                bool updated = await _coordinator.FetchOneAsync(code, force, ct, save: false);
                lock (_statusLock) { if (updated) _status.Updated++; _status.Processed++; }
                if (updated && ++sinceSave >= 25) { _store.Persist(); sinceSave = 0; }

                // robots の Crawl-delay と基準間隔の大きい方を採用
                var cd = await _fetcher.GetCrawlDelayAsync($"https://irbank.net/{code}", ct) ?? 0;
                var wait = Math.Max(_opt.DelaySeconds, cd);
                await DelaySafe(TimeSpan.FromSeconds(wait), ct);
            }
            _store.Persist(); // 巡回終了時にまとめて保存
            _log.LogInformation("自動取得が1巡完了: 更新 {Updated} / スキップ {Skipped}",
                Snapshot().Updated, Snapshot().Skipped);
        }
        finally
        {
            lock (_statusLock) { _status.Running = false; _status.CurrentCode = null; }
        }
    }

    private IReadOnlyList<string> GetTargetCodes()
    {
        if (_opt.Scope.Equals("watchlist", StringComparison.OrdinalIgnoreCase))
        {
            var file = Path.Combine(_store.DataDirectory, "codes.txt");
            if (File.Exists(file))
            {
                return File.ReadAllLines(file)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith("#"))
                    .Select(l => l.Split(',')[0].Trim())
                    .ToList();
            }
            _log.LogWarning("watchlist 指定ですが {File} が無いため全銘柄を対象にします", file);
        }
        return _store.AllCodes();
    }

    private static async Task DelaySafe(TimeSpan span, CancellationToken ct)
    {
        try { await Task.Delay(span, ct); } catch (OperationCanceledException) { }
    }
}
