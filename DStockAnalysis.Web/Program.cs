using System.Text;
using System.Text.Json.Serialization;
using DStockAnalysis.Models;
using DStockAnalysis.Services;
using DStockAnalysis.Web.Models;
using DStockAnalysis.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// JSON: 列挙体は文字列で(WPF版 DataStorageService と同じ表現)
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.PropertyNamingPolicy = null; // PascalCase 維持(Stock のプロパティ名と一致)
});

// 自動取得用 HttpClient(タイムアウト・UA はサービス側で設定)
builder.Services.AddHttpClient("scraper", c => c.Timeout = TimeSpan.FromSeconds(25));

// 設定(DI 解決時に最終構成から束縛する。テストの設定上書き等も確実に反映される)
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConfiguration>().GetSection("Fetch").Get<FetchOptions>() ?? new FetchOptions());

// アプリケーションサービス(シングルトン)
builder.Services.AddSingleton<StockStore>();
builder.Services.AddSingleton<YahooFinanceClient>();
builder.Services.AddSingleton<IndicatorFetchService>();
builder.Services.AddSingleton<IIndicatorFetcher>(sp => sp.GetRequiredService<IndicatorFetchService>());
builder.Services.AddSingleton<FetchCoordinator>();
builder.Services.AddSingleton<PresetService>();
builder.Services.AddSingleton<IndicatorFetchHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IndicatorFetchHostedService>());
builder.Services.AddSingleton<PriceRefreshHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceRefreshHostedService>());

var app = builder.Build();

// 起動時に全銘柄をロード
app.Services.GetRequiredService<StockStore>().Initialize();

app.UseDefaultFiles();
app.UseStaticFiles();

// ===== メタ情報 =====
app.MapGet("/api/meta", (StockStore store, PresetService presets) =>
{
    var date = store.MasterDate;
    bool stale = date.HasValue && (DateTime.Today - date.Value).TotalDays > 35;
    return Results.Ok(new MetaDto
    {
        MasterDate = date,
        Total = store.Count,
        FetchedCount = store.FetchedCount,
        FullyFetchedCount = store.FullyFetchedCount,
        UnfetchedCount = store.UnfetchedCount,
        MasterStale = stale,
        Sectors = store.Sectors(),
        Markets = store.Markets(),
        Scales = store.Scales(),
        BenefitCategories = store.BenefitCategories(),
        BenefitMonths = store.BenefitMonths(),
        Presets = presets.GetPresets().Select(p => new PresetDto { Name = p.Name, Criteria = p.Build() }).ToList()
    });
});

// ===== スクリーニング =====
app.MapPost("/api/screen", (ScreeningCriteria criteria, StockStore store) =>
{
    var list = store.Screen(criteria ?? new ScreeningCriteria());
    return Results.Ok(new
    {
        count = list.Count,
        stocks = list.Select(StockSummaryDto.From).ToList()
    });
});

// ===== 個別銘柄(詳細 + 時系列 + リンク) =====
// オンデマンド取得: 銘柄を開いた時、指標がサンプル値(または refresh 指定)なら
// その場で実値を取得して反映する(robots順守。取得済みはキャッシュで即時返却)。
app.MapGet("/api/stocks/{code}", async (string code, bool? refresh,
    StockStore store, FetchCoordinator coord, YahooFinanceClient yahoo, FetchOptions opt, CancellationToken ct) =>
{
    var s = store.Get(code);
    if (s == null) return Results.NotFound();

    if (opt.OnDemand)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20)); // 遅いサイトでも応答を返す
            if (refresh == true || !coord.IsFresh(code))
            {
                // 会社予想(株探)で未取得 → 全指標を取得(暫定のYahoo値を会社予想で上書き)
                await coord.FetchOneAsync(code, refresh == true, cts.Token);
            }
            else
            {
                // 会社予想で取得済み → 株価だけ毎回最新化(日々変動・株式分割の反映のため)
                var p = await yahoo.GetChartPriceAsync(code, cts.Token);
                if (p != null && double.TryParse(p, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var pv))
                    store.UpdatePrice(code, pv);
            }
            s = store.Get(code) ?? s; // 反映後の最新を返す
        }
        catch { /* 取得失敗時は既存値のまま返す */ }
    }

    return Results.Ok(new
    {
        stock = s,
        links = store.Links(code),
        lastFetched = coord.LastFetched(code),
        buffett = store.BuffettBreakdown(s) // バフェットスコアの内訳(根拠の明細)
    });
});

// ===== 比較(複数銘柄の詳細) =====
app.MapGet("/api/compare", (string codes, StockStore store) =>
{
    var list = (codes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return Results.Ok(store.GetMany(list).Select(StockSummaryDto.From).ToList());
});

// ===== ユーザーデータ保存(メモ・バフェットチェック・興味度) =====
app.MapPost("/api/stocks/{code}/userdata", (string code, UserDataRequest req, StockStore store) =>
{
    var s = store.SaveUserData(code, req.Memo, req.BuffettCheck, req.UserInterest);
    return s == null ? Results.NotFound() : Results.Ok(new { stock = s, buffett = store.BuffettBreakdown(s) });
});

// ===== CSV 取込(実データ列単位マージ) =====
app.MapPost("/api/import", async (HttpRequest req, StockStore store) =>
{
    using var reader = new StreamReader(req.Body, Encoding.UTF8);
    var content = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(content)) return Results.BadRequest(new { error = "CSV が空です" });
    var (updated, added) = store.ImportCsv(content);
    return Results.Ok(new { updated, added });
});

// ===== テンプレ CSV 出力 =====
app.MapGet("/api/template.csv", (StockStore store) =>
{
    var csv = store.ExportTemplate();
    return Results.File(new UTF8Encoding(true).GetBytes(csv), "text/csv", "stocks_template.csv");
});

// ===== JPX 全銘柄更新 =====
app.MapPost("/api/admin/update-master", async (StockStore store) =>
{
    bool ok = await store.UpdateMasterAsync();
    return ok
        ? Results.Ok(new { ok, total = store.Count, masterDate = store.MasterDate })
        : Results.Problem("JPX マスタの取得に失敗しました");
});

// ===== 自動取得: 状態参照 =====
app.MapGet("/api/admin/fetch/status", (IndicatorFetchHostedService svc) => Results.Ok(svc.Snapshot()));

// ===== 株価の一括最新化(手動トリガー) =====
app.MapPost("/api/admin/price-refresh", async (PriceRefreshHostedService svc, CancellationToken ct) =>
{
    int n = await svc.RefreshAllAsync(ct);
    return Results.Ok(new { refreshed = n });
});

// ===== 自動取得: 任意銘柄を即時取得(手動トリガー) =====
app.MapPost("/api/admin/fetch/run", async (string? codes, bool? force,
    IndicatorFetchHostedService svc, CancellationToken ct) =>
{
    IReadOnlyList<string>? list = string.IsNullOrWhiteSpace(codes)
        ? null
        : codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    // 巡回は時間がかかるためバックグラウンドで実行
    _ = Task.Run(() => svc.RunOnceAsync(CancellationToken.None, list, force ?? false));
    await Task.CompletedTask;
    return Results.Accepted("/api/admin/fetch/status", new { started = true });
});

app.Run();

// 統合テストからアクセスできるよう公開
public partial class Program { }
