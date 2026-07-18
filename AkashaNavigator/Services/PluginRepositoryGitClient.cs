using System.IO;
using LibGit2Sharp;

namespace AkashaNavigator.Services;

internal interface IPluginRepositoryGitClient
{
    Task<string> SynchronizeAsync(
        string repositoryUrl,
        string branch,
        string repositoryDirectory,
        bool reset,
        CancellationToken cancellationToken);

    string? GetHeadCommit(string repositoryDirectory);
}

internal sealed class PluginRepositoryGitClient : IPluginRepositoryGitClient
{
    public Task<string> SynchronizeAsync(
        string repositoryUrl,
        string branch,
        string repositoryDirectory,
        bool reset,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    return Synchronize(
                        repositoryUrl,
                        branch,
                        repositoryDirectory,
                        reset,
                        cancellationToken);
                }
                catch (UserCancelledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            },
            cancellationToken);
    }

    public string? GetHeadCommit(string repositoryDirectory)
    {
        if (!Repository.IsValid(repositoryDirectory))
        {
            return null;
        }

        using var repository = new Repository(repositoryDirectory);
        return repository.Head.Tip?.Sha;
    }

    private static string Synchronize(
        string repositoryUrl,
        string branch,
        string repositoryDirectory,
        bool reset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (reset && Directory.Exists(repositoryDirectory))
        {
            DeleteRepositoryCache(repositoryDirectory);
        }

        if (!Repository.IsValid(repositoryDirectory))
        {
            if (Directory.Exists(repositoryDirectory))
            {
                DeleteRepositoryCache(repositoryDirectory);
            }

            var cloneOptions = new CloneOptions(CreateFetchOptions(cancellationToken)) {
                BranchName = branch,
                Checkout = true,
                RecurseSubmodules = false
            };
            Repository.Clone(repositoryUrl, repositoryDirectory, cloneOptions);
            cancellationToken.ThrowIfCancellationRequested();

            using var cloned = new Repository(repositoryDirectory);
            return cloned.Head.Tip?.Sha
                   ?? throw new InvalidDataException("克隆后的 catalog 分支没有提交");
        }

        using var repository = new Repository(repositoryDirectory);
        var remote = repository.Network.Remotes["origin"]
                     ?? throw new InvalidDataException("插件仓库缺少 origin 远端");
        if (!string.Equals(remote.Url, repositoryUrl, StringComparison.Ordinal))
        {
            repository.Network.Remotes.Update(
                remote.Name,
                update => update.Url = repositoryUrl);
            remote = repository.Network.Remotes["origin"]!;
        }

        Commands.Fetch(
            repository,
            remote.Name,
            new[] { $"+refs/heads/{branch}:refs/remotes/{remote.Name}/{branch}" },
            CreateFetchOptions(cancellationToken),
            null);
        cancellationToken.ThrowIfCancellationRequested();

        var remoteBranch = repository.Branches[$"{remote.Name}/{branch}"]
                           ?? throw new InvalidDataException($"远端分支不存在: {branch}");
        if (string.Equals(
                repository.Head.FriendlyName,
                branch,
                StringComparison.Ordinal) &&
            string.Equals(
                repository.Head.Tip?.Sha,
                remoteBranch.Tip.Sha,
                StringComparison.Ordinal) &&
            !repository.RetrieveStatus().IsDirty)
        {
            return remoteBranch.Tip.Sha;
        }

        var localBranch = repository.Branches[branch]
                          ?? repository.CreateBranch(branch, remoteBranch.Tip);
        Commands.Checkout(
            repository,
            localBranch,
            new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
        repository.Reset(ResetMode.Hard, remoteBranch.Tip);
        repository.Branches.Update(
            localBranch,
            update => update.TrackedBranch = remoteBranch.CanonicalName);

        return remoteBranch.Tip.Sha;
    }

    private static FetchOptions CreateFetchOptions(
        CancellationToken cancellationToken)
    {
        return new FetchOptions {
            OnProgress = _ => !cancellationToken.IsCancellationRequested,
            OnTransferProgress = _ => !cancellationToken.IsCancellationRequested
        };
    }

    private static void DeleteRepositoryCache(string repositoryDirectory)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(repositoryDirectory));
        var rootPath = Path.TrimEndingDirectorySeparator(
            Path.GetPathRoot(fullPath) ?? string.Empty);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)) ||
            string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("拒绝删除无效的插件仓库缓存路径");
        }

        DeleteDirectoryTree(fullPath);
    }

    private static void DeleteDirectoryTree(string directory)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                File.SetAttributes(entryPath, FileAttributes.Normal);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    Directory.Delete(entryPath);
                }
                else
                {
                    File.Delete(entryPath);
                }

                continue;
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                DeleteDirectoryTree(entryPath);
                continue;
            }

            File.SetAttributes(entryPath, attributes & ~FileAttributes.ReadOnly);
            File.Delete(entryPath);
        }

        File.SetAttributes(directory, FileAttributes.Normal);
        Directory.Delete(directory);
    }
}
