using KeyceCode.Services;
using Xunit;

namespace KeyceCode.Tests;

public sealed class PreviewServiceTests
{
    [Fact]
    public void RenderMarkdown_ProducesSafeHtmlWithHeadingsAndCode()
    {
        var html = PreviewService.RenderMarkdown("# Titre\n\nDu **texte** et `code`.\n\n```csharp\nConsole.WriteLine();\n```", "test.md");

        Assert.Contains("<h1>Titre</h1>", html);
        Assert.Contains("<strong>texte</strong>", html);
        Assert.Contains("<pre><code>", html);
        Assert.Contains("Console.WriteLine();", html);
    }

    [Fact]
    public void CanPreview_RecognizesHtmlCssAndMarkdown()
    {
        var service = new PreviewService();
        Assert.True(service.CanPreview(".html"));
        Assert.True(service.CanPreview(".css"));
        Assert.True(service.CanPreview(".md"));
        Assert.False(service.CanPreview(".cs"));
    }

    [Fact]
    public void RenderCss_EmbedsStylesInAStandalonePreview()
    {
        var html = PreviewService.RenderCss(".card { color: red; }");

        Assert.Contains(".card { color: red; }", html);
        Assert.Contains("Aperçu CSS", html);
        Assert.Contains("<button>", html);
    }

    [Fact]
    public void CreatePreviewFileUsesUnsavedHtmlContent()
    {
        var source = Path.Combine(Path.GetTempPath(), "KeycePreview", "index.html");
        var path = new PreviewService().CreatePreviewFile(source, "<h1>Contenu non enregistré</h1>");

        try
        {
            var html = File.ReadAllText(path);
            Assert.Contains("Contenu non enregistré", html);
            Assert.Contains("<base", html);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
