namespace CanDoItAll.SharedKernel;

public sealed record WorkbenchTabState(
    string TabId,
    string Title,
    string Route,
    bool IsDirty = false,
    bool IsSleeping = false,
    bool IsPinned = false,
    bool CanClose = true,
    string? ProjectScope = null,
    string TabKind = "page",
    Guid? ProjectId = null,
    string? RestoreKey = null,
    bool CanSleep = true,
    string? CapsuleKey = null,
    string? Description = null,
    string? SnapshotJson = null,
    DateTimeOffset? LastActivatedAtUtc = null,
    int Order = 0);

public sealed record WorkbenchRestoreFailure(
    string TabId,
    string Title,
    string Summary);

public sealed record WorkbenchRestoreReport(
    DateTimeOffset RestoredAtUtc,
    int RestoredCount,
    IReadOnlyList<WorkbenchRestoreFailure> Failures);

public sealed record WorkbenchSessionSnapshot(
    int Version,
    string? ActiveTabId,
    IReadOnlyList<WorkbenchTabState> Tabs,
    string? CompatibilityMarker = null,
    DateTimeOffset? SavedAtUtc = null,
    IReadOnlyList<WorkbenchRestoreFailure>? RestoreFailures = null);

public interface IWorkbenchStateStore
{
    ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default);
}
