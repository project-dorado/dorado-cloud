using DoradoCloud.Shared;
using DoradoCloud.Shared.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Abstractions;

/// <summary>
/// Convenience base that mounts a module under <c>/{apiVersion}/{name}</c>.
/// Legacy Zune compatibility modules (see <see cref="LegacyModuleBase"/>) mount
/// at the root instead and are constrained to one or more host names.
/// </summary>
public abstract class EndpointModuleBase : IEndpointModule
{
    /// <summary>
    /// Endpoints bound to a specific host are given a lower order than the
    /// host-agnostic root so that, when a legacy module and the modern API both
    /// match a route such as <c>/</c>, the host-specific module wins instead of
    /// producing an ambiguous match.
    /// </summary>
    private const int HostSpecificEndpointOrder = -1000;

    public abstract string Name { get; }

    /// <summary>Route prefix for the module. Defaults to <c>/{apiVersion}/{name}</c>.</summary>
    protected virtual string RoutePrefix => $"/{DoradoCloudInfo.ApiVersion}/{Name}";

    /// <summary>
    /// Host names this module answers for. Empty means the module is reachable
    /// on any host (the modern <c>/v1</c> surface).
    /// </summary>
    public virtual IReadOnlyList<string> Hosts => Array.Empty<string>();

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);

        if (Hosts.Count > 0)
        {
            group.RequireHost(Hosts.ToArray());
            ((IEndpointConventionBuilder)group).Add(endpointBuilder =>
            {
                if (endpointBuilder is RouteEndpointBuilder routeEndpoint)
                {
                    routeEndpoint.Order = HostSpecificEndpointOrder;
                }
            });
        }

        MapV1(group);
    }

    protected abstract void MapV1(RouteGroupBuilder group);

    protected static IResult Ping(string module, string status = "ok")
        => Results.Ok(new PingResponse(module, status, DoradoCloudInfo.ServiceVersion));
}
