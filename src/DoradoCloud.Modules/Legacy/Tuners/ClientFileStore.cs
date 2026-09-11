using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Legacy.Tuners;

/// <summary>Resolves PC-client resource files from the configured external corpus.</summary>
public sealed class ClientFileStore(IOptions<TunersOptions> options, ILogger<ClientFileStore> logger)
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public bool CorpusAvailable => ResolveRoot() is not null;

    public bool TryResolve(string fileName, out string path)
    {
        path = string.Empty;
        var root = ResolveRoot();
        if (root is null || string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")
            || Path.GetFileName(fileName) != fileName)
        {
            logger.LogWarning("Rejected client resource with separators or traversal: {FileName}", fileName);
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, fileName));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) || !File.Exists(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    public static string ContentTypeFor(string path)
        => ContentTypes.TryGetContentType(path, out var contentType) ? contentType : "application/octet-stream";

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
