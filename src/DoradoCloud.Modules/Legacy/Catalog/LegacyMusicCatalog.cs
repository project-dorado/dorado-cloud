using System.Xml.Linq;
using DoradoCloud.Legacy;
using DoradoCloud.Legacy.Atom;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// MusicBrainz-backed implementation of the legacy catalog feeds. Ids are
/// emitted as <c>urn:uuid:{mbid}</c> and the incoming ids are normalized by
/// <see cref="ILegacyIdMapper"/>.
/// </summary>
public sealed class LegacyMusicCatalog(
    MusicBrainzClient catalog,
    ILegacyIdMapper ids,
    ILogger<LegacyMusicCatalog> logger) : ILegacyMusicCatalog
{
    private const string Ns = LegacyConstants.MusicCatalogNamespace;

    public Task<XElement> HubsAsync(string locale, CancellationToken cancellationToken)
    {
        var feed = Feed("Music", "music", $"/v3.2/{locale}/hubs/music");
        feed.AddEntry("Genres", "genre", $"/v3.2/{locale}/music/genre");
        feed.AddEntry("Top tracks", "tracks", $"/v3.2/{locale}/music/chart/zune/tracks");
        feed.AddEntry("New albums", "albums", $"/v3.2/{locale}/music/chart/zune/albums");
        return Task.FromResult(feed.Build());
    }

    public async Task<XElement> GenresAsync(string locale, CancellationToken cancellationToken)
    {
        var genres = await catalog.ListGenresAsync(cancellationToken);
        var feed = Feed("Genres", "genre", $"/v3.2/{locale}/music/genre").Author("Dorado Cloud");

        foreach (var genre in genres)
        {
            var id = GenreId(genre);
            feed.AddEntry(genre, id, $"/v3.2/{locale}/music/genre/{id}");
        }

        return feed.Build();
    }

    public Task<XElement> GenreAsync(string locale, string genreId, CancellationToken cancellationToken)
    {
        var feed = Feed(genreId, genreId, $"/v3.2/{locale}/music/genre/{genreId}");
        feed.AddEntry(genreId, genreId, $"/v3.2/{locale}/music/genre/{genreId}");
        return Task.FromResult(feed.Build());
    }

    public async Task<XElement> GenreAlbumsAsync(string locale, string genreId, CancellationToken cancellationToken)
    {
        var genres = await catalog.ListGenresAsync(cancellationToken);
        var name = genres.FirstOrDefault(g => GenreId(g) == genreId) ?? genreId.Replace('-', ' ');

        var feed = Feed(name, GenreId(name), $"/v3.2/{locale}/music/genre/{genreId}/albums");
        foreach (var item in await catalog.SearchReleasesByTagAsync(name, 50, cancellationToken))
        {
            AddAlbum(feed, locale, item);
        }

        return feed.Build();
    }

    public async Task<XElement> AlbumSearchAsync(string locale, string query, CancellationToken cancellationToken)
    {
        var result = await catalog.SearchAsync(query, "release", 25, cancellationToken);
        var feed = Feed(query, "album", $"/v3.2/{locale}/music/album");
        foreach (var item in result.Items)
        {
            AddAlbum(feed, locale, item);
        }

        return feed.Build();
    }

    public async Task<XElement?> AlbumDetailAsync(string locale, string albumId, CancellationToken cancellationToken)
    {
        var release = await catalog.GetReleaseAsync(albumId, cancellationToken);
        if (release is null)
        {
            logger.LogDebug("Legacy album {AlbumId} not found", albumId);
            return null;
        }

        var feed = Feed(release.Title, LegacyId(release.Mbid), AlbumHref(locale, release.Mbid))
            .Container("image", element => element.Add(new XElement(XNamespace.Get(Ns) + "id", LegacyId(release.Mbid))));

        foreach (var track in release.Tracks)
        {
            AddTrack(feed, locale, track.Mbid, track.Title, track.ArtistId, track.ArtistName, track.Position, track.LengthMs);
        }

        return feed.Build();
    }

    public async Task<XElement?> ArtistAsync(string locale, string artistId, CancellationToken cancellationToken)
    {
        var artist = await catalog.GetArtistAsync(artistId, cancellationToken);
        if (artist is null)
        {
            logger.LogDebug("Legacy artist {ArtistId} not found", artistId);
            return null;
        }

        var feed = Feed(artist.Name, LegacyId(artist.Mbid), ArtistHref(locale, artist.Mbid))
            .Element("sortTitle", artist.SortName)
            .Element("isVariousArtist", "False");

        return feed.Build();
    }

    public async Task<XElement> ArtistAlbumsAsync(string locale, string artistId, CancellationToken cancellationToken)
    {
        var feed = Feed(artistId, LegacyId(artistId), $"/v3.2/{locale}/music/artist/{artistId}/albums");
        foreach (var item in await catalog.BrowseReleasesByArtistAsync(artistId, 100, cancellationToken))
        {
            AddAlbum(feed, locale, item);
        }

        return feed.Build();
    }

    public async Task<XElement> ArtistTracksAsync(string locale, string artistId, CancellationToken cancellationToken)
    {
        var feed = Feed(artistId, LegacyId(artistId), $"/v3.2/{locale}/music/artist/{artistId}/tracks");
        var position = 1;
        foreach (var item in await catalog.BrowseRecordingsByArtistAsync(artistId, 100, cancellationToken))
        {
            AddTrack(feed, locale, item.Mbid, item.Title, artistId, item.Artist, position++, null);
        }

        return feed.Build();
    }

    public async Task<XElement> TrackSearchAsync(string locale, string query, CancellationToken cancellationToken)
    {
        var result = await catalog.SearchAsync(query, "recording", 25, cancellationToken);
        var feed = Feed(query, "track", $"/v3.2/{locale}/music/track");
        foreach (var item in result.Items)
        {
            AddTrack(feed, locale, item.Mbid, item.Title, string.Empty, item.Artist, 0, null);
        }

        return feed.Build();
    }

    public async Task<XElement?> TrackDetailAsync(string locale, string trackId, CancellationToken cancellationToken)
    {
        var recording = await catalog.GetRecordingAsync(trackId, cancellationToken);
        if (recording is null)
        {
            logger.LogDebug("Legacy track {TrackId} not found", trackId);
            return null;
        }

        var feed = Feed(recording.Title, LegacyId(recording.Mbid), TrackHref(locale, recording.Mbid));
        feed.AddEntry(recording.Title, LegacyId(recording.Mbid), TrackHref(locale, recording.Mbid), entry =>
        {
            if (!string.IsNullOrWhiteSpace(recording.ArtistId) || !string.IsNullOrWhiteSpace(recording.ArtistName))
            {
                entry.PrimaryArtist(LegacyId(recording.ArtistId), recording.ArtistName);
            }

            entry.ElementIf("albumId", LegacyIdOrNull(recording.AlbumId));
            entry.ElementIf("albumTitle", string.IsNullOrWhiteSpace(recording.AlbumTitle) ? null : recording.AlbumTitle);
            entry.ElementIf("duration", FormatDuration(recording.LengthMs));
        });

        return feed.Build();
    }

    public Task<XElement> ChartTracksAsync(string locale, CancellationToken cancellationToken)
        => Task.FromResult(Feed("Tracks", "tracks", $"/v3.2/{locale}/music/chart/zune/tracks").Build());

    public async Task<XElement> SimilarTracksAsync(string locale, string trackId, CancellationToken cancellationToken)
    {
        var selfHref = $"/v4.0/{locale}/track/{trackId}/similarTracks";
        var recording = await catalog.GetRecordingAsync(trackId, cancellationToken);
        var feed = Feed("Similar tracks", "similar", selfHref);

        if (recording is null || string.IsNullOrWhiteSpace(recording.ArtistId))
        {
            return feed.Build();
        }

        foreach (var item in await catalog.BrowseRecordingsByArtistAsync(recording.ArtistId, 25, cancellationToken))
        {
            if (item.Mbid == recording.Mbid)
            {
                continue;
            }

            AddTrack(feed, locale, item.Mbid, item.Title, recording.ArtistId, item.Artist, 0, null);
        }

        return feed.Build();
    }

    private AtomFeedBuilder Feed(string title, string id, string? selfHref = null)
        => new(title, id, selfHref, Ns);

    private void AddAlbum(AtomFeedBuilder feed, string locale, CatalogSearchItem item)
    {
        var id = LegacyId(item.Mbid);
        feed.AddEntry(item.Title, id, AlbumHref(locale, item.Mbid), entry =>
        {
            if (!string.IsNullOrWhiteSpace(item.Artist))
            {
                entry.PrimaryArtist(string.Empty, item.Artist);
            }

            entry.Image(id);
        });
    }

    private void AddTrack(
        AtomFeedBuilder feed,
        string locale,
        string mbid,
        string title,
        string artistId,
        string artistName,
        int position,
        int? lengthMs)
    {
        var id = LegacyId(mbid);
        feed.AddEntry(title, id, TrackHref(locale, mbid), entry =>
        {
            if (!string.IsNullOrWhiteSpace(artistId) || !string.IsNullOrWhiteSpace(artistName))
            {
                entry.PrimaryArtist(LegacyId(artistId), artistName);
            }

            entry.ElementIf("duration", FormatDuration(lengthMs));
            if (position > 0)
            {
                entry.Element("trackNumber", position.ToString());
            }
        });
    }

    private string LegacyId(string providerId)
        => string.IsNullOrWhiteSpace(providerId) ? string.Empty : ids.ToLegacy(providerId);

    private string? LegacyIdOrNull(string providerId)
        => string.IsNullOrWhiteSpace(providerId) ? null : ids.ToLegacy(providerId);

    private static string AlbumHref(string locale, string id) => $"/v3.2/{locale}/music/album/{id}";
    private static string TrackHref(string locale, string id) => $"/v3.2/{locale}/music/track/{id}";
    private static string ArtistHref(string locale, string id) => $"/v3.2/{locale}/music/artist/{id}";

    /// <summary>A stable slug id for a genre name (used in genre links).</summary>
    public static string GenreId(string name)
    {
        var chars = name.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        return new string(chars).Trim('-');
    }

    private static string? FormatDuration(int? milliseconds)
    {
        if (milliseconds is null or <= 0)
        {
            return null;
        }

        var time = TimeSpan.FromMilliseconds(milliseconds.Value);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }
}
