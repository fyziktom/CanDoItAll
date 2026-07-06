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
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPath);

        var sourcePath = Path.GetFullPath(request.SourcePath);
        var outputPath = Path.GetFullPath(request.OutputPath);
        if (!File.Exists(sourcePath))
        {
            var message = $"Document source file '{sourcePath}' was not found.";
            return CreateFailure(request, sourcePath, outputPath, message);
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            var message = $"Document output path '{outputPath}' does not include a directory.";
            return CreateFailure(request, sourcePath, outputPath, message);
        }

        try
        {
            Directory.CreateDirectory(outputDirectory);
            await using var result = await client.ConvertAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var markdown = result.Markdown ?? string.Empty;
            await File.WriteAllTextAsync(outputPath, markdown, cancellationToken).ConfigureAwait(false);

            return new WorkspaceDocumentMarkdownConversionResult(
                Succeeded: true,
                Message: $"Converted document '{Path.GetFileName(sourcePath)}' to markdown.",
                SourcePath: sourcePath,
                OutputPath: outputPath,
                MarkdownCharacterCount: markdown.Length,
                Diagnostics: string.Empty);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var message = $"Document conversion failed for '{sourcePath}'. {exception.Message}";
            return CreateFailure(request, sourcePath, outputPath, message);
        }
    }

    private static WorkspaceDocumentMarkdownConversionResult CreateFailure(
        WorkspaceDocumentMarkdownConversionRequest request,
        string sourcePath,
        string outputPath,
        string message)
        => new(
            Succeeded: false,
            Message: message,
            SourcePath: string.IsNullOrWhiteSpace(sourcePath) ? request.SourcePath : sourcePath,
            OutputPath: string.IsNullOrWhiteSpace(outputPath) ? request.OutputPath : outputPath,
            MarkdownCharacterCount: 0,
            Diagnostics: message);
}
