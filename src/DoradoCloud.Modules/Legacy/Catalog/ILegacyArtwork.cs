using DoradoCloud.Modules.Artwork;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Artwork lookup for the legacy image catalog host, backed by the shared
/// artwork CDN (Cover Art Archive, cached in object storage).
/// </summary>
public interface ILegacyArtwork
{
    /// <summary>Fetches a front cover by legacy (Zune GUID / MBID) identifier.</summary>
    Task<ArtworkResult?> GetCoverAsync(string id, int width, CancellationToken cancellationToken);
}
