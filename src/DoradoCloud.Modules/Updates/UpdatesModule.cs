using DoradoCloud.Modules.Abstractions;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Updates;

/// <summary>
/// Signed application-update manifests for the Dorado and Dorado-HD clients.
/// Publishing requires the <c>UpdatesAdmin</c> policy; the feed and the public
/// verification key are open.
/// </summary>
public sealed class UpdatesModule : EndpointModuleBase
{
    public override string Name => "updates";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/signing-key", (UpdateSigningKeyProvider keys) =>
            Results.Text(keys.GetPublicKeyPem(), "application/x-pem-file"))
            .WithName("updates_signing_key");

        group.MapGet("/{app}/{channel}", async (
            string app,
            string channel,
            ReleaseService releases,
            CancellationToken cancellationToken) =>
        {
            var release = await releases.GetLatestAsync(app, channel, cancellationToken);
            return Results.Ok(new UpdateCheckResponse(
                app,
                channel,
                release is not null,
                release,
                release is null ? "No release published for this app/channel." : null));
        }).WithName("updates_check");

        group.MapPost("/publish", async (
            PublishReleaseRequest request,
            ReleaseService releases,
            CancellationToken cancellationToken) =>
        {
            var release = await releases.PublishAsync(request, cancellationToken);
            return Results.Ok(release);
        })
        .RequireAuthorization("UpdatesAdmin")
        .WithName("updates_publish");
    }
}
