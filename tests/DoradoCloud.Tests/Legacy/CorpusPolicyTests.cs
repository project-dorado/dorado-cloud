using DoradoCloud.Modules.Legacy.Apps;
using DoradoCloud.Modules.Legacy.Resources;
using DoradoCloud.Modules.Legacy.Tiles;
using DoradoCloud.Modules.Legacy.Tuners;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DoradoCloud.Tests.Legacy;

/// <summary>
/// Guards the corpus policy: legacy services default to disabled, never escape
/// their configured root, and the external corpora stay out of git.
/// </summary>
public sealed class CorpusPolicyTests
{
    [Fact]
    public void Corpus_options_default_to_disabled()
    {
        Assert.Equal(string.Empty, new ResourcesOptions().CorpusRoot);
        Assert.Equal(string.Empty, new TilesOptions().CorpusRoot);
        Assert.Equal(string.Empty, new AppsOptions().CorpusRoot);
        Assert.Equal(string.Empty, new TunersOptions().CorpusRoot);
    }

    [Fact]
    public void Firmware_resolver_rejects_traversal_and_non_cabs()
    {
        var root = Path.Combine(Path.GetTempPath(), "dorado-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var catalog = new FirmwareCatalog(
                Options.Create(new ResourcesOptions { CorpusRoot = root }),
                NullLogger<FirmwareCatalog>.Instance);

            Assert.False(catalog.TryResolveFile("../secret.cab", out _));
            Assert.False(catalog.TryResolveFile("sub/../x.cab", out _));
            Assert.False(catalog.TryResolveFile("notes.txt", out _));
            Assert.False(catalog.TryResolveFile("missing.cab", out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void External_corpora_are_gitignored()
    {
        var repoRoot = FindRepoRoot();
        var gitignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));

        Assert.Contains("corpus/", gitignore);
        Assert.Contains("*.cab", gitignore);
        Assert.Contains("*.zcp", gitignore);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".gitignore")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
