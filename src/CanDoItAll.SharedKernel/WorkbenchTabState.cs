namespace CanDoItAll.SharedKernel;

public static class WorkbenchTabKinds
{
    public const string Page = "page";
    public const string ProjectOverview = "project-overview";
    public const string ProjectStructure = "project-structure";
    public const string ProjectCalendar = "project-calendar";
    public const string PromptWizardSession = "prompt-wizard-session";
    public const string Processes = "processes";
    public const string ValidationRun = "validation-run";
    public const string TestPlan = "test-plan";
    public const string PromptDetail = "prompt-detail";
    public const string Settings = "settings";
}

public sealed record WorkbenchTabDescriptor(
    string TabId,
    string Title,
    string Route,
    string TabKind = WorkbenchTabKinds.Page,
    Guid? ProjectId = null,
    Guid? ArtifactId = null,
    string? ArtifactKind = null,
    string? ArtifactKey = null,
    string? RestoreKey = null,
    string? ProjectScope = null,
    string? ProjectName = null,
    string? PhaseName = null,
    string? Description = null,
    string? SnapshotJson = null,
    string? CapsuleKey = null,
    string? TabGroup = null,
    bool IsPinned = false,
    bool CanClose = true,
    bool CanSleep = true);

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
    string? ArtifactKey = null,
    string? ArtifactKind = null,
    Guid? ArtifactId = null,
    string? ProjectName = null,
    string? PhaseName = null,
    string? TabGroup = null,
    DateTimeOffset? ClosedAtUtc = null,
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
    IReadOnlyList<WorkbenchRestoreFailure>? RestoreFailures = null,
    IReadOnlyList<WorkbenchTabState>? RecentTabs = null,
    Guid? ProfileId = null,
    string? ProfileFingerprint = null);

public interface IWorkbenchStateStore
{
    ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default);
}
