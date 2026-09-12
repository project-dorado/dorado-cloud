using System.Net;
using System.Text.RegularExpressions;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Tests;

/// <summary>
/// End-to-end email verification and password reset against the in-memory email
/// sink: reuse, expiry, unknown users, and email-verification enforcement.
/// </summary>
public sealed class AccountEmailFlowTests(EmailCapturingFactory factory) : IClassFixture<EmailCapturingFactory>
{
    private static readonly Regex LinkPattern = new("href=\"([^\"]+)\"", RegexOptions.Compiled);

    private HttpClient Client()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static Uri EmailedLink(SentEmail email)
        => new(LinkPattern.Match(email.Body).Groups[1].Value);

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await AuthTestHelper.AntiforgeryTokenAsync(client, path);
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_account_stays_generic_and_sends_nothing()
    {
        var client = Client();
        var before = factory.Sender.Sent.Count;

        var response = await PostFormAsync(client, "/account/forgot-password",
            new Dictionary<string, string> { ["email"] = AuthTestHelper.NewEmail() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("If that account exists", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, factory.Sender.Sent.Count);
    }

    [Fact]
    public async Task Password_reset_link_is_single_use_and_changes_the_password()
    {
        var client = Client();
        var email = AuthTestHelper.NewEmail();
        Assert.Equal(HttpStatusCode.Found, (await AuthTestHelper.RegisterAsync(client, email)).StatusCode);

        var forgot = await PostFormAsync(client, "/account/forgot-password",
            new Dictionary<string, string> { ["email"] = email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        var mail = factory.Sender.LastTo(email);
        Assert.NotNull(mail);
        Assert.Contains("Reset", mail!.Subject);
        var token = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(EmailedLink(mail).Query)["token"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var firstReset = await PostFormAsync(client, "/account/reset-password",
            new Dictionary<string, string> { ["password"] = "brand-new-pass", ["token"] = token });
        Assert.Equal(HttpStatusCode.OK, firstReset.StatusCode);

        // The old password is gone; the new one works.
        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthTestHelper.LoginAsync(client, email, "password123")).StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await AuthTestHelper.LoginAsync(client, email, "brand-new-pass")).StatusCode);

        // The same token cannot be replayed.
        var replay = await PostFormAsync(client, "/account/reset-password",
            new Dictionary<string, string> { ["password"] = "another-pass-1", ["token"] = token });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task Expired_reset_token_is_rejected()
    {
        var client = Client();
        var email = AuthTestHelper.NewEmail();
        Assert.Equal(HttpStatusCode.Found, (await AuthTestHelper.RegisterAsync(client, email)).StatusCode);

        await PostFormAsync(client, "/account/forgot-password",
            new Dictionary<string, string> { ["email"] = email });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DoradoDbContext>();
            var row = await db.AccountTokens.SingleAsync(t => t.Purpose == AccountTokenService.PurposePasswordReset);
            row.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var mail = factory.Sender.LastTo(email)!;
        var token = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(EmailedLink(mail).Query)["token"].ToString();

        var reset = await PostFormAsync(client, "/account/reset-password",
            new Dictionary<string, string> { ["password"] = "brand-new-pass", ["token"] = token });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task Verification_link_is_single_use_and_marks_the_email_verified()
    {
        var client = Client();
        var email = AuthTestHelper.NewEmail();
        Assert.Equal(HttpStatusCode.Found, (await AuthTestHelper.RegisterAsync(client, email)).StatusCode);

        var mail = factory.Sender.LastTo(email);
        Assert.NotNull(mail);
        Assert.Contains("Verify", mail!.Subject);
        var link = EmailedLink(mail);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(link.PathAndQuery)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DoradoDbContext>();
            var account = await db.Accounts.SingleAsync(a => a.Email == email);
            Assert.True(account.EmailVerified);
        }

        // Single use: following the link again is rejected.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(link.PathAndQuery)).StatusCode);
    }
}

/// <summary>Login/OIDC behavior when <c>Identity:Security:RequireEmailVerification</c> is on.</summary>
public sealed class RequireEmailVerificationTests(RequireEmailVerificationFactory factory)
    : IClassFixture<RequireEmailVerificationFactory>
{
    private static readonly Regex LinkPattern = new("href=\"([^\"]+)\"", RegexOptions.Compiled);

    private HttpClient Client()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Unverified_accounts_cannot_sign_in_until_they_verify()
    {
        var client = Client();
        var email = AuthTestHelper.NewEmail();

        var register = await AuthTestHelper.RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.Contains("verify", await register.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.Unauthorized, (await AuthTestHelper.LoginAsync(client, email)).StatusCode);

        var mail = factory.Sender.LastTo(email);
        Assert.NotNull(mail);
        var link = new Uri(LinkPattern.Match(mail!.Body).Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(link.PathAndQuery)).StatusCode);

        Assert.Equal(HttpStatusCode.Found, (await AuthTestHelper.LoginAsync(client, email)).StatusCode);
    }

    [Fact]
    public async Task Registration_defers_the_session_until_verification()
    {
        var client = Client();
        var email = AuthTestHelper.NewEmail();

        await AuthTestHelper.RegisterAsync(client, email);

        // No cookie session was established, so OIDC authorization prompts for login.
        var authorize = await client.GetAsync(AuthTestHelper.AuthorizeUrl());
        Assert.Equal(HttpStatusCode.Found, authorize.StatusCode);
        Assert.Contains("/account/login", authorize.Headers.Location!.ToString());
    }
}
