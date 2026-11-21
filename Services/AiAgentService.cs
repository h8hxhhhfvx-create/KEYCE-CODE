using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class AiAgentService
{
    private static readonly Regex FileBlock = new(
        "<file\\s+path=\\\"(?<path>[^\\\"]+)\\\">(?<content>[\\s\\S]*?)</file>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly OllamaChatService chatService;
    private IReadOnlyList<AgentChange>? lastAppliedPlan;

    public AiAgentService(OllamaChatService? chatService = null) => this.chatService = chatService ?? new OllamaChatService();

    public async Task<IReadOnlyList<AgentChange>> GeneratePlanAsync(
        string endpoint, string model, string projectRoot, string instruction, string? currentContext = null,
        CancellationToken cancellationToken = default)
    {
        var root = GetRoot(projectRoot);
        if (string.IsNullOrWhiteSpace(instruction)) throw new ArgumentException("La demande de l’agent est vide.", nameof(instruction));
        var projectContext = ReadProjectContext(root);
        var prompt = "Tu es l’agent de modification de KEYCE CODE. Réponds uniquement avec un ou plusieurs blocs exactement sous la forme " +
            "<file path=\"chemin/relatif\">contenu complet du fichier</file>. Les chemins doivent être relatifs au projet. " +
            "Ne modifie jamais de fichier directement et n’inclus aucune explication hors des blocs. Demande utilisateur : " + instruction;
        prompt += "\nContexte des fichiers du projet (lecture seule) :\n" + projectContext;
        if (!string.IsNullOrWhiteSpace(currentContext)) prompt += "\nContexte du fichier courant :\n" + currentContext;
        var response = await chatService.ChatAsync(endpoint, model, [("system", prompt)], cancellationToken);
        return ParsePlan(root, response);
    }

    public async Task ApplyPlanAsync(string projectRoot, IReadOnlyList<AgentChange> changes, CancellationToken cancellationToken = default)
    {
        var root = GetRoot(projectRoot);
        foreach (var change in changes)
        {
            var target = ResolveInsideRoot(root, change.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temp = target + ".keyce-tmp";
            await File.WriteAllTextAsync(temp, change.Content, cancellationToken);
            File.Move(temp, target, true);
        }
        lastAppliedPlan = changes.ToArray();
    }

    public async Task RestoreLastPlanAsync(string projectRoot, CancellationToken cancellationToken = default)
    {
        var root = GetRoot(projectRoot);
        if (lastAppliedPlan is null) throw new InvalidOperationException("Aucune modification récente de l’agent à annuler.");
        foreach (var change in lastAppliedPlan.Reverse())
        {
            var target = ResolveInsideRoot(root, change.RelativePath);
            if (change.ExistingContent is null)
            {
                if (File.Exists(target)) File.Delete(target);
            }
            else
            {
                await File.WriteAllTextAsync(target, change.ExistingContent, cancellationToken);
            }
        }
        lastAppliedPlan = null;
    }

    public IReadOnlyList<AgentChange> ParsePlan(string projectRoot, string response)
    {
        var root = GetRoot(projectRoot);
        var changes = new List<AgentChange>();
        foreach (Match match in FileBlock.Matches(response))
        {
            var relativePath = match.Groups["path"].Value.Trim().Replace('/', Path.DirectorySeparatorChar);
            var target = ResolveInsideRoot(root, relativePath);
            var existing = File.Exists(target) ? File.ReadAllText(target) : null;
            changes.Add(new AgentChange(Path.GetRelativePath(root, target), match.Groups["content"].Value.TrimStart('\r', '\n'), existing));
        }
        if (changes.Count == 0) throw new InvalidOperationException("L’agent n’a proposé aucun bloc de fichier exploitable.");
        return changes;
    }

    public string ReadProjectContext(string projectRoot)
    {
        var root = GetRoot(projectRoot);
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => IsContextFile(path) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                           !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .OrderBy(path => path)
            .Take(20);
        var builder = new System.Text.StringBuilder();
        var remaining = 16_000;
        foreach (var file in files)
        {
            if (remaining <= 0) break;
            try
            {
                var content = File.ReadAllText(file);
                var relative = Path.GetRelativePath(root, file);
                var excerpt = content.Length > 3_000 ? content[..3_000] + "\n[tronqué]" : content;
                var block = $"\n--- {relative} ---\n{excerpt}\n";
                if (block.Length > remaining) block = block[..remaining];
                builder.Append(block);
                remaining -= block.Length;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return builder.Length == 0 ? "(aucun fichier de contexte lisible)" : builder.ToString();
    }

    private static bool IsContextFile(string path) => Path.GetExtension(path).ToLowerInvariant() is ".cs" or ".csproj" or ".sln" or ".slnx" or ".md";

    private static string GetRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Le dossier du projet est vide.", nameof(projectRoot));
        var root = Path.GetFullPath(projectRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        return root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    private static string ResolveInsideRoot(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("L’agent a proposé un chemin absolu ou vide.");
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Chemin refusé hors du projet : {relativePath}");
        return full;
    }
}
