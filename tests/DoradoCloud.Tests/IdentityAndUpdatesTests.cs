using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class IdentityAndUpdatesTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private const string DesktopRedirect = "http://127.0.0.1:7890/callback";

    // ---- helpers ---------------------------------------------------------

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@dorado.local";

    private async Task<HttpClient> RegisterAndSignInAsync(HttpClient client, string email, string password = "password123")
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["displayName"] = "Test User",
            ["__RequestVerificationToken"] = await AuthTestHelper.AntiforgeryTokenAsync(client, "/account/register")
        });

        var response = await client.PostAsync("/account/register", form);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return client;
    }

    private static async Task<string> ClientCredentialsTokenAsync(CloudApiFactory factory)
    {
        var client = factory.CreateClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "dorado-cloud-smoke",
            ["client_secret"] = "dev-secret",
            ["scope"] = "dorado.api"
        });

        var response = await client.PostAsync("/connect/token", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    // ---- full authorization-code + PKCE flow -----------------------------

    [Fact]
    public async Task Authorization_code_flow_signs_a_user_in()
    {
        var email = NewEmail();

        // Exercises register + antiforgery + consent + authorization-code + PKCE.
        var accessToken = await AuthTestHelper.AccessTokenAsync(factory, email);

        using var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var me = await authed.GetFromJsonAsync<JsonElement>("/v1/identity/me");
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(me.GetProperty("accountId").GetString()));
    }

    [Fact]
    public async Task Authorize_without_a_session_challenges_to_login()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "dorado-desktop",
            ["response_type"] = "code",
            ["scope"] = "openid dorado.api",
            ["redirect_uri"] = DesktopRedirect,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });

        var response = await client.GetAsync(authorizeUrl);

        // A challenge to the cookie scheme redirects an interactive user to the login page.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location!.ToString());
    }

    // ---- device registry -------------------------------------------------

    [Fact]
    public async Task Devices_can_be_registered_listed_and_removed()
    {
        var accessToken = await AccessTokenForNewUserAsync();
        using var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var register = await authed.PostAsJsonAsync("/v1/identity/me/devices",
            new RegisterDeviceRequest("Workstation", "windows", "SN-1", "1.1.0"));
        register.EnsureSuccessStatusCode();
        var device = await register.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.NotNull(device);

        var list = await authed.GetFromJsonAsync<List<DeviceDto>>("/v1/identity/me/devices");
        Assert.Contains(list!, d => d.Id == device!.Id);

        var delete = await authed.DeleteAsync($"/v1/identity/me/devices/{device!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var after = await authed.GetFromJsonAsync<List<DeviceDto>>("/v1/identity/me/devices");
        Assert.DoesNotContain(after!, d => d.Id == device.Id);
    }

    // ---- settings sync ---------------------------------------------------

    [Fact]
    public async Task Settings_use_optimistic_concurrency()
    {
        var accessToken = await AccessTokenForNewUserAsync();
        using var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var create = await authed.PutAsJsonAsync("/v1/identity/me/settings",
            new PutSettingsRequest("{\"theme\":\"dark\"}", null));
        create.EnsureSuccessStatusCode();
        var v1 = await create.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(1, v1!.Version);

        var update = await authed.PutAsJsonAsync("/v1/identity/me/settings",
            new PutSettingsRequest("{\"theme\":\"light\"}", 1));
        update.EnsureSuccessStatusCode();
        var v2 = await update.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(2, v2!.Version);
        Assert.Contains("light", v2.PayloadJson);

        var stale = await authed.PutAsJsonAsync("/v1/identity/me/settings",
            new PutSettingsRequest("{\"theme\":\"stale\"}", 1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    // ---- signed update feed ---------------------------------------------

    [Fact]
    public async Task Published_releases_are_served_and_signed()
    {
        var token = await ClientCredentialsTokenAsync(factory);
        using var publisher = factory.CreateClient();
        publisher.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var publish = await publisher.PostAsJsonAsync("/v1/updates/publish",
            new PublishReleaseRequest("dorado", "stable", "1.2.0",
                "https://example.invalid/dorado/Dorado-1.2.0.zip", "deadbeef", "test release"));
        publish.EnsureSuccessStatusCode();
        var release = await publish.Content.ReadFromJsonAsync<UpdateReleaseDto>();
        Assert.NotNull(release);
        Assert.False(string.IsNullOrWhiteSpace(release!.Signature));

        using var client = factory.CreateClient();
        var check = await client.GetFromJsonAsync<UpdateCheckResponse>("/v1/updates/dorado/stable");
        Assert.True(check!.Available);

        var publicKeyPem = await client.GetStringAsync("/v1/updates/signing-key");
        var manifest = new UpdateManifest(
            release.App, release.Channel, release.Version, release.Url,
            release.Sha256, release.PublishedAt, release.Notes);

        Assert.True(UpdateManifestCrypto.Verify(manifest, release.Signature, publicKeyPem),
            "the served release signature must verify against the published signing key");
    }

    private async Task<string> AccessTokenForNewUserAsync()
        => await AuthTestHelper.AccessTokenAsync(factory, NewEmail());
}
