using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed record OperationAck(bool Ok);

public sealed record OperationCount(
    int Count,
    IReadOnlyList<ProjectStructureDeletionWarning>? DeletionWarnings = null)
{
    public IReadOnlyList<string> Warnings
        => (DeletionWarnings ?? [])
            .Select(warning => $"{warning.Message} {warning.Remediation}")
            .ToArray();
}

public sealed record ProjectStructureScopeInput(
    ProjectStructureLeaseScopeKind ScopeKind,
    Guid? ProjectId = null,
    string? NodeId = null,
    string? RepositoryRoot = null,
    string? BranchName = null);

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
    int? DurationSeconds = null,
    ProjectStructureNodeActionCapabilities? ActionCapabilities = null);

public sealed record ProjectStructureReadToolData(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureCompactNode> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings,
    ProjectStructureReadSource Source = ProjectStructureReadSource.CanonicalCurrent)
{
    [JsonPropertyOrder(-100)]
    public int NodeCount => Nodes.Count;

    [JsonPropertyOrder(-99)]
    public int LinkCount => Links.Count;
}
