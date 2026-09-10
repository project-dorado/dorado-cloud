using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Catalog;

/// <summary>
/// Shared catalog of artists, releases and recordings backed by MusicBrainz
/// (with Cover Art Archive artwork links).
/// </summary>
public sealed class CatalogModule : EndpointModuleBase
{
    public override string Name => "catalog";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/search", async (
            string? q,
            string? type,
            int? limit,
            MusicBrainzClient catalog,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "query_required" });
            }

            return Results.Ok(await catalog.SearchAsync(q, type ?? "release-group", limit ?? 20, cancellationToken));
        }).WithName("catalog_search");

        group.MapGet("/artists/{mbid}", async (
            string mbid,
            MusicBrainzClient catalog,
            CancellationToken cancellationToken) =>
        {
            var artist = await catalog.GetArtistAsync(mbid, cancellationToken);
            return artist is null ? Results.NotFound(new { error = "artist_not_found" }) : Results.Ok(artist);
        }).WithName("catalog_artist");
    }
}
