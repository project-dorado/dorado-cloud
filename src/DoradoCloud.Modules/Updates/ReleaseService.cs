using DoradoCloud.Modules.Data;
using DoradoCloud.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Modules.Updates;

/// <summary>Publishes and serves signed application releases for the update feed.</summary>
public sealed class ReleaseService(DoradoDbContext db, UpdateSigningKeyProvider keys)
{
    public async Task<UpdateReleaseDto?> GetLatestAsync(
        string app,
        string channel,
        CancellationToken cancellationToken = default)
    {
        var channelKey = Normalize(channel);
        var release = await db.UpdateReleases
            .Where(r => r.App == app && r.Channel == channelKey && r.IsActive)
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return release is null ? null : ToDto(release);
    }

    public async Task<UpdateReleaseDto> PublishAsync(
        PublishReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var publishedAt = DateTimeOffset.UtcNow;
        var manifest = new UpdateManifest(
            request.App.Trim(),
            Normalize(request.Channel),
            request.Version.Trim(),
            request.Url.Trim(),
            request.Sha256.Trim(),
            publishedAt,
            request.Notes ?? string.Empty);

        string signature;
        using (var key = keys.GetPrivateKey())
        {
            signature = UpdateManifestCrypto.Sign(manifest, key);
        }

        var release = new UpdateRelease
        {
            App = manifest.App,
            Channel = manifest.Channel,
            Version = manifest.Version,
            Url = manifest.Url,
            Sha256 = manifest.Sha256,
            Notes = manifest.Notes,
            PublishedAt = publishedAt,
            Signature = signature,
            Algorithm = "RS256"
        };

        db.UpdateReleases.Add(release);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(release);
    }

    private static string Normalize(string channel)
        => string.IsNullOrWhiteSpace(channel) ? "stable" : channel.Trim().ToLowerInvariant();

    private static UpdateReleaseDto ToDto(UpdateRelease r)
        => new(r.App, r.Channel, r.Version, r.Url, r.Sha256, r.PublishedAt, r.Notes, r.Signature, r.Algorithm);
}
