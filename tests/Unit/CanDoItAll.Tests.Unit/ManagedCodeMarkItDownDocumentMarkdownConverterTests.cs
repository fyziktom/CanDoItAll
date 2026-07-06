using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Tools.Documents;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedCodeMarkItDownDocumentMarkdownConverterTests
{
    [Fact]
    public async Task ConvertToMarkdownAsync_converts_html_to_markdown_file()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var sourcePath = Path.Combine(root, "quote.html");
            var outputPath = Path.Combine(root, "quote.md");
            await File.WriteAllTextAsync(
                sourcePath,
                "<html><body><h1>Quote</h1><p>Model XR-1 costs 12500 USD.</p></body></html>");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

            var result = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(sourcePath, outputPath));

            Assert.True(result.Succeeded, result.Diagnostics);
            Assert.True(File.Exists(outputPath));
            var markdown = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("Quote", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Model XR-1", markdown, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(markdown.Length, result.MarkdownCharacterCount);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ConvertToMarkdownAsync_missing_source_fails_explicitly()
    {
        var root = CreateWorkspaceRoot();
        try
        {
            var outputPath = Path.Combine(root, "missing.md");
            var converter = new ManagedCodeMarkItDownDocumentMarkdownConverter();

            var result = await converter.ConvertToMarkdownAsync(
                new WorkspaceDocumentMarkdownConversionRequest(
                    Path.Combine(root, "missing.pdf"),
                    outputPath));

            Assert.False(result.Succeeded);
            Assert.Contains("was not found", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateWorkspaceRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "candoitall-managedcode-markitdown-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}

