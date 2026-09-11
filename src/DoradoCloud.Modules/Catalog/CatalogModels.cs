namespace DoradoCloud.Modules.Catalog;

/// <summary>A release (album) with its track list, as needed by the legacy feed shapes.</summary>
public sealed record CatalogRelease(
    string Mbid,
    string Title,
    string ArtistId,
    string ArtistName,
    string Date,
    IReadOnlyList<CatalogReleaseTrack> Tracks);

/// <summary>A track within a release.</summary>
public sealed record CatalogReleaseTrack(
    string Mbid,
    string Title,
    int Position,
    int? LengthMs,
    string ArtistId,
    string ArtistName);

/// <summary>A standalone recording with its primary release, for track lookups.</summary>
public sealed record CatalogRecording(
    string Mbid,
    string Title,
    string ArtistId,
    string ArtistName,
    string AlbumId,
    string AlbumTitle,
    int? LengthMs);
