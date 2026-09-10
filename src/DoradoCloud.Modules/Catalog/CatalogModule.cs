using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Catalog;

/// <summary>
/// Shared catalog of artists, releases and recordings. M0 returns an empty,
/// well-formed result; the MusicBrainz / Cover Art Archive / Discogs ingest
/// lands in M3 (see docs/adr).
/// </summary>
public sealed class CatalogModule : EndpointModuleBase
{
    public override string Name => "catalog";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/search", (string? q, int? limit) => Results.Ok(new
        {
            query = q ?? string.Empty,
            total = 0,
            limit = limit ?? 20,
            items = Array.Empty<object>(),
            note = "Catalog indexing is scheduled for milestone M3."
        })).WithName("catalog_search");
    }
}
