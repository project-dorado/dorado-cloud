using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace DoradoCloud.Tests.Legacy;

public sealed class TilesModuleTests : IClassFixture<TilesModuleTests.TilesFactory>
{
    private const string TilesHost = "tiles.zune.net";

    public sealed class TilesFactory : CloudApiFactory
    {
        public static readonly byte[] JpegBytes = { 9, 8, 7 };
        public static readonly byte[] PngBytes = { 1, 2, 3, 4 };

        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "dorado-tiles-" + Guid.NewGuid().ToString("N"));

        public TilesFactory()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Background"));
            Directory.CreateDirectory(Path.Combine(Root, "Avatar"));
            File.WriteAllBytes(Path.Combine(Root, "Background", "bg1.jpg"), JpegBytes);
            File.WriteAllBytes(Path.Combine(Root, "Avatar", "me.png"), PngBytes);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Tiles:CorpusRoot", Root);
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

    private readonly TilesFactory _factory;

    public TilesModuleTests(TilesFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Host = TilesHost;
        return request;
    }

    [Fact]
    public async Task Background_tile_is_served_as_jpeg()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/tiles/background/bg1.jpg");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(TilesFactory.JpegBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Avatar_tile_keeps_its_png_content_type()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/tiles/avatar/me.png");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Missing_tile_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/tiles/background/nope.jpg");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_type_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/tiles/banner/bg1.jpg");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_is_not_supported()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Post, "/tiles/avatar/me.jpg");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Tiles_host_is_required()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/tiles/background/bg1.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
