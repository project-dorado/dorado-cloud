using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Recommendations;

/// <summary>
/// QuickMix similarity/recommendation service. M0 exposes the contract; the
/// ListenBrainz / AcousticBrainz ingest and the pgvector ranking pipeline land
/// in M5.
/// </summary>
public sealed class RecommendationsModule : EndpointModuleBase
{
    public override string Name => "recs";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapPost("/quickmix", (QuickMixRequest request) => Results.Ok(new
        {
            seed = request.Seed,
            mode = request.Mode ?? "similar",
            total = 0,
            items = Array.Empty<object>(),
            note = "QuickMix ranking is scheduled for milestone M5."
        })).WithName("recs_quickmix");
    }

    public sealed record QuickMixRequest(string Seed, string? Mode, int? Limit);
}
