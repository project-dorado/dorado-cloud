using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Tiles;

/// <summary>
/// Recreates <c>tiles.zune.net</c>: member background and avatar images. The
/// original host also accepted uploads; Dorado is read-only and answers those
/// with <c>501</c>. Images come from an external, untracked corpus.
/// </summary>
public sealed class TilesModule : LegacyModuleBase
{
    public override string Name => "tiles";

    public override IReadOnlyList<string> Hosts => new[] { "tiles.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud tiles service.", "text/plain"));

        group.MapGet("/tiles/{typeStr}/{zuneTag}", (string typeStr, string zuneTag, TileStore store) =>
        {
            if (!store.TryResolve(typeStr, zuneTag, out var path))
            {
                return Results.NotFound();
            }

            return Results.File(path, TileStore.ContentTypeFor(path));
        }).WithName("legacy_tile_get");

        group.MapPost("/tiles/{typeStr}/{zuneTag}", () => Results.Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Tile upload not supported",
            detail: "Dorado Cloud serves tiles read-only from a configured corpus; it does not accept uploads."))
            .WithName("legacy_tile_post");
    }
}
