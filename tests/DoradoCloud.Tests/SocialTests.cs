using System.Net;
using System.Net.Http.Json;
using System.Text;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class SocialTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private async Task<(HttpClient Client, string Handle)> NewProfileAsync(string prefix)
    {
        var token = await AuthTestHelper.AccessTokenAsync(factory, AuthTestHelper.NewEmail());
        var client = AuthTestHelper.Bearer(factory, token);
        var handle = AuthTestHelper.NewHandle(prefix);

        var response = await client.PutAsJsonAsync("/v1/social/profiles/me",
            new UpsertProfileRequest(handle, prefix, "hello"));
        response.EnsureSuccessStatusCode();

        return (client, handle);
    }

    [Fact]
    public async Task Profile_follow_feed_and_card_flow()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (bob, bobHandle) = await NewProfileAsync("bob");

        var follow = await bob.PostAsync($"/v1/social/profiles/{aliceHandle}/follow", null);
        Assert.Equal(HttpStatusCode.NoContent, follow.StatusCode);

        var followers = await alice.GetFromJsonAsync<List<ProfileDto>>($"/v1/social/profiles/{aliceHandle}/followers");
        Assert.Contains(followers!, p => p.Handle == bobHandle);

        var following = await bob.GetFromJsonAsync<List<ProfileDto>>($"/v1/social/profiles/{bobHandle}/following");
        Assert.Contains(following!, p => p.Handle == aliceHandle);

        var post = await alice.PostAsJsonAsync("/v1/social/me/activities",
            new PostActivityRequest("now-playing", "{\"title\":\"Everything In Its Right Place\"}"));
        post.EnsureSuccessStatusCode();

        var feed = await bob.GetFromJsonAsync<List<ActivityDto>>("/v1/social/me/feed");
        Assert.Contains(feed!, a => a.Handle == aliceHandle && a.Kind == "now-playing");

        var card = await alice.GetFromJsonAsync<ZuneCardDto>($"/v1/social/profiles/{aliceHandle}/zunecard");
        Assert.NotNull(card);
        Assert.Equal(1, card!.Followers);
        Assert.Equal(0, card.Following);
        Assert.True(card.Activities >= 1);
        Assert.Contains(card.Badges, b => b.Code == "first-post" && b.EarnedAt is not null);

        var bobCard = await bob.GetFromJsonAsync<ZuneCardDto>($"/v1/social/profiles/{bobHandle}/zunecard");
        Assert.Contains(bobCard!.Badges, b => b.Code == "connector" && b.EarnedAt is not null);
    }

    [Fact]
    public async Task Feed_only_includes_followed_accounts()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (carol, _) = await NewProfileAsync("carol");

        await alice.PostAsJsonAsync("/v1/social/me/activities", new PostActivityRequest("rated", "{}"));

        // carol does not follow alice.
        var feed = await carol.GetFromJsonAsync<List<ActivityDto>>("/v1/social/me/feed");

        Assert.DoesNotContain(feed!, a => a.Handle == aliceHandle);
    }

    [Fact]
    public async Task Blocking_removes_account_from_feed()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (bob, _) = await NewProfileAsync("bob");

        await bob.PostAsync($"/v1/social/profiles/{aliceHandle}/follow", null);
        await alice.PostAsJsonAsync("/v1/social/me/activities", new PostActivityRequest("now-playing", "{}"));

        var before = await bob.GetFromJsonAsync<List<ActivityDto>>("/v1/social/me/feed");
        Assert.Contains(before!, a => a.Handle == aliceHandle);

        var block = await bob.PostAsync($"/v1/social/profiles/{aliceHandle}/block", null);
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        var after = await bob.GetFromJsonAsync<List<ActivityDto>>("/v1/social/me/feed");
        Assert.DoesNotContain(after!, a => a.Handle == aliceHandle);
    }

    [Fact]
    public async Task Reports_can_be_filed_listed_and_resolved()
    {
        var (alice, _) = await NewProfileAsync("alice");
        var (bob, bobHandle) = await NewProfileAsync("bob");

        var bobProfile = await bob.GetFromJsonAsync<ProfileDto>("/v1/social/profiles/me");

        var report = await alice.PostAsJsonAsync("/v1/social/reports",
            new ReportRequest(bobProfile!.AccountId, null, "spam"));
        report.EnsureSuccessStatusCode();
        var created = await report.Content.ReadFromJsonAsync<ReportDto>();
        Assert.Equal("open", created!.Status);

        var list = await alice.GetFromJsonAsync<List<ReportDto>>("/v1/social/admin/reports");
        Assert.Contains(list!, r => r.Id == created.Id && r.SubjectAccountId == bobProfile.AccountId);

        var resolve = await alice.PostAsJsonAsync($"/v1/social/admin/reports/{created.Id}/resolve",
            new ResolveReportRequest("resolved"));
        resolve.EnsureSuccessStatusCode();
        var resolved = await resolve.Content.ReadFromJsonAsync<ReportDto>();
        Assert.Equal("resolved", resolved!.Status);
    }

    [Fact]
    public async Task Handles_are_validated_and_unique()
    {
        var token = await AuthTestHelper.AccessTokenAsync(factory, AuthTestHelper.NewEmail());
        using var client = AuthTestHelper.Bearer(factory, token);

        var invalid = await client.PutAsJsonAsync("/v1/social/profiles/me",
            new UpsertProfileRequest("BAD HANDLE", "X", null));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var handle = AuthTestHelper.NewHandle("taken");
        var first = await client.PutAsJsonAsync("/v1/social/profiles/me", new UpsertProfileRequest(handle, "First", null));
        first.EnsureSuccessStatusCode();

        var otherToken = await AuthTestHelper.AccessTokenAsync(factory, AuthTestHelper.NewEmail());
        using var other = AuthTestHelper.Bearer(factory, otherToken);
        var duplicate = await other.PutAsJsonAsync("/v1/social/profiles/me", new UpsertProfileRequest(handle, "Second", null));

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Inbox_requires_a_bearer_token()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/social/me/inbox");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_sent_message_appears_in_the_modern_inbox()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (bob, _) = await NewProfileAsync("bob");

        await SendLegacyAsync(aliceHandle, "mira", "Hello", "Howdy, Zune!");

        // The modern client reads the same store without the legacy host.
        var inbox = await alice.GetFromJsonAsync<List<InboxMessageDto>>("/v1/social/me/inbox");
        var message = inbox!.Single(m => m.Subject == "Hello");
        Assert.Equal("mira", message.SenderTag);
        Assert.Equal(aliceHandle, message.RecipientTag);
        Assert.Contains("Howdy, Zune!", message.Body);
        Assert.False(message.IsRead);

        // The inbox is scoped to the signed-in handle.
        var bobInbox = await bob.GetFromJsonAsync<List<InboxMessageDto>>("/v1/social/me/inbox");
        Assert.DoesNotContain(bobInbox!, m => m.Subject == "Hello");
    }

    [Fact]
    public async Task Inbox_mark_read_updates_state_and_is_scoped_to_the_recipient()
    {
        var (alice, aliceHandle) = await NewProfileAsync("alice");
        var (bob, _) = await NewProfileAsync("bob");

        await SendLegacyAsync(aliceHandle, "mira", "Hello", "hi");

        var before = await alice.GetFromJsonAsync<List<InboxMessageDto>>("/v1/social/me/inbox");
        var message = before!.Single(m => m.Subject == "Hello");

        var marked = await alice.PostAsync($"/v1/social/me/inbox/{message.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);

        var after = await alice.GetFromJsonAsync<List<InboxMessageDto>>("/v1/social/me/inbox");
        Assert.True(after!.Single(m => m.Id == message.Id).IsRead);

        // A different signed-in handle cannot mark someone else's message.
        var foreign = await bob.PostAsync($"/v1/social/me/inbox/{message.Id}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public void Modern_social_routes_are_host_agnostic()
    {
        // Guard: the modern /v1/social surface must not be host-constrained.
        // A route that only answers on a legacy Host is exactly the bug that
        // made the HD cloud inbox silently unreachable; keep it from recurring.
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var social = endpoints.OfType<RouteEndpoint>()
            .Where(e => (e.RoutePattern.RawText ?? string.Empty).Contains("v1/social", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(social);
        Assert.All(social, endpoint =>
            Assert.DoesNotContain(endpoint.Metadata, m => m.GetType().Name == "HostAttribute"));

        Assert.Contains(social, e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/') == "v1/social/me/inbox");
        Assert.Contains(social, e => (e.RoutePattern.RawText ?? string.Empty).TrimStart('/') == "v1/social/me/inbox/{id:guid}/read");
    }

    private async Task SendLegacyAsync(string handle, string from, string subject, string body)
    {
        using var send = new HttpRequestMessage(
            HttpMethod.Post,
            $"/messaging/{handle}/send?from={from}")
        {
            Content = new StringContent(
                $"<Message><Subject>{subject}</Subject><Body>{body}</Body></Message>",
                Encoding.UTF8,
                "application/xml")
        };
        send.Headers.Host = "inbox.zune.net";
        (await factory.CreateClient().SendAsync(send)).EnsureSuccessStatusCode();
    }
}
