using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class AiAgentService
{
    private static readonly Regex FileBlock = new(
        "<file\\s+path=\\\"(?<path>[^\\\"]+)\\\">(?<content>[\\s\\S]*?)</file>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly OllamaChatService chatService;

    public AiAgentService(OllamaChatService? chatService = null) => this.chatService = chatService ?? new OllamaChatService();

    public async Task<IReadOnlyList<AgentChange>> GeneratePlanAsync(
        string endpoint, string model, string projectRoot, string instruction, string? currentContext = null,
        CancellationToken cancellationToken = default)
    {
        var root = GetRoot(projectRoot);
        if (string.IsNullOrWhiteSpace(instruction)) throw new ArgumentException("La demande de l’agent est vide.", nameof(instruction));
        var prompt = "Tu es l’agent de modification de KEYCE CODE. Réponds uniquement avec un ou plusieurs blocs exactement sous la forme " +
            "<file path=\"chemin/relatif\">contenu complet du fichier</file>. Les chemins doivent être relatifs au projet. " +
            "Ne modifie jamais de fichier directement et n’inclus aucune explication hors des blocs. Demande utilisateur : " + instruction;
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
