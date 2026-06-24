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
        // 擬似指標は生成しない。起動直後は全銘柄が未取得(指標0・スコア0)。
        Assert.Equal(0, store.FetchedCount);
        Assert.Equal(store.Count, store.UnfetchedCount);
        var sample = store.Get(store.AllCodes().First())!;
        Assert.False(sample.IndicatorsFetched);
        Assert.Equal(0, sample.PER);
        Assert.Equal(0, sample.BuffettScore);
    }

    [Fact]
    public void Screen_AppliesCriteriaAndSortsByBuffettDesc()
    {
        var store = NewStore();
        // 実データを数銘柄へ反映(未取得のままでは指標で絞れないため)
        var codes = store.AllCodes().Take(5).ToList();
        foreach (var c in codes)
            store.ApplyFetched(new[] { (c, new Dictionary<string, string> { ["DividendYield"] = "5.0", ["PER"] = "10", ["ROE"] = "12" }) });

        var result = store.Screen(new ScreeningCriteria { DividendYield = new RangeFilter { Min = 4 } });
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
        Assert.Equal(origRoe, after.ROE, 3);          // 未指定列は維持(未取得なら0のまま)
        Assert.True(after.IndicatorsFetched);          // 実データ化
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

        Assert.False(store.Get(code)!.IndicatorsFetched); // 取得前は未取得
        int n = store.ApplyFetched(new[] { (code, values) });
        Assert.Equal(1, n);
        var s = store.Get(code)!;
        Assert.Equal(8.5, s.PER, 3);
        Assert.Equal(18.0, s.ROE, 3);
        Assert.True(s.IndicatorsFetched);
    }

    [Fact]
    public void ApplyFetched_MarksBenefitUnknownAndTotalYieldDividendOnly()
    {
        var store = NewStore();
        var code = store.AllCodes().First();

        // 自動取得(優待列は含まない)を反映。優待は取得対象外。
        store.ApplyFetched(new[] { (code, new Dictionary<string, string> { ["PER"] = "10.0", ["DividendYield"] = "3.0" }) });

        var s = store.Get(code)!;
        Assert.True(s.IndicatorsFetched);
        Assert.True(s.BenefitUnknown);            // 優待は未取得扱い
        Assert.False(s.HasShareholderBenefit);
        Assert.Equal(0, s.BenefitYield);
        Assert.Equal(s.DividendYield, s.TotalYield, 3); // 総合利回り=配当のみ
    }

    [Fact]
    public void ImportCsv_WithBenefitColumns_MarksBenefitKnown()
    {
        var store = NewStore();
        var code = store.AllCodes().First();
        store.ApplyFetched(new[] { (code, new Dictionary<string, string> { ["PER"] = "10.0" }) });
        Assert.True(store.Get(code)!.BenefitUnknown);

        // CSV で優待列を取り込むと実データ優待として扱う
        store.ImportCsv($"Code,HasShareholderBenefit,BenefitContent,BenefitYield\n{code},1,QUOカード1000円,1.5\n");
        var s = store.Get(code)!;
        Assert.False(s.BenefitUnknown);
        Assert.True(s.HasShareholderBenefit);
    }

    [Fact]
    public void UpdatePrice_RefreshesPriceAndRederivesMetrics()
    {
        var store = NewStore();
        var code = store.AllCodes().First();
        // 会社予想ベースの指標を反映(株探相当): PER/PBR/配当利回り。EPS等は派生。
        store.ApplyFetched(new[] { (code, new Dictionary<string, string>
            { ["PER"] = "10", ["PBR"] = "2", ["DividendYield"] = "4" }) });

        // 後日、株価が分割等で変わった想定 → 株価だけ最新化
        var s = store.UpdatePrice(code, 1000);
        Assert.NotNull(s);
        Assert.Equal(1000, s!.Price, 3);
        Assert.Equal(100, s.EPS, 1);        // 1000/PER10
        Assert.Equal(500, s.BPS, 0);        // 1000/PBR2
        Assert.Equal(40, s.Dividend, 1);    // 1000×4%/...=40
        Assert.Equal(40, s.PayoutRatio, 0); // 40/EPS100×100
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
