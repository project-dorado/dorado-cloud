using System.Net;
using System.Net.Http.Json;
using DoradoCloud.Shared.Contracts;

namespace DoradoCloud.Tests.Legacy;

public sealed class LegacySocialModuleTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private const string SocialHost = "socialapi.zune.net";

    private static HttpRequestMessage Hosted(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = SocialHost;
        return request;
    }

    private async Task<(HttpClient Client, string Handle)> NewProfileAsync(string prefix)
    {
        var token = await AuthTestHelper.AccessTokenAsync(factory, AuthTestHelper.NewEmail());
        var client = AuthTestHelper.Bearer(factory, token);
        var handle = AuthTestHelper.NewHandle(prefix);

        var response = await client.PutAsJsonAsync(
            "/v1/social/profiles/me", new UpsertProfileRequest(handle, $"Display {prefix}", "bio"));
        response.EnsureSuccessStatusCode();

        return (client, handle);
    }

    [Fact]
    public async Task Member_endpoint_returns_a_feed()
    {
        var (_, handle) = await NewProfileAsync("alice");

        var client = factory.CreateClient();
        using var request = Hosted($"/members/{handle}");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains($"<a:id>{handle}</a:id>", xml);
        Assert.Contains("Display alice", xml);
    }

    [Fact]
    public async Task Search_requires_a_query_and_finds_handles()
    {
        var (_, handle) = await NewProfileAsync("bob");

        var client = factory.CreateClient();
        using var missingQuery = Hosted("/members");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(missingQuery)).StatusCode);

        using var search = Hosted("/members?q=" + Uri.EscapeDataString(handle));
        var found = await client.SendAsync(search);
        found.EnsureSuccessStatusCode();
        Assert.Contains(handle, await found.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_member_is_404()
    {
        var client = factory.CreateClient();
        using var request = Hosted("/members/does-not-exist");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Friends_feed_lists_following()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (_, bobHandle) = await NewProfileAsync("bob");

        var follow = await alice.PostAsync($"/v1/social/profiles/{bobHandle}/follow", null);
        follow.EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        using var request = Hosted($"/members/{aliceHandle}/friends");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Contains(bobHandle, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Badges_feed_lists_earned_badges()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");

        var grant = await alice.PostAsync("/v1/social/me/badges/first-post", null);
        grant.EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        using var request = Hosted($"/members/{aliceHandle}/badges");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<code>first-post</code>", xml);
    }
}
