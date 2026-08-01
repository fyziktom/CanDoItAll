using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public interface IProjectStructureRuntimeGateway
{
    Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default);

    Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
        Guid projectId,
        ProjectStructureRuntimeReadRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
        Guid projectId,
        ProjectStructureRuntimeNodeCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default);

    Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
        Guid projectId,
        ProjectStructureRuntimeAssetCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default);
}

public static class ProjectStructureRuntimeIdempotencyMetadata
{
    public const string MetadataPropertyName = "workflowProjectWrite";
    public const string IdempotencyKeyPropertyName = "idempotencyKey";
    public const string BatchIdempotencyKeyPropertyName = "batchIdempotencyKey";
}

public sealed class UnavailableProjectStructureRuntimeGateway : IProjectStructureRuntimeGateway
{
    public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
        => throw CreateException();

    public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(
        Guid projectId,
        ProjectStructureRuntimeReadRequest request,
        CancellationToken cancellationToken = default)
        => throw CreateException();

    public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(
        Guid projectId,
        ProjectStructureRuntimeNodeCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default)
        => throw CreateException();

    public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(
        Guid projectId,
        ProjectStructureRuntimeAssetCreateRequest request,
        ProjectStructureRuntimeAgentContext agent,
        CancellationToken cancellationToken = default)
        => throw CreateException();

    private static InvalidOperationException CreateException()
        => new("Project-structure executor requires IProjectStructureRuntimeGateway, but no project-structure gateway is registered in this host.");
}

public enum ProjectStructureRuntimeProjectStatus
{
    Draft,
    Active,
    OnHold,
    Completed,
    Archived
}

public enum ProjectStructureRuntimeProjectRole
{
    None,
    ActiveProject,
    Subproject,
    ParentProject,
    AdditionalParentProject
}

public sealed record ProjectStructureRuntimeAgentContext(
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string SessionId);

public sealed record ProjectStructureRuntimeProjectSummary(
    Guid Id,
    string Name,
    ProjectStructureRuntimeProjectStatus Status,
    string CurrentPhase,
    int PhaseCount,
    int ParentCount,
    int ChildCount,
    DateTimeOffset UpdatedAtUtc,
    string PrimaryCustomerName = "",
    string PrimaryDeliveryUnitName = "",
    string PrimaryOwnerName = "",
    string RelatedPartySearchText = "");

public sealed record ProjectStructureRuntimeReadRequest(
    IReadOnlyList<string>? NodeIds = null,
    IReadOnlyList<string>? SubtreeRootIds = null,
    IReadOnlyList<ProjectObjectType>? ObjectTypes = null,
    IReadOnlyList<ProjectStructureRuntimeProjectRole>? ProjectRoles = null,
    IReadOnlyList<string>? Statuses = null,
    bool OnlyUnfinished = false,
    int? MaxPriority = null,
    bool IncludeLinks = false,
    bool IncludeLayout = false,
    bool IncludeMetadata = false,
    bool IncludeNotes = false,
    bool IncludeAssets = false,
    int? Take = null);

public sealed record ProjectStructureRuntimeReadResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureRuntimeNodeSummary> Nodes,
    IReadOnlyList<ProjectStructureRuntimeLinkSummary> Links,
    IReadOnlyList<string> Warnings);

public sealed record ProjectStructureRuntimeNodeSummary(
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
    ProjectStructureRuntimeProjectRole ProjectRole,
    Guid? RelatedProjectId,
    int ParentProjectCount,
    double? X,
    double? Y,
    int? DurationSeconds = null,
    ProjectStructureRuntimeNodeActionCapabilities? ActionCapabilities = null);

public sealed record ProjectStructureRuntimeLinkSummary(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored);

public sealed record ProjectStructureRuntimeNodeActionCapabilities(
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
    IReadOnlyList<ProjectStructureRuntimeNodeActionDescriptor> Actions,
    IReadOnlyList<string> Guidance);

public sealed record ProjectStructureRuntimeNodeActionDescriptor(
    string ActionId,
    string Label,
    string Surface,
    string Description);

public sealed record ProjectStructureRuntimeMediaPayload(
    string FileName,
    string ContentType,
    string Base64Data);

public sealed record ProjectStructureRuntimeNodeCreateRequest(
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
    ProjectStructureRuntimeMediaPayload? Media = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    int? DurationSeconds = null,
    string? IdempotencyKey = null,
    string? IdempotencyBatchKey = null);

public sealed record ProjectStructureRuntimeAssetCreateRequest(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    ProjectStructureRuntimeMediaPayload? Media,
    string? ParentNodeKey = null,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    string? SourceWorkspacePath = null,
    string? SourceFileName = null,
    string? SourceContentType = null,
    string? IdempotencyKey = null,
    string? IdempotencyBatchKey = null);
