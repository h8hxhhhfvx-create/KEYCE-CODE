using KeyceCode.Models;
using KeyceCode.ViewModels;
using Xunit;

namespace KeyceCode.Tests;

public sealed class EditorViewModelTests
{
    [Fact]
    public void AddTab_SelectsTheAddedTab()
    {
        var viewModel = new EditorViewModel();
        var tab = NewTab("Program.cs");

        viewModel.AddTab(tab);

        Assert.Single(viewModel.Tabs);
        Assert.Same(tab, viewModel.ActiveTab);
        Assert.True(tab.IsActive);
    }

    [Fact]
    public void ActivatingAnotherTab_DeactivatesThePreviousTab()
    {
        var viewModel = new EditorViewModel();
        var first = NewTab("Program.cs");
        var second = NewTab("Main.cs");
        viewModel.AddTab(first);
        viewModel.AddTab(second);

        viewModel.ActiveTab = first;

        Assert.Same(first, viewModel.ActiveTab);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
    }

    [Fact]
    public void RemovingActiveTab_SelectsTheNextAvailableTab()
    {
        var viewModel = new EditorViewModel();
        var first = NewTab("Program.cs");
        var second = NewTab("Main.cs");
        viewModel.AddTab(first);
        viewModel.AddTab(second);

        viewModel.RemoveTab(second);

        Assert.Single(viewModel.Tabs);
        Assert.Same(first, viewModel.ActiveTab);
    }

    private static EditorTab NewTab(string name) => new()
    {
        File = new CodeFile { Name = name, FullPath = Path.Combine("C:\\Projet", name) }
    };
}
