using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.SharedKernel;
using System.Text.Json;
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

[JsonConverter(typeof(FlexibleProjectStructureLeaseScopeKindJsonConverter))]
public enum ProjectStructureLeaseScopeKind
{
    Project,
    ProjectNode,
    RepoBranch
}

internal sealed class FlexibleProjectStructureLeaseScopeKindJsonConverter : JsonConverter<ProjectStructureLeaseScopeKind>
{
    public override ProjectStructureLeaseScopeKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number &&
            reader.TryGetInt32(out var numericValue) &&
            Enum.IsDefined(typeof(ProjectStructureLeaseScopeKind), numericValue))
        {
            return (ProjectStructureLeaseScopeKind)numericValue;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var rawValue = reader.GetString();
            if (int.TryParse(rawValue, out numericValue) &&
                Enum.IsDefined(typeof(ProjectStructureLeaseScopeKind), numericValue))
            {
                return (ProjectStructureLeaseScopeKind)numericValue;
            }

            if (Enum.TryParse<ProjectStructureLeaseScopeKind>(rawValue, ignoreCase: true, out var parsedValue) &&
                Enum.IsDefined(parsedValue))
            {
                return parsedValue;
            }
        }

        throw new JsonException("Invalid project-structure lease scope kind. Use Project, ProjectNode, RepoBranch, or the corresponding numeric value.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ProjectStructureLeaseScopeKind value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }
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

public sealed record ProjectStructureNodeCatalogResponse(
    IReadOnlyList<ProjectStructureNodeCatalogItem> Items,
    IReadOnlyList<ProjectStructureNodeCatalogObjectType> ObjectTypes,
    IReadOnlyList<ProjectStructureLinkKindCatalogItem> LinkKinds,
    IReadOnlyList<string> Guidance);

public sealed record ProjectStructureNodeCatalogItem(
    string ActionId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string GroupKey,
    string Label,
    string Description,
    string DefaultTitle,
    string TitleLabel,
    string SubtitleLabel,
    string NotesLabel,
    bool RequiresFile,
    string AcceptedFileTypes,
    IReadOnlyList<ProjectStructureNodeCatalogField> InputFields,
    IReadOnlyList<ProjectStructureNodeCatalogDefaultValue> DefaultInputValues,
    IReadOnlyList<string> Aliases);

public sealed record ProjectStructureNodeCatalogField(
    string Key,
    string Label,
    string InputMode,
    string Placeholder,
    bool IsRequired,
    IReadOnlyList<ProjectStructureNodeCatalogOption> Options);

public sealed record ProjectStructureNodeCatalogOption(
    string Value,
    string Label);

public sealed record ProjectStructureNodeCatalogDefaultValue(
    string Key,
    string Value);

public sealed record ProjectStructureNodeCatalogObjectType(
    ProjectObjectType ObjectType,
    string Label,
    bool IsUserCreatable,
    IReadOnlyList<string> CreatableSubtypes);

public sealed record ProjectStructureLinkKindCatalogItem(
    ProjectObjectLinkKind Kind,
    string Label,
    string Guidance);

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

public enum ProjectStructureWorkflowInputSourceKind
{
    ParentNode,
    ParentSubtree,
    SelectedNode,
    FilePath,
    FolderPath,
    ManualJson
}

public sealed record ProjectStructureWorkflowInputSource(
    ProjectStructureWorkflowInputSourceKind Kind,
    string Key,
    string Label,
    string Value,
    bool IsEnabled = true);

public sealed class ProjectStructureWorkflowInputSettings
{
    public bool IncludeProject { get; set; } = true;

    public bool IncludeParentNode { get; set; } = true;

    public bool IncludeParentNodeDetails { get; set; } = true;

    public bool IncludeParentSubtree { get; set; }

    public bool IncludeAssets { get; set; } = true;

    public IReadOnlyList<string> SelectedNodeIds { get; set; } = [];

    public IReadOnlyList<ProjectStructureWorkflowInputSource> AdditionalSources { get; set; } = [];

    public string ManualInputJson { get; set; } = "{}";

    public static ProjectStructureWorkflowInputSettings Default() => new();
}

public sealed record ProjectStructureWorkflowNodeCreateInput(
    WorkflowId WorkflowId,
    WorkflowVersionId? VersionId = null,
    string? Title = null,
    string? Subtitle = null,
    string? Notes = null,
    ProjectStructureWorkflowInputSettings? InputSettings = null,
    double? X = null,
    double? Y = null,
    string? LeaseToken = null);

public sealed record ProjectStructureWorkflowNodeCreateResult(
    Guid ProjectId,
    ProjectStructureNodeSummary Node,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureWorkflowAddOptionsInput(
    WorkflowId? WorkflowId = null,
    WorkflowVersionId? VersionId = null,
    ProjectStructureWorkflowInputSettings? InputSettings = null,
    IReadOnlyList<string>? SelectedNodeIds = null);

public sealed record ProjectStructureWorkflowDefinitionOption(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    string DisplayName,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowRuntimeBackendKind PreferredBackend,
    bool IsSelectable,
    string DisabledReason);

public sealed record ProjectStructureWorkflowInputPreviewRow(
    string Label,
    string Value);

public sealed record ProjectStructureWorkflowInputPreviewSection(
    string Title,
    string Summary,
    IReadOnlyList<ProjectStructureWorkflowInputPreviewRow> Rows);

public sealed record ProjectStructureWorkflowInputPreview(
    string Summary,
    string InputJson,
    IReadOnlyList<ProjectStructureWorkflowInputPreviewSection> Sections);

public sealed record ProjectStructureWorkflowAddOptionsResult(
    Guid ProjectId,
    ProjectStructureNodeSummary ParentNode,
    IReadOnlyList<ProjectStructureWorkflowDefinitionOption> Workflows,
    WorkflowId? SelectedWorkflowId,
    WorkflowVersionId? SelectedVersionId,
    ProjectStructureWorkflowInputSettings InputSettings,
    ProjectStructureWorkflowInputPreview Preview,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureWorkflowNodeStartInput(
    WorkflowRuntimeBackendKind? RequestedBackend = null,
    string RequestedBy = "project-structure",
    string? LeaseToken = null);

public sealed record ProjectStructureWorkflowRunEventSummary(
    WorkflowEventKind Kind,
    string Message,
    string NodeId,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectStructureWorkflowRunArtifactSummary(
    WorkflowArtifactKind Kind,
    string Name,
    string ContentType,
    string StoragePath,
    string Summary);

public sealed record ProjectStructureWorkflowExecutionSummary(
    WorkflowRunId? RunId,
    WorkflowRunState State,
    string WorkflowName,
    string RunSummary,
    int CurrentStepIndex,
    int StepCount,
    IReadOnlyList<ProjectStructureWorkflowRunArtifactSummary> Artifacts,
    IReadOnlyList<string> CreatedNodeIds,
    IReadOnlyList<string> CreatedAssetIds,
    IReadOnlyList<string> CreatedFilePaths);

public sealed record ProjectStructureWorkflowRunStatus(
    WorkflowRunId? RunId,
    WorkflowRunState State,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    int CurrentStepIndex,
    int StepCount,
    string Message,
    ProjectStructureWorkflowExecutionSummary Summary,
    IReadOnlyList<ProjectStructureWorkflowRunEventSummary> RecentEvents);

public sealed record ProjectStructureWorkflowNodeStartResult(
    Guid ProjectId,
    string NodeId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowRunId RunId,
    string Route,
    ProjectStructureWorkflowRunStatus Status,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureNodeCommandInput(
    ProjectStructureCommandKind CommandKind,
    string? LeaseToken = null);

public sealed record ProjectStructureSubtreeTransferInput(
    Guid TargetProjectId,
    string? LeaseToken = null);

public sealed record ProjectStructureNodesToSubprojectInput(
    string Name,
    IReadOnlyList<string> NodeIds,
    string? Description = null,
    string? Objective = null,
    string? CurrentPhase = null,
    ProjectStatus Status = ProjectStatus.Active,
    bool IncludeDescendants = true,
    string? LeaseToken = null);

public sealed record ProjectStructureNodesToSubprojectResult(
    Guid SourceProjectId,
    Guid TargetProjectId,
    string TargetProjectName,
    IReadOnlyList<string> RequestedNodeIds,
    IReadOnlyList<string> MovedNodeIds,
    int MovedNodeCount,
    int MovedRootCount,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureAssetCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload? Media,
    string? ParentNodeKey = null,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    string? SourceWorkspacePath = null,
    string? SourceFileName = null,
    string? SourceContentType = null);

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
