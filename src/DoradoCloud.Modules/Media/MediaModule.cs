using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Media;

/// <summary>
/// DRM-free media streaming for public-domain / Creative Commons content only.
/// The M6 legal gate is enforced by <c>Media:Enabled</c> (default <c>false</c>):
/// while disabled the module answers <c>501</c>. Ingestion is admin-only and
/// requires a license on the allowlist.
/// </summary>
public sealed class MediaModule : EndpointModuleBase
{
    public override string Name => "media";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/{id}", async (
            string id, MediaService media, CancellationToken cancellationToken) =>
        {
            if (!media.Enabled)
            {
                return Disabled();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return Results.NotFound(new { error = "media_not_found" });
            }

            var asset = await media.FindAsync(guid, cancellationToken);
            if (asset is null)
            {
                return Results.NotFound(new { error = "media_not_found" });
            }

            var content = await media.OpenAsync(asset, cancellationToken);
            return content is null
                ? Results.NotFound(new { error = "media_content_missing" })
                : Results.File(content, asset.ContentType);
        }).WithName("media_stream");

        group.MapPost("", async (
            MediaIngestRequest request, MediaService media, CancellationToken cancellationToken) =>
        {
            var (asset, error) = await media.IngestAsync(request, cancellationToken);
            return asset is null
                ? error == "media_disabled"
                    ? Disabled()
                    : Results.BadRequest(new { error })
                : Results.Created($"/v1/media/{asset.Id}", MediaService.ToDto(asset));
        }).RequireAuthorization("Admin").WithName("media_ingest");
    }

    private static IResult Disabled() => Results.Problem(
        statusCode: StatusCodes.Status501NotImplemented,
        title: "Media streaming not yet enabled",
        detail: "M6 serves public-domain / CC content only, after legal review. No copyrighted media is hosted.");
}
