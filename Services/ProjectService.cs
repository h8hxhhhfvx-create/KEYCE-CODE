using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class ProjectService
{
    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
        { ".git", ".vs", "bin", "obj", "node_modules" };

    public Project Open(string selectedPath)
    {
        var fullPath = Path.GetFullPath(selectedPath);
        string? entryFile;
        string root;

        if (Directory.Exists(fullPath))
        {
            root = fullPath;
            entryFile = FindProjectFile(root);
            if (entryFile is null)
                throw new InvalidOperationException("Ce dossier ne contient aucun fichier .sln, .slnx ou .csproj.");
        }
        else if (File.Exists(fullPath))
        {
            var extension = Path.GetExtension(fullPath);
            if (!new[] { ".sln", ".slnx", ".csproj" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Sélectionne un fichier .sln, .slnx ou .csproj.");
            root = Path.GetDirectoryName(fullPath)!;
            entryFile = fullPath;
        }
        else throw new FileNotFoundException("Le dossier ou fichier de projet n’existe pas.", fullPath);

        var project = new Project
        {
            Name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            RootPath = root,
            EntryFilePath = entryFile
        };
        AddChildren(project.Files, root);
        return project;
    }

    public void Refresh(Project project)
    {
        project.Files.Clear();
        AddChildren(project.Files, project.RootPath);
        project.EntryFilePath = FindProjectFile(project.RootPath);
    }

    private static string? FindProjectFile(string root)
    {
        foreach (var pattern in new[] { "*.sln", "*.slnx", "*.csproj" })
        {
            var match = Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                .FirstOrDefault(path => !path.Split(Path.DirectorySeparatorChar).Any(IgnoredFolders.Contains));
            if (match is not null) return match;
        }
        return null;
    }

    private static void AddChildren(List<CodeFile> target, string directory)
    {
        foreach (var childDirectory in Directory.EnumerateDirectories(directory)
                     .Where(path => !IgnoredFolders.Contains(Path.GetFileName(path)))
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var child = new CodeFile { Name = Path.GetFileName(childDirectory), FullPath = childDirectory, IsDirectory = true };
            target.Add(child);
            AddChildren(child.Children, childDirectory);
        }

        foreach (var path in Directory.EnumerateFiles(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            target.Add(new CodeFile { Name = Path.GetFileName(path), FullPath = path });
    }
}
