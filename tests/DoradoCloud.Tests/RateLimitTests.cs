using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace DoradoCloud.Tests;

/// <summary>
/// Verifies the auth rate limiter rejects a burst past the per-minute permit.
/// Uses a dedicated factory configured with a tiny limit so the shared fixture's
/// budget is untouched.
/// </summary>
public sealed class RateLimitTests
{
    [Fact]
    public async Task TokenEndpoint_Returns429PastTheConfiguredPermit()
    {
        using var factory = new LowAuthLimitFactory();
        var client = factory.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 80; i++)
        {
            var response = await client.PostAsync(
                "/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "unsupported" }));
            codes.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.True(
            codes.Contains(HttpStatusCode.TooManyRequests),
            $"expected a 429; saw codes: {string.Join(",", codes.Distinct())}");
    }

    private sealed class LowAuthLimitFactory : CloudApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Auth:RateLimit:PermitPerMinute"] = "3" }));
        }
    }
}
