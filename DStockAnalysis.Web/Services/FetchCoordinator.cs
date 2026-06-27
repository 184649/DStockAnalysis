using System.Collections.Concurrent;
using System.Text.Json;

namespace DStockAnalysis.Web.Services;

/// <summary>
/// 実データ取得のキャッシュと「1銘柄取得→反映」処理を一元管理する。
/// オンデマンド取得(個別分析を開いた時)とバックグラウンド巡回取得の両方から共有され、
/// 取得済み日時(fetch_state.json)で二重取得を防ぐ(MaxAgeDays 以内はスキップ)。
/// </summary>
public class FetchCoordinator
{
    private readonly StockStore _store;
    private readonly IIndicatorFetcher _fetcher;
    private readonly FetchOptions _opt;
    private readonly ILogger<FetchCoordinator> _log;
    private readonly string _cacheFile;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CacheEntry> _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _inflight = new();

    /// <summary>取得指標スキーマのバージョン。取得する指標を増やしたら上げる。
    /// これより古いバージョンで取得した銘柄は「未更新」とみなし、開いた時に自動で再取得する
    /// (例: 営業利益率・ROA・各CF・成長率を株探財務ページから取得するようにした v2)。</summary>
    private const int SchemaVersion = 2;

    private class CacheEntry { public DateTime At { get; set; } public int Ver { get; set; } }

    public FetchCoordinator(StockStore store, IIndicatorFetcher fetcher, FetchOptions opt,
        ILogger<FetchCoordinator> log)
    {
        _store = store;
        _fetcher = fetcher;
        _opt = opt;
        _log = log;
        _cacheFile = Path.Combine(_store.DataDirectory, "fetch_state.json");
        _cache = LoadCache();
    }

    /// <summary>MaxAgeDays 以内かつ現行スキーマで取得済みか。
    /// 古いスキーマ(指標が少ない)で取得した銘柄は false=要再取得とする。</summary>
    public bool IsFresh(string code)
    {
        lock (_cacheLock)
            return _cache.TryGetValue(code, out var e) && e.Ver >= SchemaVersion
                   && (DateTime.UtcNow - e.At).TotalDays < _opt.MaxAgeDays;
    }

    public DateTime? LastFetched(string code)
    {
        lock (_cacheLock) return _cache.TryGetValue(code, out var e) ? e.At : null;
    }

    /// <summary>
    /// 1銘柄の実値を取得して StockStore に反映する。
    /// force=false かつ取得済み(新しい)ならスキップ。並行リクエストは銘柄単位で直列化する。
    /// 戻り値: 実際に値を更新したか。
    /// </summary>
    public async Task<bool> FetchOneAsync(string code, bool force, CancellationToken ct, bool save = true)
    {
        if (!force && IsFresh(code)) return false;

        var gate = _inflight.GetOrAdd(code, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!force && IsFresh(code)) return false; // ロック取得後に再確認(他リクエストが取得済みの可能性)

            var values = await _fetcher.FetchAsync(code, _opt.UseKabutan, ct);
            bool updated = false;
            if (values.Count > 0)
            {
                updated = _store.ApplyFetched(new[] { (code, values) }, save) > 0;
                lock (_cacheLock) { _cache[code] = new CacheEntry { At = DateTime.UtcNow, Ver = SchemaVersion }; SaveCacheNoLock(); }
            }
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    private Dictionary<string, CacheEntry> LoadCache()
    {
        try
        {
            if (!File.Exists(_cacheFile)) return new();
            var text = File.ReadAllText(_cacheFile);
            // 新形式: { "code": { "At": ..., "Ver": n } }
            try
            {
                var v2 = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(text);
                if (v2 != null && v2.Values.All(e => e != null)) return v2!;
            }
            catch { }
            // 旧形式: { "code": "2026-06-25T..." } → Ver=1 として読み込む(現行スキーマより古い=要再取得)
            var v1 = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(text);
            if (v1 != null)
                return v1.ToDictionary(kv => kv.Key, kv => new CacheEntry { At = kv.Value, Ver = 1 });
        }
        catch { }
        return new();
    }

    private void SaveCacheNoLock()
    {
        try { File.WriteAllText(_cacheFile, JsonSerializer.Serialize(_cache)); } catch { }
    }
}
