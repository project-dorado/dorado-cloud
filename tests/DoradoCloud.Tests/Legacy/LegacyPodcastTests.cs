using System.Net;
using DoradoCloud.Modules.Legacy.Catalog;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Legacy podcast hub routes and the SSRF guard for RSS passthrough.</summary>
public sealed class LegacyPodcastTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "catalog.zune.net";
        return request;
    }

    [Fact]
    public async Task Hub_returns_a_feed()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/v3.2/en-US/music/hub/podcast");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Chart_and_categories_degrade_to_empty_feeds_without_keys()
    {
        var client = factory.CreateClient();

        using var chart = Hosted("/podcastchart/zune/podcasts");
        var chartResponse = await client.SendAsync(chart);
        chartResponse.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await chartResponse.Content.ReadAsStringAsync());

        using var categories = Hosted("/podcastCategories");
        var categoriesResponse = await client.SendAsync(categories);
        categoriesResponse.EnsureSuccessStatusCode();
        Assert.Contains("<a:feed", await categoriesResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Podcast_requires_a_query_or_url()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/v3.2/en-US/podcast");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("http://example.com/feed.xml")]
    [InlineData("https://127.0.0.1/feed.xml")]
    [InlineData("https://localhost/feed.xml")]
    [InlineData("https://10.0.0.1/feed.xml")]
    [InlineData("https://192.168.1.10/feed.xml")]
    [InlineData("https://host.internal/feed.xml")]
    public async Task Rss_passthrough_rejects_unsafe_urls(string url)
    {
        var client = factory.CreateClient();
        using var request = Hosted("/podcast?url=" + Uri.EscapeDataString(url));

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("192.168.0.1", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("::1", true)]
    [InlineData("myhost.local", true)]
    [InlineData("example.com", false)]
    [InlineData("8.8.8.8", false)]
    public void Ssrf_guard_classifies_hosts(string host, bool blocked)
        => Assert.Equal(blocked, RssProxyClient.IsBlockedHost(host));
}
