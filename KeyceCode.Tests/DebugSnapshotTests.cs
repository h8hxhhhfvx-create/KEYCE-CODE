using KeyceCode.Models;
using Xunit;

namespace KeyceCode.Tests;

public sealed class DebugSnapshotTests
{
    [Fact]
    public void SnapshotStoresFramesAndVariables()
    {
        var snapshot = new DebugSnapshot(1, [new DebugFrame("Main", "Demo.cs", 4, 5, 10)], [new DebugVariable("value", "42", "int")]);
        Assert.Equal("Main", Assert.Single(snapshot.Frames).Name);
        Assert.Equal("42", Assert.Single(snapshot.Variables).Value);
    }
}
