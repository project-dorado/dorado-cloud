using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace DoradoCloud.Tests.Legacy;

public sealed class AppsModuleTests : IClassFixture<AppsModuleTests.AppsFactory>
{
    private const string CatalogHost = "catalog.zune.net";

    public sealed class AppsFactory : CloudApiFactory
    {
        public const string PackageId = "racing.game";
        public static readonly byte[] PackageBytes = { 1, 3, 3, 7 };

        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "dorado-apps-" + Guid.NewGuid().ToString("N"));

        public AppsFactory()
        {
            Directory.CreateDirectory(Path.Combine(Root, "racing"));
            File.WriteAllBytes(Path.Combine(Root, "racing", "Game.zcp"), PackageBytes);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Apps:CorpusRoot", Root);
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

    private readonly AppsFactory _factory;

    public AppsModuleTests(AppsFactory factory) => _factory = factory;

    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = CatalogHost;
        return request;
    }

    [Fact]
    public async Task Categories_list_corpus_folders()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("/appCategories");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("racing", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Apps_lists_packages_with_free_metadata()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("/apps");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<a:id>{AppsFactory.PackageId}</a:id>", xml);
        Assert.Contains("<cost>Free</cost>", xml);
        Assert.Contains("<store>Zest</store>", xml);
    }

    [Fact]
    public async Task Package_is_streamed()
    {
        var client = _factory.CreateClient();
        using var request = Hosted($"/apps/{AppsFactory.PackageId}/package");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(AppsFactory.PackageBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Missing_app_is_404()
    {
        var client = _factory.CreateClient();
        using var request = Hosted("/apps/does-not-exist");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>With no app corpus the catalog is empty and packages are 404.</summary>
public sealed class AppsDisabledTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "catalog.zune.net";
        return request;
    }

    [Fact]
    public async Task Package_is_404_without_a_corpus()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/apps/anything/package");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Apps_feed_is_empty_but_valid()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/apps");

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }
}
