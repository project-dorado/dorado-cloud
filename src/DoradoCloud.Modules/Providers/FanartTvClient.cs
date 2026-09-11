using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Providers;

/// <summary>
/// Fanart.tv adapter: artist background/thumbnail art by MusicBrainz id. No-op
/// unless <c>Providers:FanartTv:ApiKey</c> is configured.
/// </summary>
public sealed class FanartTvClient(
    HttpClient http,
    IOptions<EnrichmentOptions> options,
    IDistributedCache cache)
{
    public async Task<string?> GetArtistImageAsync(string mbid, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.FanartTv.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(mbid))
        {
            return null;
        }

        var url = await cache.GetOrCreateAsync(
            $"fanart:artist:{mbid}", TimeSpan.FromHours(24), async token =>
            {
                using var response = await http.GetAsync(
                    $"v3/music/{Uri.EscapeDataString(mbid)}?api_key={Uri.EscapeDataString(apiKey)}", token);
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                return FirstUrl(json, "artistbackground") ?? FirstUrl(json, "artistthumb") ?? string.Empty;
            }, cancellationToken);

        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private static string? FirstUrl(JsonElement json, string property)
    {
        if (!json.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            {
                return url.GetString();
            }
        }

        return null;
    }
}
