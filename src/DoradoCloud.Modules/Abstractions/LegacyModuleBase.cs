using Microsoft.AspNetCore.Routing;

namespace DoradoCloud.Modules.Abstractions;

/// <summary>
/// Base class for legacy Zune web-service compatibility modules. Legacy modules
/// mount at the root (no <c>/v1/{name}</c> prefix) and are dispatched by the
/// request <c>Host</c> header, recreating the original <c>*.zune.net</c> hosts.
/// </summary>
public abstract class LegacyModuleBase : EndpointModuleBase, ILegacyModule
{
    /// <summary>Legacy services live at the root, not under <c>/v1</c>.</summary>
    protected sealed override string RoutePrefix => string.Empty;

    public abstract override IReadOnlyList<string> Hosts { get; }

    /// <summary>
    /// Legacy modules do not expose the modern JSON <c>/ping</c> contract; they
    /// are exercised by their own wire-shape tests.
    /// </summary>
    protected sealed override void MapV1(RouteGroupBuilder group) => MapLegacy(group);

    protected abstract void MapLegacy(RouteGroupBuilder group);
}
