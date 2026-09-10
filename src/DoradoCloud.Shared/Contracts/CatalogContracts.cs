namespace DoradoCloud.Shared.Contracts;

// ---- Catalog (MusicBrainz + Cover Art Archive) --------------------------

public sealed record CatalogSearchItem(
    string Type,
    string Mbid,
    string Title,
    string Artist,
    string Date,
    string? CoverArtUrl);

public sealed record CatalogSearchResponse(
    string Query,
    string Type,
    int Total,
    string Attribution,
    IReadOnlyList<CatalogSearchItem> Items);

public sealed record CatalogArtistDetail(
    string Mbid,
    string Name,
    string SortName,
    string Country,
    string Disambiguation,
    string? CoverArtUrl);

// ---- Artwork ------------------------------------------------------------

public sealed record ArtworkAttribution(string Provider, string? License, string? SourceUrl);
