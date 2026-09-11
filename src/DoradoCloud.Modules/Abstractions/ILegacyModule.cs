namespace DoradoCloud.Modules.Abstractions;

/// <summary>
/// Marker for a legacy Zune web-service compatibility module: a module that
/// recreates one of the original <c>*.zune.net</c> hosts (Atom/XML wire shapes)
/// so a hosts-patched Zune client can reach Dorado Cloud. Legacy modules mount
/// at the root and are dispatched by the request <c>Host</c> header.
/// </summary>
public interface ILegacyModule : IEndpointModule
{
    /// <summary>The host names this legacy service answers for.</summary>
    IReadOnlyList<string> Hosts { get; }
}
