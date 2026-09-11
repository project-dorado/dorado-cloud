using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Exercises <c>resources.zune.net</c> against a temporary corpus. The CAB bytes
/// are a synthetic sequence; no firmware is shipped or committed.
/// </summary>
public sealed class ResourcesModuleTests : IClassFixture<ResourcesModuleTests.CorpusFactory>
{
    private const string ResourcesHost = "resources.zune.net";

    public sealed class CorpusFactory : CloudApiFactory
    {
        public const string CabName = "KeelBaseline.cab";
        public static readonly byte[] CabBytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        public string CorpusDir { get; } =
            Path.Combine(Path.GetTempPath(), "dorado-cloud-corpus-" + Guid.NewGuid().ToString("N"));

        public CorpusFactory()
        {
            Directory.CreateDirectory(CorpusDir);
            File.WriteAllBytes(Path.Combine(CorpusDir, CabName), CabBytes);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Resources:CorpusRoot", CorpusDir);
            base.ConfigureWebHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try
            {
                if (Directory.Exists(CorpusDir))
                {
                    Directory.Delete(CorpusDir, recursive: true);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private readonly CorpusFactory _factory;

    public ResourcesModuleTests(CorpusFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Host = ResourcesHost;
        return request;
    }

    [Fact]
    public async Task Manifest_lists_available_devices_with_absolute_urls()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/FirmwareUpdate.xml");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);

        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<FirmwareUpdates>", xml);
        Assert.Contains("KeelBaseline.cab", xml);
        Assert.Contains("http://resources.zune.net/KeelBaseline.cab", xml);
        // Only devices whose CAB is present in the corpus are advertised.
        Assert.DoesNotContain("DracoBaseline.cab", xml);
    }

    [Fact]
    public async Task V4_5_zuneprod_serves_the_same_manifest()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/v4_5/zuneprod.xml");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("KeelBaseline.cab", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cab_is_streamed_from_the_corpus()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/KeelBaseline.cab");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal(CorpusFactory.CabBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Cab_supports_range_requests()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/KeelBaseline.cab");
        request.Headers.Range = new RangeHeaderValue(0, 15);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(CorpusFactory.CabBytes[..16], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Non_cab_files_are_rejected()
    {
        var client = _factory.CreateClient();
        using var request = Hosted(HttpMethod.Get, "/appsettings.json");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Root_is_host_switched()
    {
        var client = _factory.CreateClient();

        using var legacyRequest = Hosted(HttpMethod.Get, "/");
        var legacy = await client.SendAsync(legacyRequest);
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Contains("Dorado Cloud resources service", await legacy.Content.ReadAsStringAsync());

        var modern = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, modern.StatusCode);
        Assert.Contains("Dorado Cloud", await modern.Content.ReadAsStringAsync());
    }
}

/// <summary>With no corpus configured the firmware service fails closed.</summary>
public sealed class ResourcesDisabledTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "resources.zune.net";
        return request;
    }

    [Fact]
    public async Task Manifest_is_404_without_a_corpus()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/FirmwareUpdate.xml");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cab_is_404_without_a_corpus()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/KeelBaseline.cab");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
