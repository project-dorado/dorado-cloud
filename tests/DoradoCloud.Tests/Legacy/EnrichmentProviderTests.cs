using System.Net;
using System.Text;
using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Unit tests for the optional enrichment providers (M12).</summary>
public sealed class EnrichmentProviderTests
{
    private static IOptions<EnrichmentOptions> Configured(string provider) => Options.Create(new EnrichmentOptions
    {
        FanartTv = new FanartTvOptions { ApiKey = provider == "fanart" ? "key" : string.Empty },
        TheAudioDb = new TheAudioDbOptions { ApiKey = provider == "audiodb" ? "key" : string.Empty },
        Discogs = new DiscogsOptions { Token = provider == "discogs" ? "token" : string.Empty }
    });

    private static StubHandler Json(string json) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });

    private static HttpClient Http(StubHandler handler, string baseAddress)
        => new(handler) { BaseAddress = new Uri(baseAddress) };

    [Fact]
    public async Task Fanart_returns_the_first_artist_image()
    {
        const string json = """
        { "artistbackground": [ { "url": "https://assets.fanart.tv/fanart/music/x/background.jpg" } ] }
        """;
        var client = new FanartTvClient(Http(Json(json), "https://webservice.fanart.tv/"), Configured("fanart"), new FakeDistributedCache());

        var url = await client.GetArtistImageAsync("abc", CancellationToken.None);

        Assert.Equal("https://assets.fanart.tv/fanart/music/x/background.jpg", url);
    }

    [Fact]
    public async Task Fanart_is_a_noop_without_a_key()
    {
        var client = new FanartTvClient(Http(Json("{}"), "https://webservice.fanart.tv/"), Configured("none"), new FakeDistributedCache());
        Assert.Null(await client.GetArtistImageAsync("abc", CancellationToken.None));
    }

    [Fact]
    public async Task AudioDb_returns_the_artist_thumb()
    {
        const string json = """
        { "artists": [ { "strArtistThumb": "https://r2.theaudiodb.com/images/media/artist/thumb/x.jpg" } ] }
        """;
        var client = new TheAudioDbClient(Http(Json(json), "https://theaudiodb.com/"), Configured("audiodb"), new FakeDistributedCache());

        var url = await client.GetArtistImageAsync("abc", CancellationToken.None);

        Assert.Equal("https://r2.theaudiodb.com/images/media/artist/thumb/x.jpg", url);
    }

    [Fact]
    public async Task Discogs_returns_a_search_result_image()
    {
        const string json = """
        { "results": [ { "cover_image": "", "thumb": "https://i.discogs.com/thumb.jpg" } ] }
        """;
        var client = new DiscogsClient(Http(Json(json), "https://api.discogs.com/"), Configured("discogs"), new FakeDistributedCache());

        var url = await client.SearchArtistImageAsync("Radiohead", CancellationToken.None);

        Assert.Equal("https://i.discogs.com/thumb.jpg", url);
    }

    [Fact]
    public async Task Discogs_is_a_noop_without_a_token()
    {
        var client = new DiscogsClient(Http(Json("{}"), "https://api.discogs.com/"), Configured("none"), new FakeDistributedCache());
        Assert.Null(await client.SearchArtistImageAsync("Radiohead", CancellationToken.None));
    }
}
