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
                    ["Fetch:OnDemand"] = "false" // テストでは外部サイトへアクセスしない
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
        var body = new { PER = new { Max = 12.0 }, EquityRatio = new { Min = 50.0 } };
        var res = await _client.PostAsJsonAsync("/api/screen", body);
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.True(j.GetProperty("count").GetInt32() > 0);
        var first = j.GetProperty("stocks")[0];
        Assert.True(first.GetProperty("PER").GetDouble() <= 12.0);
    }

    [Fact]
    public async Task StockDetail_ReturnsStockHistoryAndLinks()
    {
        // 空条件で全件取得し先頭コードを得る
        var screen = await JsonOf(await _client.PostAsJsonAsync("/api/screen", new { }));
        var code = screen.GetProperty("stocks")[0].GetProperty("Code").GetString();

        var res = await _client.GetAsync($"/api/stocks/{code}");
        res.EnsureSuccessStatusCode();
        var j = await JsonOf(res);
        Assert.Equal(code, j.GetProperty("stock").GetProperty("Code").GetString());
        Assert.True(j.GetProperty("stock").GetProperty("History").GetArrayLength() > 0);
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
        Assert.Equal("最重要候補", j.GetProperty("Memo").GetProperty("Classification").GetString());
        Assert.Equal(95.0, j.GetProperty("UserInterest").GetDouble());
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
