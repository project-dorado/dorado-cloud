namespace DoradoCloud.Shared.Contracts;

/// <summary>Uniform module liveness payload returned by every module's <c>/ping</c>.</summary>
public sealed record PingResponse(string Module, string Status, string Version);

/// <summary>
/// A signed application-update manifest. The clients verify <see cref="Sha256"/>
/// and the detached <c>Signature</c> (RS256 over the canonical manifest JSON)
/// before applying.
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

// ---- Identity / account -------------------------------------------------

/// <summary>
/// Anonymous principal envelope returned by <c>GET /v1/identity/me</c>. Fields
/// are nullable because the endpoint is reachable with a valid bearer token
/// whose subject may not yet be linked to a local account.
/// </summary>
public sealed record MeResponse(Guid? AccountId, string? Subject, string? Name, string? Email);

public sealed record AccountDto(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);

public sealed record DeviceDto(
    Guid Id,
    string Name,
    string Platform,
    string? Serial,
    string? AppVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);

public sealed record RegisterDeviceRequest(string Name, string Platform, string? Serial, string? AppVersion);

public sealed record SettingsDto(string PayloadJson, int Version, DateTimeOffset UpdatedAt);

public sealed record PutSettingsRequest(string PayloadJson, int? ExpectedVersion);

// ---- Updates ------------------------------------------------------------

public sealed record UpdateReleaseDto(
    string App,
    string Channel,
    string Version,
    string Url,
    string Sha256,
    DateTimeOffset PublishedAt,
    string Notes,
    string Signature,
    string Algorithm);

public sealed record UpdateCheckResponse(
    string App,
    string Channel,
    bool Available,
    UpdateReleaseDto? Release,
    string? Note);

public sealed record PublishReleaseRequest(
    string App,
    string Channel,
    string Version,
    string Url,
    string Sha256,
    string? Notes);
