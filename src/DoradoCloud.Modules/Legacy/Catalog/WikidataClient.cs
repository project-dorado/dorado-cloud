using System.Net.Http.Json;
using System.Text.Json;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Providers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Minimal Wikidata client used to resolve a keyless artist image: the
/// MusicBrainz → Wikidata (P18) → Wikimedia Commons path. No API key required.
/// </summary>
public sealed class WikidataClient(HttpClient http, IDistributedCache cache, IOptions<CatalogOptions> options)
{
    /// <summary>Commons <c>Special:FilePath</c> URL for the entity's P18 image, or null.</summary>
    public async Task<string?> GetCommonsImageUrlAsync(string wikidataId, int width, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(wikidataId))
        {
            return null;
        }

        var size = width is 250 or 500 or 1200 ? width : 500;
        var key = $"wikidata:image:{wikidataId}:{size}";

        var url = await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(options.Value.CacheSeconds), async token =>
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"w/api.php?action=wbgetclaims&entity={Uri.EscapeDataString(wikidataId)}&property=P18&format=json");
            request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);

            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(token);
            var fileName = ExtractFileName(json);
            return string.IsNullOrWhiteSpace(fileName)
                ? string.Empty
                : $"https://commons.wikimedia.org/wiki/Special:FilePath/{Uri.EscapeDataString(fileName)}?width={size}";
        }, cancellationToken);

        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private static string ExtractFileName(JsonElement json)
    {
        if (!json.TryGetProperty("claims", out var claims)
            || !claims.TryGetProperty("P18", out var images)
            || images.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var image in images.EnumerateArray())
        {
            if (image.TryGetProperty("mainsnak", out var snak)
                && snak.TryGetProperty("datavalue", out var value)
                && value.TryGetProperty("value", out var fileName)
                && fileName.ValueKind == JsonValueKind.String)
            {
                return fileName.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
