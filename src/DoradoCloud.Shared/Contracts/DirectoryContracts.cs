namespace DoradoCloud.Shared.Contracts;

// ---- Podcast directory (Podcast Index) ---------------------------------

public sealed record PodcastResult(
    string FeedId,
    string Title,
    string Author,
    string Description,
    string ImageUrl,
    string FeedUrl,
    string Categories,
    string Language);

public sealed record PodcastSearchResponse(
    string Query,
    int Total,
    bool Configured,
    string Attribution,
    IReadOnlyList<PodcastResult> Items);

/// <summary>A Podcast Index category (id + display name).</summary>
public sealed record PodcastCategory(string Id, string Name);

// ---- Radio directory (Radio-Browser) -----------------------------------

public sealed record RadioStationResult(
    string StationId,
    string Name,
    string Url,
    string Favicon,
    string Country,
    string CountryCode,
    string Tags,
    string Codec,
    int Bitrate,
    int Votes);

public sealed record RadioSearchResponse(
    string Query,
    int Total,
    bool Configured,
    string Attribution,
    IReadOnlyList<RadioStationResult> Items);
