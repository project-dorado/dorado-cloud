using DoradoCloud.Legacy;
using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Catalog;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>Adapts the shared artwork CDN for legacy image requests.</summary>
public sealed class LegacyArtwork(
    ArtworkService artwork,
    MusicBrainzClient brainz,
    WikidataClient wikidata,
    ILegacyIdMapper ids) : ILegacyArtwork
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

    public async Task<ArtworkResult?> GetArtistImageAsync(string id, int width, CancellationToken cancellationToken)
    {
        if (!ids.TryFromLegacy(id, out var mbid))
        {
            return null;
        }

        try
        {
            var wikidataId = await brainz.GetArtistWikidataIdAsync(mbid, cancellationToken);
            if (string.IsNullOrWhiteSpace(wikidataId))
            {
                return null;
            }

            var commonsUrl = await wikidata.GetCommonsImageUrlAsync(wikidataId, width, cancellationToken);
            if (string.IsNullOrWhiteSpace(commonsUrl))
            {
                return null;
            }

            return await artwork.GetAsync(commonsUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is ArtworkHostNotAllowedException or HttpRequestException)
        {
            return null;
        }
    }
}
