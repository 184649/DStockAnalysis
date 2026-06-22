using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DStockAnalysis.Models;

namespace DStockAnalysis.Services;

/// <summary>
/// ローカル保存(JSON)。%AppData%\DStockAnalysis 配下に保存する。
/// ・stocks.json   : 取り込んだ銘柄データ
/// ・userdata.json : メモ / バフェットチェック / 興味度(銘柄コード単位)
/// ・settings.json : 比較対象・スクリーニング条件・アプリ設定
/// </summary>
public class DataStorageService
{
    private readonly string _dir;
    private readonly string _stocksPath;
    private readonly string _userDataPath;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <param name="baseDir">保存先ディレクトリ。null の場合は %AppData%\DStockAnalysis。テスト時に差し替え可能。</param>
    public DataStorageService(string? baseDir = null)
    {
        _dir = baseDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DStockAnalysis");
        Directory.CreateDirectory(_dir);
        _stocksPath = Path.Combine(_dir, "stocks.json");
        _userDataPath = Path.Combine(_dir, "userdata.json");
        _settingsPath = Path.Combine(_dir, "settings.json");
    }

    public string DataDirectory => _dir;
    public bool HasSavedStocks => File.Exists(_stocksPath);

    // ===== 銘柄本体 =====
    public void SaveStocks(IEnumerable<Stock> stocks)
        => File.WriteAllText(_stocksPath, JsonSerializer.Serialize(stocks.ToList(), Options));

    public List<Stock> LoadStocks()
    {
        if (!File.Exists(_stocksPath)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<Stock>>(File.ReadAllText(_stocksPath), Options) ?? new();
        }
        catch { return new(); }
    }

    // ===== ユーザーデータ(メモ/チェック) =====
    public void SaveUserData(IEnumerable<Stock> stocks)
    {
        var list = stocks.Select(s => new StockUserData
        {
            Code = s.Code,
            Memo = s.Memo,
            BuffettCheck = s.BuffettCheck,
            UserInterest = s.UserInterest
        }).ToList();
        File.WriteAllText(_userDataPath, JsonSerializer.Serialize(list, Options));
    }

    public Dictionary<string, StockUserData> LoadUserData()
    {
        if (!File.Exists(_userDataPath)) return new();
        try
        {
            var list = JsonSerializer.Deserialize<List<StockUserData>>(File.ReadAllText(_userDataPath), Options) ?? new();
            return list.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.First());
        }
        catch { return new(); }
    }

    /// <summary>読み込んだ銘柄に、保存済みのメモ/チェックをマージする。</summary>
    public void ApplyUserData(IEnumerable<Stock> stocks)
    {
        var map = LoadUserData();
        foreach (var s in stocks)
        {
            if (map.TryGetValue(s.Code, out var u))
            {
                s.Memo = u.Memo ?? new StockMemo();
                s.BuffettCheck = u.BuffettCheck ?? new BuffettCheck();
                s.UserInterest = u.UserInterest;
            }
        }
    }

    // ===== 設定 =====
    public void SaveSettings(AppSettings settings)
        => File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, Options));

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return new();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), Options) ?? new();
        }
        catch { return new(); }
    }
}
