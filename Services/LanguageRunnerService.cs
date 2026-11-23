using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class LanguageRunnerService
{
    private readonly ToolDetectionService toolDetectionService = new();
    private static readonly Lazy<string?> VisualStudioVcVars = new(FindVisualStudioVcVars);
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "Python", [".js"] = "JavaScript", [".mjs"] = "JavaScript", [".php"] = "PHP",
        [".rb"] = "Ruby", [".pl"] = "Perl", [".lua"] = "Lua", [".go"] = "Go", [".java"] = "Java",
        [".ps1"] = "PowerShell", [".bat"] = "Batch", [".cmd"] = "Batch", [".sh"] = "Shell",
        [".c"] = "C", [".cpp"] = "C++", [".cc"] = "C++", [".cxx"] = "C++", [".rs"] = "Rust",
        [".ts"] = "TypeScript", [".tsx"] = "TypeScript"
    };

    public IReadOnlyCollection<string> SupportedExtensions => Names.Keys.ToArray();

    public LanguageCommand? Resolve(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var extension = Path.GetExtension(fullPath);
        if (!Names.TryGetValue(extension, out var language)) return null;
        var quoted = Quote(fullPath);

        if (extension.Equals(".c", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cpp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cxx", StringComparison.OrdinalIgnoreCase))
        {
            var native = CompileAndRunNative(fullPath, extension.Equals(".c", StringComparison.OrdinalIgnoreCase));
            return new LanguageCommand(language, native.Executable, native.Arguments, native.RequiredTools);
        }

        if (extension.Equals(".rs", StringComparison.OrdinalIgnoreCase))
        {
            var native = CompileAndRunRust(fullPath);
            return new LanguageCommand(language, native.Executable, native.Arguments, ["rustc"]);
        }

        var (executable, arguments) = extension.ToLowerInvariant() switch
        {
            ".py" => ("python", quoted),
            ".js" or ".mjs" => ("node", quoted),
            ".php" => ("php", quoted),
            ".rb" => ("ruby", quoted),
            ".pl" => ("perl", quoted),
            ".lua" => ("lua", quoted),
            ".go" => ("go", "run " + quoted),
            ".java" => CompileAndRunJava(fullPath),
            ".ps1" => ("powershell", $"-NoProfile -ExecutionPolicy Bypass -File {quoted}"),
            ".bat" or ".cmd" => ("cmd", $"/c {quoted}"),
            ".sh" => ("bash", quoted),
            ".ts" or ".tsx" => ("node", $"--experimental-strip-types {quoted}"),
            _ => throw new InvalidOperationException("Extension non prise en charge.")
        };
        var requiredTools = extension.ToLowerInvariant() switch
        {
            ".java" => new[] { "javac", "java" },
            _ => new[] { executable }
        };
        return new LanguageCommand(language, executable, arguments, requiredTools);
    }

    public IReadOnlyList<ToolStatus> CheckTools(LanguageCommand command) =>
        (command.RequiredTools is { Count: > 0 } ? command.RequiredTools : [command.Executable])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(toolDetectionService.Inspect)
        .ToArray();

    private static (string Executable, string Arguments, IReadOnlyList<string> RequiredTools) CompileAndRunNative(string sourcePath, bool isC)
    {
        var outputPath = CreateTemporaryExecutablePath(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var source = Quote(sourcePath);
        var output = Quote(outputPath);
        var vcvars = VisualStudioVcVars.Value;
        if (vcvars is not null)
        {
            var objectPath = Quote(Path.ChangeExtension(outputPath, ".obj"));
            var flags = isC ? string.Empty : "/EHsc ";
            return ("cmd", $"/d /c call {Quote(vcvars)} && cl /nologo {flags}{source} /Fe:{output} /Fo:{objectPath} && {output}", [vcvars]);
        }

        var compiler = isC ? "gcc" : "g++";
        return ("cmd", $"/d /c {compiler} {source} -o {output} && {output}", [compiler]);
    }

    private static (string Executable, string Arguments) CompileAndRunRust(string sourcePath)
    {
        var outputPath = CreateTemporaryExecutablePath(sourcePath);
        return ("cmd", $"/d /c rustc {Quote(sourcePath)} -o {Quote(outputPath)} && {Quote(outputPath)}");
    }

    private static string CreateTemporaryExecutablePath(string sourcePath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "KeyceCodeNative", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
    }

    private static string? FindVisualStudioVcVars()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio")
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                var path = Directory.EnumerateFiles(root, "vcvars64.bat", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false
                    })
                    .FirstOrDefault(candidate => candidate.Contains("Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase));
                if (path is not null) return path;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return null;
    }

    private static (string Executable, string Arguments) CompileAndRunJava(string sourcePath)
    {
        var directory = Path.GetDirectoryName(sourcePath)!;
        var className = Path.GetFileNameWithoutExtension(sourcePath);
        var source = Quote(sourcePath);
        var classPath = Quote(directory);
        return ("cmd", $"/d /c javac -d {classPath} {source} && java -cp {classPath} {className}");
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
