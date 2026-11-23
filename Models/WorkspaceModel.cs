using KeyceCode.Contracts;
using KeyceCode.Services;

namespace KeyceCode.Models;

/// <summary>Implémentation minimale de l’espace de travail pour découpler l’UI.</summary>
public sealed class WorkspaceModel : IWorkspace
{
    private readonly FileService fileService;
    private readonly List<IDocument> documents = [];

    public WorkspaceModel(string rootPath, FileService fileService)
    {
        RootPath = Path.GetFullPath(rootPath);
        this.fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    public string RootPath { get; }
    public IReadOnlyList<IDocument> Documents => documents;
    public IDocument? ActiveDocument { get; private set; }
    public event EventHandler? DocumentsChanged;

    public async Task<IDocument> OpenDocumentAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = documents.FirstOrDefault(document =>
            string.Equals(document.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            return existing;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var text = await Task.Run(() => fileService.ReadText(fullPath), cancellationToken).ConfigureAwait(false);
        var document = new EditorDocument(fullPath, text, fileService, fileService.IsReadOnly(fullPath));
        documents.Add(document);
        ActiveDocument = document;
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return document;
    }

    public bool CloseDocument(string documentId, bool discardChanges = false)
    {
        var document = documents.FirstOrDefault(item => string.Equals(item.Id, documentId, StringComparison.Ordinal));
        if (document is null) return false;
        if (document.IsDirty && !discardChanges) return false;
        documents.Remove(document);
        ActiveDocument = documents.LastOrDefault();
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetActiveDocument(string? documentId)
    {
        ActiveDocument = documentId is null
            ? null
            : documents.FirstOrDefault(item => string.Equals(item.Id, documentId, StringComparison.Ordinal));
    }
}
