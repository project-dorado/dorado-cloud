using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Directory;

/// <summary>
/// Podcast (Podcast Index) and radio (Radio-Browser) search. Results carry
/// provider attribution; when a provider is unconfigured the endpoint returns a
/// well-formed empty response rather than an error.
/// </summary>
public sealed class DirectoryModule : EndpointModuleBase
{
    public override string Name => "directory";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/podcasts/search", async (
            string? q,
            int? limit,
            PodcastIndexClient podcasts,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "query_required" });
            }

            return Results.Ok(await podcasts.SearchAsync(q, limit ?? 20, cancellationToken));
        }).WithName("directory_podcasts_search");

        group.MapGet("/radio/search", async (
            string? q,
            string? country,
            string? tag,
            int? limit,
            RadioBrowserClient radio,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(tag))
            {
                return Results.BadRequest(new { error = "query_required" });
            }

            return Results.Ok(await radio.SearchAsync(q ?? string.Empty, limit ?? 30, country, tag, cancellationToken));
        }).WithName("directory_radio_search");
    }
}
