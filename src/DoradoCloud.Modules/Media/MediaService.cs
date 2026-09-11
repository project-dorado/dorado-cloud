using System.Security.Cryptography;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Providers;
using DoradoCloud.Modules.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Media;

/// <summary>
/// Ingests and serves DRM-free (PD/CC) media. Ingestion is disabled unless
/// <c>Media:Enabled=true</c>, enforces a license allowlist, and is SSRF-guarded.
/// Bytes are stored content-addressed in object storage.
/// </summary>
public sealed class MediaService(
    HttpClient http,
    DoradoDbContext db,
    IObjectStorage storage,
    IOptions<MediaOptions> options,
    ILogger<MediaService> logger)
{
    public bool Enabled => options.Value.Enabled;

    public async Task<(MediaAsset? Asset, string? Error)> IngestAsync(
        MediaIngestRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enabled)
        {
            return (null, "media_disabled");
        }

        var license = (request.License ?? string.Empty).Trim();
        if (!options.Value.AllowedLicenses.Contains(license, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "license_not_allowed");
        }

        if (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return (null, "source_must_be_https");
        }

        if (SsrfGuard.IsBlockedHost(uri.Host))
        {
            return (null, "source_host_not_allowed");
        }

        byte[] content;
        string contentType;
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, "source_fetch_failed");
            }

            content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogDebug(ex, "Media source fetch failed for {Url}", request.SourceUrl);
            return (null, "source_fetch_failed");
        }

        if (content.Length == 0 || content.Length > options.Value.MaxBytes)
        {
            return (null, "source_size_rejected");
        }

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        await storage.PutAsync("media/" + hash, content, contentType, cancellationToken);

        var asset = new MediaAsset
        {
            Title = (request.Title ?? string.Empty).Trim(),
            Creator = (request.Creator ?? string.Empty).Trim(),
            License = license,
            LicenseUrl = (request.LicenseUrl ?? string.Empty).Trim(),
            SourceUrl = request.SourceUrl.Trim(),
            Provider = (request.Provider ?? string.Empty).Trim(),
            ContentType = contentType,
            ContentHash = hash,
            SizeBytes = content.Length
        };

        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return (asset, null);
    }

    public Task<MediaAsset?> FindAsync(Guid id, CancellationToken cancellationToken)
        => db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public Task<byte[]?> OpenAsync(MediaAsset asset, CancellationToken cancellationToken)
        => storage.GetAsync("media/" + asset.ContentHash, cancellationToken);

    public static MediaAssetDto ToDto(MediaAsset asset)
        => new(asset.Id, asset.Title, asset.Creator, asset.License, asset.LicenseUrl,
            asset.SourceUrl, asset.Provider, asset.ContentType, asset.SizeBytes, asset.CreatedAt);
}
