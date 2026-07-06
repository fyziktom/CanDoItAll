namespace CanDoItAll.AgentFramework.Core;

public interface IWorkspaceDocumentMarkdownConverter
{
    Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
        WorkspaceDocumentMarkdownConversionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceDocumentMarkdownConversionRequest(
    string SourcePath,
    string OutputPath);

public sealed record WorkspaceDocumentMarkdownConversionResult(
    bool Succeeded,
    string Message,
    string SourcePath,
    string OutputPath,
    int MarkdownCharacterCount,
    string Diagnostics);

