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

public sealed record WorkspaceImageInspectionResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Format,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    string Diagnostics);

public sealed record WorkspaceImageAnalysisResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Prompt,
    string ProviderName,
    string Model,
    string Analysis,
    int InputTokens,
    int OutputTokens,
    string Diagnostics);

public sealed record WorkspaceAnalyzedImageRecord(
    string Path,
    string Format,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    string Message,
    string Diagnostics);

public sealed record WorkspaceImagesAnalysisResult(
    bool Succeeded,
    string Message,
    IReadOnlyList<WorkspaceAnalyzedImageRecord> Images,
    IReadOnlyList<string> Paths,
    string Prompt,
    string ProviderName,
    string Model,
    string Analysis,
    int InputTokens,
    int OutputTokens,
    string Diagnostics);
