using DoradoCloud.Modules;
using DoradoCloud.Modules.Artwork;
using DoradoCloud.Modules.Catalog;
using DoradoCloud.Modules.Directory;
using DoradoCloud.Modules.Identity;
using DoradoCloud.Modules.Social;
using DoradoCloud.Modules.Storage;
using DoradoCloud.Modules.Updates;
using DoradoCloud.Shared;
using Microsoft.OpenApi.Models;
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

// CORS: the official instance is called by desktop/mobile clients from arbitrary origins.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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
