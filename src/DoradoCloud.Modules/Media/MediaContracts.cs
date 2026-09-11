namespace DoradoCloud.Modules.Media;

/// <summary>Request to ingest a PD/CC media asset (admin only).</summary>
public sealed record MediaIngestRequest(
    string Title,
    string? Creator,
    string License,
    string? LicenseUrl,
    string SourceUrl,
    string? Provider);

/// <summary>A stored media asset.</summary>
public sealed record MediaAssetDto(
    Guid Id,
    string Title,
    string Creator,
    string License,
    string LicenseUrl,
    string SourceUrl,
    string Provider,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);
