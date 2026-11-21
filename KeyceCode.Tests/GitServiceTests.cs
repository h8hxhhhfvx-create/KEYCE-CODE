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
}
