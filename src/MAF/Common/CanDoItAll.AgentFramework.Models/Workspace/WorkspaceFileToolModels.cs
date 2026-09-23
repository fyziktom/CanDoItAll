using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public interface IWorkspaceToolOperationResult
{
    bool Succeeded { get; }

    string Message { get; }
}

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

    public ToolExecutionSideEffectMode DeclaredSideEffectMode { get; init; }
}

public sealed record WorkspaceFileListEntry(
    string RelativePath,
    string PathKind,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc);

public sealed class WorkspaceFileReadSelection(string rootPath, bool isWorkspacePath, ImmutableArray<string> selectedPaths) {
    private readonly string root = Path.IsPathFullyQualified(rootPath)
        ? rootPath : throw new ArgumentException("A file read selection requires its original physical root.", nameof(rootPath));
    private readonly ImmutableArray<string> paths = !selectedPaths.IsDefault
        ? selectedPaths : throw new ArgumentException("A file read selection requires its selected physical paths.", nameof(selectedPaths));

    public bool IsWorkspacePath { get; } = isWorkspacePath;
    public int Count => paths.Length;
    public string GetRootPath() => root;
    public ImmutableArray<string> GetSelectedPaths() => paths;
    public override string ToString() => $"Workspace file read selection ({Count} targets).";
}

public sealed record WorkspaceFileListResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string RootPath,
    string SearchPattern,
    IReadOnlyList<WorkspaceFileListEntry> Entries,
    bool IsTruncated) : IWorkspaceToolOperationResult {
    [JsonIgnore]
    public WorkspaceFileReadSelection? ReadSelection { get; init; }
}

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
    bool IsTruncated) : IWorkspaceToolOperationResult {
    [JsonIgnore]
    public WorkspaceFileReadSelection? ReadSelection { get; init; }
}

public sealed record WorkspaceTextFileReadResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string Content,
    int TotalCharacters,
    bool IsTruncated) : IWorkspaceToolOperationResult;

public sealed record WorkspacePathStatResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    bool Exists,
    string PathKind,
    long? SizeBytes,
    DateTimeOffset? LastWriteTimeUtc,
    int? ChildCount) : IWorkspaceToolOperationResult;

public sealed record WorkspacePathHashResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string Path,
    string PathKind,
    string Algorithm,
    string Hash,
    long SizeBytes,
    int FileCount,
    bool IsTruncated) : IWorkspaceToolOperationResult;

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
    int CharacterCount) : IWorkspaceToolOperationResult;

public sealed record WorkspaceArchiveMutationResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string SourcePath,
    string DestinationPath,
    int FileCount,
    long TotalBytes,
    bool IsTruncated) : IWorkspaceToolOperationResult;

public sealed record WorkspaceTextDiffResult(
    bool Succeeded,
    string Message,
    WorkspaceToolReceipt Receipt,
    string LeftPath,
    string RightPath,
    string DiffPreview,
    int AddedLineCount,
    int RemovedLineCount,
    bool IsTruncated) : IWorkspaceToolOperationResult;
