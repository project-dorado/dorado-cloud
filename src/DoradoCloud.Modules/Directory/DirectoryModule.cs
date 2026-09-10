using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Directory;

/// <summary>
/// Podcast and radio directory. M0 exposes the contract; Podcast Index and
/// Radio-Browser adapters land in M2.
/// </summary>
public sealed class DirectoryModule : EndpointModuleBase
{
    public override string Name => "directory";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/podcasts/search", (string? q) => Results.Ok(new
        {
            query = q ?? string.Empty,
            total = 0,
            items = Array.Empty<object>(),
            note = "Podcast directory is scheduled for milestone M2."
        })).WithName("directory_podcasts_search");

        group.MapGet("/radio/search", (string? q) => Results.Ok(new
        {
            query = q ?? string.Empty,
            total = 0,
            items = Array.Empty<object>(),
            note = "Radio directory is scheduled for milestone M2."
        })).WithName("directory_radio_search");
    }
}
