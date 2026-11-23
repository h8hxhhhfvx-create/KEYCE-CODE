using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class GitHubAccountServiceTests
{
    [Fact]
    public void ParseAccounts_RemovesBlankLinesAndDuplicates()
    {
        var accounts = GitHubAccountService.ParseAccounts("gracejbl\r\n\r\ngracejbl\nother-user\n");

        Assert.Equal(["gracejbl", "other-user"], accounts.Select(account => account.Login));
    }

    [Fact]
    public void ParseAccounts_DoesNotExposeCredentialFields()
    {
        var accounts = GitHubAccountService.ParseAccounts("gracejbl");

        Assert.Single(accounts);
        Assert.DoesNotContain("token", accounts[0].Login, StringComparison.OrdinalIgnoreCase);
    }
}
