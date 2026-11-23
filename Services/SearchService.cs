using System.Text;
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

    public IReadOnlyList<ReplacementResult> ReplaceAll(string rootDirectory, string query, string replacement, bool caseSensitive = false, bool regularExpression = false, int maximumFiles = 1000)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Le dossier de recherche est vide.", nameof(rootDirectory));
        if (string.IsNullOrEmpty(query)) return [];
        if (!Directory.Exists(rootDirectory)) throw new DirectoryNotFoundException(rootDirectory);
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var regex = regularExpression ? new Regex(query, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) : null;
        var results = new List<ReplacementResult>();
        foreach (var file in EnumerateFiles(rootDirectory))
        {
            if (results.Count >= maximumFiles) break;
            try
            {
                if (new FileInfo(file).Length > 2_000_000) continue;
                var original = File.ReadAllText(file);
                var occurrences = regex is null ? CountOccurrences(original, query, comparison) : regex.Matches(original).Count;
                if (occurrences == 0) continue;
                var updated = regex is null ? ReplaceIgnoreCase(original, query, replacement, comparison) : regex.Replace(original, replacement);
                WriteAtomically(file, updated);
                results.Add(new ReplacementResult(file, occurrences));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (DecoderFallbackException) { }
        }
        return results;
    }

    private static void WriteAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new IOException("Le dossier du fichier est introuvable.");
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.keyce-replace-tmp");
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static int CountOccurrences(string text, string query, StringComparison comparison)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(query, index, comparison)) >= 0; index += query.Length) count++;
        return count;
    }

    private static string ReplaceIgnoreCase(string text, string query, string replacement, StringComparison comparison)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var start = 0;
        while (true)
        {
            var index = text.IndexOf(query, start, comparison);
            if (index < 0) { builder.Append(text, start, text.Length - start); break; }
            builder.Append(text, start, index - start).Append(replacement);
            start = index + query.Length;
        }
        return builder.ToString();
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
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                files = Directory.EnumerateFiles(directory);
                directories = Directory.EnumerateDirectories(directory);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            foreach (var file in files) yield return file;
            foreach (var child in directories)
            {
                if (IgnoredDirectories.Contains(Path.GetFileName(child))) continue;
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
    }
}
