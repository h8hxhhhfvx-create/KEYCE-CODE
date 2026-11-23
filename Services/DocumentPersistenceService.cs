namespace KeyceCode.Services;

public sealed record DocumentSaveResult(string Content, bool WasFormatted);

/// <summary>Centralise l’écriture et le formatage optionnel d’un document.</summary>
public sealed class DocumentPersistenceService
{
    private readonly FileService fileService;
    private readonly FormatService formatService;

    public DocumentPersistenceService(FileService? fileService = null, FormatService? formatService = null)
    {
        this.fileService = fileService ?? new FileService();
        this.formatService = formatService ?? new FormatService();
    }

    public DocumentSaveResult Save(string path, string content, bool formatCSharp, int indentationSize)
    {
        var output = content ?? string.Empty;
        var shouldFormat = formatCSharp && string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase);
        if (shouldFormat) output = formatService.FormatCSharp(output, indentationSize);
        fileService.WriteText(path, output);
        return new DocumentSaveResult(output, shouldFormat);
    }
}
