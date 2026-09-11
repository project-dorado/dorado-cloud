using System.Net;
using System.Net.Http.Headers;
using DoradoCloud.Client;

namespace DoradoCloud.Client.Tests;

public sealed class CloudAuthHandlerTests
{
    private static readonly Uri Base = new("https://cloud.example/");

    [Fact]
    public async Task SendAsync_AttachesStoredBearerToken()
    {
        var store = new InMemoryCloudCredentialStore(new CloudCredential("token-1"));
        var inner = new MockHttpHandler((req, _) =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("token-1", req.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var handler = new DoradoCloudAuthHandler(store, Base, innerHandler: inner);
        using var http = new HttpClient(handler) { BaseAddress = Base };

        var response = await http.GetAsync("v1/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_SendsAnonymousWhenNoCredential()
    {
        var store = new InMemoryCloudCredentialStore();
        var inner = new MockHttpHandler((req, _) =>
        {
            Assert.Null(req.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var handler = new DoradoCloudAuthHandler(store, Base, innerHandler: inner);
        using var http = new HttpClient(handler) { BaseAddress = Base };

        var response = await http.GetAsync("v1/catalog/search?q=x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_ProactivelyRefreshesExpiredTokenAndStoresIt()
    {
        var store = new InMemoryCloudCredentialStore(
            new CloudCredential("old-token", "refresh-1", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var refreshCalls = 0;
        var handler = new MockHttpHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/connect/token"))
            {
                refreshCalls++;
                Assert.Equal(HttpMethod.Post, req.Method);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"new-token\",\"expires_in\":3600,\"refresh_token\":\"refresh-2\"}"),
                });
            }

            Assert.Equal("new-token", req.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var auth = new DoradoCloudAuthHandler(store, Base, refreshHandler: handler, innerHandler: handler);
        using var http = new HttpClient(auth) { BaseAddress = Base };

        var response = await http.GetAsync("v1/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, refreshCalls);
        var stored = await store.GetAsync();
        Assert.Equal("new-token", stored!.AccessToken);
        Assert.Equal("refresh-2", stored.RefreshToken);
    }

    [Fact]
    public async Task SendAsync_RefreshesOnceAndReplaysAfterUnauthorized()
    {
        var store = new InMemoryCloudCredentialStore(
            new CloudCredential("stale-token", "refresh-1", DateTimeOffset.UtcNow.AddHours(1)));

        var apiCalls = 0;
        var handler = new MockHttpHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/connect/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"fresh-token\",\"expires_in\":3600,\"refresh_token\":\"refresh-2\"}"),
                });
            }

            apiCalls++;
            if (apiCalls == 1)
            {
                Assert.Equal("stale-token", req.Headers.Authorization?.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            Assert.Equal("fresh-token", req.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var auth = new DoradoCloudAuthHandler(store, Base, refreshHandler: handler, innerHandler: handler);
        using var http = new HttpClient(auth) { BaseAddress = Base };

        var response = await http.GetAsync("v1/identity/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, apiCalls);
    }

    [Fact]
    public async Task SendAsync_ReturnsUnauthorizedWhenNoRefreshTokenAvailable()
    {
        var store = new InMemoryCloudCredentialStore(new CloudCredential("stale-token"));
        var inner = new MockHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var auth = new DoradoCloudAuthHandler(store, Base, innerHandler: inner);
        using var http = new HttpClient(auth) { BaseAddress = Base };

        var response = await http.GetAsync("v1/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_DoesNotRefreshWhenTokenStillValid()
    {
        var store = new InMemoryCloudCredentialStore(
            new CloudCredential("good-token", "refresh-1", DateTimeOffset.UtcNow.AddHours(1)));

        var refreshCalls = 0;
        var handler = new MockHttpHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/connect/token"))
            {
                refreshCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var auth = new DoradoCloudAuthHandler(store, Base, refreshHandler: handler, innerHandler: handler);
        using var http = new HttpClient(auth) { BaseAddress = Base };

        await http.GetAsync("v1/identity/me");

        Assert.Equal(0, refreshCalls);
    }
}
