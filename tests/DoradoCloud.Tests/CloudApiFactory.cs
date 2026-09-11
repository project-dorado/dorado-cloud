using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DoradoCloud.Tests;

/// <summary>
/// Boots the API with an isolated SQLite database, an HTTP (non-TLS) issuer and
/// a temporary update-signing key, so the full auth and release flows are
/// exercisable without certificates or files leaking between tests.
/// </summary>
public class CloudApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"dorado-cloud-test-{Guid.NewGuid():N}.db");

    private readonly string _signingKeyPath =
        Path.Combine(Path.GetTempPath(), $"dorado-cloud-updates-{Guid.NewGuid():N}.pem");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Auth:Issuer", "http://localhost/");
        builder.UseSetting("Auth:DisableTransportSecurity", "true");
        builder.UseSetting("Auth:UseDevelopmentCertificates", "true");
        builder.UseSetting("Auth:SqlitePath", _dbPath);
        builder.UseSetting("ConnectionStrings:Postgres", string.Empty);
        builder.UseSetting("Updates:SigningKeyPath", _signingKeyPath);
        // Keep tests off the network: the radio directory is disabled and the
        // podcast directory has no key (both then degrade to empty responses).
        builder.UseSetting("Directory:RadioBrowser:Enabled", "false");
        // Disable the auth rate limiter for functional tests; RateLimitTests
        // exercises it with a dedicated low-limit factory.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Auth:RateLimit:PermitPerMinute"] = "100000" }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_dbPath);
        TryDelete(_signingKeyPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
