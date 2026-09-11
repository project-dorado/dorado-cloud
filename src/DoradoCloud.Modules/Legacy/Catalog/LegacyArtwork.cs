using DoradoCloud.Legacy;
using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Providers;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Adapts the shared artwork CDN for legacy image requests. Artist imagery is
/// resolved best-effort through a keyless Wikidata path plus the configured
/// Fanart.tv / TheAudioDB / Discogs enrichment providers.
/// </summary>
public sealed class LegacyArtwork(
    ArtworkService artwork,
    MusicBrainzClient brainz,
    WikidataClient wikidata,
    FanartTvClient fanart,
    TheAudioDbClient audioDb,
    DiscogsClient discogs,
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
            // 1. Keyless: MusicBrainz -> Wikidata P18 -> Wikimedia Commons.
            var wikidataId = await brainz.GetArtistWikidataIdAsync(mbid, cancellationToken);
            if (!string.IsNullOrWhiteSpace(wikidataId))
            {
                var commonsUrl = await wikidata.GetCommonsImageUrlAsync(wikidataId, width, cancellationToken);
                if (await TryFetchAsync(commonsUrl, cancellationToken) is { } commons)
                {
                    return commons;
                }
            }

            // 2. Configured enrichment providers (MBID-keyed).
            if (await TryFetchAsync(await fanart.GetArtistImageAsync(mbid, cancellationToken), cancellationToken) is { } fromFanart)
            {
                return fromFanart;
            }

            if (await TryFetchAsync(await audioDb.GetArtistImageAsync(mbid, cancellationToken), cancellationToken) is { } fromAudioDb)
            {
                return fromAudioDb;
            }

            // 3. Discogs (by artist name).
            var artist = await brainz.GetArtistAsync(mbid, cancellationToken);
            if (artist is not null
                && await TryFetchAsync(await discogs.SearchArtistImageAsync(artist.Name, cancellationToken), cancellationToken) is { } fromDiscogs)
            {
                return fromDiscogs;
            }

            return null;
        }
        catch (Exception ex) when (ex is ArtworkHostNotAllowedException or HttpRequestException)
        {
            return null;
        }
    }

    private async Task<ArtworkResult?> TryFetchAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            return await artwork.GetAsync(url, cancellationToken);
        }
        catch (ArtworkHostNotAllowedException)
        {
            return null;
        }
    }
}
