using DoradoCloud.Modules.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace DoradoCloud.Tests;

public sealed class ObjectStorageTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"dorado-cloud-storage-{Guid.NewGuid():N}");

    private LocalFileSystemObjectStorage CreateStorage()
        => new(Options.Create(new StorageOptions { Provider = "local", LocalRoot = _root }));

    [Fact]
    public async Task Objects_round_trip_and_report_existence()
    {
        var storage = CreateStorage();

        Assert.False(await storage.ExistsAsync("artwork/abc.png"));

        await storage.PutAsync("artwork/abc.png", [1, 2, 3], "image/png");
        Assert.True(await storage.ExistsAsync("artwork/abc.png"));

        var content = await storage.GetAsync("artwork/abc.png");
        Assert.Equal(new byte[] { 1, 2, 3 }, content);

        Assert.Null(await storage.GetAsync("artwork/missing.png"));
    }

    [Fact]
    public async Task Keys_with_path_traversal_are_rejected()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.PutAsync("artwork/../../escape.png", [1], "image/png"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}
