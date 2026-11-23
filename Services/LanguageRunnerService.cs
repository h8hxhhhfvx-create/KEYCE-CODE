using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class LanguageRunnerService
{
    private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".py"] = "Python", [".js"] = "JavaScript", [".mjs"] = "JavaScript", [".php"] = "PHP",
        [".rb"] = "Ruby", [".pl"] = "Perl", [".lua"] = "Lua", [".go"] = "Go", [".java"] = "Java",
        [".ps1"] = "PowerShell", [".bat"] = "Batch", [".cmd"] = "Batch", [".sh"] = "Shell"
    };

    public IReadOnlyCollection<string> SupportedExtensions => Names.Keys.ToArray();

    public LanguageCommand? Resolve(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var extension = Path.GetExtension(fullPath);
        if (!Names.TryGetValue(extension, out var language)) return null;
        var quoted = Quote(fullPath);
        var (executable, arguments) = extension.ToLowerInvariant() switch
        {
            ".py" => ("python", quoted),
            ".js" or ".mjs" => ("node", quoted),
            ".php" => ("php", quoted),
            ".rb" => ("ruby", quoted),
            ".pl" => ("perl", quoted),
            ".lua" => ("lua", quoted),
            ".go" => ("go", "run " + quoted),
            ".java" => ("java", quoted),
            ".ps1" => ("powershell", $"-NoProfile -ExecutionPolicy Bypass -File {quoted}"),
            ".bat" or ".cmd" => ("cmd", $"/c {quoted}"),
            ".sh" => ("bash", quoted),
            _ => throw new InvalidOperationException("Extension non prise en charge.")
        };
        return new LanguageCommand(language, executable, arguments);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
