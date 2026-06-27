using DStockAnalysis.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DStockAnalysis.Web.Tests.Integration;

/// <summary>
/// FetchCoordinator(オンデマンド取得＋キャッシュ)の結合テスト。
/// フェッチャはフェイク実装に差し替え、ネットワークアクセスなしで
/// 「取得→反映→実データ化」「キャッシュによるスキップ」「force 再取得」を検証する。
/// </summary>
public class FetchCoordinatorTests : IDisposable
{
    private readonly string _dir;

    public FetchCoordinatorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dstock_coord_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    /// <summary>固定値を返すフェイクフェッチャ。呼び出し回数を数える。</summary>
    private sealed class FakeFetcher : IIndicatorFetcher
    {
        private readonly Dictionary<string, string> _values;
        public int Calls { get; private set; }
        public FakeFetcher(Dictionary<string, string> values) => _values = values;
        public Task<Dictionary<string, string>> FetchAsync(string code, bool useKabutan, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new Dictionary<string, string>(_values));
        }
        public Task<double?> GetCrawlDelayAsync(string url, CancellationToken ct) => Task.FromResult<double?>(null);
    }

    private (StockStore store, FetchCoordinator coord, FakeFetcher fake) Build(Dictionary<string, string> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataDir"] = _dir }).Build();
        var store = new StockStore(config, NullLogger<StockStore>.Instance);
        store.Initialize();
        var fake = new FakeFetcher(values);
        var opt = new FetchOptions { MaxAgeDays = 6 };
        var coord = new FetchCoordinator(store, fake, opt, NullLogger<FetchCoordinator>.Instance);
        return (store, coord, fake);
    }

    [Fact]
    public async Task FetchOne_AppliesRealValuesAndMarksNotSample()
    {
        var (store, coord, _) = Build(new() { ["PER"] = "8.5", ["ROE"] = "18.0" });
        var code = store.AllCodes().First();
        Assert.False(store.Get(code)!.IndicatorsFetched);

        bool updated = await coord.FetchOneAsync(code, force: false, CancellationToken.None);

        Assert.True(updated);
        var s = store.Get(code)!;
        Assert.Equal(8.5, s.PER, 3);
        Assert.Equal(18.0, s.ROE, 3);
        Assert.True(s.IndicatorsFetched);
    }

    [Fact]
    public async Task FetchOne_FreshCache_IsSkipped()
    {
        var (store, coord, fake) = Build(new() { ["PER"] = "8.5" });
        var code = store.AllCodes().First();

        await coord.FetchOneAsync(code, false, CancellationToken.None);
        Assert.Equal(1, fake.Calls);
        Assert.True(coord.IsFresh(code));

        // 2回目は MaxAgeDays 以内なので取得しない
        bool second = await coord.FetchOneAsync(code, false, CancellationToken.None);
        Assert.False(second);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task FetchOne_Force_RefetchesEvenIfFresh()
    {
        var (store, coord, fake) = Build(new() { ["PER"] = "8.5" });
        var code = store.AllCodes().First();

        await coord.FetchOneAsync(code, false, CancellationToken.None);
        await coord.FetchOneAsync(code, force: true, CancellationToken.None);

        Assert.Equal(2, fake.Calls);
    }

    [Fact] // 旧スキーマ(指標が少ない頃)で取得済みの銘柄は「未更新」とみなし再取得する
    public async Task FetchOne_OldSchemaCache_IsRefetched()
    {
        // 旧形式の fetch_state.json(コード→日時のみ。スキーマ版が無い=v1扱い)を最近の日時で用意
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataDir"] = _dir }).Build();
        var store = new StockStore(config, NullLogger<StockStore>.Instance);
        store.Initialize();
        var code = store.AllCodes().First();
        var stateFile = Path.Combine(_dir, "fetch_state.json");
        File.WriteAllText(stateFile,
            $"{{\"{code}\":\"{DateTime.UtcNow:o}\"}}"); // 旧形式・本日取得

        var fake = new FakeFetcher(new() { ["OperatingMargin"] = "12.0" });
        var coord = new FetchCoordinator(store, fake, new FetchOptions { MaxAgeDays = 6 },
            NullLogger<FetchCoordinator>.Instance);

        // 日時は新しくても旧スキーマなので未更新扱い → 再取得される
        Assert.False(coord.IsFresh(code));
        bool updated = await coord.FetchOneAsync(code, force: false, CancellationToken.None);
        Assert.True(updated);
        Assert.Equal(1, fake.Calls);
        Assert.True(coord.IsFresh(code)); // 取得後は現行スキーマで fresh
    }

    [Fact]
    public async Task FetchOne_EmptyResult_DoesNotMarkFresh()
    {
        var (store, coord, fake) = Build(new()); // 空 = 取得失敗相当
        var code = store.AllCodes().First();

        bool updated = await coord.FetchOneAsync(code, false, CancellationToken.None);

        Assert.False(updated);
        Assert.False(coord.IsFresh(code));       // 失敗はキャッシュしない(次回再試行)
        Assert.False(store.Get(code)!.IndicatorsFetched);
    }
}
