namespace DoradoCloud.Modules.Legacy.Apps;

/// <summary>
/// Configuration for the read-only Zune HD app catalog. Packages (<c>.zcp</c>)
/// are Microsoft content and are never bundled: they are listed and streamed
/// from an external, untracked corpus. No purchase or DRM is involved.
/// </summary>
public sealed class AppsOptions
{
    /// <summary>Directory holding <c>.zcp</c> app packages (may be nested).</summary>
    public string CorpusRoot { get; set; } = string.Empty;

    public string PackageExtension { get; set; } = ".zcp";

    /// <summary>Upper bound on packages enumerated per request.</summary>
    public int MaxPackages { get; set; } = 2000;
}
