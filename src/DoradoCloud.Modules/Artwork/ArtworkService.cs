using System.Security.Cryptography;
using System.Text;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Modules.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Artwork;

/// <summary>
/// Fetches, caches and serves artwork. Objects are stored content-addressed in
/// <see cref="IObjectStorage"/>; arbitrary proxy URLs are restricted to an
/// allowlist of provider hosts to prevent SSRF.
/// </summary>
public sealed class ArtworkService(
    HttpClient http,
    IObjectStorage storage,
    IOptions<ArtworkOptions> options,
    HostRateLimiter limiter,
    ILogger<ArtworkService> logger)
{
    public async Task<ArtworkResult?> GetCoverArtFrontAsync(
        string releaseGroupMbid,
        int size,
        CancellationToken cancellationToken)
    {
        size = size is 250 or 500 or 1200 ? size : 500;
        var url = $"{options.Value.CoverArtBaseUrl.TrimEnd('/')}/release-group/{Uri.EscapeDataString(releaseGroupMbid)}/front-{size}";
        return await GetAsync(url, cancellationToken);
    }

    public async Task<ArtworkResult?> GetAsync(string url, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (!IsAllowed(uri.Host, settings.AllowedHosts))
        {
            throw new ArtworkHostNotAllowedException(uri.Host);
        }

        var key = "artwork/" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();

        var cached = await storage.GetAsync(key, cancellationToken);
        if (cached is not null)
        {
            return new ArtworkResult(cached, GuessContentType(url));
        }

        await limiter.WaitAsync(uri.Host, TimeSpan.FromMilliseconds(settings.RateLimitMs), cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(settings.UserAgent);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug("Artwork fetch for {Url} returned {Status}.", url, response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0 || content.Length > settings.MaxBytes)
        {
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? GuessContentType(url);
        await storage.PutAsync(key, content, contentType, cancellationToken);

        return new ArtworkResult(content, contentType);
    }

    private static bool IsAllowed(string host, string[] allowed)
        => allowed.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

    private static string GuessContentType(string url)
        => Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
}

public sealed record ArtworkResult(byte[] Content, string ContentType);
