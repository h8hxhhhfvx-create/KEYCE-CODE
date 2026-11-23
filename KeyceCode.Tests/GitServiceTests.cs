using KeyceCode.Services;
using System.Diagnostics;
using Xunit;

namespace KeyceCode.Tests;

public sealed class GitServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ReadsCurrentBranch()
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var status = await new GitService(repository).GetStatusAsync();
        Assert.False(string.IsNullOrWhiteSpace(status.Branch));
    }

    [Fact]
    public async Task GetBranchesAsync_ContainsCurrentBranch()
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var service = new GitService(repository);
        var status = await service.GetStatusAsync();
        var branches = await service.GetBranchesAsync();
        Assert.Contains(branches, branch => branch.IsCurrent && branch.Name == status.Branch);
    }

    [Theory]
    [InlineData("feature//editor")]
    [InlineData("feature/editor..test")]
    [InlineData("feature/editor.")]
    [InlineData("feature/editor~test")]
    [InlineData("feature/editor @{bad}")]
    public async Task CreateBranchAsync_RejectsNamesGitCannotRepresent(string name)
    {
        var repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        await Assert.ThrowsAsync<ArgumentException>(() => new GitService(repository).CreateBranchAsync(name));
    }

    [Fact]
    public async Task DiffHistoryAndUnstageWorkInATemporaryRepository()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeGitTest");
        try
        {
            await RunGit(directory.FullName, "init", "-b", "main");
            await RunGit(directory.FullName, "config", "user.name", "KEYCE Tests");
            await RunGit(directory.FullName, "config", "user.email", "tests@keyce.local");
            var file = Path.Combine(directory.FullName, "README.md");
            await File.WriteAllTextAsync(file, "one\n");

            var service = new GitService(directory.FullName);
            await service.StageAsync(["README.md"]);
            await service.CommitAsync("initial commit");
            await File.WriteAllTextAsync(file, "one\ntwo\n");

            var diff = await service.GetDiffAsync();
            Assert.Contains("+two", diff, StringComparison.Ordinal);

            await service.StageAsync(["README.md"]);
            var stagedDiff = await service.GetDiffAsync(staged: true);
            Assert.Contains("+two", stagedDiff, StringComparison.Ordinal);
            await service.UnstageAsync(["README.md"]);

            var history = await service.GetHistoryAsync(10);
            Assert.Contains(history, commit => commit.Subject == "initial commit");
        }
        finally
        {
            await DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task StashMergeAndRebaseWorkWithLocalBranches()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeGitBranchTest");
        try
        {
            await RunGit(directory.FullName, "init", "-b", "main");
            await RunGit(directory.FullName, "config", "user.name", "KEYCE Tests");
            await RunGit(directory.FullName, "config", "user.email", "tests@keyce.local");
            var file = Path.Combine(directory.FullName, "Program.txt");
            await File.WriteAllTextAsync(file, "base\n");
            var service = new GitService(directory.FullName);
            await service.StageAsync(["Program.txt"]);
            await service.CommitAsync("base");

            await service.CreateBranchAsync("feature/test");
            await File.WriteAllTextAsync(file, "base\nfeature\n");
            await service.StageAsync(["Program.txt"]);
            await service.CommitAsync("feature");
            await service.CheckoutAsync("main");
            await service.MergeAsync("feature/test");
            Assert.Contains("feature", await File.ReadAllTextAsync(file));

            await File.WriteAllTextAsync(file, "base\nfeature\nlocal change\n");
            await service.StashPushAsync("save local change");
            Assert.Empty((await service.GetStatusAsync()).Files);
            await service.StashPopAsync();
            Assert.NotEmpty((await service.GetStatusAsync()).Files);
            await service.StashPushAsync("prepare rebase");

            await service.CheckoutAsync("feature/test");
            await service.RebaseAsync("main");
        }
        finally
        {
            await DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task FetchPullAndPushWorkWithALocalRemote()
    {
        var root = Directory.CreateTempSubdirectory("KeyceCodeGitRemoteTest");
        var remote = Directory.CreateDirectory(Path.Combine(root.FullName, "remote.git"));
        var local = Directory.CreateDirectory(Path.Combine(root.FullName, "local"));
        try
        {
            await RunGit(remote.FullName, "init", "--bare");
            await RunGit(local.FullName, "init", "-b", "main");
            await RunGit(local.FullName, "config", "user.name", "KEYCE Tests");
            await RunGit(local.FullName, "config", "user.email", "tests@keyce.local");
            await RunGit(local.FullName, "remote", "add", "origin", remote.FullName);
            var file = Path.Combine(local.FullName, "README.md");
            await File.WriteAllTextAsync(file, "one\n");
            var service = new GitService(local.FullName);
            await service.StageAsync(["README.md"]);
            await service.CommitAsync("initial");
            await RunGit(local.FullName, "push", "-u", "origin", "main");

            await service.FetchAsync();
            await service.PullAsync();
            await File.WriteAllTextAsync(file, "one\ntwo\n");
            await service.StageAsync(["README.md"]);
            await service.CommitAsync("second");
            await service.PushAsync();

            Assert.Equal(0, (await RunGit(remote.FullName, "show", "main:README.md")).ExitCode);
        }
        finally
        {
            await DeleteDirectoryAsync(root);
        }
    }

    private static async Task<(int ExitCode, string StdOut)> RunGit(string directory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        Assert.True(process.Start());
        await process.WaitForExitAsync();
        var error = await process.StandardError.ReadToEndAsync();
        var output = await process.StandardOutput.ReadToEndAsync();
        Assert.True(process.ExitCode == 0, error);
        return (process.ExitCode, output);
    }

    private static async Task DeleteDirectoryAsync(DirectoryInfo directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!directory.Exists) return;
                foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                    file.Attributes = FileAttributes.Normal;
                directory.Delete(recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4) { await Task.Delay(100); }
            catch (UnauthorizedAccessException) when (attempt < 4) { await Task.Delay(100); }
        }
    }
}
