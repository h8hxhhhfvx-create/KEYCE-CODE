namespace KeyceCode.Services;

/// <summary>Registre des contributions validées d’une extension.</summary>
public sealed class ExtensionContributionRegistry
{
    private readonly List<ExtensionCommand> commands = [];
    private readonly List<ExtensionLanguage> languages = [];
    private readonly List<ExtensionTheme> themes = [];
    private readonly List<ExtensionView> views = [];
    private readonly List<ExtensionTask> tasks = [];

    public IReadOnlyList<ExtensionCommand> Commands => commands;
    public IReadOnlyList<ExtensionLanguage> Languages => languages;
    public IReadOnlyList<ExtensionTheme> Themes => themes;
    public IReadOnlyList<ExtensionView> Views => views;
    public IReadOnlyList<ExtensionTask> Tasks => tasks;

    public void Clear()
    {
        commands.Clear(); languages.Clear(); themes.Clear(); views.Clear(); tasks.Clear();
    }

    internal void MergeFrom(ExtensionContributionRegistry source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var command in source.Commands) Replace(commands, command);
        foreach (var language in source.Languages) Replace(languages, language);
        foreach (var theme in source.Themes) Replace(themes, theme);
        foreach (var view in source.Views) Replace(views, view);
        foreach (var task in source.Tasks) Replace(tasks, task);
    }

    public void RegisterCommand(string id, string label, Action action)
    {
        Validate(id, label);
        ArgumentNullException.ThrowIfNull(action);
        Replace(commands, new ExtensionCommand(id, label, action));
    }

    public void RegisterLanguage(string id, string displayName, IReadOnlyCollection<string> extensions)
    {
        Validate(id, displayName);
        ArgumentNullException.ThrowIfNull(extensions);
        var normalized = extensions.Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalized.Length == 0) throw new ArgumentException("Un langage doit déclarer au moins une extension.", nameof(extensions));
        Replace(languages, new ExtensionLanguage(id, displayName, normalized));
    }

    public void RegisterTheme(string id, string displayName)
    {
        Validate(id, displayName);
        Replace(themes, new ExtensionTheme(id, displayName));
    }

    public void RegisterView(string id, string title)
    {
        Validate(id, title);
        Replace(views, new ExtensionView(id, title));
    }

    public void RegisterTask(string id, string label, Func<CancellationToken, Task> executeAsync)
    {
        Validate(id, label);
        ArgumentNullException.ThrowIfNull(executeAsync);
        Replace(tasks, new ExtensionTask(id, label, executeAsync));
    }

    private static void Validate(string id, string label)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Une contribution d’extension doit avoir un identifiant et un libellé.");
    }

    private static void Replace<T>(List<T> target, T value) where T : class
    {
        var id = value switch
        {
            ExtensionCommand command => command.Id,
            ExtensionLanguage language => language.Id,
            ExtensionTheme theme => theme.Id,
            ExtensionView view => view.Id,
            ExtensionTask task => task.Id,
            _ => throw new ArgumentException("Type de contribution inconnu.")
        };
        var index = target.FindIndex(item => string.Equals(GetId(item), id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) target[index] = value;
        else target.Add(value);
    }

    private static string GetId<T>(T value) where T : class => value switch
    {
        ExtensionCommand command => command.Id,
        ExtensionLanguage language => language.Id,
        ExtensionTheme theme => theme.Id,
        ExtensionView view => view.Id,
        ExtensionTask task => task.Id,
        _ => string.Empty
    };
}
