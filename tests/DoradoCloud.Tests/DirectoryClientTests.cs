using System.Net;
using System.Text;
using DoradoCloud.Modules.Directory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class DirectoryClientTests
{
    // ---- Podcast Index ---------------------------------------------------

    [Fact]
    public async Task Podcast_search_is_unconfigured_without_keys()
    {
        var client = new PodcastIndexClient(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new DirectoryOptions()),
            new FakeDistributedCache());

        var result = await client.SearchAsync("dorado", 10, CancellationToken.None);

        Assert.False(result.Configured);
        Assert.Empty(result.Items);
        Assert.Contains("Podcast Index", result.Attribution);
    }

    [Fact]
    public async Task Podcast_search_maps_feeds_and_signs_the_request()
    {
        const string json = """
        {
          "status": "true",
          "feeds": [
            {
              "id": 42,
              "title": "Dorado Radio",
              "author": "Tester",
              "description": "A podcast about media",
              "image": "https://img/cover.png",
              "url": "https://feed.example/rss",
              "categories": { "music": 1, "technology": 2 },
              "language": "en"
            }
          ],
          "count": 1
        }
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var cache = new FakeDistributedCache();
        var options = Options.Create(new DirectoryOptions
        {
            PodcastIndex = new PodcastIndexOptions { ApiKey = "key", ApiSecret = "secret" }
        });

        var client = new PodcastIndexClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.podcastindex.org/") }, options, cache);
        var first = await client.SearchAsync("dorado", 10, CancellationToken.None);
        var second = await client.SearchAsync("dorado", 10, CancellationToken.None);

        Assert.True(first.Configured);
        var feed = Assert.Single(first.Items);
        Assert.Equal("42", feed.FeedId);
        Assert.Equal("Dorado Radio", feed.Title);
        Assert.Equal("music, technology", feed.Categories);

        // Request carried the Podcast Index auth headers.
        Assert.Equal("key", handler.LastRequest!.Headers.GetValues("X-Auth-Key").Single());
        Assert.True(handler.LastRequest.Headers.Contains("Authorization"));
        Assert.Contains("search/byterm", handler.LastRequest.RequestUri!.ToString());

        // Cached: the second call must not hit the network again.
        Assert.Equal(1, handler.Calls);
        Assert.Equal(first.Items.Count, second.Items.Count);
    }

    // ---- Radio-Browser ---------------------------------------------------

    [Fact]
    public async Task Radio_search_is_empty_when_disabled()
    {
        var client = new RadioBrowserClient(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
            Options.Create(new DirectoryOptions { RadioBrowser = new RadioBrowserOptions { Enabled = false } }),
            new FakeDistributedCache());

        var result = await client.SearchAsync("jazz", 10, null, null, CancellationToken.None);

        Assert.False(result.Configured);
        Assert.Empty(result.Items);
        Assert.Contains("Radio-Browser", result.Attribution);
    }

    [Fact]
    public async Task Radio_search_maps_stations()
    {
        const string json = """
        [
          {
            "stationuuid": "abc-123",
            "name": "Dorado Jazz",
            "url_resolved": "https://stream.example/jazz",
            "url": "https://stream.example/jazz",
            "favicon": "https://img/fav.png",
            "country": "The Netherlands",
            "countrycode": "NL",
            "tags": "jazz,smooth",
            "codec": "MP3",
            "bitrate": 192,
            "votes": 77
          }
        ]
        """;

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var client = new RadioBrowserClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://de1.api.radio-browser.info/") },
            Options.Create(new DirectoryOptions()),
            new FakeDistributedCache());

        var result = await client.SearchAsync("jazz", 10, "NL", "jazz", CancellationToken.None);

        Assert.True(result.Configured);
        var station = Assert.Single(result.Items);
        Assert.Equal("Dorado Jazz", station.Name);
        Assert.Equal("NL", station.CountryCode);
        Assert.Equal(192, station.Bitrate);
        Assert.Contains("stations/search", handler.LastRequest!.RequestUri!.ToString());
    }

    // ---- test doubles ----------------------------------------------------

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

        public byte[]? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            _store.Remove(key);
            return Task.CompletedTask;
        }
    }
}
