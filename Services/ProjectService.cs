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

    public string CreateCSharpProject(string parentDirectory, string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName) ||
            projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(projectName), projectName, StringComparison.Ordinal) ||
            projectName is "." or "..")
            throw new ArgumentException("Saisis un nom de projet C# valide.", nameof(projectName));

        var root = Path.GetFullPath(Path.Combine(parentDirectory, projectName));
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("Un fichier ou dossier portant ce nom existe déjà.");

        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, projectName + ".csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "Program.cs"), """
            Console.WriteLine("Bonjour depuis KEYCE CODE !");
            """);
        return Path.Combine(root, projectName + ".csproj");
    }

    private static string? FindProjectFile(string root)
    {
        foreach (var pattern in new[] { "*.sln", "*.slnx", "*.csproj" })
        {
            var match = EnumerateFilesSafely(root, pattern)
                .FirstOrDefault(path => !path.Split(Path.DirectorySeparatorChar).Any(IgnoredFolders.Contains));
            if (match is not null) return match;
        }
        return null;
    }

    private static void AddChildren(List<CodeFile> target, string directory)
    {
        foreach (var childDirectory in EnumerateDirectoriesSafely(directory)
                     .Where(path => !IgnoredFolders.Contains(Path.GetFileName(path)))
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var child = new CodeFile { Name = Path.GetFileName(childDirectory), FullPath = childDirectory, IsDirectory = true };
            target.Add(child);
            AddChildren(child.Children, childDirectory);
        }

        foreach (var path in EnumerateFilesCurrentSafely(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            target.Add(new CodeFile { Name = Path.GetFileName(path), FullPath = path });
    }

    private static IEnumerable<string> EnumerateDirectoriesSafely(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory)
                .Where(path =>
                {
                    try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; }
                    catch (UnauthorizedAccessException) { return false; }
                    catch (IOException) { return false; }
                })
                .ToArray();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string directory, string pattern = "*")
    {
        var pending = new Stack<string>([directory]);
        var results = new List<string>();
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try { results.AddRange(Directory.EnumerateFiles(current, pattern)); }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            foreach (var child in EnumerateDirectoriesSafely(current)
                         .Where(path => !IgnoredFolders.Contains(Path.GetFileName(path))))
                pending.Push(child);
        }
        return results;
    }

    private static IEnumerable<string> EnumerateFilesCurrentSafely(string directory)
    {
        try { return Directory.EnumerateFiles(directory).ToArray(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }
}
