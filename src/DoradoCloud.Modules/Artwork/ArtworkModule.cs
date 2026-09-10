using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Artwork;

/// <summary>
/// Artwork proxy/cache/resize service. M0 exposes liveness plus the planned
/// signed-URL surface; the Fanart.tv / TheAudioDB / Wikimedia fetchers and the
/// object-storage cache land in M3.
/// </summary>
public sealed class ArtworkModule : EndpointModuleBase
{
    public override string Name => "artwork";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/{**path}", (string path) => Results.Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Artwork fetching not yet enabled",
            detail: "The artwork CDN is scheduled for milestone M3."))
            .WithName("artwork_get");
    }
}
