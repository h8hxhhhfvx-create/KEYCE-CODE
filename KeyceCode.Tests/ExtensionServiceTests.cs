using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class ExtensionServiceTests
{
    [Fact]
    public void Load_CreatesExtensionDirectoryAndReturnsEmptyList()
    {
        var directory = Path.Combine(Path.GetTempPath(), "keyce-ext-" + Guid.NewGuid());
        try
        {
            var service = new ExtensionService(directory);
            Assert.Empty(service.Load(null));
            Assert.True(Directory.Exists(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void TryReadManifest_ValidatesVersionCapabilitiesAndPermissions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "keyce-manifest-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "manifest.json");
            File.WriteAllText(path, """
                {
                  "id": "demo.extension",
                  "name": "Demo",
                  "version": "1.2.0",
                  "entryAssembly": "Demo.dll",
                  "capabilities": ["commands", "languages"],
                  "permissions": ["workspace.read"]
                }
                """);

            Assert.True(ExtensionService.TryReadManifest(path, out var manifest, out var error));
            Assert.Null(error);
            Assert.Equal("demo.extension", manifest.Id);
            Assert.Contains("commands", manifest.Capabilities);
            Assert.Contains("workspace.read", manifest.Permissions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("bad id", "1.0.0", "unknown.dll", "Identifiant")]
    [InlineData("demo", "not-version", "demo.dll", "Version")]
    [InlineData("demo", "1.0.0", "../demo.dll", "Assembly")]
    public void TryReadManifest_RejectsUnsafeOrInvalidFields(string id, string version, string assembly, string message)
    {
        var directory = Path.Combine(Path.GetTempPath(), "keyce-manifest-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "manifest.json");
            File.WriteAllText(path, $$"""
                { "id": "{{id}}", "name": "Demo", "version": "{{version}}", "entryAssembly": "{{assembly}}" }
                """);

            Assert.False(ExtensionService.TryReadManifest(path, out _, out var error));
            Assert.Contains(message, error, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Load_ReportsMissingManifestInsteadOfLoadingArbitraryDll()
    {
        var root = Path.Combine(Path.GetTempPath(), "keyce-ext-" + Guid.NewGuid());
        var package = Path.Combine(root, "without-manifest");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "anything.dll"), "not an assembly");
        try
        {
            var result = Assert.Single(new ExtensionService(root).Load(null));
            Assert.False(result.Loaded);
            Assert.Contains("manifest", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Load_ReportsIncompatibleAssemblyWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), "keyce-ext-" + Guid.NewGuid());
        var package = Path.Combine(root, "incompatible");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "manifest.json"), """
            { "id": "incompatible", "name": "Incompatible", "version": "1.0.0", "entryAssembly": "broken.dll", "capabilities": [] }
            """);
        File.WriteAllText(Path.Combine(package, "broken.dll"), "not a .NET assembly");
        try
        {
            var result = Assert.Single(new ExtensionService(root).Load(null));
            Assert.False(result.Loaded);
            Assert.NotNull(result.Error);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ContributionRegistry_NormalizesAndReplacesContributions()
    {
        var registry = new ExtensionContributionRegistry();
        registry.RegisterCommand("demo.run", "Run", () => { });
        registry.RegisterCommand("demo.run", "Run updated", () => { });
        registry.RegisterLanguage("demo.lang", "Demo", ["CS", ".demo"]);
        registry.RegisterTheme("demo.dark", "Demo Dark");
        registry.RegisterView("demo.view", "Demo View");
        registry.RegisterTask("demo.task", "Demo Task", _ => Task.CompletedTask);

        Assert.Single(registry.Commands);
        Assert.Equal("Run updated", registry.Commands[0].Label);
        Assert.Equal([".cs", ".demo"], registry.Languages[0].Extensions);
        Assert.Single(registry.Themes);
        Assert.Single(registry.Views);
        Assert.Single(registry.Tasks);
        await registry.Tasks[0].ExecuteAsync(CancellationToken.None);
    }

    [Fact]
    public void ContributionRegistry_MergesAValidatedExtensionBatch()
    {
        var target = new ExtensionContributionRegistry();
        var batch = new ExtensionContributionRegistry();
        target.RegisterCommand("shared.command", "Original", () => { });
        batch.RegisterCommand("shared.command", "Extension", () => { });
        batch.RegisterTheme("demo.dark", "Demo Dark");

        target.MergeFrom(batch);

        Assert.Equal("Extension", Assert.Single(target.Commands).Label);
        Assert.Equal("Demo Dark", Assert.Single(target.Themes).DisplayName);
    }

    [Fact]
    public void PackageService_InstallsDisablesUpdatesAndRemovesPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), "keyce-package-" + Guid.NewGuid());
        var source = Path.Combine(root, "source");
        var installed = Path.Combine(root, "installed");
        Directory.CreateDirectory(source);
        try
        {
            WriteManifest(source, "1.0.0");
            var service = new ExtensionPackageService(installed);
            var created = service.Install(source);
            Assert.True(created.Enabled);
            Assert.Single(service.ListInstalled());

            Assert.True(service.SetEnabled("demo", false));
            Assert.False(service.ListInstalled().Single().Enabled);
            Assert.True(service.SetEnabled("demo", true));

            WriteManifest(source, "2.0.0");
            var updated = service.Update(source);
            Assert.Equal("2.0.0", updated.Version);
            Assert.True(service.Remove("demo"));
            Assert.Empty(service.ListInstalled());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static void WriteManifest(string directory, string version) => File.WriteAllText(Path.Combine(directory, "manifest.json"), $$"""
        { "id": "demo", "name": "Demo", "version": "{{version}}", "entryAssembly": "demo.dll", "capabilities": ["commands"] }
        """);
}
