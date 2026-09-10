using DoradoCloud.Shared;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Abstractions;

/// <summary>Convenience base that mounts a module under <c>/{apiVersion}/{name}</c>.</summary>
public abstract class EndpointModuleBase : IEndpointModule
{
    public abstract string Name { get; }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup($"/{DoradoCloudInfo.ApiVersion}/{Name}");
        MapV1(group);
    }

    protected abstract void MapV1(RouteGroupBuilder group);

    protected static IResult Ping(string module, string status = "ok")
        => Results.Ok(new PingResponse(module, status, DoradoCloudInfo.ServiceVersion));
}
