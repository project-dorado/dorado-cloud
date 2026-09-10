using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// The gateway is a thin YARP reverse proxy in front of the API. Self-hosters
// can run the API alone; the official instance fronts it with the gateway for
// TLS termination, rate limiting and blue/green routing.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Dorado Cloud Gateway",
    upstream = builder.Configuration["ReverseProxy:Clusters:api:Destinations:primary:Address"]
})).WithName("root");

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();

/// <summary>Exposed for integration tests.</summary>
public partial class Program;
