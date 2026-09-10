using System.Net;
using System.Text;
using System.Text.Json;
using DoradoCloud.Client;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Client.Tests;

public sealed class WireShapeTests
{
    [Fact]
    public async Task PingModuleAsync_DeserializesServerPayload()
    {
        var json = "{\"module\":\"catalog\",\"status\":\"ok\",\"version\":\"1.0.0\"}";
        var client = NewGetClient("/v1/catalog/ping", HttpStatusCode.OK, json);

        var ping = await client.PingModuleAsync("catalog");

        Assert.NotNull(ping);
        Assert.Equal("catalog", ping!.Module);
        Assert.Equal("ok", ping.Status);
        Assert.Equal("1.0.0", ping.Version);
    }

    [Fact]
    public async Task CheckForUpdateAsync_DeserializesCanonicalUpdateCheckResponse()
    {
        var manifest = new UpdateManifest(
            "dorado", "stable", "0.4.0",
            "https://example.invalid/dorado-0.4.0.zip",
            Convert.ToHexString(new byte[32]).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "release notes");
        var release = new UpdateReleaseDto(
            manifest.App, manifest.Channel, manifest.Version, manifest.Url,
            manifest.Sha256, manifest.PublishedAt, manifest.Notes,
            Signature: Convert.ToBase64String(new byte[256]),
            Algorithm: "RS256");
        var json = JsonSerializer.Serialize(
            new UpdateCheckResponse(manifest.App, manifest.Channel, true, release, null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var client = NewGetClient("/v1/updates/dorado/stable", HttpStatusCode.OK, json);

        var check = await client.CheckForUpdateAsync("dorado", "stable");

        Assert.NotNull(check);
        Assert.True(check!.Available);
        Assert.NotNull(check.Release);
        Assert.Equal("0.4.0", check.Release!.Version);
        Assert.Equal("RS256", check.Release.Algorithm);
    }

    [Fact]
    public async Task GetMeAsync_DeserializesAnonymousMeResponse()
    {
        var json = "{\"accountId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"subject\":\"abc\",\"name\":\"jane\",\"email\":\"jane@example.com\"}";
        var client = NewGetClient("/v1/identity/me", HttpStatusCode.OK, json);

        var me = await client.GetMeAsync();

        Assert.NotNull(me);
        Assert.Equal(Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"), me!.AccountId);
        Assert.Equal("jane@example.com", me.Email);
    }

    [Fact]
    public async Task CatalogSearchAsync_PassesQueryTypeLimit()
    {
        var json = "{\"query\":\"mb\",\"type\":\"artist\",\"total\":1,\"attribution\":\"MusicBrainz\",\"items\":[{\"type\":\"artist\",\"mbid\":\"abc\",\"title\":\"MB\",\"artist\":\"\",\"date\":\"\",\"coverArtUrl\":null}]}";
        var handler = new MockHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            var uri = req.RequestUri!.AbsoluteUri;
            Assert.Contains("q=mb", uri);
            Assert.Contains("type=artist", uri);
            Assert.Contains("limit=10", uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var response = await client.CatalogSearchAsync("mb", "artist", limit: 10);

        Assert.NotNull(response);
        Assert.Single(response!.Items);
        Assert.Equal("artist", response.Items[0].Type);
    }

    [Fact]
    public async Task DirectoryPodcastSearchAsync_PassesQuery()
    {
        var json = "{\"query\":\"tech\",\"total\":0,\"configured\":false,\"attribution\":\"Podcast Index\",\"items\":[]}";
        var handler = new MockHttpHandler((req, _) =>
        {
            var uri = req.RequestUri!.AbsoluteUri;
            Assert.Contains("/directory/podcasts/search", uri);
            Assert.Contains("q=tech", uri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var response = await client.DirectoryPodcastSearchAsync("tech");

        Assert.NotNull(response);
        Assert.False(response!.Configured);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task RecsQuickMixAsync_DeserializesResponse()
    {
        var json = "{\"seed\":\"mb\",\"seedMbid\":\"abc\",\"total\":1,\"attribution\":\"MusicBrainz\",\"items\":[{\"mbid\":\"def\",\"name\":\"X\",\"score\":1.0,\"reasons\":[\"tag:rock\"]}]}";
        var client = NewGetClient("/v1/recs/quickmix?seed=mb&limit=20", HttpStatusCode.OK, json);

        var recs = await client.RecsQuickMixAsync("mb");

        Assert.NotNull(recs);
        Assert.Single(recs!.Items);
        Assert.Equal("def", recs.Items[0].Mbid);
        Assert.Contains("tag:rock", recs.Items[0].Reasons);
    }

    [Fact]
    public async Task GetProfileAsync_DeserializesProfileDto()
    {
        var json = "{\"accountId\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"handle\":\"jane\",\"displayName\":\"Jane\",\"bio\":\"\",\"followers\":3,\"following\":1,\"activities\":7,\"isFollowing\":false,\"isBlocked\":false,\"createdAt\":\"2026-01-01T00:00:00Z\"}";
        var client = NewGetClient("/v1/social/profiles/jane", HttpStatusCode.OK, json);

        var profile = await client.GetProfileAsync("jane");

        Assert.NotNull(profile);
        Assert.Equal("jane", profile!.Handle);
        Assert.Equal(3, profile.Followers);
    }

    [Fact]
    public async Task FollowAsync_PostsToExpectedRoute()
    {
        var handler = new MockHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/v1/social/profiles/jane/follow", req.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var ok = await client.FollowAsync("jane");

        Assert.True(ok);
    }

    [Fact]
    public async Task RegisterDeviceAsync_SerializesRegisterDeviceRequest()
    {
        var json = "{\"id\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\",\"name\":\"Pixel\",\"platform\":\"android\",\"serial\":\"deadbeef\",\"appVersion\":\"0.1.0\",\"createdAt\":\"2026-01-01T00:00:00Z\",\"lastSeenAt\":\"2026-01-01T00:00:00Z\"}";
        var handler = new MockHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/v1/identity/me/devices", req.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(json) });
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var device = await client.RegisterDeviceAsync(new RegisterDeviceRequest("Pixel", "android", "deadbeef", "0.1.0"));

        Assert.NotNull(device);
        Assert.Equal("Pixel", device!.Name);
        Assert.Equal("android", device.Platform);
    }

    [Fact]
    public async Task PutSettingsAsync_PassesExpectedVersion()
    {
        var json = "{\"payloadJson\":\"{}\",\"version\":2,\"updatedAt\":\"2026-01-01T00:00:00Z\"}";
        var handler = new MockHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.EndsWith("/v1/identity/me/settings", req.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var settings = await client.PutSettingsAsync(new PutSettingsRequest("{}", ExpectedVersion: 1));

        Assert.NotNull(settings);
        Assert.Equal(2, settings!.Version);
    }

    [Fact]
    public async Task VerifyReleaseSignatureAsync_RoundTripsKeyAndManifest()
    {
        var keyPem = TestKeys.GenerateRsaPem();
        var manifest = new UpdateManifest(
            "dorado", "stable", "0.4.0",
            "https://example.invalid/dorado-0.4.0.zip",
            Convert.ToHexString(new byte[32]).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "notes");
        var signature = TestKeys.Sign(manifest, keyPem.Private);

        var handler = new MockHttpHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/v1/updates/signing-key"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(keyPem.Public, Encoding.UTF8, "application/x-pem-file"),
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        var client = new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        var ok = await client.VerifyReleaseSignatureAsync(manifest, signature);

        Assert.True(ok);
    }

    private static DoradoCloudClient NewGetClient(string pathSuffix, HttpStatusCode status, string json)
    {
        var handler = new MockHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith(pathSuffix.Split('?')[0], req.RequestUri!.AbsolutePath);
            if (pathSuffix.Contains('?'))
            {
                var qs = pathSuffix.Split('?', 2)[1];
                foreach (var kv in qs.Split('&'))
                {
                    var key = kv.Split('=', 2)[0];
                    Assert.Contains($"{key}=", req.RequestUri!.Query);
                }
            }
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
        });
        return new DoradoCloudClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
    }
}

internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public MockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}
