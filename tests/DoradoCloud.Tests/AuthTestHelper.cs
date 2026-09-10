using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace DoradoCloud.Tests;

/// <summary>Drives the full account + PKCE flow against the in-memory host.</summary>
internal static class AuthTestHelper
{
    public const string DesktopRedirect = "http://127.0.0.1:7890/callback";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@dorado.local";

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Registers (or logs in) a user, then runs authorization-code + PKCE to obtain an access token.</summary>
    public static async Task<string> AccessTokenAsync(CloudApiFactory factory, string email, string password = "password123")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var register = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["displayName"] = "Test User"
        }));

        if (register.StatusCode != System.Net.HttpStatusCode.Found)
        {
            var login = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["email"] = email,
                ["password"] = password
            }));
            login.EnsureSuccessStatusCode();
        }

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "dorado-desktop",
            ["response_type"] = "code",
            ["scope"] = "openid profile email dorado.api offline_access",
            ["redirect_uri"] = DesktopRedirect,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });

        var authorize = await client.GetAsync(authorizeUrl);
        var code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].ToString();

        var token = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "dorado-desktop",
            ["code"] = code,
            ["redirect_uri"] = DesktopRedirect,
            ["code_verifier"] = verifier
        }));
        token.EnsureSuccessStatusCode();

        var json = await token.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    public static HttpClient Bearer(CloudApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static string NewHandle(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..16];
}
