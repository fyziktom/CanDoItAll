using CanDoItAll.AgentFramework.Core;
using MarkItDown;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace CanDoItAll.Tools.Documents;

public sealed class ManagedCodeMarkItDownDocumentMarkdownConverter : IWorkspaceDocumentMarkdownConverter
{
    private readonly MarkItDownClient client = new(new MarkItDownOptions {
        RootPath = Path.Combine(Path.GetTempPath(), "candoitall-document-conversion")
    });

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

        string fullMarkdown;
        try
        {
            if (Path.GetExtension(sourcePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) {
                using var document = PdfDocument.Open(sourcePath);
                var pages = new List<string>();
                foreach (var page in document.GetPages()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    pages.Add(ContentOrderTextExtractor.GetText(page, addDoubleNewline: true).Trim());
                }
                fullMarkdown = string.Join("\n\n", pages);
                if (string.IsNullOrWhiteSpace(fullMarkdown)) {
                    return CreateFailure(sourcePath, "The PDF has no selectable text. Supply a text-bearing document; OCR is not configured.");
                }
            } else {
                await using var converted = await client.ConvertAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                fullMarkdown = converted.Markdown ?? string.Empty;
            }
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
        catch (Exception exception) when (exception is PdfDocumentFormatException or PdfDocumentEncryptedException) {
            return CreateFailure(sourcePath, "The PDF could not be read. It may be damaged or password protected.");
        }

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
