using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace KeyceCode.Services;

public sealed class PreviewService
{
    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".html", ".htm", ".css", ".md", ".markdown" };

    public bool CanPreview(string extension) => PreviewExtensions.Contains(extension);

    public string CreatePreviewFile(string sourcePath, string content)
    {
        var extension = Path.GetExtension(sourcePath);
        var directory = Path.Combine(Path.GetTempPath(), "KeyceCodeMarkdownPreview");
        Directory.CreateDirectory(directory);
        var previewPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(sourcePath) + "-preview.html");
        var html = extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
            ? RenderCss(content, Path.GetFileName(sourcePath))
            : extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
                ? PrepareHtml(content, sourcePath)
                : RenderMarkdown(content, Path.GetFileName(sourcePath));
        File.WriteAllText(previewPath, html, Encoding.UTF8);
        return previewPath;
    }

    public static string RenderCss(string css, string title = "CSS")
    {
        return $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title><style>{css ?? string.Empty}</style></head><body><h1 class=\"title\">Aperçu CSS</h1><section class=\"card\"><h2>Titre de démonstration</h2><p>Texte d’exemple pour vérifier les styles.</p><button>Bouton</button></section></body></html>";
    }

    private static string PrepareHtml(string content, string sourcePath)
    {
        var baseHref = WebUtility.HtmlEncode(new Uri(Path.GetFullPath(Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory) + Path.DirectorySeparatorChar).AbsoluteUri);
        var html = content ?? string.Empty;
        if (!Regex.IsMatch(html, "<html\\b", RegexOptions.IgnoreCase))
            html = $"<!doctype html><html><head><meta charset=\"utf-8\"></head><body>{html}</body></html>";
        if (!Regex.IsMatch(html, "<base\\b", RegexOptions.IgnoreCase))
            html = new Regex("<head([^>]*)>", RegexOptions.IgnoreCase).Replace(
                html, $"<head$1><base href=\"{baseHref}\">", 1);
        return html;
    }

    public static string RenderMarkdown(string markdown, string title = "Markdown")
    {
        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(title))
            .Append("</title><style>body{font-family:Segoe UI,Arial;max-width:900px;margin:40px auto;padding:0 24px;line-height:1.6;color:#24292f}code,pre{font-family:Consolas,monospace;background:#f6f8fa}code{padding:2px 4px}pre{padding:14px;overflow:auto}blockquote{border-left:4px solid #0e7490;padding-left:16px;color:#57606a}</style></head><body>");

        var inCode = false;
        foreach (var rawLine in (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine;
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                html.Append(inCode ? "</code></pre>" : "<pre><code>");
                inCode = !inCode;
                continue;
            }
            if (inCode) { html.Append(WebUtility.HtmlEncode(line)).Append('\n'); continue; }
            if (line.StartsWith("### ", StringComparison.Ordinal)) html.Append("<h3>").Append(Inline(line[4..])).Append("</h3>");
            else if (line.StartsWith("## ", StringComparison.Ordinal)) html.Append("<h2>").Append(Inline(line[3..])).Append("</h2>");
            else if (line.StartsWith("# ", StringComparison.Ordinal)) html.Append("<h1>").Append(Inline(line[2..])).Append("</h1>");
            else if (line.StartsWith("> ", StringComparison.Ordinal)) html.Append("<blockquote>").Append(Inline(line[2..])).Append("</blockquote>");
            else if (line.StartsWith("- ", StringComparison.Ordinal)) html.Append("<li>").Append(Inline(line[2..])).Append("</li>");
            else if (string.IsNullOrWhiteSpace(line)) html.Append("<br>");
            else html.Append("<p>").Append(Inline(line)).Append("</p>");
        }
        if (inCode) html.Append("</code></pre>");
        return html.Append("</body></html>").ToString();
    }

    private static string Inline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = Regex.Replace(encoded, @"`([^`]+)`", "<code>$1</code>");
        encoded = Regex.Replace(encoded, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        encoded = Regex.Replace(encoded, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\">$1</a>");
        return encoded;
    }
}
