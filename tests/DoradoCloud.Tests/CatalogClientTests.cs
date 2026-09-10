using System.Net;
using System.Text;
using System.Text.Json;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class CatalogClientTests
{
    private static MusicBrainzClient CreateClient(StubHandler handler, FakeDistributedCache cache)
    {
        var options = Options.Create(new CatalogOptions { MusicBrainzRateLimitMs = 0 });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/") };
        return new MusicBrainzClient(http, options, cache, new HostRateLimiter());
    }

    [Fact]
    public async Task Artist_search_maps_results_and_sends_a_user_agent()
    {
        const string json = """
        {
          "artists": [
            { "id": "mbid-1", "name": "Radiohead", "sort-name": "Radiohead", "country": "GB", "disambiguation": "" }
          ]
        }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler, new FakeDistributedCache());
        var result = await client.SearchAsync("radiohead", "artist", 10, CancellationToken.None);

        Assert.Equal("artist", result.Type);
        var item = Assert.Single(result.Items);
        Assert.Equal("Radiohead", item.Title);
        Assert.Equal("mbid-1", item.Mbid);
        Assert.Contains("MusicBrainz", result.Attribution);
        Assert.NotEmpty(handler.LastRequest!.Headers.UserAgent);
        Assert.Contains("artist/?query=radiohead", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Release_group_search_exposes_cover_art_url()
    {
        const string json = """
        {
          "release-groups": [
            {
              "id": "rg-1",
              "title": "In Rainbows",
              "primary-type": "Album",
              "first-release-date": "2007-10-10",
              "artist-credit": [ { "name": "Radiohead" } ]
            }
          ]
        }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler, new FakeDistributedCache());
        var result = await client.SearchAsync("in rainbows", "release", 10, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("release-group", item.Type);
        Assert.Equal("Radiohead", item.Artist);
        Assert.Equal("https://coverartarchive.org/release-group/rg-1/front-250", item.CoverArtUrl);
    }

    [Fact]
    public async Task Search_is_cached()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "artists": [] }""", Encoding.UTF8, "application/json")
        });

        var client = CreateClient(handler, new FakeDistributedCache());
        await client.SearchAsync("x", "artist", 5, CancellationToken.None);
        await client.SearchAsync("x", "artist", 5, CancellationToken.None);

        Assert.Equal(1, handler.Calls);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public byte[]? Get(string key) => _store.TryGetValue(key, out var v) ? v : null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { _store[key] = value; return Task.CompletedTask; }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) { _store.Remove(key); return Task.CompletedTask; }
    }
}
