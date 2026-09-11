using System.Net;
using DoradoCloud.Modules.Data;
using DoradoCloud.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace DoradoCloud.Tests.Legacy;

/// <summary>Unit tests for account tokens and consent grants (M11).</summary>
public sealed class AccountSecurityServiceTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "dorado-security-" + Guid.NewGuid().ToString("N") + ".db");

    private DoradoDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DoradoDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        var db = new DoradoDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Account_token_is_single_use()
    {
        using var db = NewContext();
        var service = new AccountTokenService(db);
        var accountId = Guid.NewGuid();

        var token = await service.IssueAsync(accountId, AccountTokenService.PurposePasswordReset, TimeSpan.FromHours(1), CancellationToken.None);

        Assert.Equal(accountId, await service.ConsumeAsync(token, AccountTokenService.PurposePasswordReset, CancellationToken.None));
        Assert.Null(await service.ConsumeAsync(token, AccountTokenService.PurposePasswordReset, CancellationToken.None));
        Assert.Null(await service.ConsumeAsync(token, AccountTokenService.PurposeEmailVerification, CancellationToken.None));
    }

    [Fact]
    public async Task Expired_account_token_is_rejected()
    {
        using var db = NewContext();
        var service = new AccountTokenService(db);
        var token = await service.IssueAsync(Guid.NewGuid(), AccountTokenService.PurposeEmailVerification, TimeSpan.FromHours(1), CancellationToken.None);

        var row = await db.AccountTokens.SingleAsync();
        row.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        Assert.Null(await service.ConsumeAsync(token, AccountTokenService.PurposeEmailVerification, CancellationToken.None));
    }

    [Fact]
    public async Task Consent_is_required_until_granted()
    {
        using var db = NewContext();
        var service = new ConsentService(db);
        var accountId = Guid.NewGuid();
        var scopes = new[] { "openid", "dorado.api" };

        Assert.False(await service.HasConsentAsync(accountId, "dorado-desktop", scopes, CancellationToken.None));

        await service.GrantAsync(accountId, "dorado-desktop", scopes, CancellationToken.None);
        Assert.True(await service.HasConsentAsync(accountId, "dorado-desktop", scopes, CancellationToken.None));

        // A different scope set requires fresh consent.
        Assert.False(await service.HasConsentAsync(accountId, "dorado-desktop", new[] { "openid", "offline_access" }, CancellationToken.None));
        Assert.Equal("dorado.api openid", ConsentService.NormalizeScopes(scopes));
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            try
            {
                var path = _dbPath + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }
}

/// <summary>HTTP-level hardening checks (antiforgery + open-redirect).</summary>
public sealed class IdentitySecurityEndpointTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Login_page_includes_an_antiforgery_field()
    {
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/account/login");

        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task Login_without_antiforgery_is_rejected()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = "nobody@dorado.local",
            ["password"] = "password123"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_an_external_return_url()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = AuthTestHelper.NewEmail();

        var register = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = "password123",
            ["displayName"] = "Test User",
            ["__RequestVerificationToken"] = await AuthTestHelper.AntiforgeryTokenAsync(client, "/account/register")
        }));
        Assert.Equal(HttpStatusCode.Found, register.StatusCode);

        var login = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = "password123",
            ["returnUrl"] = "https://evil.example/steal",
            ["__RequestVerificationToken"] = await AuthTestHelper.AntiforgeryTokenAsync(client, "/account/login")
        }));

        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Equal("/", login.Headers.Location!.OriginalString);
    }
}
