using KeyceCode.Services;
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
}
