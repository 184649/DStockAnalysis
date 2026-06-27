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
    private readonly YahooFinanceClient _yahoo;
    private readonly ILogger<IndicatorFetchService> _log;
    private readonly Dictionary<string, RobotsRules> _robots = new();
    private readonly SemaphoreSlim _robotsLock = new(1, 1);

    private const string UA =
        "Mozilla/5.0 (compatible; DStockAnalysis-personal/1.0; +weekly local research)";

    public IndicatorFetchService(IHttpClientFactory factory, YahooFinanceClient yahoo, ILogger<IndicatorFetchService> log)
    {
        _http = factory.CreateClient("scraper");
        _yahoo = yahoo;
        _log = log;
    }

    /// <summary>1銘柄分の指標を取得して列名→文字列値の辞書で返す(取得できた列のみ)。</summary>
    public async Task<Dictionary<string, string>> FetchAsync(string code, bool useKabutan, CancellationToken ct)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1) 株探(会社予想ベース) を最優先: 現在値/PER/PBR/配当利回り/時価総額。
        //    日本の投資家が実際に見る「会社予想」値に揃える(Yahoo の trailing 値とは異なる)。
        await MergeFrom(row, code, $"https://kabutan.jp/stock/?code={code}", ParseKabutan, ct);

        // 2) 株探 業績・財務ページ(通期実績): 利益率/ROE/ROA/総資産回転率/各CF/自己資本比率/
        //    有利子負債比率/各成長率。日本基準の通期値を Yahoo の TTM より優先するため先に取得する。
        await MergeFrom(row, code, $"https://kabutan.jp/stock/finance?code={code}", ParseKabutanFinance, ct);

        // 3) Yahoo!ファイナンス(構造化JSON): 株探で取れなかった指標の補完 + 現在値。
        foreach (var kv in await _yahoo.FetchAsync(code, ct))
            if (!row.ContainsKey(kv.Key)) row[kv.Key] = kv.Value;

        // 4) IR BANK: 自己資本比率(株探・Yahoo で取れない場合のフォールバック)。
        await MergeFrom(row, code, $"https://irbank.net/{code}", ParseIrBank, ct);

        Derive(row); // 株探の株価/PER/PBR/利回りから EPS・BPS・1株配当・配当性向・MIX係数を算出
        return row;
    }

    private async Task MergeFrom(Dictionary<string, string> row, string code, string url,
        Func<string, string, Dictionary<string, string>> parse, CancellationToken ct)
    {
        var html = await FetchAsync(url, ct);
        if (html == null) return;
        foreach (var kv in parse(html, code))
            if (!row.ContainsKey(kv.Key)) row[kv.Key] = kv.Value;
    }

    /// <summary>取得済みの実値から、サイトに直接出ない派生指標を「不足分のみ」補う。
    /// 既に取得できている値(例: Yahoo の配当性向)は上書きしない。</summary>
    public static void Derive(Dictionary<string, string> row)
    {
        double? G(string k) => row.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        // 既存キーは尊重し、無い場合のみ補完する
        void P(string k, double v, int dec = 2) { if (!row.ContainsKey(k)) row[k] = Math.Round(v, dec).ToString(CultureInfo.InvariantCulture); }

        var per = G("PER"); var pbr = G("PBR"); var price = G("Price");
        var dy = G("DividendYield");

        // EPS(予想) = 株価 / 予想PER(株探の会社予想PERに整合)
        if (price is > 0 && per is > 0) P("EPS", price.Value / per.Value, 1);
        var eps = G("EPS");

        if (per is > 0 && pbr is > 0) P("MixFactor", per.Value * pbr.Value, 1);
        if (price is > 0 && pbr is > 0) P("BPS", price.Value / pbr.Value, 0);

        // 1株配当 = 株価 × 配当利回り(%) / 100
        if (price is > 0 && dy is > 0)
        {
            double dividend = price.Value * dy.Value / 100.0;
            P("Dividend", dividend);
            // 配当性向(%) = 1株配当 / EPS × 100
            if (eps is > 0) P("PayoutRatio", dividend / eps.Value * 100.0);
        }
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

    public static Dictionary<string, string> ParseIrBank(string html, string code)
    {
        var d = new Dictionary<string, string>();
        // 自己資本比率(株主資本比率)はテキストではなくバーの幅(style="width:NN%")で表現される。
        // 例: ...title="… 自己資本比率" …>株主資本比率(連)</a></dt><dd><span class="ratio" style="width:37.83%;">
        // 株探に無い指標のため IR BANK から取得する(他指標は会社予想ベースで揃えるため取らない)。
        Put(d, "EquityRatio", Find(html, @"自己資本比率[\s\S]{0,200}?width:\s*([0-9]+\.?[0-9]*)\s*%"));
        return d;
    }

    public static Dictionary<string, string> ParseKabutan(string html, string code)
    {
        var d = new Dictionary<string, string>();
        // 注: 株価(現在値)は株探のテキスト位置が銘柄により不安定(日付"06"を誤取得する等)なため取得しない。
        //     株価は Yahoo! の構造化データ(regularMarketPrice)を使う。株探は会社予想の指標のみ担当。

        // 株価指標テーブル(会社予想ベース): PER / PBR / 利回り の3セル。"－" は欠損として扱う。
        // 例: ...利回り</th><th>信用倍率</th></tr></thead><tbody><tr>
        //     <td>10.6<span class="fs9">倍</span></td><td>0.80…</td><td>3.72…</td>...
        var m = Regex.Match(html, @"利回り</th>[\s\S]*?<tbody>[\s\S]*?<td>([^<]*)<[\s\S]*?</td>\s*<td>([^<]*)<[\s\S]*?</td>\s*<td>([^<]*)<");
        if (m.Success)
        {
            Put(d, "PER", CleanNum(m.Groups[1].Value));
            Put(d, "PBR", CleanNum(m.Groups[2].Value));
            Put(d, "DividendYield", CleanNum(m.Groups[3].Value));
        }

        // 時価総額: 「N兆M億円」または「M億円」→ 百万円
        var mm = Regex.Match(html, @"時価総額</th>\s*<td[^>]*>([\d,]+)<span>兆</span>(?:\s*([\d,]+)<span>億)?");
        if (mm.Success)
        {
            double mil = (Num(mm.Groups[1].Value) ?? 0) * 1_000_000
                       + (mm.Groups[2].Success ? (Num(mm.Groups[2].Value) ?? 0) * 100 : 0);
            if (mil > 0) d["MarketCap"] = Math.Round(mil).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            var mo = Regex.Match(html, @"時価総額</th>\s*<td[^>]*>([\d,]+)<span>億");
            if (mo.Success && Num(mo.Groups[1].Value) is { } v)
                d["MarketCap"] = Math.Round(v * 100).ToString(CultureInfo.InvariantCulture);
        }
        return d;
    }

    /// <summary>
    /// 株探 業績・財務ページ(/stock/finance)の各推移表から通期実績の指標を抽出する。
    /// 4つの表(通期業績 / 収益性 / キャッシュフロー / 財務)を id アンカーで特定して解析する。
    ///  - 収益性表: 売上営業利益率(=営業利益率)/ROE/ROA/総資産回転率
    ///  - 通期業績表: 売上高・営業益・経常益・最終益・1株益 → 経常/純利益率・前年比成長率・3年CAGR
    ///  - CF表: フリーCF/営業CF/投資CF/財務CF(百万円)
    ///  - 財務表: 自己資本比率 / 有利子負債倍率(→比率%)
    /// 取得できた列だけ返す(プレミアム限定で隠れている列は欠損のままにする)。
    /// </summary>
    public static Dictionary<string, string> ParseKabutanFinance(string html, string code)
    {
        var d = new Dictionary<string, string>();

        // 収益性推移: [売上高, 営業益, 売上営業利益率, ROE, ROA, 総資産回転率, 修正1株益]
        var prof = TableByAnchor(html, "oc_b1_year_profit");
        if (prof != null)
        {
            var r = ExtractFinanceRows(prof).Where(x => !x.forecast)
                .LastOrDefault(x => x.cells.Count >= 6 && x.cells[3].HasValue);
            if (r.cells != null)
            {
                Put(d, "OperatingMargin", Str(r.cells[2]));
                Put(d, "ROE", Str(r.cells[3]));
                Put(d, "ROA", Str(r.cells[4]));
                Put(d, "TotalAssetTurnover", Str(r.cells[5]));
            }
        }

        // 通期業績推移: [売上高, 営業益, 経常益, 最終益, 修正1株益, 修正1株配]
        double? latestRev = null, latestOcf = null;
        var res = TableByAnchor(html, "oc_b1_year_result");
        if (res != null)
        {
            var act = ExtractFinanceRows(res).Where(x => x.cells.Count >= 4 && !x.forecast).ToList();
            if (act.Count > 0)
            {
                var last = act[^1].cells;
                double? rev = last[0], op = last[1], ord = last[2], net = last[3];
                double? eps = last.Count > 4 ? last[4] : null;
                latestRev = rev;
                if (rev is > 0)
                {
                    if (ord.HasValue) Put(d, "OrdinaryProfitMargin", Round1(ord.Value / rev.Value * 100));
                    if (net.HasValue) Put(d, "NetProfitMargin", Round1(net.Value / rev.Value * 100));
                    if (op.HasValue && !d.ContainsKey("OperatingMargin")) Put(d, "OperatingMargin", Round1(op.Value / rev.Value * 100));
                }
                if (act.Count >= 2)
                {
                    var prev = act[^2].cells;
                    PutGrowth(d, "RevenueGrowthRate", prev[0], rev);
                    PutGrowth(d, "RevenueGrowth1Y", prev[0], rev);
                    PutGrowth(d, "OperatingProfitGrowthRate", prev[1], op);
                    PutGrowth(d, "OrdinaryProfitGrowthRate", prev[2], ord);
                    PutGrowth(d, "NetProfitGrowthRate", prev[3], net);
                    if (eps.HasValue && prev.Count > 4) PutGrowth(d, "EpsGrowthRate", prev[4], eps);
                }
                if (act.Count >= 4 && act[^4].cells[0] is > 0 && rev is > 0)
                    Put(d, "RevenueGrowth3Y", Round1((Math.Pow(rev.Value / act[^4].cells[0]!.Value, 1.0 / 3) - 1) * 100));
            }
        }

        // キャッシュフロー推移: [営業益, フリーCF, 営業CF, 投資CF, 財務CF, 現金等残高, 現金比率]
        var cf = TableByAnchor(html, "oc_b1_cf");
        if (cf != null)
        {
            var rows = ExtractFinanceRows(cf).Where(x => x.cells.Count >= 5).ToList();
            var act = rows.Where(x => !x.forecast).ToList();
            var pick = act.Count > 0 ? act[^1].cells : (rows.Count > 0 ? rows[^1].cells : null);
            if (pick != null)
            {
                Put(d, "FreeCashFlow", Str(pick[1]));
                Put(d, "OperatingCF", Str(pick[2]));
                Put(d, "InvestingCF", Str(pick[3]));
                Put(d, "FinancingCF", Str(pick[4]));
                latestOcf = pick[2];
            }
        }

        // 財務推移: [1株純資産, 自己資本比率, 総資産, 自己資本, 剰余金, 有利子負債倍率]
        var bs = TableByAnchor(html, "oc_b1_bs");
        if (bs != null)
        {
            var rows = ExtractFinanceRows(bs).Where(x => x.cells.Count >= 6).ToList();
            if (rows.Count > 0)
            {
                var last = rows[^1].cells;
                Put(d, "EquityRatio", Str(last[1]));
                if (last[5] is { } debtMult) Put(d, "InterestBearingDebtRatio", Round1(debtMult * 100)); // 倍率→%
            }
        }

        // 営業CFマージン = 営業CF / 売上高 × 100(いずれも百万円・通期)
        if (latestOcf is > 0 && latestRev is > 0 && !d.ContainsKey("OperatingCashFlowMargin"))
            Put(d, "OperatingCashFlowMargin", Round1(latestOcf.Value / latestRev.Value * 100));

        return d;
    }

    /// <summary>id アンカー(oc_b1_*)を含む &lt;table&gt;…&lt;/table&gt; を切り出す。</summary>
    private static string? TableByAnchor(string html, string anchorId)
    {
        var i = html.IndexOf(anchorId, StringComparison.Ordinal);
        if (i < 0) return null;
        var ts = html.LastIndexOf("<table", i, StringComparison.Ordinal);
        var te = html.IndexOf("</table>", i, StringComparison.Ordinal);
        if (ts < 0 || te <= ts) return null;
        return html.Substring(ts, te - ts + 8);
    }

    /// <summary>推移表の tbody から、期(YYYY.MM)を持つデータ行を (各セル数値, 予想か, 期) で抽出する。
    /// colspan を含む行(開閉ボタン・プレミアム告知)は除外する。</summary>
    private static List<(List<double?> cells, bool forecast, string period)> ExtractFinanceRows(string tableHtml)
    {
        var rows = new List<(List<double?>, bool, string)>();
        var tb = Regex.Match(tableHtml, @"<tbody>([\s\S]*?)</tbody>");
        var body = tb.Success ? tb.Groups[1].Value : tableHtml;
        foreach (Match tr in Regex.Matches(body, @"<tr[^>]*>([\s\S]*?)</tr>"))
        {
            var inner = tr.Groups[1].Value;
            if (inner.Contains("colspan")) continue;
            var thm = Regex.Match(inner, @"<th[^>]*>([\s\S]*?)</th>");
            if (!thm.Success) continue;
            var thText = Strip(thm.Groups[1].Value);
            var pm = Regex.Match(thText, @"\d{4}\.\d{2}");
            if (!pm.Success) continue;
            bool forecast = thText.Contains('予');
            var cells = new List<double?>();
            foreach (Match td in Regex.Matches(inner, @"<td[^>]*>([\s\S]*?)</td>"))
                cells.Add(NumOrNull(Strip(td.Groups[1].Value)));
            rows.Add((cells, forecast, pm.Value));
        }
        return rows;
    }

    private static void PutGrowth(Dictionary<string, string> d, string key, double? prev, double? cur)
    {
        if (prev is > 0 && cur.HasValue) Put(d, key, Round1((cur.Value - prev.Value) / prev.Value * 100));
    }

    private static string? Str(double? v) => v?.ToString(CultureInfo.InvariantCulture);
    private static string Round1(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

    private static double? NumOrNull(string s)
    {
        s = s.Trim();
        if (s.Length == 0 || s is "－" or "-" or "--" or "—" or "…") return null;
        return Num(s);
    }

    /// <summary>"－"/"-"/空 を欠損(null)とし、それ以外は数値文字列に正規化する。</summary>
    private static string? CleanNum(string s)
    {
        s = s.Trim();
        if (s.Length == 0 || s is "－" or "-" or "--" or "—") return null;
        return Num(s)?.ToString(CultureInfo.InvariantCulture);
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
