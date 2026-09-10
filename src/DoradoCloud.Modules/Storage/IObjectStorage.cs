namespace DoradoCloud.Modules.Storage;

/// <summary>Content-addressed object storage for cached binary assets (artwork).</summary>
public interface IObjectStorage
{
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
