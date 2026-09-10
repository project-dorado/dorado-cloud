using System.Reflection;
using DoradoCloud.Modules.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DoradoCloud.Modules;

public static class ModuleExtensions
{
    /// <summary>Registers every <see cref="IEndpointModule"/> found in the given assemblies.</summary>
    public static IServiceCollection AddDoradoModules(this IServiceCollection services, params Assembly[] assemblies)
    {
        var sources = assemblies.Length > 0 ? assemblies : new[] { typeof(ModuleExtensions).Assembly };

        foreach (var assembly in sources)
        {
            foreach (var type in assembly.GetTypes()
                         .Where(t => typeof(IEndpointModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
            {
                services.AddSingleton(typeof(IEndpointModule), type);
            }
        }

        return services;
    }

    /// <summary>Maps every registered <see cref="IEndpointModule"/>.</summary>
    public static IEndpointRouteBuilder MapDoradoModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetServices<IEndpointModule>())
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
