namespace DStockAnalysis.Services;

/// <summary>外部データソースへのリンク(名称とURL)。</summary>
public class ReferenceLink
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>
/// 銘柄コードから、最新の実データを確認できる外部サイトへのディープリンクを生成する。
/// IR BANK / バフェット・コード / 株探 / 株マップ / みんかぶ / 株主優待ガイド に対応。
/// (これらサイトの内容を自動収集はせず、ユーザーが最新の実数値を確認・取得するための導線)
/// </summary>
public class ReferenceLinkService
{
    public List<ReferenceLink> BuildLinks(string code)
    {
        code = (code ?? "").Trim();
        return new List<ReferenceLink>
        {
            new() { Name = "IR BANK",        Url = $"https://irbank.net/{code}" },
            new() { Name = "バフェット・コード", Url = $"https://www.buffett-code.com/company/{code}/" },
            new() { Name = "株探",            Url = $"https://kabutan.jp/stock/?code={code}" },
            new() { Name = "株マップ",         Url = $"https://jp.kabumap.com/servlets/kabumap/Action?SRC=basic/base/main&codetext={code}" },
            new() { Name = "みんかぶ",         Url = $"https://minkabu.jp/stock/{code}" },
            new() { Name = "株主優待ガイド",    Url = $"https://minkabu.jp/stock/{code}/yutai" },
        };
    }
}
