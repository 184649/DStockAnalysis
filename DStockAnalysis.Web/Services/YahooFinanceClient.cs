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

    /// <summary>1銘柄の主要指標を取得する。取得できなければ空辞書。</summary>
    public async Task<Dictionary<string, string>> FetchAsync(string code, CancellationToken ct)
    {
        try
        {
            var json = await GetSummaryAsync(code, ct);
            if (json == null) return new();
            return ParseQuoteSummary(json);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogInformation("[yahoo] {Code}: {Msg}", code, e.Message);
            return new();
        }
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

        var price = Raw("price", "regularMarketPrice");
        if (price is null or <= 0) return d; // 株価が取れない＝信頼できないので何も返さない
        Put("Price", price);

        Put("PER", Raw("summaryDetail", "trailingPE"));
        Put("PBR", Raw("defaultKeyStatistics", "priceToBook"));
        Put("EPS", Raw("defaultKeyStatistics", "trailingEps"));

        // 利回り・性向・ROE は小数(0.0365)で返るため % に換算
        var dy = Raw("summaryDetail", "dividendYield");
        d["DividendYield"] = Math.Round((dy ?? 0) * 100, 2).ToString(CultureInfo.InvariantCulture); // 無配は 0
        var payout = Raw("summaryDetail", "payoutRatio");
        if (payout.HasValue) Put("PayoutRatio", payout.Value * 100);
        else if ((dy ?? 0) == 0) d["PayoutRatio"] = "0";
        Put("ROE", Raw("financialData", "returnOnEquity") is { } roe ? roe * 100 : null);

        return d;
    }
}
