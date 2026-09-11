using System.Net;
using DoradoCloud.Modules.Storage;
using Microsoft.Extensions.Caching.Distributed;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Captures requests and returns canned responses for unit tests.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
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

/// <summary>A minimal in-memory <see cref="IDistributedCache"/> for tests.</summary>
internal sealed class FakeDistributedCache : IDistributedCache
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

/// <summary>An in-memory <see cref="IObjectStorage"/> for tests.</summary>
internal sealed class InMemoryObjectStorage : IObjectStorage
{
    public Dictionary<string, byte[]> Objects { get; } = new();

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Objects.TryGetValue(key, out var value) ? value : null);

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        Objects[key] = content;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(Objects.ContainsKey(key));
}
