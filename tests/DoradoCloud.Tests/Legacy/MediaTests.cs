using System.Net;
using System.Net.Http.Json;
using System.Text;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Media;
using DoradoCloud.Modules.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Unit tests for the legal-gated media service: disabled by default, license
/// allowlist, HTTPS/SSRF guard, and content-addressed storage.
/// </summary>
public sealed class MediaServiceTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "dorado-media-" + Guid.NewGuid().ToString("N") + ".db");

    private DoradoDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DoradoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new DoradoDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static MediaService Create(
        StubHandler handler,
        IObjectStorage storage,
        DoradoDbContext db,
        bool enabled = true)
    {
        var settings = new MediaOptions
        {
            Enabled = enabled,
            AllowedLicenses = new[] { "CC0", "PD" }
        };

        return new MediaService(
            new HttpClient(handler), db, storage, Options.Create(settings), NullLogger<MediaService>.Instance);
    }

    private static StubHandler Bytes(byte[] content) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent(content)
    });

    private static MediaIngestRequest Request(string url = "https://archive.org/download/x/track.mp3", string license = "CC0")
        => new("Track", "Artist", license, "https://creativecommons.org/publicdomain/zero/1.0/", url, "Internet Archive");

    [Fact]
    public async Task Disabled_by_default()
    {
        using var db = NewContext();
        var service = Create(Bytes(new byte[] { 1 }), new InMemoryObjectStorage(), db, enabled: false);

        var (asset, error) = await service.IngestAsync(Request(), CancellationToken.None);

        Assert.Null(asset);
        Assert.Equal("media_disabled", error);
    }

    [Fact]
    public async Task Rejects_a_license_off_the_allowlist()
    {
        using var db = NewContext();
        var service = Create(Bytes(new byte[] { 1 }), new InMemoryObjectStorage(), db);

        var (asset, error) = await service.IngestAsync(Request(license: "AllRightsReserved"), CancellationToken.None);

        Assert.Null(asset);
        Assert.Equal("license_not_allowed", error);
    }

    [Theory]
    [InlineData("http://archive.org/x.mp3", "source_must_be_https")]
    [InlineData("https://127.0.0.1/x.mp3", "source_host_not_allowed")]
    [InlineData("https://10.0.0.1/x.mp3", "source_host_not_allowed")]
    public async Task Rejects_unsafe_sources(string url, string expectedError)
    {
        using var db = NewContext();
        var service = Create(Bytes(new byte[] { 1 }), new InMemoryObjectStorage(), db);

        var (asset, error) = await service.IngestAsync(Request(url), CancellationToken.None);

        Assert.Null(asset);
        Assert.Equal(expectedError, error);
    }

    [Fact]
    public async Task Ingests_and_serves_whitelisted_content()
    {
        using var db = NewContext();
        var storage = new InMemoryObjectStorage();
        var bits = Encoding.UTF8.GetBytes("public-domain-audio");
        var service = Create(Bytes(bits), storage, db);

        var (asset, error) = await service.IngestAsync(Request(), CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(asset);
        Assert.Single(storage.Objects);
        Assert.Equal(bits, await service.OpenAsync(asset!, CancellationToken.None));
        Assert.NotNull(await service.FindAsync(asset!.Id, CancellationToken.None));
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            try
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }
}

/// <summary>HTTP wiring for the media gate.</summary>
public sealed class MediaModuleTests : IClassFixture<MediaModuleTests.EnabledMediaFactory>
{
    public sealed class EnabledMediaFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("Media:Enabled", "true");
            base.ConfigureWebHost(builder);
        }
    }

    private readonly EnabledMediaFactory _enabled;

    public MediaModuleTests(EnabledMediaFactory enabled) => _enabled = enabled;

    [Fact]
    public async Task Disabled_media_returns_501()
    {
        using var factory = new CloudApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/v1/media/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Enabled_media_missing_id_is_404()
    {
        var client = _enabled.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/v1/media/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/v1/media/not-a-guid")).StatusCode);
    }

    [Fact]
    public async Task Ingest_requires_admin()
    {
        var client = _enabled.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/media", new MediaIngestRequest(
            "T", "C", "CC0", null, "https://archive.org/x.mp3", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
