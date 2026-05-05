using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureAgentHttpHeaders
{
    public const string AgentId = "X-CanDoItAll-Agent-Id";
    public const string AgentName = "X-CanDoItAll-Agent-Name";
    public const string MachineName = "X-CanDoItAll-Agent-Machine";
    public const string RepositoryRoot = "X-CanDoItAll-Agent-RepoRoot";
    public const string BranchName = "X-CanDoItAll-Agent-Branch";
    public const string SessionId = "X-CanDoItAll-Agent-Session";
    public const string AgentToken = "X-CanDoItAll-Agent-Token";
    public const string EstimatedMinutes = "X-CanDoItAll-Estimated-Minutes";
}

public sealed record ProjectStructureAgentContext(
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string SessionId);

public enum ProjectStructureLeaseScopeKind
{
    Project,
    ProjectNode,
    RepoBranch
}

public sealed record ProjectStructureLeaseAcquireRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string Reason,
    int DurationMinutes = 15);

public sealed record ProjectStructureLeaseRenewRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken,
    int DurationMinutes = 15);

public sealed record ProjectStructureLeaseReleaseRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken);

public sealed record ProjectStructureLeaseSnapshot(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string Reason,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsActive);

public sealed record ProjectStructureLeaseConflict(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string Reason,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ProjectStructureProjectSaveRequest(
    string Name,
    string Description,
    string Objective,
    string CurrentPhase,
    ProjectStatus Status = ProjectStatus.Draft,
    DateTime? TargetDateUtc = null,
    string? LeaseToken = null);

public sealed record ProjectStructureSubprojectChangeRequest(
    Guid ChildProjectId,
    Guid? CurrentParentProjectId = null,
    string? LeaseToken = null);

public sealed record ProjectStructureReadRequest(
    IReadOnlyList<string>? NodeIds = null,
    IReadOnlyList<string>? SubtreeRootIds = null,
    IReadOnlyList<ProjectObjectType>? ObjectTypes = null,
    IReadOnlyList<ProjectStructureProjectRole>? ProjectRoles = null,
    IReadOnlyList<string>? Statuses = null,
    bool OnlyUnfinished = false,
    int? MaxPriority = null,
    bool IncludeLinks = false,
    bool IncludeLayout = false,
    bool IncludeMetadata = false,
    bool IncludeNotes = false,
    bool IncludeAssets = false,
    int? Take = null);

public sealed record ProjectStructureNodeActionDescriptor(
    string ActionId,
    string Label,
    string Surface,
    string Description);

public sealed record ProjectStructureNodeActionCapabilities(
    bool CanRunNormally,
    bool CanRunAsAdministrator,
    bool CanOpenInFileExplorer,
    bool CanOpenInNewTab,
    string RuntimeDisplayName,
    string RuntimeDisplayCommand,
    string RuntimeWorkingDirectory,
    string OpenInNewTabRoute,
    string StorageProvider,
    string StorageLocatorKind,
    string StorageLocator,
    IReadOnlyList<ProjectStructureNodeActionDescriptor> Actions,
    IReadOnlyList<string> Guidance);

public sealed record ProjectStructureNodeSummary(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string? Notes,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId,
    string? MediaRelativePath,
    string? MediaContentType,
    string? MediaOriginalFileName,
    IReadOnlyList<string> Badges,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? MetadataJson,
    ProjectStructureProjectRole ProjectRole,
    Guid? RelatedProjectId,
    int ParentProjectCount,
    double? X,
    double? Y,
    int? DurationSeconds = null,
    ProjectStructureNodeActionCapabilities? ActionCapabilities = null);

public sealed record ProjectStructureLinkSummary(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored);

public sealed record ProjectStructureReadResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureNodeSummary> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings);

[JsonConverter(typeof(ProjectStructureNodeCreateInputJsonConverter))]
public sealed record ProjectStructureNodeCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    string? ParentNodeKey,
    double? X = null,
    double? Y = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null,
    ProjectObjectMediaPayload? Media = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

[JsonConverter(typeof(ProjectStructureNodeEditInputJsonConverter))]
public sealed record ProjectStructureNodeEditInput(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectType? ObjectType = null,
    string? ObjectSubtype = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

public sealed record ProjectStructureNodeMetadataInput(
    string MetadataJson,
    string? Notes = null,
    string? Status = null,
    string? LeaseToken = null);

public sealed record ProjectStructureStatusBatchInput(
    IReadOnlyList<string> NodeIds,
    string Status,
    string? LeaseToken = null);

public sealed record ProjectStructureStatusInput(
    string Status,
    string? LeaseToken = null);

public sealed record ProjectStructureProgressBatchInput(
    IReadOnlyList<string> NodeIds,
    string ProgressMode,
    int ProgressPercent,
    string? LeaseToken = null);

public sealed record ProjectStructureProgressInput(
    string ProgressMode,
    int ProgressPercent,
    string? LeaseToken = null);

public sealed record ProjectStructureMarkerBatchInput(
    IReadOnlyList<string> NodeIds,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    string? LeaseToken = null);

public enum ProjectStructureMarkerMutationMode
{
    Replace,
    Add,
    Toggle,
    Remove,
    Clear
}

public sealed record ProjectStructureMarkerInput(
    ProjectStructureMarkerMutationMode Mode = ProjectStructureMarkerMutationMode.Replace,
    string MarkerIcon = "",
    string MarkerTone = "",
    string MarkerLabel = "",
    string? LeaseToken = null);

public sealed record ProjectStructurePriorityBatchInput(
    IReadOnlyList<string> NodeIds,
    int Priority,
    string? LeaseToken = null);

public sealed record ProjectStructurePriorityInput(
    int Priority,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeTypeInput(
    ProjectObjectType ObjectType,
    string? ObjectSubtype = null,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeMoveInput(
    string NodeId,
    double X,
    double Y,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeRecomposeInput(
    string RootNodeId,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeReparentInput(
    string NodeId,
    string? ParentNodeKey,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeParentInput(
    string? ParentNodeKey,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeDeleteInput(
    string? LeaseToken = null);

public sealed record ProjectStructureLinkInput(
    string SourceNodeId,
    string TargetNodeId,
    ProjectObjectLinkKind Kind = ProjectObjectLinkKind.DependsOn,
    string? LeaseToken = null);

public sealed record ProjectStructureLinkChangeResult(
    bool Changed,
    ProjectStructureLinkSummary Link);

public sealed record ProjectStructureProcessDefinitionLinkInput(
    Guid ProcessDefinitionId,
    string? LeaseToken = null);

public sealed record ProjectStructureNodeCommandInput(
    ProjectStructureCommandKind CommandKind,
    string? LeaseToken = null);

public sealed record ProjectStructureSubtreeTransferInput(
    Guid TargetProjectId,
    string? LeaseToken = null);

public sealed record ProjectStructureAssetCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload Media,
    string? ParentNodeKey = null,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null);

public sealed record ProjectStructureApprovalRequestCreateInput(
    string Title,
    string Subtitle,
    string Notes,
    string RequestedOperation,
    string? ParentNodeKey = null,
    int? EstimatedMinutes = null,
    string? MetadataJson = null,
    string? LeaseToken = null);

public sealed record ProjectStructureAssetDescriptor(
    Guid ProjectId,
    string NodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Route,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    string MetadataJson,
    bool IsReadonly,
    string RevisionParentNodeId);

public sealed record ProjectStructureAssetContentDescriptor(
    ProjectStructureAssetDescriptor Asset,
    long ContentLength,
    string Base64Data);

public sealed record ProjectStructureAssetRevisionRequest(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload Media,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null);

public sealed record ProjectStructureChecklistRequest(
    int? MaxPriority = null,
    IReadOnlyList<ProjectObjectType>? ObjectTypes = null,
    bool IncludePaused = false,
    int? Take = null);

public sealed record ProjectStructureChecklistPrerequisite(
    string NodeId,
    string Title,
    string Status,
    int EffectivePriority,
    string Reason);

public sealed record ProjectStructureChecklistItem(
    string NodeId,
    string? ParentNodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    string Route,
    IReadOnlyList<ProjectStructureChecklistPrerequisite> Prerequisites);

public sealed record ProjectStructureChecklistResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureChecklistItem> Items,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureDependencyQueryRequest(
    IReadOnlyList<string>? NodeIds = null,
    bool IncludeFinished = true,
    int? DefaultDurationSeconds = null,
    int? Take = null);

public sealed record ProjectStructureDependencyRelationSummary(
    string NodeId,
    string Title,
    string Status,
    int EffectivePriority,
    bool IsFinished,
    string Reason);

public sealed record ProjectStructureDependencyItem(
    string NodeId,
    string? ParentNodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    bool IsFinished,
    bool IsPausedOrStopped,
    bool CanExecute,
    int? DurationSeconds,
    int EffectiveDurationSeconds,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string Route,
    IReadOnlyList<ProjectStructureDependencyRelationSummary> Prerequisites,
    IReadOnlyList<ProjectStructureDependencyRelationSummary> Dependents);

public sealed record ProjectStructureDependencyResponse(
    Guid ProjectId,
    string ProjectName,
    int DefaultDurationSeconds,
    IReadOnlyList<ProjectStructureDependencyItem> Items,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureProcessNodeStartInput(
    Guid? ProcessDefinitionId = null,
    bool RunHrMatch = true,
    bool Execute = false,
    bool IncludeLaunchPlan = false,
    string RequestedBy = "project-structure-api",
    string? LeaseToken = null);

public sealed record ProjectStructureProcessNodeStartResult(
    Guid ProjectId,
    string NodeId,
    Guid ProcessDefinitionId,
    Guid LaunchPlanId,
    Guid? RunId,
    string Stage,
    string Route,
    ProcessLaunchPlanDetails? LaunchPlan,
    IReadOnlyList<string> Warnings);

public enum ProjectStructureImportSourceKind
{
    Mermaid,
    DocxOutline,
    XmindMap,
    JsonOutline
}

public sealed record ProjectStructureImportRequest(
    Guid ProjectId,
    string? ParentNodeKey,
    ProjectStructureImportSourceKind SourceKind,
    string Title,
    string? SourceText = null,
    ProjectObjectMediaPayload? SourceAsset = null,
    string ContainerBlockSubtype = "delivery",
    string LeafWorkItemSubtype = "task",
    string? LeaseToken = null);

public sealed record ProjectStructureImportResult(
    Guid ProjectId,
    string ContainerNodeId,
    string? SourceNodeId,
    IReadOnlyList<string> CreatedNodeIds,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureAnalyticsQueryRequest(
    Guid? ProjectId = null,
    string? OperationName = null,
    string? AgentId = null,
    bool? Succeeded = null,
    int Take = 50);

public sealed record ProjectStructureAnalyticsEntry(
    Guid Id,
    string OperationName,
    Guid? ProjectId,
    string? NodeKey,
    ProjectStructureLeaseScopeKind? ScopeKind,
    string? ScopeKey,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    bool Succeeded,
    long DurationMs,
    int WarningCount,
    string? ErrorCode,
    string? ErrorMessage,
    string RequestSummaryJson,
    string ResponseSummaryJson,
    string WarningsJson,
    DateTimeOffset OccurredAtUtc);

public sealed record ProjectStructureAnalyticsResponse(
    IReadOnlyList<ProjectStructureAnalyticsEntry> Entries);

public enum ProjectManagementGuidanceCategory
{
    Mission,
    Planning,
    Estimation,
    Approval,
    Reporting,
    Risk,
    Collaboration
}

public sealed record ProjectManagementGuidanceQueryRequest(
    IReadOnlyList<ProjectManagementGuidanceCategory>? Categories = null,
    string? Query = null,
    int Take = 10);

public sealed record ProjectManagementGuidanceEntry(
    string Id,
    ProjectManagementGuidanceCategory Category,
    string Title,
    string Summary,
    string Guidance,
    IReadOnlyList<string> Tags,
    bool IsMissionAnchor);

public sealed record ProjectManagementGuidanceResponse(
    IReadOnlyList<ProjectManagementGuidanceEntry> Entries);

public class ProjectStructureAgentException : Exception
{
    public ProjectStructureAgentException(int statusCode, string errorCode, string message, object? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public object? Details { get; }
}

public sealed class ProjectStructureLeaseConflictException : ProjectStructureAgentException
{
    public ProjectStructureLeaseConflictException(ProjectStructureLeaseConflict conflict)
        : base(409, "LeaseConflict", $"Scope '{conflict.ScopeKey}' is currently leased by '{conflict.AgentName}' on '{conflict.MachineName}'.", conflict)
    {
        Conflict = conflict;
    }

    public ProjectStructureLeaseConflict Conflict { get; }
}
