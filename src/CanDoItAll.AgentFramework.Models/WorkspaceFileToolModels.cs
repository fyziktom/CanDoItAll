namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkspaceArtifactReference(
    string Zone,
    string RelativePath,
    string DisplayName,
    string ContentType,
    string Summary);

public sealed record WorkspaceToolReceipt(
    string Operation,
    bool MutatesWorkspace,
    string Boundary,
    string Outcome,
    string Message,
    string ReceiptRelativePath,
    IReadOnlyList<string> TargetPaths,
    IReadOnlyList<WorkspaceArtifactReference> ArtifactReferences,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public Guid? ExecutionRunId { get; init; }
}

public sealed record WorkspaceFileListEntry(
    string RelativePath,
    string PathKind,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc);

public sealed record WorkspaceFileListResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string RootPath,
    string SearchPattern,
    IReadOnlyList<WorkspaceFileListEntry> Entries,
    bool IsTruncated);

public sealed record WorkspaceTextSearchMatch(
    string RelativePath,
    int Score,
    string Snippet);

public sealed record WorkspaceTextSearchResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Query,
    string RootPath,
    IReadOnlyList<WorkspaceTextSearchMatch> Matches,
    bool IsTruncated);

public sealed record WorkspaceTextFileReadResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Content,
    int TotalCharacters,
    bool IsTruncated);

public sealed record WorkspacePathStatResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    bool Exists,
    string PathKind,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc,
    int? ChildCount);

public sealed record WorkspaceFileMutationResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string? DestinationPath,
    string PathKind,
    bool PathExistedBefore,
    bool CreatedNewPath,
    bool OverwroteExistingPath,
    int CharacterCount);

public sealed record WorkspaceTextDiffResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string LeftPath,
    string RightPath,
    string DiffPreview,
    int AddedLineCount,
    int RemovedLineCount,
    bool IsTruncated);
