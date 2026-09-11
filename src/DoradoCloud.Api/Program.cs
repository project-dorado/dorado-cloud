using DoradoCloud.Modules;
using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Directory;
using DoradoCloud.Modules.Identity;
using DoradoCloud.Modules.Legacy.Apps;
using DoradoCloud.Modules.Legacy.Catalog;
using DoradoCloud.Modules.Legacy.Inbox;
using DoradoCloud.Modules.Legacy.Login;
using DoradoCloud.Modules.Legacy.Resources;
using DoradoCloud.Modules.Legacy.Session;
using DoradoCloud.Modules.Legacy.Tiles;
using DoradoCloud.Modules.Legacy.Tuners;
using DoradoCloud.Modules.Media;
using DoradoCloud.Modules.Recommendations;
using DoradoCloud.Modules.Social;
using DoradoCloud.Modules.Storage;
using DoradoCloud.Modules.Updates;
using DoradoCloud.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Modular monolith: every IEndpointModule in the Modules assembly is discovered
// and mapped. Modules can be split into independent services later without
// changing the public routes.
builder.Services.AddDoradoModules(typeof(CatalogModule).Assembly);

// OpenIddict server + local validation, EF Core persistence, health checks.
builder.Services.AddDoradoIdentity(builder.Configuration, builder.Environment);
builder.Services.AddDoradoUpdates();
builder.Services.AddDoradoSocial();
builder.Services.AddDoradoRecommendations(builder.Configuration);
builder.Services.AddHealthChecks();

// Response caching: Redis when configured, otherwise an in-process cache.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Redis:Configuration"]))
{
    builder.Services.AddStackExchangeRedisCache(options =>
        options.Configuration = builder.Configuration["Redis:Configuration"]);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddDoradoDirectory(builder.Configuration);

// Legacy Zune compatibility services (host-routed, Atom/XML). Firmware CABs
// are streamed from an external, untracked corpus.
builder.Services.AddDoradoLegacyResources(builder.Configuration);
builder.Services.AddDoradoLegacyCatalog();
builder.Services.AddDoradoLegacyTiles(builder.Configuration);
builder.Services.AddDoradoLegacyApps(builder.Configuration);
builder.Services.AddDoradoLegacyTuners(builder.Configuration);
builder.Services.AddDoradoLegacyInbox();
builder.Services.AddDoradoLegacySessions(builder.Configuration);
builder.Services.AddDoradoLegacyLogin(builder.Configuration);

// DRM-free (PD/CC) media; legal-gated and disabled unless Media:Enabled=true.
builder.Services.AddDoradoMedia(builder.Configuration);

// Object storage (local FS default; S3/MinIO when Storage:Provider=s3), catalog
// (MusicBrainz + Cover Art Archive) and the artwork CDN.
builder.Services.AddDoradoStorage(builder.Configuration);
builder.Services.AddDoradoCatalog(builder.Configuration);
builder.Services.AddDoradoArtwork(builder.Configuration);

// OpenAPI contract (Swagger UI at /swagger, spec at /swagger/v1/swagger.json).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(DoradoCloudInfo.ApiVersion, new OpenApiInfo
    {
        Title = "Dorado Cloud API",
        Version = DoradoCloudInfo.ApiVersion,
        Description =
            "Community cloud services for the Dorado desktop and Android clients. " +
            "Dorado is an independent homage; it does not contact or emulate Microsoft Zune services " +
            "and hosts no copyrighted media."
    });
});

// CORS: browsers are constrained to a configured allowlist. In development an
// empty list falls back to permissive; outside development an empty list means
// no cross-origin access (same-origin only), rather than wide-open.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else if (builder.Environment.IsDevelopment())
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
}));

// Auth endpoints are rate-limited (per client IP) to blunt credential-stuffing
// and token abuse. A global, path-partitioned limiter is used so the policy does
// not depend on endpoint metadata or middleware ordering.
var authPermitPerMinute = builder.Configuration.GetValue("Auth:RateLimit:PermitPerMinute", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isAuth = path.EndsWith("/connect/token", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/account/login", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/account/register", StringComparison.OrdinalIgnoreCase);
        if (!isAuth)
        {
            return RateLimitPartition.GetNoLimiter("anonymous");
        }

        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = authPermitPerMinute,
            QueueLimit = 0,
        });
    });
});

// OpenTelemetry is opt-in: set Otel:Endpoint (OTLP) to export traces/metrics.
var otlpEndpoint = builder.Configuration["Otel:Endpoint"];
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    var endpoint = new Uri(otlpEndpoint);
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("dorado-cloud-api"))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(exporter => exporter.Endpoint = endpoint))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddOtlpExporter(exporter => exporter.Endpoint = endpoint));
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint($"/swagger/{DoradoCloudInfo.ApiVersion}/swagger.json", "Dorado Cloud API"));

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Legacy Zune clients authenticate with a WS-Trust ticket in the
// `Authorization: WLID1.0 <ticket>` header; resolve it to an account.
app.UseLegacyZuneSessions();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.MapGet("/", () => Results.Ok(new
{
    service = DoradoCloudInfo.ProductName,
    version = DoradoCloudInfo.ServiceVersion,
    api = $"/{DoradoCloudInfo.ApiVersion}",
    docs = "/swagger"
})).WithName("root");

app.MapDoradoModules();
app.MapDoradoAuthEndpoints();

await app.UseDoradoIdentityAsync();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
