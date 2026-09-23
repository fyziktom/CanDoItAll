using CanDoItAll.AgentFramework.Core;
using MarkItDown;

namespace CanDoItAll.Tools.Documents;

public sealed class ManagedCodeMarkItDownDocumentMarkdownConverter : IWorkspaceDocumentMarkdownConverter
{
    private readonly MarkItDownClient client = new();

    public async Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
        WorkspaceDocumentMarkdownConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        if (request.MaxCharacters is { } maxCharacters)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxCharacters);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = Path.GetFullPath(request.SourcePath);
        if (!File.Exists(sourcePath))
        {
            return CreateFailure(sourcePath, "Document source file was not found.");
        }

        DocumentConverterResult converted;
        try
        {
            converted = await client.ConvertAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        }
        catch (MarkItDownException exception)
        {
            var fileName = Path.GetFileName(sourcePath);
            return CreateFailure(sourcePath, exception switch
            {
                UnsupportedFormatException => $"Document '{fileName}' has a format the markdown converter does not support. Convert a PDF, Office, HTML, CSV, or text document instead.",
                MissingDependencyException => $"Document '{fileName}' needs a converter component that is not installed on this host.",
                _ => $"Document '{fileName}' could not be converted to markdown. The file may be damaged, protected, or not the format its extension suggests."
            });
        }

        await using var result = converted;
        var fullMarkdown = result.Markdown ?? string.Empty;
        var markdown = request.MaxCharacters is { } limit && fullMarkdown.Length > limit
            ? fullMarkdown[..limit]
            : fullMarkdown;

        return new WorkspaceDocumentMarkdownConversionResult(
            Succeeded: true,
            Message: $"Converted document '{Path.GetFileName(sourcePath)}' to markdown.",
            SourcePath: sourcePath,
            Markdown: markdown,
            TotalMarkdownCharacters: fullMarkdown.Length,
            IsTruncated: markdown.Length < fullMarkdown.Length,
            Diagnostics: string.Empty);
    }

    private static WorkspaceDocumentMarkdownConversionResult CreateFailure(
        string sourcePath,
        string message)
        => new(
            Succeeded: false,
            Message: message,
            SourcePath: sourcePath,
            Markdown: string.Empty,
            TotalMarkdownCharacters: 0,
            IsTruncated: false,
            Diagnostics: message);
}
