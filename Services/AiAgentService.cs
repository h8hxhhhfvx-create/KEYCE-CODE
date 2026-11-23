using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class AiAgentService
{
    private static readonly Regex FileBlock = new(
        "<file\\s+path=\\\"(?<path>[^\\\"]+)\\\">(?<content>[\\s\\S]*?)</file>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly OllamaChatService chatService;
    private readonly AiRouterService? aiRouter;
    private IReadOnlyList<AgentChange>? lastAppliedPlan;

    public AiAgentService(OllamaChatService? chatService = null, AiRouterService? router = null)
    {
        this.chatService = chatService ?? new OllamaChatService();
        aiRouter = router;
    }

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
        var response = aiRouter?.ProviderNames.Count > 0
            ? await GenerateWithRouterOrOllamaAsync(endpoint, model, prompt, cancellationToken)
            : await chatService.ChatAsync(endpoint, model, [("system", prompt)], cancellationToken);
        return ParsePlan(root, response);
    }

    private async Task<string> GenerateWithRouterOrOllamaAsync(string endpoint, string model, string prompt, CancellationToken cancellationToken)
    {
        try { return (await aiRouter!.ChatAsync([("system", prompt)], cancellationToken)).Content; }
        catch (InvalidOperationException) when (!cancellationToken.IsCancellationRequested)
        {
            return await chatService.ChatAsync(endpoint, model, [("system", prompt)], cancellationToken);
        }
    }

    public async Task ApplyPlanAsync(string projectRoot, IReadOnlyList<AgentChange> changes, CancellationToken cancellationToken = default)
    {
        var root = GetRoot(projectRoot);
        if (changes.Count == 0) throw new ArgumentException("Le plan de l’agent est vide.", nameof(changes));

        var prepared = changes.Select(change =>
        {
            var target = ResolveInsideRoot(root, change.RelativePath);
            var existed = File.Exists(target);
            return new PreparedChange(change.RelativePath, target, change.Content, existed ? File.ReadAllText(target) : null, existed);
        }).ToArray();

        // Un nouveau plan invalide l’historique précédent, même si son application échoue.
        lastAppliedPlan = null;
        var applied = new List<PreparedChange>();
        try
        {
            foreach (var change in prepared)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureSafeParentDirectory(root, change.Target);
                Directory.CreateDirectory(Path.GetDirectoryName(change.Target)!);
                var temp = Path.Combine(Path.GetDirectoryName(change.Target)!, $".{Path.GetFileName(change.Target)}.{Guid.NewGuid():N}.keyce-tmp");
                try
                {
                    await File.WriteAllTextAsync(temp, change.Content, cancellationToken);
                    File.Move(temp, change.Target, true);
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                applied.Add(change);
            }
            lastAppliedPlan = prepared.Select(change => new AgentChange(change.RelativePath, change.Content, change.OriginalContent)).ToArray();
        }
        catch
        {
            foreach (var change in applied.AsEnumerable().Reverse())
            {
                if (change.ExistedBefore) await File.WriteAllTextAsync(change.Target, change.OriginalContent ?? string.Empty);
                else if (File.Exists(change.Target)) File.Delete(change.Target);
            }
            throw;
        }
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
                EnsureSafeParentDirectory(root, target);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, change.ExistingContent, cancellationToken);
            }
        }
        lastAppliedPlan = null;
    }

    public IReadOnlyList<AgentChange> ParsePlan(string projectRoot, string response)
    {
        var root = GetRoot(projectRoot);
        var changes = new List<AgentChange>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FileBlock.Matches(response))
        {
            var relativePath = match.Groups["path"].Value.Trim().Replace('/', Path.DirectorySeparatorChar);
            var target = ResolveInsideRoot(root, relativePath);
            var normalizedPath = Path.GetRelativePath(root, target);
            if (!paths.Add(normalizedPath))
                throw new InvalidOperationException($"L’agent a proposé plusieurs blocs pour le même fichier : {normalizedPath}");
            var existing = File.Exists(target) ? File.ReadAllText(target) : null;
            changes.Add(new AgentChange(normalizedPath, match.Groups["content"].Value.TrimStart('\r', '\n'), existing));
        }
        if (changes.Count == 0) throw new InvalidOperationException("L’agent n’a proposé aucun bloc de fichier exploitable.");
        return changes;
    }

    public string ReadProjectContext(string projectRoot)
    {
        var root = GetRoot(projectRoot);
        var files = EnumerateContextFiles(root)
            .Where(IsContextFile)
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

    private static IEnumerable<string> EnumerateContextFiles(string root)
    {
        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                files = Directory.EnumerateFiles(directory).ToArray();
                directories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            foreach (var file in files) yield return file;
            foreach (var child in directories)
            {
                var name = Path.GetFileName(child);
                if (name is ".git" or "bin" or "obj") continue;
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
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

    private static void EnsureSafeParentDirectory(string root, string target)
    {
        var parent = Directory.GetParent(target);
        while (parent is not null && parent.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(parent.FullName) && (File.GetAttributes(parent.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Chemin refusé : le dossier « {parent.FullName} » est un lien symbolique ou une jonction.");
            if (string.Equals(parent.FullName.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
            parent = parent.Parent;
        }
    }

    private sealed record PreparedChange(string RelativePath, string Target, string Content, string? OriginalContent, bool ExistedBefore);
}
