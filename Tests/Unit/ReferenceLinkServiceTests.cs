using DStockAnalysis.Services;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class ReferenceLinkServiceTests
{
    private readonly ReferenceLinkService _svc = new();

    [Fact] // UT-RL-01: 6つの外部データソースリンクを生成
    public void BuildLinks_ReturnsAllSources()
    {
        var links = _svc.BuildLinks("7203");
        var names = links.ConvertAll(l => l.Name);
        Assert.Contains("IR BANK", names);
        Assert.Contains("バフェット・コード", names);
        Assert.Contains("株探", names);
        Assert.Contains("株マップ", names);
        Assert.Contains("みんかぶ", names);
        Assert.Contains("株主優待ガイド", names);
        Assert.All(links, l => Assert.False(string.IsNullOrWhiteSpace(l.Url)));
    }

    [Fact] // UT-RL-02: 銘柄コードが各URLに埋め込まれる
    public void BuildLinks_EmbedsCode()
    {
        var links = _svc.BuildLinks("9433");
        Assert.All(links, l => Assert.Contains("9433", l.Url));
        Assert.Contains(links, l => l.Url == "https://irbank.net/9433");
        Assert.Contains(links, l => l.Url == "https://kabutan.jp/stock/?code=9433");
    }

    [Fact] // UT-RL-03: 英字付きコードにも対応
    public void BuildLinks_HandlesAlphanumericCode()
    {
        var links = _svc.BuildLinks("285A");
        Assert.All(links, l => Assert.Contains("285A", l.Url));
    }
}
