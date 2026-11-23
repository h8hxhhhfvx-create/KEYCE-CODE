using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class CompilerServiceTests
{
    [Fact]
    public async Task BuildAsync_BuildsTheCurrentKeyceProject()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "KeyceCode.csproj"));
        var service = new CompilerService();

        var result = await service.BuildAsync(projectPath);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Display)));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.IsError);
    }

    [Fact]
    public async Task RunAsync_ExecutesASingleCSharpFileAndForwardsOutput()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeRunTest");
        try
        {
            var file = Path.Combine(directory.FullName, "Program.cs");
            await File.WriteAllTextAsync(file, "System.Console.WriteLine(\"KEYCE_RUN_OK\");");
            var output = new System.Text.StringBuilder();
            var service = new CompilerService();
            service.OutputReceived += (text, _) => output.Append(text);

            var result = await service.RunAsync(file);

            Assert.True(result.Success, output.ToString());
            Assert.Contains("KEYCE_RUN_OK", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task BuildAsync_InvalidProjectReturnsStructuredDiagnostic()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeDiagnosticTest");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "Invalid.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "Program.cs"),
                "class Broken { static void Main() { int value = 1 } }");

            var result = await new CompilerService().BuildAsync(directory.FullName);

            var diagnostic = Assert.Single(result.Diagnostics, item => item.Code == "CS1002");
            Assert.Equal(1, diagnostic.Line);
            Assert.True(diagnostic.Column > 0);
            Assert.True(diagnostic.IsError);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task BuildAsync_BuildsAnSlnxSolution()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeSlnxTest");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Program.cs"), "System.Console.WriteLine(\"SLNX_OK\");");
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Wrong.csproj"), "<Project>");
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.slnx"), """
                <Solution>
                  <Project Path="Sample.csproj" />
                </Solution>
                """);

            var result = await new CompilerService().BuildAsync(directory.FullName);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Display)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ExecutesTheProjectSelectedByAnSlnxSolution()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeSlnxRunTest");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Program.cs"), "System.Console.WriteLine(\"SLNX_RUN_OK\");");
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.slnx"), """
                <Solution>
                  <Project Path="Sample.csproj" />
                </Solution>
                """);
            var output = new System.Text.StringBuilder();
            var service = new CompilerService();
            service.OutputReceived += (text, _) => output.Append(text);

            var result = await service.RunAsync(directory.FullName);

            Assert.True(result.Success, output.ToString());
            Assert.Contains("SLNX_RUN_OK", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PrefersAnExecutableProjectInAnSlnxSolution()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeMultiProjectSolution");
        try
        {
            var libraryDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "Library"));
            var appDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "App"));
            await File.WriteAllTextAsync(Path.Combine(libraryDirectory.FullName, "Library.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(libraryDirectory.FullName, "Library.cs"), "public static class LibraryCode { public static string Value => \"LIBRARY\"; }");
            await File.WriteAllTextAsync(Path.Combine(appDirectory.FullName, "App.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(appDirectory.FullName, "Program.cs"), "System.Console.WriteLine(\"APP_OK\");");
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Workspace.slnx"), """
                <Solution>
                  <Project Path="Library\Library.csproj" />
                  <Project Path="App\App.csproj" />
                </Solution>
                """);
            var output = new System.Text.StringBuilder();
            var service = new CompilerService();
            service.OutputReceived += (text, _) => output.Append(text);

            var result = await service.RunAsync(Path.Combine(directory.FullName, "Workspace.slnx"));

            Assert.True(result.Success, output.ToString());
            Assert.Contains("APP_OK", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("LIBRARY", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ResolveRunnableProject_PrefersExecutableProjectInSolution()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeResolveProject");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Library.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            var solution = Path.Combine(directory.FullName, "Workspace.slnx");
            await File.WriteAllTextAsync(solution, "<Solution><Project Path=\"Library.csproj\" /><Project Path=\"App.csproj\" /></Solution>");

            var result = new CompilerService().ResolveRunnableProject(solution);

            Assert.Equal(Path.Combine(directory.FullName, "App.csproj"), result);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
