using System.Text.RegularExpressions;
using KeyceCode.Models;

namespace KeyceCode.Services;

public sealed class SearchService
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", ".vs"
    };

    public IReadOnlyList<SearchResult> Search(string rootDirectory, string query, bool caseSensitive = false, bool regularExpression = false, int maximumResults = 1000)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Le dossier de recherche est vide.", nameof(rootDirectory));
        if (string.IsNullOrEmpty(query)) return [];
        if (!Directory.Exists(rootDirectory)) throw new DirectoryNotFoundException(rootDirectory);

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var regex = regularExpression ? new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) : null;
        var results = new List<SearchResult>();

        foreach (var file in EnumerateFiles(rootDirectory))
        {
            if (results.Count >= maximumResults) break;
            try
            {
                if (new FileInfo(file).Length > 2_000_000) continue;
                var lineNumber = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNumber++;
                    var match = regex?.Match(line);
                    var index = regex is null ? line.IndexOf(query, comparison) : match!.Index;
                    if (index < 0 || (regex is not null && !match!.Success)) continue;
                    results.Add(new SearchResult(file, lineNumber, index + 1, line));
                    if (results.Count >= maximumResults) break;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return results;
    }

    private static IEnumerable<string> EnumerateFiles(string rootDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(rootDirectory));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> files;
            IEnumerable<string> directories;
            try
            {
                files = Directory.EnumerateFiles(directory);
                directories = Directory.EnumerateDirectories(directory);
            }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var file in files) yield return file;
            foreach (var child in directories)
                if (!IgnoredDirectories.Contains(Path.GetFileName(child))) pending.Push(child);
        }
    }
}
