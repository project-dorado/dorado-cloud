using System.Net;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Verifies the host-dispatch foundation: a legacy module mounts at the root
/// and only answers on its declared hosts, while the host-agnostic modern API
/// keeps working.
/// </summary>
public sealed class HostDispatchTests
{
    private const string ProbeHost = "probe.legacy.test";

    private sealed class HostProbeModule : LegacyModuleBase
    {
        public override string Name => "hostprobe";

        public override IReadOnlyList<string> Hosts => new[] { ProbeHost };

        protected override void MapLegacy(RouteGroupBuilder group)
        {
            group.MapGet("/whoami", () => Results.Text("legacy-host", "text/plain"));
        }
    }

    private sealed class HostProbeFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services => services.AddSingleton<IEndpointModule, HostProbeModule>());
            base.ConfigureWebHost(builder);
        }
    }

    [Fact]
    public async Task Legacy_endpoint_is_only_reachable_on_its_host()
    {
        using var factory = new HostProbeFactory();
        var client = factory.CreateClient();

        var withoutHost = await client.GetAsync("/whoami");
        Assert.Equal(HttpStatusCode.NotFound, withoutHost.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Host = ProbeHost;
        var withHost = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, withHost.StatusCode);
        Assert.Equal("legacy-host", await withHost.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Modern_api_still_answers_on_a_legacy_host()
    {
        using var factory = new HostProbeFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/catalog/ping");
        request.Headers.Host = ProbeHost;
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
