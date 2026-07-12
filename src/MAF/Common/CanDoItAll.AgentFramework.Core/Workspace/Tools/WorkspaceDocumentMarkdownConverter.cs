namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceDocumentMarkdownConverter
{
    Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
        WorkspaceDocumentMarkdownConversionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceDocumentMarkdownConversionRequest(
    string SourcePath,
    int? MaxCharacters = null);

public sealed record WorkspaceDocumentMarkdownConversionResult(
    bool Succeeded,
    string Message,
    string SourcePath,
    string Markdown,
    int TotalMarkdownCharacters,
    bool IsTruncated,
    string Diagnostics);
