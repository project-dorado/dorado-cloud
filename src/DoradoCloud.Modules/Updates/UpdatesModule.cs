using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Updates;

/// <summary>
/// Signed application-update manifests for the Dorado and Dorado-HD clients.
/// M0 serves an empty/not-found manifest; signing and release ingestion land in
/// M1.
/// </summary>
public sealed class UpdatesModule : EndpointModuleBase
{
    public override string Name => "updates";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/{app}/{channel}", (string app, string channel) => Results.Ok(new
        {
            app,
            channel,
            available = false,
            manifest = (UpdateManifest?)null,
            note = "Update feed is scheduled for milestone M1."
        })).WithName("updates_check");
    }
}
