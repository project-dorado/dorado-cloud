using System.Net;
using System.Text;
using DoradoCloud.Legacy;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Legacy.Catalog;
using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Unit tests for the MusicBrainz → Atom mapping. Responses are canned JSON;
/// no network access and no Microsoft content.
/// </summary>
public sealed class LegacyMusicCatalogTests
{
    private static LegacyMusicCatalog Create(StubHandler handler)
    {
        var options = Options.Create(new CatalogOptions { MusicBrainzRateLimitMs = 0 });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/") };
        var mb = new MusicBrainzClient(http, options, new FakeDistributedCache(), new HostRateLimiter());
        return new LegacyMusicCatalog(mb, new LegacyIdMapper(), NullLogger<LegacyMusicCatalog>.Instance);
    }

    private static StubHandler Json(string json) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });

    [Fact]
    public async Task Album_search_emits_albums_with_images()
    {
        const string json = """
        {
          "release-groups": [
            { "id": "11111111-1111-1111-1111-111111111111", "title": "In Rainbows", "first-release-date": "2007-10-10",
              "artist-credit": [ { "name": "Radiohead" } ] }
          ]
        }
        """;

        var catalog = Create(Json(json));
        var feed = await catalog.AlbumSearchAsync("en-US", "in rainbows", CancellationToken.None);
        var xml = feed.ToXml();

        Assert.Contains("<a:entry>", xml);
        Assert.Contains("<a:title type=\"text\">In Rainbows</a:title>", xml);
        Assert.Contains("<a:id>urn:uuid:11111111-1111-1111-1111-111111111111</a:id>", xml);
        Assert.Contains("<image><id>urn:uuid:11111111-1111-1111-1111-111111111111</id></image>", xml);
        Assert.Contains("<name>Radiohead</name>", xml);
        Assert.Contains("href=\"/v3.2/en-US/music/album/11111111-1111-1111-1111-111111111111\"", xml);
    }

    [Fact]
    public async Task Album_detail_maps_tracks_and_duration()
    {
        const string json = """
        {
          "id": "22222222-2222-2222-2222-222222222222", "title": "In Rainbows", "date": "2007-10-10",
          "artist-credit": [ { "artist": { "id": "33333333-3333-3333-3333-333333333333", "name": "Radiohead" } } ],
          "media": [ { "tracks": [
            { "position": "1", "length": 253000,
              "recording": { "id": "44444444-4444-4444-4444-444444444444", "title": "15 Step",
                "artist-credit": [ { "artist": { "id": "33333333-3333-3333-3333-333333333333", "name": "Radiohead" } } ] } },
            { "position": "2", "length": 260000, "recording": { "id": "55555555-5555-5555-5555-555555555555", "title": "Bodysnatchers" } }
          ] } ]
        }
        """;

        var catalog = Create(Json(json));
        var feed = await catalog.AlbumDetailAsync("en-US", "22222222-2222-2222-2222-222222222222", CancellationToken.None);

        Assert.NotNull(feed);
        var xml = feed!.ToXml();
        Assert.Contains("<a:title type=\"text\">In Rainbows</a:title>", xml);
        Assert.Contains("<image><id>urn:uuid:22222222-2222-2222-2222-222222222222</id></image>", xml);
        Assert.Contains("<a:id>urn:uuid:44444444-4444-4444-4444-444444444444</a:id>", xml);
        Assert.Contains("<duration>4:13</duration>", xml);
        Assert.Contains("<trackNumber>2</trackNumber>", xml);
        Assert.Contains("<a:id>urn:uuid:55555555-5555-5555-5555-555555555555</a:id>", xml);
    }

    [Fact]
    public async Task Genres_feed_lists_slug_ids()
    {
        const string json = """{ "genres": [ { "name": "rock" }, { "name": "hip hop" } ] }""";

        var catalog = Create(Json(json));
        var feed = await catalog.GenresAsync("en-US", CancellationToken.None);
        var xml = feed.ToXml();

        Assert.Contains("<a:id>rock</a:id>", xml);
        Assert.Contains("<a:id>hip-hop</a:id>", xml);
        Assert.Contains("href=\"/v3.2/en-US/music/genre/hip-hop\"", xml);
    }

    [Fact]
    public async Task Similar_tracks_excludes_the_seed()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            var json = url.Contains("artist=", StringComparison.Ordinal)
                ? """
                  { "recordings": [
                    { "id": "rec-1", "title": "Seed", "artist-credit": [ { "name": "Radiohead" } ] },
                    { "id": "rec-2", "title": "Other", "artist-credit": [ { "name": "Radiohead" } ] }
                  ] }
                  """
                : """
                  { "id": "rec-1", "title": "Seed",
                    "artist-credit": [ { "artist": { "id": "artist-1", "name": "Radiohead" } } ],
                    "releases": [ { "id": "rel-1", "title": "In Rainbows" } ] }
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var catalog = Create(handler);
        var feed = await catalog.SimilarTracksAsync("en-US", "rec-1", CancellationToken.None);
        var xml = feed.ToXml();

        Assert.DoesNotContain("Seed", xml);
        Assert.Contains("Other", xml);
    }

    [Theory]
    [InlineData("rock", "rock")]
    [InlineData("Hip Hop", "hip-hop")]
    [InlineData("Drum & Bass", "drum---bass")]
    public void Genre_slug_is_stable(string name, string expected)
        => Assert.Equal(expected, LegacyMusicCatalog.GenreId(name));
}
