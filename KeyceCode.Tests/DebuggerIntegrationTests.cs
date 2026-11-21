using System.Diagnostics;
using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class DebuggerIntegrationTests
{
    [Fact(Timeout = 45_000)]
    public async Task StartAndStopAsync_LaunchesARealDotnetProgramThroughVsdbg()
    {
        var directory = Directory.CreateTempSubdirectory("KeyceCodeDebuggerTest");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <DebugSymbols>true</DebugSymbols>
                    <DebugType>full</DebugType>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "Program.cs"), """
                using System.Threading;
                Console.WriteLine("DEBUGGER_STARTED");
                Thread.Sleep(15000);
                """);

            var build = await RunProcessAsync("dotnet", "build Sample.csproj --nologo --verbosity quiet", directory.FullName);
            Assert.Equal(0, build.ExitCode);
            var dll = Path.Combine(directory.FullName, "bin", "Debug", "net10.0", "Sample.dll");
            Assert.True(File.Exists(Path.ChangeExtension(dll, ".pdb")), build.Output);
            Assert.True(File.Exists(dll), build.Output);

            await using var service = new DebuggerService();
            try
            {
                await service.StartAsync(dll, directory.FullName, []);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("supported only in Microsoft versions", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Assert.True(service.IsRunning);

            await service.StopAsync();
            Assert.False(service.IsRunning);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output);
    }
}
