using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoradoCloud.Shared.Contracts;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class ApiTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Root_reports_service_identity()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dorado Cloud", body);
    }

    [Theory]
    [InlineData("catalog")]
    [InlineData("artwork")]
    [InlineData("directory")]
    [InlineData("recs")]
    [InlineData("social")]
    [InlineData("updates")]
    [InlineData("media")]
    [InlineData("identity")]
    public async Task Every_module_answers_ping(string module)
    {
        var client = factory.CreateClient();

        var ping = await client.GetFromJsonAsync<PingResponse>($"/v1/{module}/ping");

        Assert.NotNull(ping);
        Assert.Equal(module, ping!.Module);
        Assert.Equal("ok", ping.Status);
    }

    [Fact]
    public async Task Openid_discovery_document_is_published()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("token_endpoint", out var tokenEndpoint));
        Assert.Contains("connect/token", tokenEndpoint.GetString());
    }

    [Fact]
    public async Task Protected_endpoint_requires_a_token()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/identity/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_credentials_issues_a_token_accepted_by_the_api()
    {
        var client = factory.CreateClient();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "dorado-cloud-smoke",
            ["client_secret"] = "dev-secret",
            ["scope"] = "dorado.api"
        });

        var tokenResponse = await client.PostAsync("/connect/token", form);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        using var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var me = await authed.GetAsync("/v1/identity/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Directory_podcast_search_degrades_without_keys()
    {
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PodcastSearchResponse>("/v1/directory/podcasts/search?q=dorado");

        Assert.NotNull(result);
        Assert.False(result!.Configured);
        Assert.Contains("Podcast Index", result.Attribution);
    }

    [Fact]
    public async Task Directory_radio_search_reports_disabled_state()
    {
        var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<RadioSearchResponse>("/v1/directory/radio/search?q=jazz");

        Assert.NotNull(result);
        Assert.False(result!.Configured);
    }

    [Fact]
    public async Task Catalog_search_requires_a_query()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/catalog/search");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Artwork_proxy_rejects_non_allowlisted_hosts()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/v1/artwork/proxy?url=" + Uri.EscapeDataString("https://evil.example/x.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QuickMix_requires_a_seed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/recs/quickmix");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
