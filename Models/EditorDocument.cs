using KeyceCode.Contracts;
using KeyceCode.Services;

namespace KeyceCode.Models;

/// <summary>Implémentation testable du document indépendant du contrôle visuel.</summary>
public sealed class EditorDocument : IDocument
{
    private readonly FileService fileService;
    private string text;

    public EditorDocument(string filePath, string text, FileService fileService, bool isReadOnly = false)
    {
        FilePath = Path.GetFullPath(filePath);
        this.text = text ?? string.Empty;
        this.fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        IsReadOnly = isReadOnly;
        LanguageId = Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant() switch
        {
            "cs" => "csharp",
            "js" or "jsx" => "javascript",
            "ts" or "tsx" => "typescript",
            "py" => "python",
            "html" or "htm" => "html",
            "css" => "css",
            "json" => "json",
            "md" => "markdown",
            _ => "plaintext"
        };
    }

    public string Id => FilePath;
    public string FilePath { get; }
    public string DisplayName => Path.GetFileName(FilePath);
    public string LanguageId { get; }
    public bool IsReadOnly { get; }
    public bool IsDirty { get; private set; }
    public event EventHandler? Changed;

    public string Text
    {
        get => text;
        set
        {
            if (IsReadOnly) throw new InvalidOperationException("Le document est en lecture seule.");
            if (string.Equals(text, value, StringComparison.Ordinal)) return;
            text = value ?? string.Empty;
            IsDirty = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsReadOnly) throw new UnauthorizedAccessException("Le document est en lecture seule.");
        await Task.Run(() => fileService.WriteText(FilePath, text), cancellationToken).ConfigureAwait(false);
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkClean() => IsDirty = false;
}
