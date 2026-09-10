namespace DoradoCloud.Modules.Social;

/// <summary>Static catalog of earnable badges.</summary>
public static class BadgeCatalog
{
    public sealed record Definition(string Code, string Name, string Description);

    public static readonly IReadOnlyList<Definition> All =
    [
        new("first-post", "first post", "Published your first activity"),
        new("connector", "connector", "Followed another listener"),
        new("collector", "collector", "Shared a rated track"),
        new("socialite", "socialite", "Gained ten followers"),
        new("early-adopter", "early adopter", "Joined Dorado Cloud early")
    ];

    public static Definition? Find(string code)
        => All.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
}
