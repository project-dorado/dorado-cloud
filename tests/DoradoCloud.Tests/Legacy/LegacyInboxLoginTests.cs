using System.Net;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;

namespace DoradoCloud.Tests.Legacy;

public sealed class LegacyInboxModuleTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    private const string InboxHost = "inbox.zune.net";

    private static HttpRequestMessage Hosted(HttpMethod method, string path, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Host = InboxHost;
        return request;
    }

    [Fact]
    public async Task Sent_message_appears_in_the_inbox()
    {
        var client = factory.CreateClient();
        const string body = "<Message><Subject>Hello</Subject><Body>Howdy, Zune!</Body></Message>";
        using var send = Hosted(HttpMethod.Post, "/messaging/alice/send",
            new StringContent(body, Encoding.UTF8, "application/xml"));
        var sent = await client.SendAsync(send);
        sent.EnsureSuccessStatusCode();

        using var list = Hosted(HttpMethod.Get, "/messaging/alice/inbox");
        var response = await client.SendAsync(list);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("<subject>Hello</subject>", xml);
        Assert.Contains("Howdy, Zune!", xml);
    }

    [Fact]
    public async Task Inbox_host_is_required()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/messaging/alice/inbox");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class LegacyLoginGatedTests(CloudApiFactory factory) : IClassFixture<CloudApiFactory>
{
    [Fact]
    public async Task Disabled_bridge_returns_501()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/RST2.srf")
        {
            Content = new StringContent("<RequestSecurityToken/>", Encoding.UTF8, "application/xml")
        };
        request.Headers.Host = "login.zune.net";

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}

public sealed class LegacyLoginEnabledTests : IClassFixture<LegacyLoginEnabledTests.LoginFactory>
{
    public sealed class LoginFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Legacy:Login:Enabled", "true");
            base.ConfigureWebHost(builder);
        }
    }

    private readonly LoginFactory _factory;

    public LegacyLoginEnabledTests(LoginFactory factory) => _factory = factory;

    private static HttpRequestMessage Login(string email, string password)
    {
        var body = $"<RequestSecurityToken><Username>{email}</Username><Password>{password}</Password></RequestSecurityToken>";
        var request = new HttpRequestMessage(HttpMethod.Post, "/RST2.srf")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/xml")
        };
        request.Headers.Host = "login.zune.net";
        return request;
    }

    private static string ExtractTicket(string xml)
        => XDocument.Parse(xml).Descendants()
            .First(e => e.Name.LocalName == "BinarySecurityToken").Value;

    [Fact]
    public async Task Valid_credentials_return_a_compact_security_token()
    {
        var email = AuthTestHelper.NewEmail();
        await AuthTestHelper.AccessTokenAsync(_factory, email);

        var client = _factory.CreateClient();
        using var request = Login(email, "password123");
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("wsse:BinarySecurityToken", xml);
        Assert.Contains("Id=\"Compact1\"", xml);
        Assert.Contains("AuthMembername", xml);
        Assert.False(string.IsNullOrWhiteSpace(ExtractTicket(xml)));
    }

    [Fact]
    public async Task Invalid_credentials_are_401()
    {
        var email = AuthTestHelper.NewEmail();
        await AuthTestHelper.AccessTokenAsync(_factory, email);

        var client = _factory.CreateClient();
        using var request = Login(email, "wrong-password");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_ticket_authenticates_a_legacy_inbox_send()
    {
        var email = AuthTestHelper.NewEmail();
        await AuthTestHelper.AccessTokenAsync(_factory, email);

        var client = _factory.CreateClient();
        using var login = Login(email, "password123");
        var loginResponse = await client.SendAsync(login);
        loginResponse.EnsureSuccessStatusCode();
        var ticket = ExtractTicket(await loginResponse.Content.ReadAsStringAsync());

        using var send = new HttpRequestMessage(HttpMethod.Post, "/messaging/alice/send")
        {
            Content = new StringContent(
                "<Message><Subject>Authed</Subject><Body>hi</Body></Message>",
                Encoding.UTF8, "application/xml")
        };
        send.Headers.Host = "inbox.zune.net";
        send.Headers.TryAddWithoutValidation("Authorization", "WLID1.0 " + ticket);
        var sent = await client.SendAsync(send);
        sent.EnsureSuccessStatusCode();

        using var list = new HttpRequestMessage(HttpMethod.Get, "/messaging/alice/inbox");
        list.Headers.Host = "inbox.zune.net";
        var listResponse = await client.SendAsync(list);
        var xml = await listResponse.Content.ReadAsStringAsync();

        var sender = XDocument.Parse(xml).Descendants().First(e => e.Name.LocalName == "sender").Value;
        Assert.True(Guid.TryParse(sender, out _), $"Expected an account id sender, got '{sender}'.");
    }

    [Fact]
    public async Task Unknown_ticket_does_not_authenticate()
    {
        var client = _factory.CreateClient();

        using var send = new HttpRequestMessage(HttpMethod.Post, "/messaging/alice/send")
        {
            Content = new StringContent(
                "<Message><Subject>Spoof</Subject><Body>hi</Body></Message>",
                Encoding.UTF8, "application/xml")
        };
        send.Headers.Host = "inbox.zune.net";
        send.Headers.TryAddWithoutValidation("Authorization", "WLID1.0 not-a-real-ticket");
        await client.SendAsync(send);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/messaging/alice/inbox");
        list.Headers.Host = "inbox.zune.net";
        var listResponse = await client.SendAsync(list);
        var xml = await listResponse.Content.ReadAsStringAsync();

        var sender = XDocument.Parse(xml).Descendants().First(e => e.Name.LocalName == "sender").Value;
        Assert.Equal("anonymous", sender);
    }

    [Fact]
    public async Task Pprl_config_points_at_the_login_host()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ppcrlconfig.bin");
        request.Headers.Host = "login.zune.net";
        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("WLIDSTS_WCF", xml);
        Assert.Contains("/RST2.srf", xml);
    }
}
