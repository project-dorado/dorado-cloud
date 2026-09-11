using System.Net;
using DoradoCloud.Legacy;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>Result of fetching a podcast RSS feed for passthrough.</summary>
public sealed record RssFetchResult(byte[] Content, string ContentType);

/// <summary>
/// Fetches a podcast RSS feed on behalf of the legacy client
/// (<c>catalog.zune.net/podcast?url=</c>). SSRF-hardened: HTTPS only, private /
/// loopback / local host names rejected, and a hard size cap. This is a
/// best-effort guard (it does not defend against DNS rebinding) and is
/// documented as such.
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

        if (IsBlockedHost(uri.Host))
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

    /// <summary>True when a host must not be fetched (loopback/private/local).</summary>
    public static bool IsBlockedHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".home", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var ip) && IsPrivate(ip);
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        return ip.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork =>
                bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || bytes[0] == 127,
            System.Net.Sockets.AddressFamily.InterNetworkV6 =>
                ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (bytes[0] & 0xfe) == 0xfc,
            _ => true
        };
    }
}
