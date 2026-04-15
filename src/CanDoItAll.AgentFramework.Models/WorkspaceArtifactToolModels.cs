namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkspaceDocumentConversionResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string SourcePath,
    string OutputPath,
    string MarkdownPreview,
    bool PreviewTruncated,
    string Diagnostics);

public sealed record WorkspaceSpreadsheetInspectionResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Preview,
    bool PreviewTruncated,
    string Diagnostics);
