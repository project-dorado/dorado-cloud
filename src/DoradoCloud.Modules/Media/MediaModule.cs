using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Media;

/// <summary>
/// DRM-free media streaming for public-domain / Creative Commons content only.
/// M0 exposes the contract and the policy gate; ingestion, licensing metadata
/// and CDN delivery land in M6 after legal review.
/// </summary>
public sealed class MediaModule : EndpointModuleBase
{
    public override string Name => "media";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/{id}", (string id) => Results.Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Media streaming not yet enabled",
            detail: "M6 serves public-domain / CC content only, after legal review. No copyrighted media is hosted."))
            .WithName("media_stream");
    }
}
