using KeyceCode.Contracts;
using KeyceCode.Models;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class EditorContractsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"KeyceCode-Contracts-{Guid.NewGuid():N}");
    private readonly FileService fileService = new();

    public EditorContractsTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task EditorDocument_TracksChangesAndSavesThroughFileService()
    {
        var path = Path.Combine(root, "Program.cs");
        File.WriteAllText(path, "old");
        var document = new EditorDocument(path, "old", fileService);
        var changed = 0;
        document.Changed += (_, _) => changed++;

        document.Text = "new";

        Assert.True(document.IsDirty);
        await document.SaveAsync();
        Assert.False(document.IsDirty);
        Assert.Equal("new", File.ReadAllText(path));
        Assert.True(changed >= 2);
        Assert.Equal("csharp", document.LanguageId);
    }

    [Fact]
    public async Task Workspace_OpensOnceAndProtectsDirtyDocuments()
    {
        var path = Path.Combine(root, "notes.md");
        await File.WriteAllTextAsync(path, "hello");
        var workspace = new WorkspaceModel(root, fileService);
        var notifications = 0;
        workspace.DocumentsChanged += (_, _) => notifications++;

        var first = await workspace.OpenDocumentAsync(path);
        var second = await workspace.OpenDocumentAsync(path);
        first.Text = "modified";

        Assert.Same(first, second);
        Assert.Single(workspace.Documents);
        Assert.False(workspace.CloseDocument(first.Id));
        Assert.True(workspace.CloseDocument(first.Id, discardChanges: true));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void Contracts_ExposeEditorBoundaryWithoutUiTypes()
    {
        Assert.True(typeof(ICommand).IsInterface);
        Assert.True(typeof(IWorkspace).IsInterface);
        Assert.True(typeof(IDocument).IsInterface);
        Assert.True(typeof(ILanguageProvider).IsInterface);
        Assert.DoesNotContain(typeof(IWorkspace).GetMethods(), method =>
            method.ReturnType.FullName?.Contains("System.Windows", StringComparison.Ordinal) == true);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
