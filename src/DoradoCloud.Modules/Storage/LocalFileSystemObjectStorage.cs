using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Modules.Storage;

/// <summary>Filesystem-backed object storage (the self-host default).</summary>
public sealed class LocalFileSystemObjectStorage : IObjectStorage
{
    private readonly string _root;

    public LocalFileSystemObjectStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.LocalRoot);
        System.IO.Directory.CreateDirectory(_root);
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        return Task.FromResult(File.Exists(path) ? File.ReadAllBytes(path) : null);
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(Resolve(key)));

    /// <summary>Maps a key to a path under the root, defensively rejecting traversal.</summary>
    private string Resolve(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var safe = key.Replace('\\', '/');
        var extension = Path.GetExtension(safe);
        var sub = safe.Contains('/') ? safe[..safe.LastIndexOf('/')] : string.Empty;
        var name = hash + extension;
        var combined = Path.GetFullPath(Path.Combine(_root, sub, name));

        if (!combined.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved object path escapes the storage root.");
        }

        return combined;
    }
}
