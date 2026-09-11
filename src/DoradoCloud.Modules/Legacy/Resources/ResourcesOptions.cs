namespace DoradoCloud.Modules.Legacy.Resources;

/// <summary>
/// Configuration for the <c>resources.zune.net</c> compatibility service.
/// Firmware baseline CABs are never bundled: they are streamed from an external,
/// untracked <see cref="CorpusRoot"/>. When that root is unset the service fails
/// closed with <c>404</c>.
/// </summary>
public sealed class ResourcesOptions
{
    /// <summary>
    /// Directory holding the firmware baseline CABs (e.g. a Zune Archive mirror).
    /// Absolute, or relative to the API content root. Empty ⇒ service disabled.
    /// </summary>
    public string CorpusRoot { get; set; } = string.Empty;

    /// <summary>Base URL used to build absolute CAB links in the manifest.</summary>
    public string PublicBaseUrl { get; set; } = "http://resources.zune.net";

    /// <summary>
    /// Device/firmware catalog. When empty a built-in catalog describing the
    /// known Zune device families is used (see <see cref="FirmwareCatalogDefaults"/>).
    /// </summary>
    public List<FirmwareDeviceOptions> Devices { get; set; } = new();
}

/// <summary>One firmware update entry in the manifest.</summary>
public sealed class FirmwareDeviceOptions
{
    public int DeviceClass { get; set; } = 1;
    public int FamilyId { get; set; }
    public string HardwareId { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = FirmwareCatalogDefaults.Manufacturer;
    public string Name { get; set; } = FirmwareCatalogDefaults.Name;
    public string Version { get; set; } = string.Empty;

    /// <summary>File name of the baseline CAB inside the corpus root.</summary>
    public string FileName { get; set; } = string.Empty;

    public string Locale { get; set; } = "en-US";
    public string Title { get; set; } = FirmwareCatalogDefaults.Title;
    public string Description { get; set; } = FirmwareCatalogDefaults.Description;
    public string Eula { get; set; } = FirmwareCatalogDefaults.Eula;

    public List<string> UpgradeFrom { get; set; } = new();
}
