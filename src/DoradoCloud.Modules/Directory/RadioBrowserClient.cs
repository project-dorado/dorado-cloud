using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DoradoCloud.Shared.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Directory;

/// <summary>
/// Radio-Browser adapter. Needs no API key; can be disabled entirely via
/// <c>Directory:RadioBrowser:Enabled=false</c>.
/// </summary>
public sealed class RadioBrowserClient(
    HttpClient http,
    IOptions<DirectoryOptions> options,
    IDistributedCache cache)
{
    public async Task<RadioSearchResponse> SearchAsync(
        string query,
        int limit,
        string? country,
        string? tag,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.RadioBrowser.Enabled)
        {
            return new RadioSearchResponse(query, 0, false, DirectoryOptions.AttributionRadio, []);
        }

        limit = Math.Clamp(limit, 1, 100);
        var key = $"dir:radio:{query.Trim().ToLowerInvariant()}:{limit}:{country}:{tag}".ToLowerInvariant();

        return await cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(settings.CacheSeconds), async token =>
        {
            var queryString = new List<string>
            {
                "hidebroken=true",
                $"limit={limit}",
                "order=votes",
                "reverse=true"
            };
            if (!string.IsNullOrWhiteSpace(query))
            {
                queryString.Add($"name={Uri.EscapeDataString(query)}");
            }

            if (!string.IsNullOrWhiteSpace(country))
            {
                queryString.Add($"countrycode={Uri.EscapeDataString(country)}");
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                queryString.Add($"tag={Uri.EscapeDataString(tag)}");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get, "json/stations/search?" + string.Join("&", queryString));
            request.Headers.UserAgent.ParseAdd(settings.RadioBrowser.UserAgent);

            using var response = await http.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var stations = await response.Content.ReadFromJsonAsync<List<RadioBrowserStation>>(token) ?? [];
            var items = stations
                .Select(station => new RadioStationResult(
                    station.StationUuid ?? string.Empty,
                    station.Name ?? string.Empty,
                    station.UrlResolved ?? station.Url ?? string.Empty,
                    station.Favicon ?? string.Empty,
                    station.Country ?? string.Empty,
                    station.CountryCode ?? string.Empty,
                    station.Tags ?? string.Empty,
                    station.Codec ?? string.Empty,
                    station.Bitrate,
                    station.Votes))
                .ToList();

            return new RadioSearchResponse(query, items.Count, true, DirectoryOptions.AttributionRadio, items);
        }, cancellationToken);
    }

    private sealed record RadioBrowserStation(
        [property: JsonPropertyName("stationuuid")] string? StationUuid,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("url_resolved")] string? UrlResolved,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("favicon")] string? Favicon,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("countrycode")] string? CountryCode,
        [property: JsonPropertyName("tags")] string? Tags,
        [property: JsonPropertyName("codec")] string? Codec,
        [property: JsonPropertyName("bitrate")] int Bitrate,
        [property: JsonPropertyName("votes")] int Votes);
}
