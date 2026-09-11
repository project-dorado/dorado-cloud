using DoradoCloud.Legacy;
using DoradoCloud.Modules.Artwork;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>Adapts the shared <see cref="ArtworkService"/> for legacy image requests.</summary>
public sealed class LegacyArtwork(ArtworkService artwork, ILegacyIdMapper ids) : ILegacyArtwork
{
    public async Task<ArtworkResult?> GetCoverAsync(string id, int width, CancellationToken cancellationToken)
    {
        if (!ids.TryFromLegacy(id, out var providerId))
        {
            return null;
        }

        try
        {
            return await artwork.GetCoverArtFrontAsync(providerId, width, cancellationToken);
        }
        catch (Exception ex) when (ex is ArtworkHostNotAllowedException or HttpRequestException)
        {
            return null;
        }
    }
}
