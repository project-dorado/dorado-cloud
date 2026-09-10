using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Abstractions;

/// <summary>
/// A self-contained slice of the API. Modules are discovered at startup and
/// mapped under <c>/v1/{name}</c>, which keeps the modular monolith
/// extractable into separate services later without changing the public routes.
/// </summary>
public interface IEndpointModule
{
    /// <summary>Lower-case module slug used in the route prefix.</summary>
    string Name { get; }

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
