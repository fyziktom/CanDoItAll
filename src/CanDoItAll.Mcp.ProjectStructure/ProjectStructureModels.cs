using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Mcp.ProjectStructure;

public sealed record OperationAck(bool Ok);

public sealed record ProjectStructureScopeInput(
    ProjectStructureLeaseScopeKind ScopeKind,
    Guid? ProjectId = null,
    string? NodeId = null,
    string? RepositoryRoot = null,
    string? BranchName = null);

public sealed record ProjectStructureResolvedScope(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string DisplayKey);

public sealed record ProjectStructureCompactNode(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string Route,
    int EffectivePriority,
    string ProgressMode,
    int ProgressPercent,
    string? Notes = null,
    string? MetadataJson = null,
    string? MediaOriginalFileName = null,
    string? MediaRelativePath = null,
    string? MediaContentType = null,
    double? X = null,
    double? Y = null,
    int? DurationSeconds = null);

public sealed record ProjectStructureReadToolData(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureCompactNode> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings);
