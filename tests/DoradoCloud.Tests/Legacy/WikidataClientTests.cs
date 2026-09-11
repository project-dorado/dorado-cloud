using System.Net;
using System.Text;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Legacy.Catalog;
using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Unit tests for the keyless artist-image path: MusicBrainz -> Wikidata P18 ->
/// Wikimedia Commons. Responses are canned JSON; no network.
/// </summary>
public sealed class WikidataClientTests
{
    private static WikidataClient CreateWikidata(StubHandler handler)
    {
        var options = Options.Create(new CatalogOptions());
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.wikidata.org/") };
        return new WikidataClient(http, new FakeDistributedCache(), options);
    }

    [Fact]
    public async Task Resolves_p18_to_a_commons_file_path_url()
    {
        const string json = """
        { "claims": { "P18": [ { "mainsnak": { "datavalue": { "value": "File:Radiohead.jpg" } } } ] } }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var url = await CreateWikidata(handler).GetCommonsImageUrlAsync("Q44190", 500, CancellationToken.None);

        Assert.NotNull(url);
        Assert.Contains("commons.wikimedia.org/wiki/Special:FilePath/", url);
        Assert.Contains("width=500", url);
    }

    [Fact]
    public async Task Missing_p18_returns_null()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "claims": {} }""", Encoding.UTF8, "application/json")
        });

        Assert.Null(await CreateWikidata(handler).GetCommonsImageUrlAsync("Q1", 500, CancellationToken.None));
    }

    [Fact]
    public async Task MusicBrainz_resolves_the_wikidata_relation()
    {
        const string json = """
        { "id": "a", "relations": [ { "type": "wikidata", "url": { "resource": "https://www.wikidata.org/wiki/Q44190" } } ] }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var options = Options.Create(new CatalogOptions { MusicBrainzRateLimitMs = 0 });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/") };
        var brainz = new MusicBrainzClient(http, options, new FakeDistributedCache(), new HostRateLimiter());

        Assert.Equal("Q44190", await brainz.GetArtistWikidataIdAsync("a", CancellationToken.None));
    }
}
