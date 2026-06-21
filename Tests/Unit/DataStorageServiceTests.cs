using System.IO;
using DStockAnalysis.Models;
using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class DataStorageServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly DataStorageService _storage;

    public DataStorageServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "DStockTest_" + Guid.NewGuid().ToString("N"));
        _storage = new DataStorageService(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Fact] // UT-DS-01: 銘柄の保存・読込ラウンドトリップ
    public void SaveAndLoadStocks_RoundTrip()
    {
        var stocks = new List<Stock> { TestData.Good(), TestData.Weak() };
        _storage.SaveStocks(stocks);

        Assert.True(_storage.HasSavedStocks);
        var loaded = _storage.LoadStocks();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, s => s.Code == "0001" && s.Name == "テスト優良");
    }

    [Fact] // UT-DS-02: メモ・チェックがコード単位でマージされる
    public void ApplyUserData_MergesByCode()
    {
        var saved = TestData.Good();
        saved.Memo.DiscoveryReason = "高ROEに注目";
        saved.Memo.Classification = StockClassification.最重要候補;
        saved.BuffettCheck.UnderstandBusiness = YesNoUnknown.Yes;
        saved.UserInterest = 88;
        _storage.SaveUserData(new[] { saved });

        // 別インスタンス(CSV再取込を想定)へマージ
        var fresh = TestData.Good();
        Assert.Equal("", fresh.Memo.DiscoveryReason);
        _storage.ApplyUserData(new[] { fresh });

        Assert.Equal("高ROEに注目", fresh.Memo.DiscoveryReason);
        Assert.Equal(StockClassification.最重要候補, fresh.Memo.Classification);
        Assert.Equal(YesNoUnknown.Yes, fresh.BuffettCheck.UnderstandBusiness);
        Assert.Equal(88, fresh.UserInterest);
    }

    [Fact] // UT-DS-03: 設定の保存・読込
    public void SaveAndLoadSettings_RoundTrip()
    {
        var settings = new AppSettings { ComparisonCodes = { "7203", "8058" }, LastCsvPath = @"C:\x.csv" };
        _storage.SaveSettings(settings);
        var loaded = _storage.LoadSettings();
        Assert.Equal(2, loaded.ComparisonCodes.Count);
        Assert.Equal(@"C:\x.csv", loaded.LastCsvPath);
    }

    [Fact] // UT-DS-04: 保存ファイルが無い場合は空を返す
    public void Load_WhenEmpty_ReturnsEmpty()
    {
        Assert.False(_storage.HasSavedStocks);
        Assert.Empty(_storage.LoadStocks());
        Assert.Empty(_storage.LoadUserData());
    }
}
