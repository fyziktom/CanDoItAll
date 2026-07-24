namespace CanDoItAll.Memory.SourceGateway;

public static class MemorySourceSnapshotProviderVersions
{
    public const string WorkbenchProjectStructure = "workbench-project-structure-v2";
    public const string ProcessRuntime = "process-runtime-evidence-v2";
    public const string WorkflowRuntime = "workflow-runtime-evidence-v2";
    public const string CrmHr = "crm-hr-source-v2";
    public const string ResourceCatalog = "resource-catalog-source-v1";
    public const string ManualInput = "manual-input-source-v1";
}

public interface IProjectStructureSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProjectStructureSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeEvidenceSourceProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ProcessRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowRuntimeEvidenceSourceProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        WorkflowRuntimeEvidenceSourceRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICrmHrSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        CrmHrSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IResourceSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ResourceSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public interface IManualSourceSnapshotProvider
{
    Task<MemorySourceSnapshot> ReadSnapshotAsync(
        ManualSourceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectStructureSourceSnapshotRequest(
    Guid ProjectId,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ProcessRuntimeEvidenceSourceRequest(
    Guid? ProcessRunId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record WorkflowRuntimeEvidenceSourceRequest(
    Guid? RunId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record CrmHrSourceSnapshotRequest(
    Guid? PartyId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ResourceSourceSnapshotRequest(
    Guid? ResourceId = null,
    Guid? ProjectId = null,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);

public sealed record ManualSourceSnapshotRequest(
    Guid SourceId,
    string PayloadKind,
    string Title,
    string ContentText,
    string Locator,
    string ContentType,
    string SourceCategory,
    IReadOnlyList<string> Tags,
    MemorySourceSnapshotCursor? Cursor = null,
    int? Take = null);
