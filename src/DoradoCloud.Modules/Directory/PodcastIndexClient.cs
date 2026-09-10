using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DoradoCloud.Shared.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Directory;

/// <summary>
/// Podcast Index adapter. Requires an API key/secret (from <c>Directory:PodcastIndex</c>);
/// when not configured it degrades to an empty, non-error response so clients can
/// keep working offline.
/// </summary>
public sealed class PodcastIndexClient(
    HttpClient http,
    IOptions<DirectoryOptions> options,
    IDistributedCache cache)
{
    public async Task<PodcastSearchResponse> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.PodcastIndex.IsConfigured || string.IsNullOrWhiteSpace(query))
        {
            return new PodcastSearchResponse(query ?? string.Empty, 0, settings.PodcastIndex.IsConfigured,
                DirectoryOptions.AttributionPodcast, []);
        }

        limit = Math.Clamp(limit, 1, 50);
        var key = $"dir:podcasts:{query.Trim().ToLowerInvariant()}:{limit}";

        return await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(settings.CacheSeconds), async token =>
        {
            var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = Convert.ToHexString(
                    SHA1.HashData(Encoding.UTF8.GetBytes(
                        $"{settings.PodcastIndex.ApiKey}{settings.PodcastIndex.ApiSecret}{epoch}")))
                .ToLowerInvariant();

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"api/1.0/search/byterm?q={Uri.EscapeDataString(query)}&max={limit}");
            request.Headers.Add("X-Auth-Key", settings.PodcastIndex.ApiKey);
            request.Headers.Add("X-Auth-Date", epoch.ToString());
            request.Headers.Add("Authorization", signature);
            request.Headers.UserAgent.ParseAdd(settings.PodcastIndex.UserAgent);

            using var response = await http.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<PodcastIndexResponse>(token);
            var items = (payload?.Feeds ?? [])
                .Select(feed => new PodcastResult(
                    feed.Id.ToString(),
                    feed.Title ?? string.Empty,
                    feed.Author ?? string.Empty,
                    feed.Description ?? string.Empty,
                    feed.Image ?? string.Empty,
                    feed.Url ?? string.Empty,
                    CategoriesToString(feed.Categories),
                    feed.Language ?? string.Empty))
                .ToList();

            return new PodcastSearchResponse(query, items.Count, true, DirectoryOptions.AttributionPodcast, items);
        }, cancellationToken);
    }

    private static string CategoriesToString(JsonElement? categories)
    {
        if (categories is not { } value)
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => string.Join(", ", value.EnumerateObject().Select(p => p.Name)),
            JsonValueKind.Array => string.Join(", ",
                value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString())),
            _ => string.Empty
        };
    }

    private sealed record PodcastIndexResponse(
        [property: JsonPropertyName("feeds")] List<PodcastIndexFeed>? Feeds);

    private sealed record PodcastIndexFeed(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author")] string? Author,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("image")] string? Image,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("categories")] JsonElement? Categories,
        [property: JsonPropertyName("language")] string? Language);
}
