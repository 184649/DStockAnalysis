namespace DStockAnalysis.Web.Services;

/// <summary>
/// 取得済み全銘柄の株価を定期的に一括最新化する常駐サービス。
/// 財務指標(PER/PBR/ROE 等)は会社予想ベースで変化が遅いためキャッシュするが、
/// 株価は日々変動し株式分割でも変わるため、一覧で古い/分割前の価格が残らないよう
/// Yahoo の一括 quote API(50件/リクエスト)で頻繁に更新し、保存する。
/// 起動直後にも1回実行し、既存の保存データ(分割前など)を速やかに是正する。
/// </summary>
public class PriceRefreshHostedService : BackgroundService
{
    private readonly StockStore _store;
    private readonly YahooFinanceClient _yahoo;
    private readonly FetchOptions _opt;
    private readonly ILogger<PriceRefreshHostedService> _log;

    public PriceRefreshHostedService(StockStore store, YahooFinanceClient yahoo,
        FetchOptions opt, ILogger<PriceRefreshHostedService> log)
    {
        _store = store; _yahoo = yahoo; _opt = opt; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.PriceRefresh)
        {
            _log.LogInformation("株価一括最新化は無効です(Fetch:PriceRefresh)。");
            return;
        }

        // 起動直後の負荷を避けつつ、保存済みの古い株価を早めに是正する
        await DelaySafe(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RefreshAllAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception e) { _log.LogWarning(e, "株価一括最新化でエラー"); }

            await DelaySafe(TimeSpan.FromHours(Math.Max(0.25, _opt.PriceRefreshHours)), stoppingToken);
        }
    }

    /// <summary>取得済み全銘柄の現在値を一括取得して反映・保存する。</summary>
    public async Task<int> RefreshAllAsync(CancellationToken ct)
    {
        var codes = _store.FetchedCodes();
        if (codes.Count == 0) return 0;

        var quotes = await _yahoo.GetQuotesAsync(codes, ct);
        int n = 0;
        foreach (var kv in quotes)
            if (_store.UpdatePrice(kv.Key, kv.Value) != null) n++;

        if (n > 0) _store.Persist();
        _log.LogInformation("株価を一括最新化: {N}/{Total} 銘柄", n, codes.Count);
        return n;
    }

    private static async Task DelaySafe(TimeSpan span, CancellationToken ct)
    {
        try { await Task.Delay(span, ct); } catch (OperationCanceledException) { }
    }
}
