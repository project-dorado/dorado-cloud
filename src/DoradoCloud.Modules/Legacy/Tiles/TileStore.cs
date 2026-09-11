using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Tiles;

/// <summary>
/// Resolves profile tile images from the configured external corpus. Read-only
/// and confined to the corpus root; images are never bundled.
/// </summary>
public sealed class TileStore(IOptions<TilesOptions> options, ILogger<TileStore> logger)
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

    public bool CorpusAvailable => ResolveRoot() is not null;

    public bool TryResolve(string type, string zuneTag, out string path)
    {
        path = string.Empty;
        var root = ResolveRoot();
        if (root is null)
        {
            return false;
        }

        var canonicalType = type.Equals("background", StringComparison.OrdinalIgnoreCase) ? "Background"
            : type.Equals("avatar", StringComparison.OrdinalIgnoreCase) ? "Avatar"
            : null;
        if (canonicalType is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(zuneTag)
            || zuneTag.Contains('/') || zuneTag.Contains('\\') || zuneTag.Contains("..")
            || Path.GetFileName(zuneTag) != zuneTag)
        {
            logger.LogWarning("Rejected tile name with separators or traversal: {ZuneTag}", zuneTag);
            return false;
        }

        var fileName = Path.HasExtension(zuneTag) ? zuneTag : zuneTag + ".jpg";
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, canonicalType, fileName));
        var typeRoot = Path.Combine(root, canonicalType) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(typeRoot, StringComparison.Ordinal))
        {
            logger.LogWarning("Rejected tile outside the corpus root: {ZuneTag}", zuneTag);
            return false;
        }

        if (!File.Exists(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    public static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        _ => "image/jpeg"
    };

    private string? ResolveRoot()
    {
        if (string.IsNullOrWhiteSpace(options.Value.CorpusRoot))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(options.Value.CorpusRoot);
        }
        catch
        {
            return null;
        }

        return System.IO.Directory.Exists(full) ? full : null;
    }
}
