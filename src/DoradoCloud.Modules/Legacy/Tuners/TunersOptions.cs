namespace DoradoCloud.Modules.Legacy.Tuners;

/// <summary>
/// Configuration for <c>tuners.zune.net</c>, which served PC-client resources
/// to the device. Files are read from an external, untracked corpus.
/// </summary>
public sealed class TunersOptions
{
    public string CorpusRoot { get; set; } = string.Empty;
}
