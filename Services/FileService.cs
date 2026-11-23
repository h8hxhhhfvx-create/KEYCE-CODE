using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class FileService
{
    public string ReadText(string path) => File.ReadAllText(path);

    public void WriteText(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("Le dossier du fichier est introuvable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.keyce-tmp");
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public CodeFile CreateFile(string directory, string name)
    {
        ValidateName(name);
        var path = Path.GetFullPath(Path.Combine(directory, name));
        try
        {
            using (File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        }
        catch (IOException exception) when (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException("Un fichier ou dossier portant ce nom existe déjà.", exception);
        }
        return new CodeFile { Name = name, FullPath = path };
    }

    public string CreateDirectory(string parentDirectory, string name)
    {
        ValidateName(name);
        var path = Path.GetFullPath(Path.Combine(parentDirectory, name));
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("Un fichier ou dossier portant ce nom existe déjà.");
        Directory.CreateDirectory(path);
        return path;
    }

    public string Rename(string path, string newName)
    {
        ValidateName(newName);
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath)
            ?? throw new IOException("Le dossier parent est introuvable.");
        var destination = Path.Combine(parent, newName);
        if (Directory.Exists(fullPath)) Directory.Move(fullPath, destination);
        else if (File.Exists(fullPath)) File.Move(fullPath, destination);
        else throw new FileNotFoundException("L’élément à renommer n’existe plus.", fullPath);
        return Path.GetFullPath(destination);
    }

    public void Delete(string path, bool recursive = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive);
        else if (File.Exists(fullPath)) File.Delete(fullPath);
        else throw new FileNotFoundException("L’élément à supprimer n’existe plus.", fullPath);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal))
            throw new ArgumentException("Saisis un nom de fichier ou de dossier valide.", nameof(name));
    }
}
