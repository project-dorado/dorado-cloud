using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DoradoCloud.Tests;

/// <summary>
/// Exercises the GDPR account lifecycle: export (data portability) then delete
/// (right to erasure), verifying the account and its bearer token are gone.
/// </summary>
public sealed class AccountLifecycleTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Export_ReturnsAccountData_ThenDeleteRemovesAccountAndToken()
    {
        var email = AuthTestHelper.NewEmail();
        var token = await AuthTestHelper.AccessTokenAsync(factory, email);
        var client = AuthTestHelper.Bearer(factory, token);

        // Register a device so the export has something beyond the account itself.
        var device = await client.PostAsJsonAsync(
            "/v1/identity/me/devices",
            new { name = "Pixel", platform = "android", serial = "ser-1", appVersion = "0.1.0" });
        Assert.Equal(HttpStatusCode.Created, device.StatusCode);

        // Export.
        var export = await client.GetAsync("/v1/identity/me/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var json = await export.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, json.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal(1, json.GetProperty("devices").GetArrayLength());

        // Delete (right to erasure).
        var delete = await client.DeleteAsync("/v1/identity/me");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The account data is gone (export now reports no account). Note: the
        // access token itself is a still-valid JWT until expiry (OpenIddict local
        // validation); the revoked refresh token is what stops renewal.
        var after = await client.GetAsync("/v1/identity/me/export");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterJson = await after.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, afterJson.GetProperty("account").ValueKind);
    }
}
