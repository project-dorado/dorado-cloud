using System.Security.Claims;
using DoradoCloud.Modules.Data;
using DoradoCloud.Shared;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace DoradoCloud.Modules.Identity;

/// <summary>
/// Interactive account endpoints (register / login / logout / consent / email
/// verification / password reset), plus the OpenID Connect authorization and
/// token passthroughs. All HTML POST forms are antiforgery-protected.
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
        endpoints.MapGet("/account/login", (HttpContext context, IAntiforgery antiforgery, string? returnUrl) =>
            Page("sign in", LoginForm(context, antiforgery, returnUrl))).WithName("login_page");

        endpoints.MapPost("/account/login", async (
            HttpContext context, AccountService accounts, IAntiforgery antiforgery) =>
        {
            var form = await context.Request.ReadFormAsync();
            var returnUrl = SafeLocalUrl(form["returnUrl"].ToString());

            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return BadRequest("sign in", LoginForm(context, antiforgery, returnUrl), "invalid request");
            }

            var account = await accounts.ValidateCredentialsAsync(form["email"].ToString(), form["password"].ToString());
            if (account is null)
            {
                return Results.Content(
                    Render("sign in", LoginForm(context, antiforgery, returnUrl), "invalid email or password"),
                    "text/html", statusCode: 401);
            }

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(account));
            return Results.Redirect(returnUrl);
        }).WithName("login_submit");

        endpoints.MapGet("/account/register", (HttpContext context, IAntiforgery antiforgery) =>
            Page("create account", RegisterForm(context, antiforgery))).WithName("register_page");

        endpoints.MapPost("/account/register", async (
            HttpContext context,
            AccountService accounts,
            AccountTokenService tokens,
            IEmailSender email,
            IOptions<IdentitySecurityOptions> security,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return BadRequest("create account", RegisterForm(context, antiforgery), "invalid request");
            }

            var form = await context.Request.ReadFormAsync();
            var (account, error) = await accounts.RegisterAsync(
                form["email"].ToString(), form["password"].ToString(), form["displayName"].ToString());

            if (account is null)
            {
                return BadRequest("create account", RegisterForm(context, antiforgery), error);
            }

            await SendVerificationEmailAsync(account, tokens, email, security.Value, cancellationToken);

            if (security.Value.RequireEmailVerification)
            {
                // No cookie session until the emailed link is followed; otherwise
                // registration would bypass the sign-in verification gate.
                return Page("create account",
                    "<p>Check your inbox to verify your email address, then <a href=\"/account/login\">sign in</a>.</p>");
            }

            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, BuildPrincipal(account));
            return Results.Redirect("/");
        }).WithName("register_submit");

        endpoints.MapGet("/account/verify-email", async (
            string? token, AccountTokenService tokens, AccountService accounts, CancellationToken cancellationToken) =>
        {
            var accountId = await tokens.ConsumeAsync(token, AccountTokenService.PurposeEmailVerification, cancellationToken);
            if (accountId is null)
            {
                return Results.Content(
                    Render("verify email", "<p class=\"err\">This verification link is invalid or has expired.</p>", null),
                    "text/html", statusCode: 400);
            }

            await accounts.MarkEmailVerifiedAsync(accountId.Value, cancellationToken);
            return Page("verify email", "<p>Your email is verified. You can close this page and return to the app.</p>");
        }).WithName("verify_email");

        endpoints.MapGet("/account/forgot-password", (HttpContext context, IAntiforgery antiforgery) =>
            Page("reset password", ForgotPasswordForm(context, antiforgery))).WithName("forgot_password_page");

        endpoints.MapPost("/account/forgot-password", async (
            HttpContext context,
            AccountService accounts,
            AccountTokenService tokens,
            IEmailSender email,
            IOptions<IdentitySecurityOptions> security,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return BadRequest("reset password", ForgotPasswordForm(context, antiforgery), "invalid request");
            }

            var form = await context.Request.ReadFormAsync();
            var account = await accounts.FindByEmailAsync(form["email"].ToString(), cancellationToken);
            if (account is not null)
            {
                var token = await tokens.IssueAsync(
                    account.Id, AccountTokenService.PurposePasswordReset,
                    TimeSpan.FromHours(security.Value.PasswordResetTtlHours), cancellationToken);
                var link = $"{security.Value.PublicBaseUrl.TrimEnd('/')}/account/reset-password?token={Uri.EscapeDataString(token)}";
                await email.SendAsync(
                    account.Email,
                    "Reset your Dorado Cloud password",
                    $"<p>Use this link to reset your password: <a href=\"{link}\">{link}</a></p>",
                    cancellationToken);
            }

            // Never reveal whether the account exists.
            return Page("reset password", "<p>If that account exists, a reset link has been sent.</p>");
        }).WithName("forgot_password_submit");

        endpoints.MapGet("/account/reset-password", (HttpContext context, IAntiforgery antiforgery, string? token) =>
            Page("reset password", ResetPasswordForm(context, antiforgery, token ?? string.Empty))).WithName("reset_password_page");

        endpoints.MapPost("/account/reset-password", async (
            HttpContext context, AccountService accounts, AccountTokenService tokens, IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return BadRequest("reset password", ResetPasswordForm(context, antiforgery, string.Empty), "invalid request");
            }

            var form = await context.Request.ReadFormAsync();
            var password = form["password"].ToString();
            var token = form["token"].ToString();

            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            {
                return BadRequest("reset password", ResetPasswordForm(context, antiforgery, token), "password must be at least 8 characters");
            }

            var accountId = await tokens.ConsumeAsync(token, AccountTokenService.PurposePasswordReset, cancellationToken);
            if (accountId is null || !await accounts.SetPasswordAsync(accountId.Value, password, cancellationToken))
            {
                return BadRequest("reset password", ResetPasswordForm(context, antiforgery, token), "this reset link is invalid or has expired");
            }

            return Page("reset password", "<p>Your password has been updated. <a href=\"/account/login\">Sign in</a>.</p>");
        }).WithName("reset_password_submit");

        endpoints.MapPost("/account/consent", async (
            HttpContext context,
            ConsentService consent,
            IOpenIddictApplicationManager applications,
            IAntiforgery antiforgery,
            CancellationToken cancellationToken) =>
        {
            if (!await ValidAntiforgeryAsync(context, antiforgery))
            {
                return BadRequest("authorize", "<p class=\"err\">invalid request</p>", null);
            }

            var form = await context.Request.ReadFormAsync();
            var returnUrl = SafeLocalUrl(form["returnUrl"].ToString());

            var cookie = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!cookie.Succeeded || cookie.Principal?.GetAccountId() is not Guid accountId)
            {
                return Results.Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl },
                    new[] { CookieAuthenticationDefaults.AuthenticationScheme });
            }

            if (!string.Equals(form["allow"].ToString(), "true", StringComparison.OrdinalIgnoreCase))
            {
                return await DenyConsentAsync(context, form, applications, cancellationToken);
            }

            var scopes = form["scope"].ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            await consent.GrantAsync(accountId, form["clientId"].ToString(), scopes, cancellationToken);
            return Results.Redirect(returnUrl);
        }).WithName("consent_submit");

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

            // Defense in depth: a cookie session created before verification was
            // required must not mint OIDC tokens for an unverified email.
            var security = context.RequestServices.GetRequiredService<IOptions<IdentitySecurityOptions>>().Value;
            if (security.RequireEmailVerification && !account.EmailVerified)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.AccessDenied,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The email address for this account has not been verified."
                    }),
                    new[] { OpenIddictServerAspNetCoreDefaults.AuthenticationScheme });
            }

            // Require explicit consent before issuing tokens for these scopes.
            var consent = context.RequestServices.GetRequiredService<ConsentService>();
            var scopes = request.GetScopes();
            var clientId = request.ClientId ?? "unknown";

            if (!await consent.HasConsentAsync(accountId, clientId, scopes, context.RequestAborted))
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                var returnUrl = context.Request.Path + context.Request.QueryString;
                return Page("authorize", ConsentForm(context, antiforgery, clientId, scopes, returnUrl));
            }

            var principal = BuildPrincipal(account, scopes);
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

    private static async Task SendVerificationEmailAsync(
        Account account,
        AccountTokenService tokens,
        IEmailSender email,
        IdentitySecurityOptions security,
        CancellationToken cancellationToken)
    {
        var token = await tokens.IssueAsync(
            account.Id, AccountTokenService.PurposeEmailVerification,
            TimeSpan.FromHours(security.EmailVerificationTtlHours), cancellationToken);
        var link = $"{security.PublicBaseUrl.TrimEnd('/')}/account/verify-email?token={Uri.EscapeDataString(token)}";
        await email.SendAsync(
            account.Email,
            "Verify your Dorado Cloud email",
            $"<p>Confirm your email address: <a href=\"{link}\">{link}</a></p>",
            cancellationToken);
    }

    private static async Task<IResult> DenyConsentAsync(
        HttpContext context,
        IFormCollection form,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken)
    {
        // Denial is an OIDC error response: redirect back to the client with
        // error=access_denied. The destination must be a redirect URI actually
        // registered for the requesting client (no open redirect).
        var returnUrl = SafeLocalUrl(form["returnUrl"].ToString());
        var query = QueryHelpers.ParseQuery(new Uri("http://localhost" + returnUrl).Query);
        var redirectUri = query.TryGetValue("redirect_uri", out var value) ? value.ToString() : null;
        var state = query.TryGetValue("state", out var stateValue) ? stateValue.ToString() : null;
        var clientId = form["clientId"].ToString();

        if (!string.IsNullOrWhiteSpace(redirectUri) && !string.IsNullOrWhiteSpace(clientId))
        {
            var application = await applications.FindByClientIdAsync(clientId, cancellationToken);
            if (application is not null)
            {
                var registered = await applications.GetRedirectUrisAsync(application, cancellationToken);
                if (registered.Contains(redirectUri, StringComparer.Ordinal))
                {
                    var parameters = new Dictionary<string, string?>
                    {
                        ["error"] = OpenIddictConstants.Errors.AccessDenied
                    };
                    if (!string.IsNullOrEmpty(state))
                    {
                        parameters["state"] = state;
                    }

                    return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, parameters));
                }
            }
        }

        // Unverifiable client/redirect: fail closed with the local denial page.
        return Results.Content(
            Render("authorize", "<p class=\"err\">Access denied.</p>", null), "text/html", statusCode: 403);
    }

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

    private static IResult Page(string title, string body)
        => Results.Content(Render(title, body, null), "text/html");

    private static IResult BadRequest(string title, string body, string? error)
        => Results.Content(Render(title, body, error), "text/html", statusCode: 400);

    private static async Task<bool> ValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static string Field(HttpContext context, IAntiforgery antiforgery)
        => $"<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"{Html(antiforgery.GetAndStoreTokens(context).RequestToken!)}\" />";

    private static string LoginForm(HttpContext context, IAntiforgery antiforgery, string? returnUrl) => $"""
        <form method="post" action="/account/login">
          {Field(context, antiforgery)}
          <input type="hidden" name="returnUrl" value="{Html(returnUrl ?? string.Empty)}" />
          <label>email<input type="email" name="email" autocomplete="username" required /></label>
          <label>password<input type="password" name="password" autocomplete="current-password" required /></label>
          <button type="submit">sign in</button>
        </form>
        <p><a href="/account/register">create an account</a> · <a href="/account/forgot-password">forgot password?</a></p>
        """;

    private static string RegisterForm(HttpContext context, IAntiforgery antiforgery) => $"""
        <form method="post" action="/account/register">
          {Field(context, antiforgery)}
          <label>display name<input type="text" name="displayName" autocomplete="nickname" /></label>
          <label>email<input type="email" name="email" autocomplete="username" required /></label>
          <label>password<input type="password" name="password" autocomplete="new-password" minlength="8" required /></label>
          <button type="submit">create account</button>
        </form>
        <p><a href="/account/login">already have an account?</a></p>
        """;

    private static string ForgotPasswordForm(HttpContext context, IAntiforgery antiforgery) => $"""
        <form method="post" action="/account/forgot-password">
          {Field(context, antiforgery)}
          <label>email<input type="email" name="email" autocomplete="username" required /></label>
          <button type="submit">send reset link</button>
        </form>
        """;

    private static string ResetPasswordForm(HttpContext context, IAntiforgery antiforgery, string token) => $"""
        <form method="post" action="/account/reset-password">
          {Field(context, antiforgery)}
          <input type="hidden" name="token" value="{Html(token)}" />
          <label>new password<input type="password" name="password" autocomplete="new-password" minlength="8" required /></label>
          <button type="submit">set new password</button>
        </form>
        """;

    private static string ConsentForm(
        HttpContext context,
        IAntiforgery antiforgery,
        string clientId,
        IEnumerable<string> scopes,
        string returnUrl)
    {
        var scopeList = string.Join(' ', scopes);
        return $"""
        <p><strong>{Html(clientId)}</strong> is requesting access to: {Html(string.Join(", ", scopes))}</p>
        <form method="post" action="/account/consent">
          {Field(context, antiforgery)}
          <input type="hidden" name="returnUrl" value="{Html(returnUrl)}" />
          <input type="hidden" name="clientId" value="{Html(clientId)}" />
          <input type="hidden" name="scope" value="{Html(scopeList)}" />
          <button type="submit" name="allow" value="true">allow</button>
          <button type="submit" name="allow" value="false">deny</button>
        </form>
        """;
    }

    /// <summary>Only allows same-site relative redirects (prevents open redirects).</summary>
    private static string SafeLocalUrl(string? url)
        => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") ? url : "/";

    private static string Html(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace("\"", "&quot;");

    private static string Render(string title, string body, string? error) => $$"""
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
