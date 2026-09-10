using System.Security.Claims;
using DoradoCloud.Shared;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// The OpenID Connect authorization and token endpoint passthroughs.
/// The authorize endpoint auto-approves a demo principal (optionally via
/// <c>?email=</c>) so the authorization-code + PKCE flow is exercisable
/// end-to-end; M1 replaces the auto-approval with a real login and consent
/// screen. The token endpoint handles client credentials (service-to-service,
/// used by CI/smoke tests) and the code/refresh exchanges.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapDoradoAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/authorize", (HttpContext context) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            var email = context.Request.Query["email"].ToString();
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "demo@dorado.local";
            }

            var identity = new ClaimsIdentity(
                authenticationType: "DoradoUser",
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            identity.SetClaim(OpenIddictConstants.Claims.Subject, email);
            identity.SetClaim(OpenIddictConstants.Claims.Email, email);
            identity.SetClaim(OpenIddictConstants.Claims.Name, email);
            identity.SetScopes(request.GetScopes());
            identity.SetResources(DoradoCloudInfo.ApiResource);
            identity.SetDestinations(claim => new[] { OpenIddictConstants.Destinations.AccessToken });

            var principal = new ClaimsPrincipal(identity);

            return Results.SignIn(
                principal,
                properties: new AuthenticationProperties(),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }).WithName("authorize");

        endpoints.MapPost("/connect/token", async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (request.IsClientCredentialsGrantType())
            {
                var application = await applicationManager.FindByClientIdAsync(request.ClientId!)
                    ?? throw new InvalidOperationException("The client application cannot be found.");

                var identity = new ClaimsIdentity(
                    authenticationType: "DoradoService",
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);

                identity.SetClaim(OpenIddictConstants.Claims.Subject, await applicationManager.GetClientIdAsync(application));
                identity.SetClaim(OpenIddictConstants.Claims.Name, await applicationManager.GetDisplayNameAsync(application));
                identity.SetScopes(request.GetScopes());
                identity.SetResources(DoradoCloudInfo.ApiResource);
                identity.SetDestinations(claim => new[] { OpenIddictConstants.Destinations.AccessToken });

                return Results.SignIn(
                    new ClaimsPrincipal(identity),
                    properties: null,
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var principal = result.Principal
                    ?? throw new InvalidOperationException("The token cannot be retrieved.");

                principal.SetScopes(request.GetScopes());
                principal.SetResources(DoradoCloudInfo.ApiResource);

                return Results.SignIn(
                    principal,
                    properties: result.Properties,
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.BadRequest(new { error = "unsupported_grant_type" });
        }).WithName("token");

        return endpoints;
    }
}
