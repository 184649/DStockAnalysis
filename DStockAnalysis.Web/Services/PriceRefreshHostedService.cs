using System.Globalization;

namespace DStockAnalysis.Web.Services;

/// <summary>
/// 一覧(全銘柄)の指標を素早く埋め、株価を最新化する常駐サービス。
///
/// ・未取得の銘柄: Yahoo の一括 quote API(50件/req)で 株価/PER/PBR/配当利回り/EPS/BPS/配当 を
///   暫定的に取得して表示する(PER は trailing の暫定値)。一覧が「-」だらけにならないようにする。
/// ・株探(会社予想)で精密取得済みの銘柄: 暫定値で上書きせず、株価のみ最新化する
///   (古い/分割前の価格が残らないように)。
///
/// 起動直後にも実行し、保存済みデータを速やかに是正する。会社予想の精密値は
/// IndicatorFetchHostedService(株探巡回)とオンデマンド取得で順次反映される。
/// </summary>
public class PriceRefreshHostedService : BackgroundService
{
    private readonly StockStore _store;
    private readonly YahooFinanceClient _yahoo;
    private readonly FetchCoordinator _coordinator;
    private readonly FetchOptions _opt;
    private readonly ILogger<PriceRefreshHostedService> _log;

    public PriceRefreshHostedService(StockStore store, YahooFinanceClient yahoo,
        FetchCoordinator coordinator, FetchOptions opt, ILogger<PriceRefreshHostedService> log)
    {
        _store = store; _yahoo = yahoo; _coordinator = coordinator; _opt = opt; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.PriceRefresh)
        {
            _log.LogInformation("一覧の指標一括取得は無効です(Fetch:PriceRefresh)。");
            return;
        }

        await DelaySafe(TimeSpan.FromSeconds(15), stoppingToken); // 起動直後に早めに1回

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RefreshAllAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception e) { _log.LogWarning(e, "一覧の指標一括取得でエラー"); }

            await DelaySafe(TimeSpan.FromHours(Math.Max(0.25, _opt.PriceRefreshHours)), stoppingToken);
        }
    }

    /// <summary>全銘柄の指標を一括取得して反映・保存する。</summary>
    public async Task<int> RefreshAllAsync(CancellationToken ct)
    {
        var codes = _store.AllCodes();
        if (codes.Count == 0) return 0;

        var data = await _yahoo.GetQuoteIndicatorsAsync(codes, ct);
        int provisional = 0, priceOnly = 0;
        foreach (var (code, dict) in data)
        {
            if (!dict.TryGetValue("Price", out var ps) ||
                !double.TryParse(ps, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price <= 0)
                continue;

            var cur = _store.Get(code);
            bool hasFull = cur != null && cur.IndicatorsFetched && !cur.Provisional;
            if (_coordinator.IsFresh(code) || hasFull)
            {
                // 株探(会社予想・財務)で実取得済み → 株価のみ最新化(精密値・スコアは保持。暫定で上書きしない)
                if (_store.UpdatePrice(code, price) != null) priceOnly++;
            }
            else
            {
                // 未取得 → Yahoo の暫定指標で一覧を埋める(スコアは出さない)
                if (_store.ApplyFetched(new[] { (code, dict) }, save: false, provisional: true) > 0) provisional++;
            }
        }
        _store.Persist();
        _log.LogInformation("一覧の指標を一括取得: 暫定 {Prov} / 株価のみ {Price} / 全 {Total}", provisional, priceOnly, codes.Count);

        // 続けて財務指標(ROE/ROA/利益率/CF/成長率)を Yahoo から概算で一括取得し、一覧にスコアまで表示する
        // (株探の会社予想・通期は個別オープン/巡回で精緻化)。
        await EnrichFundamentalsAsync(int.MaxValue, ct);
        return provisional + priceOnly;
    }

    /// <summary>財務指標が無い銘柄(未取得・暫定)に Yahoo の財務概算(ROE/ROA/利益率/CF/成長率/有利子負債)を
    /// 取得して反映し、スコアを算出する。max で1回の処理件数を制限できる。
    /// 過負荷/スロットリングを避けるため、小さなチャンク・低並行で処理し、チャンクごとに保存する
    /// (途中で 0 件続き=スロットリングの可能性があれば長めに待機)。</summary>
    public async Task<int> EnrichFundamentalsAsync(int max, CancellationToken ct)
    {
        var codes = _store.CodesNeedingFundamentals();
        if (codes.Count == 0) return 0;
        if (max < codes.Count) codes = codes.Take(max).ToList();

        const int chunk = 200;
        int applied = 0, emptyStreak = 0;
        for (int i = 0; i < codes.Count && !ct.IsCancellationRequested; i += chunk)
        {
            var batch = codes.Skip(i).Take(chunk).ToList();
            var data = await _yahoo.GetSummaryBatchAsync(batch, concurrency: 3, ct);

            int got = 0;
            foreach (var (code, dict) in data)
                if ((dict.ContainsKey("ROE") || dict.ContainsKey("ROA") || dict.ContainsKey("OperatingMargin"))
                    && _store.ApplyFetched(new[] { (code, dict) }, save: false, provisional: false) > 0) { applied++; got++; }

            _store.Persist(); // チャンクごとに保存(途中で止まっても成果が残る)
            _log.LogInformation("財務概算(Yahoo) {Done}/{Total} 反映 (本チャンク {Got})",
                Math.Min(i + chunk, codes.Count), codes.Count, got);

            // 0件が続く=スロットリング/データ無しの可能性。長めに待って優しく継続。
            if (got == 0) { if (++emptyStreak >= 3) await DelaySafe(TimeSpan.FromSeconds(60), ct); else await DelaySafe(TimeSpan.FromSeconds(15), ct); }
            else { emptyStreak = 0; await DelaySafe(TimeSpan.FromSeconds(1), ct); }
        }
        _log.LogInformation("財務概算を反映(Yahoo): 計 {Applied} 件 / 対象 {Total}", applied, codes.Count);
        return applied;
    }

    private static async Task DelaySafe(TimeSpan span, CancellationToken ct)
    {
        try { await Task.Delay(span, ct); } catch (OperationCanceledException) { }
    }
}
