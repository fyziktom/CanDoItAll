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

        var sourcePath = request.SourcePath;

        try
        {
            sourcePath = Path.GetFullPath(request.SourcePath);
            if (!File.Exists(sourcePath))
            {
                var message = $"Document source file '{sourcePath}' was not found.";
                return CreateFailure(sourcePath, message);
            }

            await using var result = await client.ConvertAsync(sourcePath, cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = $"Document conversion failed for '{sourcePath}'. {exception.Message}";
            return CreateFailure(sourcePath, message);
        }
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
