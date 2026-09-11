using System.Xml.Linq;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Produces the MusicBrainz-backed Atom feeds for the <c>catalog.zune.net</c>
/// compatibility host, in the shapes the Zune 4.8 client parses. Detail methods
/// return <c>null</c> when the requested entity does not exist.
/// </summary>
public interface ILegacyMusicCatalog
{
    Task<XElement> HubsAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> GenresAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> GenreAsync(string locale, string genreId, CancellationToken cancellationToken);
    Task<XElement> GenreAlbumsAsync(string locale, string genreId, CancellationToken cancellationToken);
    Task<XElement> AlbumSearchAsync(string locale, string query, CancellationToken cancellationToken);
    Task<XElement?> AlbumDetailAsync(string locale, string albumId, CancellationToken cancellationToken);
    Task<XElement?> ArtistAsync(string locale, string artistId, CancellationToken cancellationToken);
    Task<XElement> ArtistAlbumsAsync(string locale, string artistId, CancellationToken cancellationToken);
    Task<XElement> ArtistTracksAsync(string locale, string artistId, CancellationToken cancellationToken);
    Task<XElement> TrackSearchAsync(string locale, string query, CancellationToken cancellationToken);
    Task<XElement?> TrackDetailAsync(string locale, string trackId, CancellationToken cancellationToken);
    Task<XElement> ChartTracksAsync(string locale, CancellationToken cancellationToken);
    Task<XElement> SimilarTracksAsync(string locale, string trackId, CancellationToken cancellationToken);
}
