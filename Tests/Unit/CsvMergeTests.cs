using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class CsvMergeTests
{
    private readonly CsvImportService _csv = new();

    [Fact] // UT-MRG-01: 列単位マージ — CSVに無い列は維持、有る列のみ上書き
    public void CopyIndicatorsFrom_OnlyOverwritesPresentColumns()
    {
        var existing = TestData.Good();           // PER=12, ROE=15, Price=3000 等
        var originalRoe = existing.ROE;
        var originalPer = existing.PER;

        // 価格だけを含む部分CSV
        var (stocks, columns) = _csv.ParseWithColumns("Code,Price\n0001,4567\n");
        var imp = Assert.Single(stocks);

        existing.CopyIndicatorsFrom(imp, columns);

        Assert.Equal(4567, existing.Price);       // 上書きされる
        Assert.Equal(originalRoe, existing.ROE);  // 維持される
        Assert.Equal(originalPer, existing.PER);  // 維持される
        Assert.False(existing.IsSampleIndicators);
    }

    [Fact] // UT-MRG-02: 列集合がCSVヘッダーを反映する
    public void ParseWithColumns_ReturnsHeaderColumns()
    {
        var (_, columns) = _csv.ParseWithColumns("Code,PER,ROE\n7203,10,12\n");
        Assert.Contains("Code", columns);
        Assert.Contains("PER", columns);
        Assert.Contains("ROE", columns);
        Assert.DoesNotContain("PBR", columns);
    }

    [Fact] // UT-MRG-03: cols=null は全項目上書き(従来動作)
    public void CopyIndicatorsFrom_NullColumns_OverwritesAll()
    {
        var existing = TestData.Good();
        var src = TestData.Weak();
        existing.CopyIndicatorsFrom(src, null);
        Assert.Equal(src.PER, existing.PER);
        Assert.Equal(src.ROE, existing.ROE);
        Assert.Equal(src.Name, existing.Name);
    }
}
