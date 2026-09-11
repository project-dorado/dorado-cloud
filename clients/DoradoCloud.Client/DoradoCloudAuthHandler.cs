using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoradoCloud.Client;

/// <summary>
/// Delegating handler that centralizes Dorado Cloud authentication for every
/// .NET client built on <see cref="DoradoCloudClient"/>:
///
/// <list type="bullet">
/// <item>attaches the stored bearer token to each outgoing request;</item>
/// <item>proactively refreshes an expired token via the OIDC
/// <c>refresh_token</c> grant before the request is sent;</item>
/// <item>on a <c>401 Unauthorized</c>, refreshes once and replays the request.</item>
/// </list>
///
/// The refresh uses the public-client (PKCE) flow, so no client secret is
/// embedded. Interactive sign-in stays with each platform (desktop loopback,
/// Android Custom Tabs); this handler only owns token lifetime.
/// </summary>
public sealed class DoradoCloudAuthHandler : DelegatingHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICloudCredentialStore _store;
    private readonly Uri _baseAddress;
    private readonly string _clientId;
    private readonly TimeSpan _expirySkew;
    private readonly HttpMessageHandler? _refreshHandler;

    public DoradoCloudAuthHandler(
        ICloudCredentialStore store,
        Uri baseAddress,
        string clientId = "dorado-desktop",
        TimeSpan? expirySkew = null,
        HttpMessageHandler? refreshHandler = null,
        HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        _store = store;
        _baseAddress = baseAddress;
        _clientId = clientId;
        _expirySkew = expirySkew ?? TimeSpan.FromSeconds(60);
        _refreshHandler = refreshHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var credential = await EnsureFreshAsync(await _store.GetAsync(cancellationToken).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

        if (credential is { HasAccessToken: true })
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && credential is { HasRefreshToken: true })
        {
            var refreshed = await RefreshAsync(credential.RefreshToken!, cancellationToken).ConfigureAwait(false);
            if (refreshed is { HasAccessToken: true })
            {
                response.Dispose();
                using var replay = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
                replay.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
                return await base.SendAsync(replay, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
    }

    private async Task<CloudCredential?> EnsureFreshAsync(CloudCredential? credential, CancellationToken cancellationToken)
    {
        if (credential is null || !credential.IsExpired(DateTimeOffset.UtcNow, _expirySkew))
        {
            return credential;
        }

        if (!credential.HasRefreshToken)
        {
            return credential;
        }

        return await RefreshAsync(credential.RefreshToken!, cancellationToken).ConfigureAwait(false) ?? credential;
    }

    private async Task<CloudCredential?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var http = new HttpClient(_refreshHandler ?? new SocketsHttpHandler())
        {
            BaseAddress = new Uri(_baseAddress.ToString().TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = refreshToken,
        });

        try
        {
            var response = await http.PostAsync("connect/token", form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(Json, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
            {
                return null;
            }

            var credential = new CloudCredential(
                token.AccessToken,
                string.IsNullOrWhiteSpace(token.RefreshToken) ? refreshToken : token.RefreshToken,
                token.ExpiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn) : null);

            await _store.StoreAsync(credential, cancellationToken).ConfigureAwait(false);
            return credential;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
