using DStockAnalysis.Models;
using DStockAnalysis.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DStockAnalysis.Web.Tests.Integration;

/// <summary>
/// StockStore の結合テスト。JPX 同梱マスタからの全銘柄ロード、スクリーニング、
/// ユーザーデータ保存→再計算、CSV 列単位マージ、テンプレ出力、取得値反映を検証する。
/// 各テストは独立した一時 DataDir を用いる(保存ファイルの衝突回避)。
/// </summary>
public class StockStoreTests : IDisposable
{
    private readonly string _dir;

    public StockStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dstock_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private StockStore NewStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataDir"] = _dir })
            .Build();
        var store = new StockStore(config, NullLogger<StockStore>.Instance);
        store.Initialize();
        return store;
    }

    [Fact]
    public void Initialize_LoadsAllListedStocksFromJpxMaster()
    {
        var store = NewStore();
        Assert.True(store.Count > 3000, $"全上場銘柄が読み込まれるはず (実際 {store.Count})");
        Assert.NotNull(store.MasterDate);
        // JPX 一覧には財務指標が無いため擬似指標で埋まる
        Assert.Equal(store.Count, store.SampleCount);
    }

    [Fact]
    public void Screen_AppliesCriteriaAndSortsByBuffettDesc()
    {
        var store = NewStore();
        var criteria = new ScreeningCriteria { DividendYield = new RangeFilter { Min = 4 } };
        var result = store.Screen(criteria);
        Assert.NotEmpty(result);
        Assert.All(result, s => Assert.True(s.DividendYield >= 4));
        // バフェットスコア降順
        for (int i = 1; i < result.Count; i++)
            Assert.True(result[i - 1].BuffettScore >= result[i].BuffettScore);
    }

    [Fact]
    public void SaveUserData_AllYesBuffett_RaisesBuffettScoreAndPersists()
    {
        var store = NewStore();
        var code = store.AllCodes().First();
        var before = store.Get(code)!.BuffettScore;

        var check = new BuffettCheck();
        foreach (var p in typeof(BuffettCheck).GetProperties().Where(p => p.PropertyType == typeof(YesNoUnknown)))
            p.SetValue(check, YesNoUnknown.Yes);

        var updated = store.SaveUserData(code, new StockMemo { Classification = StockClassification.最重要候補 }, check, 100);
        Assert.NotNull(updated);
        Assert.True(updated!.BuffettScore >= before, "全項目はいでバフェットスコアは下がらない");
        Assert.Equal(StockClassification.最重要候補, updated.Memo.Classification);

        // 再ロードしてもメモが残る
        var store2 = NewStore();
        Assert.Equal(StockClassification.最重要候補, store2.Get(code)!.Memo.Classification);
    }

    [Fact]
    public void ImportCsv_ColumnMerge_OverwritesOnlyProvidedColumns()
    {
        var store = NewStore();
        var code = store.AllCodes().First();
        var orig = store.Get(code)!;
        double origRoe = orig.ROE;

        // PER だけを実データで上書き(ROE は CSV に無いので維持)
        var csv = $"Code,PER\n{code},9.99\n";
        var (updated, added) = store.ImportCsv(csv);

        Assert.Equal(1, updated);
        Assert.Equal(0, added);
        var after = store.Get(code)!;
        Assert.Equal(9.99, after.PER, 3);
        Assert.Equal(origRoe, after.ROE, 3);          // 未指定列は維持
        Assert.False(after.IsSampleIndicators);        // 実データ化
    }

    [Fact]
    public void ImportCsv_UnknownCode_IsAdded()
    {
        var store = NewStore();
        int before = store.Count;
        var csv = "Code,Name,Market,Sector,Scale,PER\n9XXX,テスト銘柄,東証プライム,情報・通信業,大型,12.3\n";
        var (updated, added) = store.ImportCsv(csv);
        Assert.Equal(0, updated);
        Assert.Equal(1, added);
        Assert.Equal(before + 1, store.Count);
        Assert.Equal("テスト銘柄", store.Get("9XXX")!.Name);
    }

    [Fact]
    public void ApplyFetched_MergesScrapedValuesAndRecalculates()
    {
        var store = NewStore();
        var code = store.AllCodes().First();
        var values = new Dictionary<string, string> { ["PER"] = "8.5", ["ROE"] = "18.0" };

        int n = store.ApplyFetched(new[] { (code, values) });
        Assert.Equal(1, n);
        var s = store.Get(code)!;
        Assert.Equal(8.5, s.PER, 3);
        Assert.Equal(18.0, s.ROE, 3);
        Assert.False(s.IsSampleIndicators);
    }

    [Fact]
    public void ExportTemplate_ContainsHeaderAndAllCodes()
    {
        var store = NewStore();
        var csv = store.ExportTemplate();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Code,Name,Market,Sector,Scale", lines[0]);
        Assert.Equal(store.Count + 1, lines.Length); // ヘッダー + 全銘柄
    }

    [Fact]
    public void Links_ReturnsSixReferenceSources()
    {
        var store = NewStore();
        var links = store.Links("7203");
        Assert.Equal(6, links.Count);
        Assert.Contains(links, l => l.Url.Contains("irbank.net/7203"));
    }
}
