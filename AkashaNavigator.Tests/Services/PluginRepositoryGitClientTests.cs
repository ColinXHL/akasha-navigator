using System.IO;
using AkashaNavigator.Services;
using LibGit2Sharp;
using Xunit;

namespace AkashaNavigator.Tests.Services;

public sealed class PluginRepositoryGitClientTests : IDisposable
{
    private readonly string _testDirectory =
        Path.Combine(Path.GetTempPath(), $"akasha-git-client-{Guid.NewGuid():N}");

    [Fact]
    public async Task SynchronizeAsync_ClonesFetchesAndSkipsUnchangedCheckout()
    {
        var sourceDirectory = CreateSourceRepository("first");
        var cacheDirectory = Path.Combine(_testDirectory, "cache");
        var client = new PluginRepositoryGitClient();

        var firstCommit = await client.SynchronizeAsync(
            sourceDirectory,
            AppConstants.OfficialPluginRepositoryBranch,
            cacheDirectory,
            reset: false,
            CancellationToken.None);
        var indexPath = Path.Combine(
            cacheDirectory,
            AppConstants.PluginRepositoryIndexFileName);
        var firstWriteTime = File.GetLastWriteTimeUtc(indexPath);

        await Task.Delay(20);
        var unchangedCommit = await client.SynchronizeAsync(
            sourceDirectory,
            AppConstants.OfficialPluginRepositoryBranch,
            cacheDirectory,
            reset: false,
            CancellationToken.None);

        Assert.Equal(firstCommit, unchangedCommit);
        Assert.Equal(firstWriteTime, File.GetLastWriteTimeUtc(indexPath));

        var secondCommit = CommitIndex(sourceDirectory, "second");
        var fetchedCommit = await client.SynchronizeAsync(
            sourceDirectory,
            AppConstants.OfficialPluginRepositoryBranch,
            cacheDirectory,
            reset: false,
            CancellationToken.None);

        Assert.Equal(secondCommit, fetchedCommit);
        Assert.Equal("second", File.ReadAllText(indexPath));
        Assert.Equal(secondCommit, client.GetHeadCommit(cacheDirectory));
    }

    [Fact]
    public async Task SynchronizeAsync_ResetRebuildsCorruptedCache()
    {
        var sourceDirectory = CreateSourceRepository("valid");
        var cacheDirectory = Path.Combine(_testDirectory, "cache");
        var client = new PluginRepositoryGitClient();
        await client.SynchronizeAsync(
            sourceDirectory,
            AppConstants.OfficialPluginRepositoryBranch,
            cacheDirectory,
            reset: false,
            CancellationToken.None);
        DeleteDirectory(Path.Combine(cacheDirectory, ".git"));
        File.WriteAllText(
            Path.Combine(cacheDirectory, AppConstants.PluginRepositoryIndexFileName),
            "corrupted");

        var commit = await client.SynchronizeAsync(
            sourceDirectory,
            AppConstants.OfficialPluginRepositoryBranch,
            cacheDirectory,
            reset: true,
            CancellationToken.None);

        Assert.Equal("valid", File.ReadAllText(
            Path.Combine(cacheDirectory, AppConstants.PluginRepositoryIndexFileName)));
        Assert.Equal(commit, client.GetHeadCommit(cacheDirectory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            DeleteDirectory(_testDirectory);
        }
    }

    private string CreateSourceRepository(string content)
    {
        var sourceDirectory = Path.Combine(_testDirectory, "source");
        Directory.CreateDirectory(sourceDirectory);
        Repository.Init(sourceDirectory);
        using var repository = new Repository(sourceDirectory);
        var indexPath = Path.Combine(
            sourceDirectory,
            AppConstants.PluginRepositoryIndexFileName);
        File.WriteAllText(indexPath, content);
        Commands.Stage(repository, AppConstants.PluginRepositoryIndexFileName);
        repository.Commit("initial", CreateSignature(), CreateSignature());
        var catalog = repository.CreateBranch(
            AppConstants.OfficialPluginRepositoryBranch);
        Commands.Checkout(repository, catalog);
        return sourceDirectory;
    }

    private static string CommitIndex(string sourceDirectory, string content)
    {
        using var repository = new Repository(sourceDirectory);
        var indexPath = Path.Combine(
            sourceDirectory,
            AppConstants.PluginRepositoryIndexFileName);
        File.WriteAllText(indexPath, content);
        Commands.Stage(repository, AppConstants.PluginRepositoryIndexFileName);
        return repository.Commit(
            "update",
            CreateSignature(),
            CreateSignature()).Sha;
    }

    private static Signature CreateSignature()
    {
        return new Signature(
            "Akasha Tests",
            "tests@example.invalid",
            DateTimeOffset.UnixEpoch);
    }

    private static void DeleteDirectory(string directory)
    {
        foreach (var filePath in Directory.EnumerateFiles(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }
}
