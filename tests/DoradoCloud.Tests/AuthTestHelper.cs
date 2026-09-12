using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace DoradoCloud.Tests;

/// <summary>Drives the full account + antiforgery + consent + PKCE flow against the in-memory host.</summary>
internal static class AuthTestHelper
{
    public const string DesktopRedirect = "http://127.0.0.1:7890/callback";

    private const string DesktopScopes = "openid profile email dorado.api offline_access";

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@dorado.local";

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static readonly Regex AntiforgeryPattern =
        new("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>Extracts the antiforgery field from a rendered HTML page.</summary>
    public static string TokenFromHtml(string html)
    {
        var match = AntiforgeryPattern.Match(html);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    /// <summary>Fetches a page and extracts its antiforgery token.</summary>
    public static async Task<string> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return TokenFromHtml(html);
    }

    /// <summary>Builds an authorization-code + PKCE URL for the desktop client.</summary>
    public static string AuthorizeUrl(string state = "state-1", string scopes = "openid dorado.api")
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        return QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "dorado-desktop",
            ["response_type"] = "code",
            ["scope"] = scopes,
            ["redirect_uri"] = DesktopRedirect,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });
    }

    /// <summary>Registers a user through the antiforgery-protected HTML form.</summary>
    public static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password = "password123",
        string displayName = "Test User")
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["displayName"] = displayName,
            ["__RequestVerificationToken"] = await AntiforgeryTokenAsync(client, "/account/register")
        });

        return await client.PostAsync("/account/register", form);
    }

    /// <summary>Signs in through the antiforgery-protected HTML form.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = "password123")
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["__RequestVerificationToken"] = await AntiforgeryTokenAsync(client, "/account/login")
        });

        return await client.PostAsync("/account/login", form);
    }

    /// <summary>Registers (or logs in) a user, consents, then runs authorization-code + PKCE.</summary>
    public static async Task<string> AccessTokenAsync(CloudApiFactory factory, string email, string password = "password123")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var register = await RegisterAsync(client, email, password);

        if (register.StatusCode != HttpStatusCode.Found)
        {
            (await LoginAsync(client, email, password)).EnsureSuccessStatusCode();
        }

        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "dorado-desktop",
            ["response_type"] = "code",
            ["scope"] = DesktopScopes,
            ["redirect_uri"] = DesktopRedirect,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        });

        var authorize = await client.GetAsync(authorizeUrl);
        string code;

        if (authorize.StatusCode == HttpStatusCode.OK)
        {
            // First-time scope consent: submit the consent page, then re-run authorize.
            var consentHtml = await authorize.Content.ReadAsStringAsync();
            var consentForm = new Dictionary<string, string>
            {
                ["returnUrl"] = authorizeUrl,
                ["clientId"] = "dorado-desktop",
                ["scope"] = DesktopScopes,
                ["allow"] = "true",
                ["__RequestVerificationToken"] = AntiforgeryPattern.Match(consentHtml).Groups[1].Value
            };

            var consent = await client.PostAsync("/account/consent", new FormUrlEncodedContent(consentForm));
            var reauthorize = await client.GetAsync(consent.Headers.Location!);
            code = QueryHelpers.ParseQuery(reauthorize.Headers.Location!.Query)["code"].ToString();
        }
        else
        {
            code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].ToString();
        }

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
