using System.Globalization;
using System.Net;
using System.Text.Json;

namespace DStockAnalysis.Web.Services;

/// <summary>
/// Yahoo! ファイナンスの quoteSummary(構造化 JSON)から主要指標を取得する。
/// HTML スクレイピングと異なり、株価・PER・PBR・配当利回り・配当性向・EPS・ROE を
/// 構造化データとして確実に取得できる(現在値であり前日終値ではない)。
/// アクセスには crumb(認証トークン)と Cookie が必要なため、本クラスで管理・再取得する。
/// 銘柄シンボルは「{コード}.T」(東証)。
/// </summary>
public class YahooFinanceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceClient> _log;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);
    private string? _crumb;

    private const string UA =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    public YahooFinanceClient(ILogger<YahooFinanceClient> log)
    {
        _log = log;
        // Cookie を保持する専用ハンドラ(crumb 取得に必要)。シングルトンで1インスタンス使い回し。
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer(), AutomaticDecompression = DecompressionMethods.All };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UA);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
    }

    /// <summary>1銘柄の指標を取得する。取得できなければ空辞書。
    /// 株価は chart API(crumb 不要で堅牢)を最優先、財務系は quoteSummary を使う。</summary>
    public async Task<Dictionary<string, string>> FetchAsync(string code, CancellationToken ct)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = await GetSummaryAsync(code, ct);
            if (json != null) foreach (var kv in ParseQuoteSummary(json)) d[kv.Key] = kv.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogInformation("[yahoo] quoteSummary {Code}: {Msg}", code, e.Message);
        }

        // 株価は chart API(認証不要)で確実に取得し上書きする(現在値)。
        // 併せて直近3ヶ月の株価変化率・平均株価も算出する(株価変化カテゴリ)。
        try
        {
            var statsJson = await GetChartJsonAsync(code, "3mo", ct);
            if (statsJson != null)
                foreach (var kv in ParseChartStats(statsJson)) d[kv.Key] = kv.Value;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogInformation("[yahoo] chart stats {Code}: {Msg}", code, e.Message);
        }
        if (!d.ContainsKey("Price"))
        {
            var price = await GetChartPriceAsync(code, ct);
            if (price != null) d["Price"] = price;
        }

        return d;
    }

    /// <summary>chart API の生 JSON を取得する(range 指定)。crumb 不要。</summary>
    private async Task<string?> GetChartJsonAsync(string code, string range, CancellationToken ct)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{code}.T?interval=1d&range={range}";
        using var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    /// <summary>chart JSON(range=3mo)から 現在値・3ヶ月株価変化率・3ヶ月平均株価・対平均変化率を算出する。
    /// テスト可能な純粋関数(外部アクセスなし)。</summary>
    public static Dictionary<string, string> ParseChartStats(string json)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("chart").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return d;
        var r0 = result[0];
        if (!r0.TryGetProperty("indicators", out var ind) ||
            !ind.TryGetProperty("quote", out var q) || q.ValueKind != JsonValueKind.Array || q.GetArrayLength() == 0)
            return d;
        if (!q[0].TryGetProperty("close", out var closeArr) || closeArr.ValueKind != JsonValueKind.Array) return d;

        var closes = new List<double>();
        foreach (var c in closeArr.EnumerateArray())
            if (c.ValueKind == JsonValueKind.Number) closes.Add(c.GetDouble());
        if (closes.Count < 2) return d;

        double first = closes[0], last = closes[^1], avg = closes.Average();
        void P(string k, double v) => d[k] = Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
        if (first > 0) P("StockPriceChange3M", (last - first) / first * 100);          // 3ヶ月の騰落率
        if (avg > 0) P("AverageStockPriceChange3M", (last - avg) / avg * 100);          // 直近 vs 3ヶ月平均
        P("AveragePrice3M", avg);                                                       // 3ヶ月平均株価
        return d;
    }

    /// <summary>複数銘柄の現在値を一括取得する(v7/quote, 50件/リクエスト)。一覧の株価最新化に使う。</summary>
    public async Task<Dictionary<string, double>> GetQuotesAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < codes.Count; i += 50)
        {
            ct.ThrowIfCancellationRequested();
            var batch = codes.Skip(i).Take(50).ToList();
            var symbols = string.Join(",", batch.Select(c => c + ".T"));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var crumb = await EnsureCrumbAsync(ct);
                if (string.IsNullOrEmpty(crumb)) return result;
                var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(symbols)}&crumb={Uri.EscapeDataString(crumb)}";
                using var res = await _http.GetAsync(url, ct);
                if (res.StatusCode == HttpStatusCode.Unauthorized) { _crumb = null; continue; }
                if (!res.IsSuccessStatusCode) break;
                foreach (var kv in ParseQuotes(await res.Content.ReadAsStringAsync(ct))) result[kv.Key] = kv.Value;
                break;
            }
            await Task.Delay(300, ct); // 軽い間隔
        }
        return result;
    }

    /// <summary>v7/quote JSON を コード→現在値 に解析する(テスト可能な純粋関数)。</summary>
    public static Dictionary<string, double> ParseQuotes(string json)
    {
        var d = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("quoteResponse", out var qr)) return d;
        if (!qr.TryGetProperty("result", out var arr) || arr.ValueKind != JsonValueKind.Array) return d;
        foreach (var q in arr.EnumerateArray())
        {
            if (q.TryGetProperty("symbol", out var sym) && sym.ValueKind == JsonValueKind.String
                && q.TryGetProperty("regularMarketPrice", out var p) && p.ValueKind == JsonValueKind.Number
                && p.GetDouble() > 0)
            {
                var code = sym.GetString()!;
                var dot = code.IndexOf('.');
                if (dot > 0) code = code[..dot];
                d[code] = p.GetDouble();
            }
        }
        return d;
    }

    /// <summary>複数銘柄の主要指標(株価/PER/PBR/利回り/EPS/BPS/配当)を一括取得する(v7/quote, 50件/req)。
    /// 一覧を素早く埋めるための暫定値(PER は trailing)。会社予想の精密値は株探取得で上書きされる。</summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetQuoteIndicatorsAsync(IReadOnlyList<string> codes, CancellationToken ct)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < codes.Count; i += 50)
        {
            ct.ThrowIfCancellationRequested();
            var batch = codes.Skip(i).Take(50).ToList();
            var symbols = string.Join(",", batch.Select(c => c + ".T"));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var crumb = await EnsureCrumbAsync(ct);
                if (string.IsNullOrEmpty(crumb)) return result;
                var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(symbols)}&crumb={Uri.EscapeDataString(crumb)}";
                using var res = await _http.GetAsync(url, ct);
                if (res.StatusCode == HttpStatusCode.Unauthorized) { _crumb = null; continue; }
                if (!res.IsSuccessStatusCode) break;
                foreach (var kv in ParseQuoteIndicators(await res.Content.ReadAsStringAsync(ct))) result[kv.Key] = kv.Value;
                break;
            }
            await Task.Delay(250, ct);
        }
        return result;
    }

    /// <summary>v7/quote JSON を コード→指標辞書 に解析する(テスト可能)。</summary>
    public static Dictionary<string, Dictionary<string, string>> ParseQuoteIndicators(string json)
    {
        var outd = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("quoteResponse", out var qr)) return outd;
        if (!qr.TryGetProperty("result", out var arr) || arr.ValueKind != JsonValueKind.Array) return outd;
        foreach (var q in arr.EnumerateArray())
        {
            if (!q.TryGetProperty("symbol", out var sym) || sym.ValueKind != JsonValueKind.String) continue;
            var code = sym.GetString()!; var dot = code.IndexOf('.'); if (dot > 0) code = code[..dot];
            double? G(string k) => q.TryGetProperty(k, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
            var price = G("regularMarketPrice");
            if (price is null or <= 0) continue;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void P(string k, double? v, int dec = 2) { if (v.HasValue) d[k] = Math.Round(v.Value, dec).ToString(CultureInfo.InvariantCulture); }
            P("Price", price, 2);
            var per = G("trailingPE"); if (per is > 0) P("PER", per, 2);
            var pbr = G("priceToBook"); if (pbr is > 0) P("PBR", pbr, 2);
            if (per is > 0 && pbr is > 0) P("MixFactor", per.Value * pbr.Value, 1);
            P("EPS", G("epsTrailingTwelveMonths"), 1);
            P("BPS", G("bookValue"), 0);
            // 配当利回りは Yahoo の dividendYield(% と小数が混在し不安定)を使わず、
            // 1株配当 / 株価 × 100 で算出する(曖昧さ排除)。
            var dividend = G("trailingAnnualDividendRate");
            if (dividend is > 0) P("Dividend", dividend, 1);
            if (dividend is > 0 && price is > 0) P("DividendYield", dividend.Value / price.Value * 100, 2);
            outd[code] = d;
        }
        return outd;
    }

    /// <summary>chart API から現在値(regularMarketPrice)を取得する。crumb 不要で最も堅牢。</summary>
    public async Task<string?> GetChartPriceAsync(string code, CancellationToken ct)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{code}.T?interval=1d&range=1d";
            using var res = await _http.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var result = doc.RootElement.GetProperty("chart").GetProperty("result");
            if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return null;
            var meta = result[0].GetProperty("meta");
            if (meta.TryGetProperty("regularMarketPrice", out var p) && p.ValueKind == JsonValueKind.Number && p.GetDouble() > 0)
                return Math.Round(p.GetDouble(), 2).ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogInformation("[yahoo] chart {Code}: {Msg}", code, e.Message);
        }
        return null;
    }

    /// <summary>複数銘柄の財務指標(ROE/ROA/利益率/CF/成長率/有利子負債)を quoteSummary から
    /// 並行取得する(一覧を素早く埋める概算値)。robots 不要。失敗銘柄はスキップする。
    /// concurrency で同時実行数を制限し、Yahoo への過負荷を避ける。</summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> GetSummaryBatchAsync(
        IReadOnlyList<string> codes, int concurrency, CancellationToken ct, Action<int>? onProgress = null)
    {
        var result = new System.Collections.Concurrent.ConcurrentDictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        using var sem = new SemaphoreSlim(Math.Max(1, concurrency));
        int done = 0;
        var tasks = codes.Select(async code =>
        {
            await sem.WaitAsync(ct);
            try
            {
                var json = await GetSummaryAsync(code, ct);
                if (json != null)
                {
                    var d = ParseQuoteSummary(json);
                    // 財務指標(価格以外)が取れた場合のみ採用する
                    d.Remove("Price");
                    if (d.Count > 0) result[code] = d;
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _log.LogDebug("[yahoo] summary batch {Code}: {Msg}", code, e.Message);
            }
            finally
            {
                sem.Release();
                onProgress?.Invoke(Interlocked.Increment(ref done));
            }
        });
        await Task.WhenAll(tasks);
        return new Dictionary<string, Dictionary<string, string>>(result, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> GetSummaryAsync(string code, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var crumb = await EnsureCrumbAsync(ct);
            if (string.IsNullOrEmpty(crumb)) return null;

            var url = $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{code}.T" +
                      "?modules=price,summaryDetail,defaultKeyStatistics,financialData" +
                      $"&crumb={Uri.EscapeDataString(crumb)}";
            using var res = await _http.GetAsync(url, ct);
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                _crumb = null; // crumb 失効 → 再取得して1回だけリトライ
                continue;
            }
            if (!res.IsSuccessStatusCode) return null;
            return await res.Content.ReadAsStringAsync(ct);
        }
        return null;
    }

    private async Task<string?> EnsureCrumbAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_crumb)) return _crumb;
        await _crumbLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrEmpty(_crumb)) return _crumb;
            try { using var _ = await _http.GetAsync("https://fc.yahoo.com", ct); } catch { } // Cookie 取得(失敗可)
            using var res = await _http.GetAsync("https://query1.finance.yahoo.com/v1/test/getcrumb", ct);
            if (!res.IsSuccessStatusCode) return null;
            var c = (await res.Content.ReadAsStringAsync(ct)).Trim();
            // 取得失敗時は HTML が返ることがある。妥当な crumb のみ採用。
            _crumb = (c.Length is > 0 and < 30 && !c.Contains('<')) ? c : null;
            return _crumb;
        }
        finally { _crumbLock.Release(); }
    }

    /// <summary>quoteSummary JSON から指標を抽出する(列名→文字列)。テスト可能な純粋関数。</summary>
    public static Dictionary<string, string> ParseQuoteSummary(string json)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("quoteSummary").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return d;
        var r = result[0];

        double? Raw(string module, string field)
        {
            if (r.TryGetProperty(module, out var m) && m.ValueKind == JsonValueKind.Object
                && m.TryGetProperty(field, out var f) && f.ValueKind == JsonValueKind.Object
                && f.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.Number)
                return raw.GetDouble();
            return null;
        }
        void Put(string key, double? v, int dec = 2)
        { if (v.HasValue) d[key] = Math.Round(v.Value, dec).ToString(CultureInfo.InvariantCulture); }

        // 株価は株探(現在値)を優先するため、ここでは予備として保持(株探で取れない場合のフォールバック)
        Put("Price", Raw("price", "regularMarketPrice"));

        // 収益性・財務・CF・成長性を Yahoo から採用する(一覧を素早く埋める概算値。
        // 個別分析を開く/巡回取得で株探の会社予想・通期実績に精緻化される)。
        // バリュエーション(PER/PBR/配当利回り/EPS)は日本の会社予想ベース(株探)を採用するため Yahoo からは取らない。
        Put("ROE", Raw("financialData", "returnOnEquity") is { } roe ? roe * 100 : null);
        Put("ROA", Raw("financialData", "returnOnAssets") is { } roa ? roa * 100 : null);
        Put("OperatingMargin", Raw("financialData", "operatingMargins") is { } om ? om * 100 : null);
        Put("NetProfitMargin", Raw("financialData", "profitMargins") is { } pm ? pm * 100 : null);
        Put("InterestBearingDebtRatio", Raw("financialData", "debtToEquity")); // 有利子負債/自己資本(%)

        // 成長率(Yahooは直近の前年同期比。通期会社予想とは異なるため概算)
        if (Raw("financialData", "revenueGrowth") is { } rg) { Put("RevenueGrowthRate", rg * 100); Put("RevenueGrowth1Y", rg * 100); }
        if (Raw("financialData", "earningsGrowth") is { } eg) { Put("NetProfitGrowthRate", eg * 100); Put("EpsGrowthRate", eg * 100); }

        var ocf = Raw("financialData", "operatingCashflow");
        var fcf = Raw("financialData", "freeCashflow");
        var rev = Raw("financialData", "totalRevenue");
        if (ocf.HasValue) Put("OperatingCF", ocf.Value / 1_000_000.0, 0);   // 円→百万円
        if (fcf.HasValue) Put("FreeCashFlow", fcf.Value / 1_000_000.0, 0);
        if (ocf is > 0 && rev is > 0) Put("OperatingCashFlowMargin", ocf.Value / rev.Value * 100);
        // 採用しない: 時価総額(過少表示のため。会社予想ベースで株探から取得)。

        return d;
    }
}
