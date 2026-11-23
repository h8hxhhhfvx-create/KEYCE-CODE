using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class AiRouterService
{
    private readonly IReadOnlyList<(AiProviderConfiguration Configuration, IAiChatProvider Provider)> providers;
    private readonly Dictionary<string, DateTimeOffset> cooldownUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan cooldown;

    public AiRouterService(IEnumerable<(AiProviderConfiguration Configuration, IAiChatProvider Provider)> providers, TimeSpan? cooldown = null)
    {
        this.providers = providers.ToArray();
        this.cooldown = cooldown ?? TimeSpan.FromSeconds(30);
    }

    public IReadOnlyList<string> ProviderNames => providers.Select(p => p.Configuration.Name).ToArray();

    public async Task<AiProviderResult> ChatAsync(IReadOnlyList<(string Role, string Content)> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0) throw new ArgumentException("La conversation est vide.", nameof(messages));
        var failures = new List<string>();
        foreach (var (configuration, provider) in providers)
        {
            if (cooldownUntil.TryGetValue(configuration.Name, out var until) && until > DateTimeOffset.UtcNow) continue;
            try
            {
                var content = await provider.ChatAsync(configuration.Model, messages, cancellationToken);
                cooldownUntil.Remove(configuration.Name);
                return new AiProviderResult(configuration.Name, configuration.Model, content);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                cooldownUntil[configuration.Name] = DateTimeOffset.UtcNow.Add(cooldown);
                failures.Add(configuration.Name);
            }
        }
        throw new InvalidOperationException(failures.Count == 0
            ? "Aucun fournisseur IA n’est disponible."
            : "Tous les fournisseurs IA configurés sont indisponibles : " + string.Join(", ", failures) + ".");
    }
}
