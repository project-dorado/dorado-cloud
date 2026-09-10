using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace DoradoCloud.Modules.Directory;

/// <summary>Small read-through cache helper over <see cref="IDistributedCache"/>.</summary>
public static class DirectoryCache
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

        if (ttl > TimeSpan.Zero)
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
