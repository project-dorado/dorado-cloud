namespace DoradoCloud.Shared.Contracts;

public sealed record QuickMixCandidate(
    string Mbid,
    string Name,
    double Score,
    IReadOnlyList<string> Reasons);

public sealed record QuickMixResponse(
    string Seed,
    string? SeedMbid,
    int Total,
    string Attribution,
    IReadOnlyList<QuickMixCandidate> Items);
