namespace DoradoCloud.Shared.Contracts;

/// <summary>Uniform module liveness payload returned by every module's <c>/ping</c>.</summary>
public sealed record PingResponse(string Module, string Status, string Version);

/// <summary>
/// A signed application-update manifest served by the updates module.
/// The clients verify <see cref="Sha256"/> before applying.
/// </summary>
public sealed record UpdateManifest(
    string App,
    string Channel,
    string Version,
    string Url,
    string Sha256,
    DateTimeOffset PublishedAt,
    string Notes);

/// <summary>Paged envelope used by catalog/directory search endpoints.</summary>
public sealed record PagedResponse<T>(int Total, int Offset, int Limit, IReadOnlyList<T> Items);
