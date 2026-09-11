namespace DoradoCloud.Modules.Legacy.Session;

/// <summary>Configuration for legacy Zune WS-Trust sessions.</summary>
public sealed class LegacySessionOptions
{
    /// <summary>Lifetime of an issued session ticket, in hours.</summary>
    public int TtlHours { get; set; } = 720;
}
