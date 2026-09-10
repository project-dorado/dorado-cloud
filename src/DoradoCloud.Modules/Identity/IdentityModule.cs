using System.Security.Claims;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;

namespace DoradoCloud.Modules.Identity;

/// <summary>Identity liveness plus the current-principal endpoint.</summary>
public sealed class IdentityModule : EndpointModuleBase
{
    public override string Name => "identity";

    protected override void MapV1(RouteGroupBuilder group)
    {
        group.MapGet("/ping", () => Ping(Name));

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            subject = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
                      ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            name = user.Identity?.Name,
            email = user.FindFirst(OpenIddictConstants.Claims.Email)?.Value,
            claims = user.Claims.Select(c => new { c.Type, c.Value }).ToArray()
        })).RequireAuthorization();
    }
}
