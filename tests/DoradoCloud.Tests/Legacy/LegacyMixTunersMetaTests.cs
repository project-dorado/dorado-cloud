using System.Net;
using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Legacy.Catalog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DoradoCloud.Tests.Legacy;

public sealed class LegacyMixModuleTests : IClassFixture<LegacyMixModuleTests.MixFactory>
{
    public sealed class MixFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILegacyMusicCatalog>();
                services.AddSingleton<ILegacyMusicCatalog, StubMusicCatalog>();
            });
            base.ConfigureWebHost(builder);
        }
    }

    private sealed class StubMusicCatalog : ILegacyMusicCatalog
    {
        private static XElement Feed()
            => new AtomFeedBuilder("Similar tracks", "similar", null, LegacyConstants.MusicCatalogNamespace).Build();

        public Task<XElement> HubsAsync(string l, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> GenresAsync(string l, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> GenreAsync(string l, string g, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> GenreAlbumsAsync(string l, string g, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> AlbumSearchAsync(string l, string q, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement?> AlbumDetailAsync(string l, string a, CancellationToken c) => Task.FromResult<XElement?>(Feed());
        public Task<XElement?> ArtistAsync(string l, string a, CancellationToken c) => Task.FromResult<XElement?>(Feed());
        public Task<XElement> ArtistAlbumsAsync(string l, string a, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> ArtistTracksAsync(string l, string a, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> TrackSearchAsync(string l, string q, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement?> TrackDetailAsync(string l, string t, CancellationToken c) => Task.FromResult<XElement?>(Feed());
        public Task<XElement> ChartTracksAsync(string l, CancellationToken c) => Task.FromResult(Feed());
        public Task<XElement> SimilarTracksAsync(string l, string t, CancellationToken c) => Task.FromResult(Feed());
    }

    private readonly MixFactory _factory;

    public LegacyMixModuleTests(MixFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(string host, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = host;
        return request;
    }

    [Fact]
    public async Task Similar_tracks_answers_on_the_mix_host()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("mix.zune.net", "/v4.0/en-US/track/44444444-4444-4444-4444-444444444444/similarTracks");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mix_is_not_reachable_on_another_host()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("localhost", "/v4.0/en-US/track/x/similarTracks");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class LegacyTunersModuleTests : IClassFixture<LegacyTunersModuleTests.TunersFactory>
{
    public sealed class TunersFactory : CloudApiFactory
    {
        public static readonly byte[] FileBytes = { 4, 8, 15, 16, 23, 42 };

        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "dorado-tuners-" + Guid.NewGuid().ToString("N"));

        public TunersFactory()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllBytes(Path.Combine(Root, "ZunePCClient.exe"), FileBytes);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Tuners:CorpusRoot", Root);
            base.ConfigureWebHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private readonly TunersFactory _factory;

    public LegacyTunersModuleTests(TunersFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "tuners.zune.net";
        return request;
    }

    [Fact]
    public async Task Client_resource_is_served_from_the_corpus()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("/en-US/ZunePCClient/4.8/ZunePCClient.exe");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(TunersFactory.FileBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Missing_client_resource_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("/en-US/ZunePCClient/4.8/Nope.exe");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resource_is_404_without_a_corpus()
    {
        using var factory = new CloudApiFactory();
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/en-US/ZunePCClient/4.8/ZunePCClient.exe");
        request.Headers.Host = "tuners.zune.net";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class LegacyMetaServicesModuleTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Endpoints_discovery_lists_dorado_hosts()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ZuneAPI/EndPoints.aspx");
        request.Headers.Host = "fai.music.metaservices.microsoft.com";
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("catalog.zune.net", xml);
        Assert.Contains("image.catalog.zune.net", xml);
    }

    [Fact]
    public async Task Metaservices_is_not_reachable_on_another_host()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ZuneAPI/EndPoints.aspx");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
