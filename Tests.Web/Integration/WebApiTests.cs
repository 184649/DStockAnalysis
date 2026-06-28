using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DStockAnalysis.Web.Tests.Integration;

/// <summary>
/// HTTP エンドポイントの結合テスト。WebApplicationFactory でアプリを起動し、
/// メタ/スクリーニング/個別/CSV取込/テンプレ/静的ファイルの動作を検証する。
/// 自動取得は無効化し、DataDir は一時フォルダを使う。
/// </summary>
public class WebApiTests : IClassFixture<WebApiTests.ApiFactory>
{
    private readonly HttpClient _client;

    public WebApiTests(ApiFactory factory) => _client = factory.CreateClient();

    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dstock_api_" + Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DataDir"] = _dir,
                    ["Fetch:Enabled"] = "false",
                    ["Fetch:OnDemand"] = "false", // テストでは外部サイトへアクセスしない
                    ["Fetch:PriceRefresh"] = "false"
                }));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { Directory.Delete(_dir, true); } catch { }
        }
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task Meta_ReturnsUniverseAndPresets()
    {
        var res = await _client.GetAsync("/api/meta");
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.True(j.GetProperty("Total").GetInt32() > 3000);
        Assert.True(j.GetProperty("Presets").GetArrayLength() >= 15);
        Assert.True(j.GetProperty("Sectors").GetArrayLength() > 10);
    }

    [Fact]
    public async Task Screen_ReturnsMatchingStocks()
    {
        // 先頭銘柄に実データを取り込んでから指標で絞り込む(擬似値は無いため)
        var screen0 = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var code = screen0.GetProperty("stocks")[0].GetProperty("Code").GetString();
        await _client.PostAsync("/api/import",
            new StringContent($"Code,PER,EquityRatio\n{code},11.0,60.0\n", Encoding.UTF8, "text/csv"));

        var body = new { PER = new { Max = 12.0 }, EquityRatio = new { Min = 50.0 } };
        var res = await _client.PostAsJsonAsync("/api/screen", body);
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.True(j.GetProperty("count").GetInt32() > 0);
        var first = j.GetProperty("stocks")[0];
        Assert.True(first.GetProperty("PER").GetDouble() <= 12.0);
    }

    [Fact]
    public async Task StockDetail_ReturnsStockAndLinks()
    {
        // 空条件で全件取得し先頭コードを得る
        var screen = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var code = screen.GetProperty("stocks")[0].GetProperty("Code").GetString();

        var res = await _client.GetAsync($"/api/stocks/{code}");
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.Equal(code, j.GetProperty("stock").GetProperty("Code").GetString());
        // 擬似時系列は生成しない(実績データが無ければ空)。配列であることのみ確認。
        Assert.Equal(JsonValueKind.Array, j.GetProperty("stock").GetProperty("History").ValueKind);
        Assert.Equal(6, j.GetProperty("links").GetArrayLength());
    }

    [Fact]
    public async Task StockDetail_UnknownCode_Returns404()
    {
        var res = await _client.GetAsync("/api/stocks/0000ZZZ");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Import_ColumnMerge_UpdatesExistingStock()
    {
        var screen = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var code = screen.GetProperty("stocks")[0].GetProperty("Code").GetString();

        var csv = $"Code,PER\n{code},7.77\n";
        var res = await _client.PostAsync("/api/import",
            new StringContent(csv, Encoding.UTF8, "text/csv"));
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.Equal(1, j.GetProperty("updated").GetInt32());

        var detail = await JsonOf(await _client.GetAsync($"/api/stocks/{code}"));
        Assert.Equal(7.77, detail.GetProperty("stock").GetProperty("PER").GetDouble(), 3);
    }

    // ユーザー提示の網羅的 CSV を取り込み、一覧(/api/compare)と詳細(/api/stocks)の両方で
    // 値が保持・表示されることを検証する(全指標が画面で確認できる不具合の回帰防止)。
    private const string FullCsv =
        "Code,PER,PBR,ROE,EPS,BPS,DividendYield,PayoutRatio,Dividend,EquityRatio,InterestBearingDebtRatio," +
        "OperatingMargin,OrdinaryProfitMargin,NetProfitMargin,RevenueGrowth1Y,RevenueGrowth3Y,RevenueGrowth5Y,RevenueGrowth10Y," +
        "OperatingCF,InvestingCF,FinancingCF,FreeCashFlow,OperatingCashFlowMargin,BuybackAmount," +
        "DividendGrowth1Y,DividendGrowth3Y,DividendGrowth5Y,DividendGrowth10Y," +
        "StockPriceChange3M,AverageStockPriceChange3M,AveragePrice3M,PriceChange3M,PriceChangeAverage3M," +
        "BenefitContent,BenefitValue,BenefitYield,TotalYield\n" +
        "7203,10.5,1.1,12.3,250.4,3000,3.2,35.0,80,45.0,30.0,8.5,9.1,6.2,2.1,6.5,10.2,15.0," +
        "1000000,-500000,200000,500000,12.5,300000,5.0,8.0,10.0,12.0,4.5,3.2,2800,5.0,4.1,QUOカード,1000,1.0,4.2\n";

    [Fact]
    public async Task ImportFullCsv_ValuesVisibleInDetailAndCompare()
    {
        var imp = await JsonOf(await _client.PostAsync("/api/import", new StringContent(FullCsv, Encoding.UTF8, "text/csv")));
        Assert.Equal(1, imp.GetProperty("updated").GetInt32());

        // 詳細(完全な Stock)に値が保持されている
        var st = (await JsonOf(await _client.GetAsync("/api/stocks/7203"))).GetProperty("stock");
        Assert.Equal(3000, st.GetProperty("BPS").GetDouble());
        Assert.Equal(80, st.GetProperty("Dividend").GetDouble());
        Assert.Equal(1000000, st.GetProperty("OperatingCF").GetDouble());
        Assert.Equal(-500000, st.GetProperty("InvestingCF").GetDouble());
        Assert.Equal(200000, st.GetProperty("FinancingCF").GetDouble());
        Assert.Equal(15.0, st.GetProperty("RevenueGrowth10Y").GetDouble());
        Assert.Equal(12.0, st.GetProperty("DividendGrowth10Y").GetDouble());
        Assert.Equal(300000, st.GetProperty("BuybackAmount").GetDouble());
        Assert.Equal(2800, st.GetProperty("AveragePrice3M").GetDouble());
        Assert.Equal(4.1, st.GetProperty("PriceChangeAverage3M").GetDouble());
        Assert.Equal("QUOカード", st.GetProperty("BenefitContent").GetString());
        Assert.Equal(1000, st.GetProperty("BenefitValue").GetDouble());
        Assert.True(st.GetProperty("HasShareholderBenefit").GetBoolean()); // 優待列取込で有効
        Assert.False(st.GetProperty("BenefitUnknown").GetBoolean());       // CSV取込済み

        // 比較(StockSummaryDto)にも追加指標が含まれ、値が出る
        var cmp = await JsonOf(await _client.GetAsync("/api/compare?codes=7203"));
        var c = cmp[0];
        Assert.Equal(3000, c.GetProperty("BPS").GetDouble());
        Assert.Equal(1000000, c.GetProperty("OperatingCF").GetDouble());
        Assert.Equal("QUOカード", c.GetProperty("BenefitContent").GetString());
        Assert.Equal(1000, c.GetProperty("BenefitValue").GetDouble());
        Assert.Equal(15.0, c.GetProperty("RevenueGrowth10Y").GetDouble());
    }

    [Fact]
    public async Task ScreenJson_ContainsExtendedIndicatorFields()
    {
        var j = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var first = j.GetProperty("stocks")[0];
        // 追加した指標プロパティが JSON に存在する(値の有無に関わらずフィールドがある)
        foreach (var prop in new[] { "BPS", "ROA", "TotalAssetTurnover", "Dividend", "OperatingCF", "InvestingCF", "FinancingCF",
            "RevenueGrowth10Y", "DividendGrowth10Y", "BuybackAmount", "BenefitContent", "BenefitValue",
            "AveragePrice3M", "PriceChangeAverage3M", "OrdinaryProfitMargin", "Description", "IRUrl" })
            Assert.True(first.TryGetProperty(prop, out _), $"{prop} が screen レスポンスに無い");
    }

    [Fact]
    public async Task SaveUserData_RecalculatesScore()
    {
        var screen = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var code = screen.GetProperty("stocks")[0].GetProperty("Code").GetString();

        var body = new
        {
            Memo = new { Classification = "最重要候補" },
            BuffettCheck = new { UnderstandBusiness = 1, DemandIn10Years = 1, HasCompetitiveAdvantage = 1 },
            UserInterest = 95.0
        };
        var res = await _client.PostAsJsonAsync($"/api/stocks/{code}/userdata", body);
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        var st = j.GetProperty("stock");
        Assert.Equal("最重要候補", st.GetProperty("Memo").GetProperty("Classification").GetString());
        Assert.Equal(95.0, st.GetProperty("UserInterest").GetDouble());
        // バフェット採点(6サブスコア等)が stock.Buffett に含まれる
        var bf = st.GetProperty("Buffett");
        foreach (var p in new[] { "BuffettScore", "BusinessDurabilityScore", "ProfitabilityScore", "SafetyScore",
            "GrowthStabilityScore", "CapitalAllocationScore", "ValuationScore", "DataConfidence", "OverallGrade", "JudgementText" })
            Assert.True(bf.TryGetProperty(p, out _), $"{p} が stock.Buffett に無い");
    }

    [Fact] // バフェット採点が /api/screen・/api/stocks・/api/compare のレスポンスに含まれる
    public async Task BuffettScore_Present_InScreenStockAndCompare()
    {
        // 採点に必要な指標を CSV で投入(実取得化)
        var csv = "Code,PER,PBR,ROE,ROA,EPS,BPS,OperatingMargin,NetProfitMargin,EquityRatio," +
                  "RevenueGrowth5Y,OperatingProfitGrowthRate,OperatingCF,FreeCashFlow,DividendYield,PayoutRatio,MarketCap\n" +
                  "7203,15,2.0,18,10,200,1500,20,14,60,7,8,200000,150000,2.5,40,1000000\n";
        await _client.PostAsync("/api/import", new StringContent(csv, Encoding.UTF8, "text/csv"));

        string[] fields = { "BuffettScore", "BusinessDurabilityScore", "ProfitabilityScore", "SafetyScore",
            "GrowthStabilityScore", "CapitalAllocationScore", "ValuationScore", "DataConfidence", "OverallGrade",
            "JudgementText", "ScoringProfile", "HighScoreReasons", "PenaltyReasons", "RankDecisionReasons", "UsedWeights" };

        // /api/screen
        var screen = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var listed = screen.GetProperty("stocks").EnumerateArray().First(x => x.GetProperty("Code").GetString() == "7203");
        var lbf = listed.GetProperty("Buffett");
        foreach (var f in fields) Assert.True(lbf.TryGetProperty(f, out _), $"screen: {f} 欠落");
        Assert.True(lbf.GetProperty("BuffettScore").GetDouble() >= 80); // 高品質→A以上

        // /api/stocks/{code}
        var detail = (await JsonOf(await _client.GetAsync("/api/stocks/7203"))).GetProperty("stock").GetProperty("Buffett");
        foreach (var f in fields) Assert.True(detail.TryGetProperty(f, out _), $"detail: {f} 欠落");

        // /api/compare
        var cmp = (await JsonOf(await _client.GetAsync("/api/compare?codes=7203")))[0].GetProperty("Buffett");
        foreach (var f in fields) Assert.True(cmp.TryGetProperty(f, out _), $"compare: {f} 欠落");
    }

    [Fact]
    public async Task Template_ReturnsCsvDownload()
    {
        var res = await _client.GetAsync("/api/template.csv");
        res.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);
        var text = await res.Content.ReadAsStringAsync();
        Assert.StartsWith("Code,Name,Market,Sector,Scale", text);
    }

    [Fact]
    public async Task Index_StaticFile_IsServed()
    {
        var res = await _client.GetAsync("/");
        res.EnsureSuccessStatusCode();
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("DStockAnalysis", html);
    }

    [Fact]
    public async Task FetchStatus_IsAvailableAndDisabled()
    {
        var res = await _client.GetAsync("/api/admin/fetch/status");
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.False(j.GetProperty("Enabled").GetBoolean());
    }
}
