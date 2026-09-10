using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace DoradoCloud.Modules.Providers;

/// <summary>Read-through cache helper shared by the provider adapters.</summary>
public static class CacheExtensions
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T> GetOrCreateAsync<T>(
        this IDistributedCache cache,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<T>(cached, Options)!;
        }

        var value = await factory(cancellationToken);

        if (ttl > TimeSpan.Zero && value is not null)
        {
            await cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, Options),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }

        return value;
    }
}
