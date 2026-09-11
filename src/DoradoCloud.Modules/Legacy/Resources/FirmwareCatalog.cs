using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Resources;

/// <summary>
/// Resolves firmware files from the configured external corpus and builds the
/// <c>FirmwareUpdate.xml</c> manifest. Both are fail-closed: with no corpus
/// root (or a missing file) nothing is served.
/// </summary>
public sealed class FirmwareCatalog
{
    private readonly ResourcesOptions _options;
    private readonly ILogger<FirmwareCatalog> _logger;

    public FirmwareCatalog(IOptions<ResourcesOptions> options, ILogger<FirmwareCatalog> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>True when a readable corpus root is configured.</summary>
    public bool CorpusAvailable => ResolveCorpusRoot() is not null;

    /// <summary>The configured devices, filtered to those whose CAB exists.</summary>
    public IReadOnlyList<FirmwareDeviceOptions> AvailableDevices()
    {
        var devices = _options.Devices.Count > 0 ? _options.Devices : FirmwareCatalogDefaults.Create();
        return devices.Where(device => TryResolveFile(device.FileName, out _)).ToList();
    }

    /// <summary>
    /// Resolves a corpus file by name, refusing traversal and non-CAB files.
    /// </summary>
    public bool TryResolveFile(string? fileName, out string path)
    {
        path = string.Empty;
        var root = ResolveCorpusRoot();
        if (root is null || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")
            || Path.GetFileName(fileName) != fileName)
        {
            _logger.LogWarning("Rejected firmware path with separators or traversal: {FileName}", fileName);
            return false;
        }

        if (!fileName.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, fileName));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected firmware path outside the corpus root: {FileName}", fileName);
            return false;
        }

        if (!File.Exists(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    /// <summary>Builds the <c>FirmwareUpdate.xml</c> manifest for available devices.</summary>
    public XDocument BuildManifest()
    {
        var baseUrl = _options.PublicBaseUrl.TrimEnd('/');
        var root = new XElement("FirmwareUpdates");

        foreach (var device in AvailableDevices())
        {
            var update = new XElement("FirmwareUpdate",
                new XAttribute("DeviceClass", device.DeviceClass),
                new XAttribute("FamilyID", device.FamilyId),
                new XAttribute("HardwareID", device.HardwareId),
                new XAttribute("Manufacturer", device.Manufacturer),
                new XAttribute("Name", device.Name),
                new XAttribute("Version", device.Version),
                new XAttribute("URL", $"{baseUrl}/{device.FileName}"));

            update.Add(new XElement("Metadata",
                new XAttribute(XNamespace.Xml + "lang", device.Locale),
                new XElement("Title", device.Title),
                new XElement("Description", new XCData(device.Description)),
                new XElement("EULA", new XCData(device.Eula))));

            var upgrades = new XElement("SupportedUpgradeBaseVersions");
            foreach (var version in device.UpgradeFrom)
            {
                upgrades.Add(new XElement("UpgradeFrom", new XAttribute("Version", version)));
            }

            update.Add(upgrades);
            root.Add(update);
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private string? ResolveCorpusRoot()
    {
        if (string.IsNullOrWhiteSpace(_options.CorpusRoot))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(_options.CorpusRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firmware corpus root is not a valid path");
            return null;
        }

        return System.IO.Directory.Exists(full) ? full : null;
    }
}
