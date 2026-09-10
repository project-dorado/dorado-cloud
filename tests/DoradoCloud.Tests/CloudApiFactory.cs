using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DoradoCloud.Tests;

/// <summary>
/// Boots the API with an isolated SQLite database and an HTTP (non-TLS) issuer
/// so the OpenIddict flows are exercisable without certificates.
/// </summary>
public sealed class CloudApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"dorado-cloud-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("Auth:Issuer", "http://localhost/");
        builder.UseSetting("Auth:DisableTransportSecurity", "true");
        builder.UseSetting("Auth:SqlitePath", _dbPath);
        builder.UseSetting("ConnectionStrings:Postgres", string.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Best effort — the temp file is harmless.
        }
    }
}
