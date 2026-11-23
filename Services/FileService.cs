using System.Text;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class FileService
{
    public const long MaximumTextFileBytes = 20 * 1024 * 1024;

    public bool IsReadOnly(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Le fichier à ouvrir n’existe plus.", fullPath);
        return File.GetAttributes(fullPath).HasFlag(FileAttributes.ReadOnly);
    }

    public string ReadText(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Le fichier à ouvrir n’existe plus.", fullPath);
        if (info.Length > MaximumTextFileBytes)
            throw new IOException($"Le fichier est trop volumineux pour l’éditeur ({info.Length / (1024 * 1024)} Mo). Limite : 20 Mo.");

        var bytes = File.ReadAllBytes(fullPath);
        if (LooksBinary(bytes))
            throw new IOException("Ce fichier semble être binaire et ne peut pas être ouvert comme du texte.");

        return DecodeText(bytes, fullPath);
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var sampleLength = Math.Min(bytes.Length, 8192);
        for (var index = 0; index < sampleLength; index++)
        {
            if (bytes[index] == 0) return true;
        }
        return false;
    }

    private static string DecodeText(byte[] bytes, string path)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes, 3, bytes.Length - 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
            return new UTF32Encoding(false, false, true).GetString(bytes, 4, bytes.Length - 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
            return new UTF32Encoding(true, false, true).GetString(bytes, 4, bytes.Length - 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true).GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new IOException($"L’encodage du fichier « {Path.GetFileName(path)} » n’est pas UTF-8 valide et aucun BOM n’a été trouvé.", exception);
        }
    }

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
