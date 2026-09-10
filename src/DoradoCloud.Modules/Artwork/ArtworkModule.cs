using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Artwork;

/// <summary>
/// Artwork CDN: front covers by MusicBrainz release-group id, plus an
/// allowlisted proxy for provider imagery. Responses are cached in object
/// storage and served with a long-lived Cache-Control.
/// </summary>
public sealed class ArtworkModule : EndpointModuleBase
{
    public override string Name => "artwork";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/front/{mbid}", async (
            string mbid,
            int? size,
            ArtworkService artwork,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await artwork.GetCoverArtFrontAsync(mbid, size ?? 500, cancellationToken);
            return result is null
                ? Results.NotFound(new { error = "artwork_not_found" })
                : Serve(context, result);
        }).WithName("artwork_front");

        group.MapGet("/proxy", async (
            string? url,
            ArtworkService artwork,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Results.BadRequest(new { error = "url_required" });
            }

            try
            {
                var result = await artwork.GetAsync(url, cancellationToken);
                return result is null
                    ? Results.NotFound(new { error = "artwork_not_found" })
                    : Serve(context, result);
            }
            catch (ArtworkHostNotAllowedException)
            {
                return Results.BadRequest(new { error = "host_not_allowed", detail = "Only configured provider hosts may be proxied." });
            }
        }).WithName("artwork_proxy");
    }

    private static IResult Serve(HttpContext context, ArtworkResult result)
    {
        context.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.File(result.Content, result.ContentType);
    }
}
