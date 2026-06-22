using System.Globalization;
using System.Text.RegularExpressions;

namespace DStockAnalysis.Web.Services;

/// <summary>
/// 主要指標(PER/PBR/ROE/配当利回り/時価総額/EPS 等)を許可サイトから取得する。
/// tools/fetch_data.py の C# 移植版。robots.txt を順守し、十分な間隔を空けて低頻度で取得する。
///
/// 取得方針(規約・サーバ負荷への配慮):
///  - robots.txt を必ず確認し、Disallow の URL は取得しない。Crawl-delay も尊重する。
///  - リクエスト間隔を十分に空ける(既定8秒)。短期売買用ではないため低速で良い。
///  - robots が自動アクセスを拒否するバフェット・コードは対象外(リンクのみ)。
///  - 取得できた列だけを返す(不確実な値は含めない)。反映は列単位マージで安全。
/// </summary>
/// <summary>1銘柄分の指標を取得するフェッチャ。テストでは差し替え可能にするため抽象化する。</summary>
public interface IIndicatorFetcher
{
    Task<Dictionary<string, string>> FetchAsync(string code, bool useKabutan, CancellationToken ct);
    Task<double?> GetCrawlDelayAsync(string url, CancellationToken ct);
}

public class IndicatorFetchService : IIndicatorFetcher
{
    private readonly HttpClient _http;
    private readonly ILogger<IndicatorFetchService> _log;
    private readonly Dictionary<string, RobotsRules> _robots = new();
    private readonly SemaphoreSlim _robotsLock = new(1, 1);

    private const string UA =
        "Mozilla/5.0 (compatible; DStockAnalysis-personal/1.0; +weekly local research)";

    public IndicatorFetchService(IHttpClientFactory factory, ILogger<IndicatorFetchService> log)
    {
        _http = factory.CreateClient("scraper");
        _log = log;
    }

    /// <summary>1銘柄分の指標を取得して列名→文字列値の辞書で返す(取得できた列のみ)。</summary>
    public async Task<Dictionary<string, string>> FetchAsync(string code, bool useKabutan, CancellationToken ct)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await MergeFrom(row, $"https://irbank.net/{code}", ParseIrBank, ct);
        await MergeFrom(row, $"https://minkabu.jp/stock/{code}", ParseMinkabu, ct);
        if (useKabutan)
            await MergeFrom(row, $"https://kabutan.jp/stock/?code={code}", ParseKabutan, ct);

        return row;
    }

    private async Task MergeFrom(Dictionary<string, string> row, string url,
        Func<string, Dictionary<string, string>> parse, CancellationToken ct)
    {
        var html = await FetchAsync(url, ct);
        if (html == null) return;
        foreach (var kv in parse(html))
            if (!row.ContainsKey(kv.Key)) row[kv.Key] = kv.Value;
    }

    /// <summary>1ページ取得。robots 不許可なら null。</summary>
    public async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        var uri = new Uri(url);
        var rules = await GetRobotsAsync(uri, ct);
        if (!rules.IsAllowed(uri.PathAndQuery))
        {
            _log.LogInformation("[robots] 取得不可のためスキップ: {Url}", url);
            return null;
        }
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", UA);
            req.Headers.TryAddWithoutValidation("Accept-Language", "ja,en;q=0.8");
            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogInformation("[error] {Url} : {Status}", url, (int)res.StatusCode);
                return null;
            }
            return await res.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogInformation("[error] {Url} : {Msg}", url, e.Message);
            return null;
        }
    }

    public async Task<double?> GetCrawlDelayAsync(string url, CancellationToken ct)
    {
        var rules = await GetRobotsAsync(new Uri(url), ct);
        return rules.CrawlDelay;
    }

    private async Task<RobotsRules> GetRobotsAsync(Uri uri, CancellationToken ct)
    {
        var baseUrl = $"{uri.Scheme}://{uri.Authority}";
        await _robotsLock.WaitAsync(ct);
        try
        {
            if (_robots.TryGetValue(baseUrl, out var cached)) return cached;
            RobotsRules rules;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/robots.txt");
                req.Headers.TryAddWithoutValidation("User-Agent", UA);
                using var res = await _http.SendAsync(req, ct);
                rules = res.IsSuccessStatusCode
                    ? RobotsRules.Parse(await res.Content.ReadAsStringAsync(ct))
                    : RobotsRules.AllowAll();
            }
            catch
            {
                rules = RobotsRules.AllowAll(); // 取得失敗時は許可扱い(404相当)
            }
            _robots[baseUrl] = rules;
            return rules;
        }
        finally { _robotsLock.Release(); }
    }

    // ---------- サイト別の抽出 ----------

    public static Dictionary<string, string> ParseMinkabu(string html)
    {
        var t = Strip(html);
        var d = new Dictionary<string, string>();
        Put(d, "Price", Find(t, @"現在値[^0-9\-]*([0-9,]+(?:\.[0-9]+)?)"));
        Put(d, "PER", Find(t, @"PER[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍"));
        Put(d, "PBR", Find(t, @"PBR[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍"));
        Put(d, "DividendYield", Find(t, @"配当利回り[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]"));
        return d;
    }

    public static Dictionary<string, string> ParseIrBank(string html)
    {
        var t = Strip(html);
        var d = new Dictionary<string, string>();
        Put(d, "ROE", Find(t, @"ROE[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]"));
        Put(d, "PER", Find(t, @"PER[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍"));
        Put(d, "PBR", Find(t, @"PBR[^0-9\-]*([0-9,]+\.?[0-9]*)\s*倍"));
        Put(d, "DividendYield", Find(t, @"配当利回り[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]"));
        Put(d, "EPS", Find(t, @"\bEPS[^0-9\-]*([0-9,]+\.?[0-9]*)"));
        Put(d, "EquityRatio", Find(t, @"自己資本比率[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]"));
        Put(d, "PayoutRatio", Find(t, @"配当性向[^0-9\-]*([0-9]+\.?[0-9]*)\s*[%％]"));
        var mm = Regex.Match(t, @"時価総額[^0-9\-]*([0-9,]+\.?[0-9]*)\s*(兆|億)");
        if (mm.Success)
        {
            var v = Num(mm.Groups[1].Value);
            if (v.HasValue)
            {
                double m = mm.Groups[2].Value == "兆" ? v.Value * 1_000_000 : v.Value * 100;
                d["MarketCap"] = Math.Round(m).ToString(CultureInfo.InvariantCulture);
            }
        }
        return d;
    }

    public static Dictionary<string, string> ParseKabutan(string html)
    {
        var t = Strip(html);
        var d = new Dictionary<string, string>();
        Put(d, "Price", Find(t, @"現在値[^0-9\-]*([0-9,]+(?:\.[0-9]+)?)"));
        var m = Regex.Match(t, @"PER\s+PBR[^0-9\-]*([0-9]+\.[0-9]+)\s+([0-9]+\.[0-9]+)\s+([0-9]+\.[0-9]+)");
        if (m.Success)
        {
            d["PER"] = m.Groups[1].Value;
            d["PBR"] = m.Groups[2].Value;
            d["DividendYield"] = m.Groups[3].Value;
        }
        return d;
    }

    // ---------- 抽出ヘルパ ----------

    public static string Strip(string html)
    {
        var t = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(t, @"\s+", " ");
    }

    private static string? Find(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        if (!m.Success) return null;
        var v = Num(m.Groups[1].Value);
        return v?.ToString(CultureInfo.InvariantCulture);
    }

    private static double? Num(string s)
        => double.TryParse(s.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static void Put(Dictionary<string, string> d, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value)) d[key] = value!;
    }
}

/// <summary>robots.txt の最小パーサ(User-agent / Disallow / Allow / Crawl-delay)。</summary>
public class RobotsRules
{
    private readonly List<(string path, bool allow)> _rules = new();
    public double? CrawlDelay { get; private set; }

    public static RobotsRules AllowAll() => new();

    public static RobotsRules Parse(string text)
    {
        var rules = new RobotsRules();
        bool applies = false;     // 現在のグループが * またはこちらの UA に該当するか
        bool sawAny = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            var key = line[..idx].Trim().ToLowerInvariant();
            var val = line[(idx + 1)..].Trim();

            switch (key)
            {
                case "user-agent":
                    // 連続する user-agent はグループ宣言。* を採用。
                    if (!sawAny) { applies = val == "*"; }
                    else { applies = val == "*"; sawAny = false; }
                    if (val == "*") applies = true;
                    break;
                case "disallow":
                    if (applies) { sawAny = true; if (val.Length > 0) rules._rules.Add((val, false)); }
                    break;
                case "allow":
                    if (applies) { sawAny = true; if (val.Length > 0) rules._rules.Add((val, true)); }
                    break;
                case "crawl-delay":
                    if (applies && double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var cd))
                        rules.CrawlDelay = cd;
                    break;
            }
        }
        return rules;
    }

    /// <summary>最長一致ルールで取得可否を判定する(robots 標準の慣習)。</summary>
    public bool IsAllowed(string path)
    {
        string? best = null;
        bool allow = true;
        foreach (var (p, a) in _rules)
        {
            if (path.StartsWith(p, StringComparison.Ordinal) && (best == null || p.Length > best.Length))
            {
                best = p;
                allow = a;
            }
        }
        return allow;
    }
}
