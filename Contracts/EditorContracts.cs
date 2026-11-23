namespace KeyceCode.Contracts;

/// <summary>Commande applicative asynchrone, indépendante de WinForms et de WPF.</summary>
public interface ICommand
{
    event EventHandler? CanExecuteChanged;
    bool CanExecute(CommandContext context);
    Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default);
}

public sealed record CommandContext(
    string? WorkspacePath = null,
    string? DocumentPath = null,
    IReadOnlyDictionary<string, object?>? Parameters = null);

/// <summary>Document ouvert dans KEYCE CODE.</summary>
public interface IDocument
{
    string Id { get; }
    string FilePath { get; }
    string DisplayName { get; }
    string Text { get; set; }
    string LanguageId { get; }
    bool IsDirty { get; }
    bool IsReadOnly { get; }
    event EventHandler? Changed;
    Task SaveAsync(CancellationToken cancellationToken = default);
    void MarkClean();
}

/// <summary>Contrat du fournisseur de langage utilisé par l’éditeur.</summary>
public interface ILanguageProvider
{
    string Id { get; }
    string DisplayName { get; }
    IReadOnlyCollection<string> Extensions { get; }
    string LineCommentPrefix { get; }
    bool CanHandle(string? extension);
}

/// <summary>État de l’espace de travail et de ses documents ouverts.</summary>
public interface IWorkspace
{
    string RootPath { get; }
    IReadOnlyList<IDocument> Documents { get; }
    IDocument? ActiveDocument { get; }
    event EventHandler? DocumentsChanged;
    Task<IDocument> OpenDocumentAsync(string path, CancellationToken cancellationToken = default);
    bool CloseDocument(string documentId, bool discardChanges = false);
    void SetActiveDocument(string? documentId);
}
