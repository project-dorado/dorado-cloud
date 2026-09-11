namespace DoradoCloud.Modules.Legacy.Login;

/// <summary>
/// Configuration for the <c>login.zune.net</c> WS-Trust bridge. The bridge is
/// security-sensitive and therefore <b>disabled by default</b>: it stays behind
/// a documented review gate until the token model is audited.
/// </summary>
public sealed class LegacyLoginOptions
{
    /// <summary>Enable the WS-Trust login bridge. Default <c>false</c> (returns 501).</summary>
    public bool Enabled { get; set; }

    /// <summary>Public base URL advertised in <c>/ppcrlconfig.bin</c>.</summary>
    public string PublicBaseUrl { get; set; } = "https://login.zune.net";
}
