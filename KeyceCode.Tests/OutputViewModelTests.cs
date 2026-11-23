using KeyceCode.Models;
using KeyceCode.ViewModels;
using Xunit;

namespace KeyceCode.Tests;

public sealed class OutputViewModelTests
{
    [Fact]
    public void DiagnosticsView_FiltersBySeverityAndText()
    {
        var viewModel = new OutputViewModel();
        viewModel.Diagnostics.Add(new BuildDiagnostic("Program.cs", 8, 4, "error", "CS1002", " ; attendu"));
        viewModel.Diagnostics.Add(new BuildDiagnostic("Program.cs", 3, 1, "warning", "CS0168", "variable inutilisée"));
        viewModel.Diagnostics.Add(new BuildDiagnostic("Other.cs", 2, 1, "error", "CS0246", "type introuvable"));

        viewModel.DiagnosticSeverity = "Erreurs";
        Assert.Equal(2, viewModel.DiagnosticsView.Cast<BuildDiagnostic>().Count());

        viewModel.DiagnosticFilter = "CS0246";
        var diagnostic = Assert.Single(viewModel.DiagnosticsView.Cast<BuildDiagnostic>());
        Assert.Equal("Other.cs", diagnostic.File);
        Assert.Equal(1, viewModel.VisibleDiagnosticCount);
    }

    [Fact]
    public void DiagnosticsView_SortsByPosition()
    {
        var viewModel = new OutputViewModel();
        viewModel.Diagnostics.Add(new BuildDiagnostic("Program.cs", 12, 1, "error", "CS1", "late"));
        viewModel.Diagnostics.Add(new BuildDiagnostic("Program.cs", 2, 1, "error", "CS2", "early"));

        var diagnostics = viewModel.DiagnosticsView.Cast<BuildDiagnostic>().ToArray();
        Assert.Equal(2, diagnostics[0].Line);
        Assert.Equal(12, diagnostics[1].Line);
    }
}
