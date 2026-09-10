using System.Security.Claims;
using DoradoCloud.Modules.Data;
using DoradoCloud.Shared;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Interactive account endpoints (register / login / logout), plus the OpenID
/// Connect authorization and token passthroughs.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapDoradoAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapAccountEndpoints(endpoints);
        MapAuthorizeEndpoint(endpoints);
        MapTokenEndpoint(endpoints);
        return endpoints;
    }

    // ---- Accounts --------------------------------------------------------

    private static void MapAccountEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/login", (string? returnUrl) => Results.Content(Page(
            "sign in",
            $"""
             <form method="post" action="/account/login">
               <input type="hidden" name="returnUrl" value="{Html(returnUrl ?? string.Empty)}" />
               <label>email<input type="email" name="email" autocomplete="username" required /></label>
               <label>password<input type="password" name="password" autocomplete="current-password" required /></label>
               <button type="submit">sign in</button>
             </form>
             <p><a href="/account/register">create an account</a></p>
             """, null))).WithName("login_page");

        endpoints.MapPost("/account/login", async (HttpContext context, AccountService accounts) =>
        {
            var form = await context.Request.ReadFormAsync();
            var returnUrl = form["returnUrl"].ToString();
            var account = await accounts.ValidateCredentialsAsync(form["email"].ToString(), form["password"].ToString());

            if (account is null)
            {
                return Results.Content(Page(
                    "sign in",
                    $"""
                     <form method="post" action="/account/login">
                       <input type="hidden" name="returnUrl" value="{Html(returnUrl)}" />
                       <label>email<input type="email" name="email" required /></label>
                       <label>password<input type="password" name="password" required /></label>
                       <button type="submit">sign in</button>
                     </form>
                     """,
                    "invalid email or password"), "text/html", statusCode: 401);
            }

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(account));
            return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
        }).WithName("login_submit");

        endpoints.MapGet("/account/register", () => Results.Content(Page(
            "create account",
            """
            <form method="post" action="/account/register">
              <label>display name<input type="text" name="displayName" autocomplete="nickname" /></label>
              <label>email<input type="email" name="email" autocomplete="username" required /></label>
              <label>password<input type="password" name="password" autocomplete="new-password" minlength="8" required /></label>
              <button type="submit">create account</button>
            </form>
            <p><a href="/account/login">already have an account?</a></p>
            """, null))).WithName("register_page");

        endpoints.MapPost("/account/register", async (HttpContext context, AccountService accounts) =>
        {
            var form = await context.Request.ReadFormAsync();
            var (account, error) = await accounts.RegisterAsync(
                form["email"].ToString(), form["password"].ToString(), form["displayName"].ToString());

            if (account is null)
            {
                return Results.Content(Page(
                    "create account",
                    $"""
                     <form method="post" action="/account/register">
                       <label>display name<input type="text" name="displayName" /></label>
                       <label>email<input type="email" name="email" required /></label>
                       <label>password<input type="password" name="password" minlength="8" required /></label>
                       <button type="submit">create account</button>
                     </form>
                     """,
                    error), "text/html", statusCode: 400);
            }

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(account));
            return Results.Redirect("/");
        }).WithName("register_submit");

        endpoints.MapPost("/account/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        }).WithName("logout");
    }

    // ---- OIDC authorization ---------------------------------------------

    private static void MapAuthorizeEndpoint(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/connect/authorize", async (HttpContext context) =>
        {
            var request = context.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            var cookie = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!cookie.Succeeded || cookie.Principal is null)
            {
                return Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = context.Request.Path + context.Request.QueryString
                    },
                    new[] { CookieAuthenticationDefaults.AuthenticationScheme });
            }

            if (cookie.Principal.GetAccountId() is not Guid accountId)
            {
                return Results.Forbid();
            }

            var accounts = context.RequestServices.GetRequiredService<AccountService>();
            var account = await accounts.FindByIdAsync(accountId);
            if (account is null)
            {
                return Results.Forbid();
            }

            var principal = BuildPrincipal(account, request.GetScopes());
            return Results.SignIn(
                principal,
                properties: new AuthenticationProperties(),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }).WithName("authorize");
    }

    // ---- OIDC token ------------------------------------------------------

    private static void MapTokenEndpoint(IEndpointRouteBuilder endpoints)
    {
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

                var identity = new ClaimsIdentity("DoradoService", OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
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
                    properties: null,
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.BadRequest(new { error = "unsupported_grant_type" });
        }).WithName("token");
    }

    // ---- helpers ---------------------------------------------------------

    private static ClaimsPrincipal BuildPrincipal(Account account, IEnumerable<string>? scopes = null)
    {
        var identity = new ClaimsIdentity(
            authenticationType: "DoradoUser",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, account.Id.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Email, account.Email);
        identity.SetClaim(OpenIddictConstants.Claims.Name, account.DisplayName);

        if (scopes is not null)
        {
            identity.SetScopes(scopes);
            identity.SetResources(DoradoCloudInfo.ApiResource);
        }

        identity.SetDestinations(claim => new[] { OpenIddictConstants.Destinations.AccessToken });
        return new ClaimsPrincipal(identity);
    }

    private static string Html(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");

    private static string Page(string title, string body, string? error) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>dorado cloud — {{title}}</title>
          <style>
            body { background:#0d0d0f; color:#fff; font:16px/1.5 'Segoe UI',system-ui,sans-serif; display:grid; place-items:center; min-height:100vh; margin:0; }
            main { width:min(360px,90vw); }
            h1 { font-weight:300; letter-spacing:2px; text-transform:lowercase; }
            label { display:block; margin:.6rem 0; color:#9aa0a6; font-size:.85rem; }
            input { width:100%; padding:.6rem; margin-top:.25rem; background:#161618; color:#fff; border:1px solid #2a2a2e; border-radius:0; }
            button { width:100%; padding:.7rem; margin-top:.6rem; background:linear-gradient(90deg,#e91e63,#ff5722,#ffc107); color:#0d0d0f; font-weight:700; border:0; cursor:pointer; }
            a { color:#ffc107; }
            .err { color:#ff6b6b; }
          </style>
        </head>
        <body><main>
          <h1>{{title}}</h1>
          {{(error is null ? string.Empty : $"<p class=\"err\">{Html(error)}</p>")}}
          {{body}}
        </main></body>
        </html>
        """;
}
