using DoradoCloud.Legacy;
using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Modules.Artwork;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Legacy.Catalog;

/// <summary>
/// Recreates <c>image.catalog.zune.net</c>: catalog artwork by Zune/MusicBrainz
/// id, served from the shared artwork CDN. Artist imagery has no keyless
/// provider and returns <c>404</c>.
/// </summary>
public sealed class LegacyImageCatalogModule : LegacyModuleBase
{
    public override string Name => "image-catalog";

    public override IReadOnlyList<string> Hosts
        => new[] { "image.catalog.zune.net", "image.catalog-ssl.zune.net" };

    protected override void MapLegacy(RouteGroupBuilder group)
    {
        group.MapGet("/", () => Results.Text("Dorado Cloud image catalog.", LegacyConstants.TextPlain));

        group.MapGet("/v3.2/{locale}/image/{id}",
            async (string locale, string id, int? width, ILegacyArtwork artwork, CancellationToken ct) =>
            {
                var result = await artwork.GetCoverAsync(id, width ?? 500, ct);
                return result is null ? Results.NotFound() : Serve(result);
            });

        group.MapGet("/v3.2/{locale}/music/artist/{id}/{type}",
            async (string locale, string id, string type, int? width, ILegacyArtwork artwork, CancellationToken ct) =>
            {
                var result = await artwork.GetArtistImageAsync(id, width ?? 500, ct);
                return result is null
                    ? Results.NotFound(new { error = "artist_image_unavailable" })
                    : Serve(result);
            });
    }

    private static IResult Serve(ArtworkResult result) => Results.File(result.Content, result.ContentType);
}
