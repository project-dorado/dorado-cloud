using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Providers;

/// <summary>
/// TheAudioDB adapter: artist thumbnail by MusicBrainz id. No-op unless
/// <c>Providers:TheAudioDb:ApiKey</c> is configured.
/// </summary>
public sealed class TheAudioDbClient(
    HttpClient http,
    IOptions<EnrichmentOptions> options,
    IDistributedCache cache)
{
    public async Task<string?> GetArtistImageAsync(string mbid, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.TheAudioDb.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(mbid))
        {
            return null;
        }

        var url = await cache.GetOrCreateAsync(
            $"theaudiodb:artist:{mbid}", TimeSpan.FromHours(24), async token =>
            {
                using var response = await http.GetAsync(
                    $"api/v1/json/{Uri.EscapeDataString(apiKey)}/artist-mb.php?i={Uri.EscapeDataString(mbid)}", token);
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);
                if (!json.TryGetProperty("artists", out var artists)
                    || artists.ValueKind != JsonValueKind.Array
                    || artists.GetArrayLength() == 0)
                {
                    return string.Empty;
                }

                var artist = artists[0];
                foreach (var property in new[] { "strArtistThumb", "strArtistFanart", "strArtistLogo" })
                {
                    if (artist.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }

                return string.Empty;
            }, cancellationToken);

        return string.IsNullOrWhiteSpace(url) ? null : url;
    }
}
