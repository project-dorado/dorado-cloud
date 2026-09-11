using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Providers;

/// <summary>
/// Discogs adapter: artist image by name. No-op unless
/// <c>Providers:Discogs:Token</c> is configured.
/// </summary>
public sealed class DiscogsClient(
    HttpClient http,
    IOptions<EnrichmentOptions> options,
    IDistributedCache cache)
{
    public async Task<string?> SearchArtistImageAsync(string artistName, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Discogs.IsConfigured || string.IsNullOrWhiteSpace(artistName))
        {
            return null;
        }

        var key = $"discogs:artist:{artistName.Trim().ToLowerInvariant()}";
        var url = await cache.GetOrCreateAsync(key, TimeSpan.FromHours(24), async token =>
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"database/search?q={Uri.EscapeDataString(artistName)}&type=artist&token={Uri.EscapeDataString(settings.Discogs.Token)}");
            request.Headers.UserAgent.ParseAdd(settings.UserAgent);

            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            if (!json.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (var result in results.EnumerateArray())
            {
                foreach (var property in new[] { "cover_image", "thumb" })
                {
                    if (result.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }
            }

            return string.Empty;
        }, cancellationToken);

        return string.IsNullOrWhiteSpace(url) ? null : url;
    }
}
