using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Recommendations;

/// <summary>
/// QuickMix similarity recommendations. Accepts a seed (artist name or MBID)
/// and returns scored, explained candidates.
/// </summary>
public sealed class RecommendationsModule : EndpointModuleBase
{
    public override string Name => "recs";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/quickmix", async (
            string? seed,
            int? limit,
            QuickMixService quickMix,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(seed))
            {
                return Results.BadRequest(new { error = "seed_required" });
            }

            return Results.Ok(await quickMix.RecommendAsync(seed, limit ?? 20, cancellationToken));
        }).WithName("recs_quickmix");

        group.MapPost("/quickmix", async (
            QuickMixRequest request,
            QuickMixService quickMix,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Seed))
            {
                return Results.BadRequest(new { error = "seed_required" });
            }

            return Results.Ok(await quickMix.RecommendAsync(request.Seed, request.Limit ?? 20, cancellationToken));
        }).WithName("recs_quickmix_post");
    }

    public sealed record QuickMixRequest(string Seed, string? Mode, int? Limit);
}
