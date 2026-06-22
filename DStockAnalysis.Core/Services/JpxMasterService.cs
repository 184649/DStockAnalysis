using System.Globalization;
using System.IO;
using System.Net.Http;
using DStockAnalysis.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace DStockAnalysis.Services;

/// <summary>
/// 日本取引所グループ(JPX)が公開する「東証上場銘柄一覧」(data_j.xls)を読み込み、
/// 全上場銘柄(内国株式: プライム/スタンダード/グロース)を取得する。
/// 一覧は毎月更新されるため、UpdateAsync で最新版をダウンロードして差し替えられる。
///
/// 一覧にはコード/銘柄名/市場/業種/規模のみ含まれ、財務指標は含まれない。
/// 指標値は IndicatorSeedService で擬似生成し、CSV 取込で実データに上書きする。
/// </summary>
public class JpxMasterService
{
    /// <summary>JPX 公開の上場銘柄一覧 Excel。</summary>
    public const string DownloadUrl =
        "https://www.jpx.co.jp/markets/statistics-equities/misc/tvdivq0000001vg2-att/data_j.xls";

    private readonly string _cacheFile;   // 更新版(AppData)
    private readonly string _bundledFile; // 同梱版(出力フォルダ)

    public JpxMasterService(string? cacheDir = null)
    {
        cacheDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DStockAnalysis");
        Directory.CreateDirectory(cacheDir);
        _cacheFile = Path.Combine(cacheDir, "data_j.xls");
        _bundledFile = Path.Combine(AppContext.BaseDirectory, "Data", "data_j.xls");
    }

    /// <summary>有効なマスタファイルが利用可能か。</summary>
    public bool IsAvailable => File.Exists(_cacheFile) || File.Exists(_bundledFile);

    private string? ActiveFile =>
        File.Exists(_cacheFile) ? _cacheFile : (File.Exists(_bundledFile) ? _bundledFile : null);

    /// <summary>全銘柄を読み込み、擬似指標を付与して返す。</summary>
    public (List<Stock> stocks, DateTime? masterDate) LoadAll(IndicatorSeedService seed, ScoreService scorer)
    {
        var path = ActiveFile;
        if (path == null) return (new List<Stock>(), null);

        var (stocks, date) = ParseXls(path);
        foreach (var s in stocks)
        {
            if (date.HasValue) s.DataUpdated = date.Value;
            seed.FillIndicators(s);
            scorer.Recalculate(s);
        }
        return (stocks, date);
    }

    /// <summary>最新の一覧を JPX からダウンロードしてキャッシュに保存する。</summary>
    public async Task<bool> UpdateAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Add("User-Agent", "DStockAnalysis/1.0");
        var bytes = await http.GetByteArrayAsync(DownloadUrl);
        if (bytes.Length < 10000) return false; // 取得失敗(HTML等)を簡易判定
        await File.WriteAllBytesAsync(_cacheFile, bytes);
        return true;
    }

    /// <summary>data_j.xls をパースして銘柄(内国株式)とデータ日付を返す。</summary>
    private (List<Stock> stocks, DateTime? date) ParseXls(string path)
    {
        var result = new List<Stock>();
        DateTime? date = null;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        IWorkbook wb = new HSSFWorkbook(fs);
        ISheet sheet = wb.GetSheetAt(0);

        // 列: 0=日付 1=コード 2=銘柄名 3=市場・商品区分 5=33業種区分 9=規模区分
        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null) continue;

            if (date == null) date = ParseYmd(CellString(row.GetCell(0)));

            var code = CellString(row.GetCell(1)).Trim();
            var name = CellString(row.GetCell(2)).Trim();
            var rawMarket = CellString(row.GetCell(3)).Trim();
            var sector = CellString(row.GetCell(5)).Trim();
            var rawScale = CellString(row.GetCell(9)).Trim();

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name)) continue;

            var market = MapMarket(rawMarket);
            if (market == null) continue; // ETF/REIT/PRO/出資証券などは対象外

            result.Add(new Stock
            {
                Code = code,
                Name = name,
                Market = market,
                Sector = string.IsNullOrEmpty(sector) || sector == "-" ? "その他" : sector,
                Scale = MapScale(rawScale),
                DataUpdated = date ?? DateTime.Today
            });
        }
        return (result, date);
    }

    private static string? MapMarket(string raw)
    {
        if (raw.Contains("プライム")) return "東証プライム";
        if (raw.Contains("スタンダード")) return "東証スタンダード";
        if (raw.Contains("グロース")) return "東証グロース";
        return null;
    }

    private static string MapScale(string raw)
    {
        if (raw.Contains("Core30") || raw.Contains("Large70")) return "大型";
        if (raw.Contains("Mid400")) return "中型";
        return "小型";
    }

    private static DateTime? ParseYmd(string ymd)
    {
        ymd = ymd.Replace(".0", "").Trim();
        if (DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }

    private static string CellString(ICell? cell)
    {
        if (cell == null) return "";
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue ?? "",
            CellType.Numeric => FormatNumeric(cell.NumericCellValue),
            CellType.Formula => cell.CachedFormulaResultType == CellType.String
                ? cell.StringCellValue ?? ""
                : FormatNumeric(cell.NumericCellValue),
            _ => cell.ToString() ?? ""
        };
    }

    private static string FormatNumeric(double v)
        => v == Math.Floor(v) ? ((long)v).ToString(CultureInfo.InvariantCulture)
                              : v.ToString(CultureInfo.InvariantCulture);
}
