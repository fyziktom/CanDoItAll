namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

internal sealed record WorkflowSourceCandidate(
    string Key,
    string Label,
    string Kind,
    string Value,
    string Origin);

internal sealed record WorkflowSourceIngestionFile(
    string FullPath,
    string DisplayPath,
    string FileName);

internal sealed record WorkflowSourceReadResult(
    string Text,
    int TotalCharacters,
    bool IsTruncated,
    string ExtractionStatus);

internal sealed record WorkflowSourceIngestionDocument(
    string Key,
    string Label,
    string Kind,
    string Origin,
    string Path,
    string FileName,
    string Extension,
    string Text,
    int TotalCharacters,
    bool IsTruncated,
    string ExtractionStatus);

internal sealed record WorkflowSourceIngestionError(
    string Key,
    string Label,
    string Kind,
    string Value,
    string Origin,
    string Message);
