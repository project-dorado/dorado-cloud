using DoradoCloud.Legacy;
using DoradoCloud.Modules.Providers;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>Result of fetching a podcast RSS feed for passthrough.</summary>
public sealed record RssFetchResult(byte[] Content, string ContentType);

/// <summary>
/// Fetches a podcast RSS feed on behalf of the legacy client
/// (<c>catalog.zune.net/podcast?url=</c>). SSRF-hardened via
/// <see cref="SsrfGuard"/>: HTTPS only, private / loopback / local host names
/// rejected, and a hard size cap.
/// </summary>
public sealed class RssProxyClient(HttpClient http)
{
    private const int MaxBytes = 5 * 1024 * 1024;

    public async Task<RssFetchResult?> FetchAsync(string? url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (SsrfGuard.IsBlockedHost(uri.Host))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length == 0 || content.Length > MaxBytes)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? LegacyConstants.Rss;
            return new RssFetchResult(content, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
