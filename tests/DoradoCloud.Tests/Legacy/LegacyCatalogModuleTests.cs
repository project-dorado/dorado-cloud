using System.Net;
using System.Net.Http.Headers;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Legacy.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DoradoCloud.Tests.Legacy;

public sealed class LegacyCatalogModuleTests : IClassFixture<LegacyCatalogModuleTests.LegacyCatalogFactory>
{
    private const string CatalogHost = "catalog.zune.net";
    private const string ImageHost = "image.catalog.zune.net";

    public sealed class LegacyCatalogFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILegacyMusicCatalog>();
                services.RemoveAll<ILegacyArtwork>();
                services.AddSingleton<ILegacyMusicCatalog, FakeMusicCatalog>();
                services.AddSingleton<ILegacyArtwork, FakeArtwork>();
            });
            base.ConfigureWebHost(builder);
        }
    }

    private sealed class FakeMusicCatalog : ILegacyMusicCatalog
    {
        private static XElement Feed(string title, string id)
            => new AtomFeedBuilder(title, id, null, LegacyConstants.MusicCatalogNamespace).Build();

        public Task<XElement> HubsAsync(string locale, CancellationToken ct) => Task.FromResult(Feed("Music", "music"));
        public Task<XElement> GenresAsync(string locale, CancellationToken ct) => Task.FromResult(Feed("Genres", "genre"));
        public Task<XElement> GenreAsync(string locale, string genreId, CancellationToken ct) => Task.FromResult(Feed(genreId, genreId));
        public Task<XElement> GenreAlbumsAsync(string locale, string genreId, CancellationToken ct) => Task.FromResult(Feed("Albums", "albums"));
        public Task<XElement> AlbumSearchAsync(string locale, string query, CancellationToken ct) => Task.FromResult(Feed("Albums", "albums"));
        public Task<XElement?> AlbumDetailAsync(string locale, string albumId, CancellationToken ct)
            => Task.FromResult<XElement?>(albumId == "missing" ? null : Feed("Album", albumId));
        public Task<XElement?> ArtistAsync(string locale, string artistId, CancellationToken ct)
            => Task.FromResult<XElement?>(artistId == "missing" ? null : Feed("Artist", artistId));
        public Task<XElement> ArtistAlbumsAsync(string locale, string artistId, CancellationToken ct) => Task.FromResult(Feed("Albums", "albums"));
        public Task<XElement> ArtistTracksAsync(string locale, string artistId, CancellationToken ct) => Task.FromResult(Feed("Tracks", "tracks"));
        public Task<XElement> TrackSearchAsync(string locale, string query, CancellationToken ct) => Task.FromResult(Feed("Tracks", "tracks"));
        public Task<XElement?> TrackDetailAsync(string locale, string trackId, CancellationToken ct)
            => Task.FromResult<XElement?>(trackId == "missing" ? null : Feed("Track", trackId));
        public Task<XElement> ChartTracksAsync(string locale, CancellationToken ct) => Task.FromResult(Feed("Tracks", "tracks"));
        public Task<XElement> SimilarTracksAsync(string locale, string trackId, CancellationToken ct) => Task.FromResult(Feed("Similar", "similar"));
    }

    private sealed class FakeArtwork : ILegacyArtwork
    {
        public Task<ArtworkResult?> GetCoverAsync(string id, int width, CancellationToken ct)
            => Task.FromResult<ArtworkResult?>(id == "rg-1" ? new ArtworkResult(new byte[] { 1, 2, 3, 4 }, "image/jpeg") : null);

        public Task<ArtworkResult?> GetArtistImageAsync(string id, int width, CancellationToken ct)
            => Task.FromResult<ArtworkResult?>(id == "artist-1" ? new ArtworkResult(new byte[] { 5, 6, 7 }, "image/jpeg") : null);
    }

    private readonly LegacyCatalogFactory _factory;

    public LegacyCatalogModuleTests(LegacyCatalogFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return request;
    }

    [Fact]
    public async Task Hubs_returns_an_atom_feed_on_the_catalog_host()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(CatalogHost, "/v3.2/en-US/hubs/music");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Catalog_is_not_reachable_on_another_host()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("localhost", "/v3.2/en-US/hubs/music");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Album_search_requires_a_query()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(CatalogHost, "/v3.2/en-US/music/album");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_album_detail_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(CatalogHost, "/v3.2/en-US/music/album/missing");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Movie_hub_returns_a_valid_empty_feed()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(CatalogHost, "/v3.2/en-US/music/hub/movie");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Similar_tracks_route_is_available()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(CatalogHost, "/v4.0/en-US/track/rec-1/similarTracks");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Image_returns_cover_bytes_on_the_image_host()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(ImageHost, "/v3.2/en-US/image/rg-1");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Image_missing_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(ImageHost, "/v3.2/en-US/image/unknown");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Artist_image_is_served_when_available()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(ImageHost, "/v3.2/en-US/music/artist/artist-1/primaryImage");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(new byte[] { 5, 6, 7 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Unknown_artist_image_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(ImageHost, "/v3.2/en-US/music/artist/unknown-artist/primaryImage");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
