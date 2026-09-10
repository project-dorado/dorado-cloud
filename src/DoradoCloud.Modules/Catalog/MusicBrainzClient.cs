using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Shared.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Catalog;

/// <summary>
/// MusicBrainz adapter: artist / release-group / recording search and artist
/// lookup. Calls are rate-limited per MusicBrainz policy and cached.
/// </summary>
public sealed class MusicBrainzClient(
    HttpClient http,
    IOptions<CatalogOptions> options,
    IDistributedCache cache,
    HostRateLimiter limiter)
{
    public async Task<CatalogSearchResponse> SearchAsync(
        string query,
        string type,
        int limit,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var normalized = type switch
        {
            "artist" => "artist",
            "recording" or "track" or "song" => "recording",
            _ => "release-group"
        };

        limit = Math.Clamp(limit, 1, 25);
        var key = $"cat:search:{normalized}:{query.Trim().ToLowerInvariant()}:{limit}";

        return await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(settings.CacheSeconds), async token =>
        {
            var json = await GetJsonAsync(
                $"ws/2/{normalized}/?query={Uri.EscapeDataString(query)}&fmt=json&limit={limit}", token);

            var items = normalized switch
            {
                "artist" => MapArtists(json),
                "recording" => MapRecordings(json),
                _ => MapReleaseGroups(json, settings)
            };

            return new CatalogSearchResponse(query, normalized, items.Count, CatalogOptions.Attribution, items);
        }, cancellationToken);
    }

    public async Task<CatalogArtistDetail?> GetArtistAsync(string mbid, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var key = $"cat:artist:{mbid}";

        return await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(settings.CacheSeconds), async token =>
        {
            await limiter.WaitAsync(http.BaseAddress!.Host,
                TimeSpan.FromMilliseconds(settings.MusicBrainzRateLimitMs), token);

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"ws/2/artist/{Uri.EscapeDataString(mbid)}?fmt=json");
            request.Headers.UserAgent.ParseAdd(settings.UserAgent);

            using var response = await http.SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);

            return new CatalogArtistDetail(
                mbid,
                GetString(json, "name"),
                GetString(json, "sort-name"),
                GetString(json, "country"),
                GetString(json, "disambiguation"),
                null);
        }, cancellationToken);
    }

    /// <summary>Top genre tags for an artist (most-applied first).</summary>
    public async Task<IReadOnlyList<string>> GetArtistGenresAsync(string mbid, CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync($"ws/2/artist/{Uri.EscapeDataString(mbid)}?fmt=json&inc=tags", cancellationToken);

        if (!json.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return tags.EnumerateArray()
            .Select(tag => new
            {
                Name = tag.TryGetProperty("name", out var n) ? n.GetString() : null,
                Count = tag.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .OrderByDescending(t => t.Count)
            .Select(t => t.Name!.ToLowerInvariant())
            .Take(3)
            .ToList();
    }

    /// <summary>Artists related to the given artist (band members, collaborators, …).</summary>
    public async Task<IReadOnlyList<CatalogSearchItem>> GetRelatedArtistsAsync(string mbid, CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync($"ws/2/artist/{Uri.EscapeDataString(mbid)}?fmt=json&inc=artist-rels", cancellationToken);
        var results = new List<CatalogSearchItem>();

        if (!json.TryGetProperty("relations", out var relations) || relations.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var relation in relations.EnumerateArray())
        {
            if (!relation.TryGetProperty("artist", out var artist))
            {
                continue;
            }

            var id = artist.TryGetProperty("id", out var i) ? i.GetString() : null;
            var name = artist.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                results.Add(new CatalogSearchItem("artist", id, name, name, string.Empty, null));
            }
        }

        return results;
    }

    /// <summary>Artists tagged with the given genre.</summary>
    public async Task<IReadOnlyList<CatalogSearchItem>> SearchByTagAsync(string tag, int limit, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"tag:\"{tag}\"");
        var json = await GetJsonAsync(
            $"ws/2/artist/?query={query}&fmt=json&limit={Math.Clamp(limit, 1, 25)}", cancellationToken);
        return MapArtists(json);
    }

    private async Task<JsonElement> GetJsonAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        await limiter.WaitAsync(http.BaseAddress!.Host,
            TimeSpan.FromMilliseconds(settings.MusicBrainzRateLimitMs), cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.UserAgent.ParseAdd(settings.UserAgent);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static List<CatalogSearchItem> MapArtists(JsonElement json)
    {
        var items = new List<CatalogSearchItem>();
        if (!json.TryGetProperty("artists", out var artists))
        {
            return items;
        }

        foreach (var artist in artists.EnumerateArray())
        {
            items.Add(new CatalogSearchItem(
                "artist",
                GetString(artist, "id"),
                GetString(artist, "name"),
                GetString(artist, "name"),
                GetString(artist, "country"),
                null));
        }

        return items;
    }

    private static List<CatalogSearchItem> MapReleaseGroups(JsonElement json, CatalogOptions settings)
    {
        var items = new List<CatalogSearchItem>();
        if (!json.TryGetProperty("release-groups", out var groups))
        {
            return items;
        }

        foreach (var group in groups.EnumerateArray())
        {
            var id = GetString(group, "id");
            items.Add(new CatalogSearchItem(
                "release-group",
                id,
                GetString(group, "title"),
                ArtistCredit(group),
                GetString(group, "first-release-date"),
                $"{settings.CoverArtBaseUrl.TrimEnd('/')}/release-group/{id}/front-250"));
        }

        return items;
    }

    private static List<CatalogSearchItem> MapRecordings(JsonElement json)
    {
        var items = new List<CatalogSearchItem>();
        if (!json.TryGetProperty("recordings", out var recordings))
        {
            return items;
        }

        foreach (var recording in recordings.EnumerateArray())
        {
            items.Add(new CatalogSearchItem(
                "recording",
                GetString(recording, "id"),
                GetString(recording, "title"),
                ArtistCredit(recording),
                GetString(recording, "first-release-date"),
                null));
        }

        return items;
    }

    private static string ArtistCredit(JsonElement element)
    {
        if (!element.TryGetProperty("artist-credit", out var credits) || credits.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(", ", credits.EnumerateArray().Select(c =>
            c.ValueKind == JsonValueKind.Object && c.TryGetProperty("name", out var name)
                ? name.GetString()
                : string.Empty).Where(n => !string.IsNullOrEmpty(n)));
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
