namespace KeyceCode.Services;

public sealed record InstalledExtension(string Id, string Name, string Version, string Directory, bool Enabled);

/// <summary>Installe et gère les paquets d’extensions validés par leur manifeste.</summary>
public sealed class ExtensionPackageService
{
    private readonly string extensionsDirectory;

    public ExtensionPackageService(string extensionsDirectory)
    {
        this.extensionsDirectory = Path.GetFullPath(extensionsDirectory);
        Directory.CreateDirectory(this.extensionsDirectory);
    }

    public IReadOnlyList<InstalledExtension> ListInstalled()
    {
        var result = new List<InstalledExtension>();
        foreach (var directory in Directory.EnumerateDirectories(extensionsDirectory))
        {
            var enabled = !directory.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var manifestPath = Path.Combine(directory, "manifest.json");
            if (!ExtensionService.TryReadManifest(manifestPath, out var manifest, out _)) continue;
            result.Add(new InstalledExtension(manifest.Id, manifest.Name, manifest.Version, directory, enabled));
        }
        return result.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public InstalledExtension Install(string packageDirectory) => InstallOrUpdate(packageDirectory);
    public InstalledExtension Update(string packageDirectory) => InstallOrUpdate(packageDirectory);

    public bool SetEnabled(string id, bool enabled)
    {
        var current = FindDirectory(id, enabled: !enabled);
        if (current is null) return false;
        var target = enabled ? current[..^".disabled".Length] : current + ".disabled";
        if (Directory.Exists(target)) throw new IOException("Une extension portant ce nom est déjà présente.");
        Directory.Move(current, target);
        return true;
    }

    public bool Remove(string id)
    {
        var directory = FindDirectory(id, enabled: true) ?? FindDirectory(id, enabled: false);
        if (directory is null) return false;
        Directory.Delete(directory, recursive: true);
        return true;
    }

    private InstalledExtension InstallOrUpdate(string packageDirectory)
    {
        var source = Path.GetFullPath(packageDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        if (!ExtensionService.TryReadManifest(Path.Combine(source, "manifest.json"), out var manifest, out var error))
            throw new InvalidDataException(error ?? "Manifeste invalide.");
        if (IsInside(source, extensionsDirectory)) throw new InvalidOperationException("Le paquet source doit être extérieur au dossier des extensions.");

        var target = Path.Combine(extensionsDirectory, manifest.Id);
        var temporary = target + ".installing-" + Guid.NewGuid().ToString("N");
        var backup = target + ".backup-" + Guid.NewGuid().ToString("N");
        try
        {
            CopyDirectory(source, temporary);
            if (Directory.Exists(target)) Directory.Move(target, backup);
            Directory.Move(temporary, target);
            if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            return new InstalledExtension(manifest.Id, manifest.Name, manifest.Version, target, true);
        }
        catch
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
    }

    private string? FindDirectory(string id, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (var directory in Directory.EnumerateDirectories(extensionsDirectory))
        {
            if (enabled != !directory.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;
            if (ExtensionService.TryReadManifest(Path.Combine(directory, "manifest.json"), out var manifest, out _) &&
                string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase)) return directory;
        }
        return null;
    }

    private static bool IsInside(string path, string parent)
    {
        var normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, parent, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }
}
