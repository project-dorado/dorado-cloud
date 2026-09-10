using System.Net;
using System.Text;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Modules.Recommendations;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class QuickMixServiceTests
{
    private const string SeedSearchJson = """
    { "artists": [ { "id": "seed-mbid", "name": "Seed Artist", "sort-name": "Seed Artist", "country": "GB" } ] }
    """;

    private const string TagsJson = """
    { "tags": [ { "name": "jazz", "count": 5 }, { "name": "electronic", "count": 3 } ] }
    """;

    private const string RelationsJson = """
    {
      "relations": [
        { "artist": { "id": "rel-1", "name": "Related One" } },
        { "artist": { "id": "seed-mbid", "name": "Seed Artist" } }
      ]
    }
    """;

    private const string JazzJson = """
    { "artists": [ { "id": "tag-1", "name": "Tag Artist" }, { "id": "rel-1", "name": "Related One" } ] }
    """;

    private const string ElectronicJson = """
    { "artists": [ { "id": "tag-2", "name": "Electronic Artist" } ] }
    """;

    [Fact]
    public async Task QuickMix_scores_related_and_genre_candidates_and_excludes_the_seed()
    {
        var handler = new StubHandler();
        var cache = new FakeDistributedCache();
        var service = CreateService(handler, cache);

        var result = await service.RecommendAsync("Seed Artist", 10, CancellationToken.None);

        Assert.Equal("seed-mbid", result.SeedMbid);
        Assert.Equal(3, result.Total);

        var top = result.Items[0];
        Assert.Equal("rel-1", top.Mbid);
        Assert.Equal(3.0, top.Score);
        Assert.Contains("related artist", top.Reasons);
        Assert.Contains("shares genre jazz", top.Reasons);

        Assert.DoesNotContain(result.Items, c => c.Mbid == "seed-mbid");
        Assert.Contains(result.Items, c => c.Mbid == "tag-1");
        Assert.Contains(result.Items, c => c.Mbid == "tag-2");
    }

    [Fact]
    public async Task QuickMix_is_cached()
    {
        var handler = new StubHandler();
        var service = CreateService(handler, new FakeDistributedCache());

        await service.RecommendAsync("Seed Artist", 10, CancellationToken.None);
        var callsAfterFirst = handler.Calls;

        await service.RecommendAsync("Seed Artist", 10, CancellationToken.None);

        Assert.Equal(callsAfterFirst, handler.Calls);
    }

    [Fact]
    public async Task Unknown_seed_yields_no_candidates()
    {
        var handler = new StubHandler { SearchOverride = """{ "artists": [] }""" };
        var service = CreateService(handler, new FakeDistributedCache());

        var result = await service.RecommendAsync("Nobody At All", 10, CancellationToken.None);

        Assert.Null(result.SeedMbid);
        Assert.Empty(result.Items);
    }

    private static QuickMixService CreateService(StubHandler handler, FakeDistributedCache cache)
    {
        var options = Options.Create(new CatalogOptions { MusicBrainzRateLimitMs = 0, CacheSeconds = 60 });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://musicbrainz.org/") };
        var brainz = new MusicBrainzClient(http, options, cache, new HostRateLimiter());
        return new QuickMixService(brainz, cache, options);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> Urls { get; } = new();
        public string? SearchOverride { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var url = request.RequestUri!.ToString();
            Urls.Add(url);

            var json =
                url.Contains("inc=tags") ? TagsJson :
                url.Contains("inc=artist-rels") ? RelationsJson :
                url.Contains("jazz") ? JazzJson :
                url.Contains("electronic") ? ElectronicJson :
                SearchOverride ?? SeedSearchJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
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
