using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Modules.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class ArtworkServiceTests
{
    private static ArtworkService CreateService(
        StubHandler handler,
        FakeObjectStorage storage,
        ArtworkOptions? options = null)
        => new(
            new HttpClient(handler),
            storage,
            Options.Create(options ?? new ArtworkOptions()),
            new HostRateLimiter(),
            NullLogger<ArtworkService>.Instance);

    [Fact]
    public async Task Allowed_host_is_fetched_then_served_from_cache()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") } }
        });

        var storage = new FakeObjectStorage();
        var service = CreateService(handler, storage);

        var first = await service.GetAsync("https://coverartarchive.org/release-group/x/front-500", CancellationToken.None);
        var second = await service.GetAsync("https://coverartarchive.org/release-group/x/front-500", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal("image/png", first!.ContentType);
        Assert.Equal(bytes, first.Content);

        // Second call is served from storage; the network is hit only once.
        Assert.Equal(1, handler.Calls);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task Disallowed_host_is_rejected()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage()), new FakeObjectStorage());

        await Assert.ThrowsAsync<ArtworkHostNotAllowedException>(() =>
            service.GetAsync("https://evil.example/x.png", CancellationToken.None));
    }

    [Fact]
    public async Task Non_https_urls_are_ignored()
    {
        var service = CreateService(new StubHandler(_ => new HttpResponseMessage()), new FakeObjectStorage());

        Assert.Null(await service.GetAsync("http://coverartarchive.org/x.png", CancellationToken.None));
    }

    [Fact]
    public async Task Subdomain_of_an_allowed_host_is_permitted()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([9]) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }
        });

        var service = CreateService(handler, new FakeObjectStorage());
        var result = await service.GetAsync("https://upload.wikimedia.org/a.jpg", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("image/jpeg", result!.ContentType);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        private readonly Dictionary<string, (byte[] Content, string ContentType)> _store = new();

        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v.Content : null);

        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            _store[key] = (content, contentType);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.ContainsKey(key));
    }
}
