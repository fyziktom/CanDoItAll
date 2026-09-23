using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureDisclosureState {
    Complete,
    HistoricalSourceUnavailable,
    MissingOriginalLifetime,
    LifetimeChangedDuringOperation,
    UnsupportedResult
}

internal sealed record ProjectStructureDisclosureEvidence(string ToolName, Guid DatabaseProfileId, Guid AgentId,
    ProjectStructureDisclosureState State, ImmutableArray<ProjectWriteAdmission> Targets, ImmutableArray<Guid> DirectAccessProjectIds);

internal static class ProjectStructureDisclosureEvidenceCodec {
    private const string Format = "workbench-structure-result-authority";
    private const int Version = 1;

    internal static AgentToolProtocolEnvelope Write(ProjectStructureDisclosureEvidence evidence) {
        Validate(evidence);
        return AgentToolProtocolEnvelope.Create(Format, Version, JsonSerializer.Serialize(evidence));
    }

    internal static ProjectStructureDisclosureEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != Version) {
            throw new InvalidDataException("The saved Structure result has unsupported owner disclosure evidence.");
        }
        var evidence = JsonSerializer.Deserialize<ProjectStructureDisclosureEvidence>(envelope.PayloadJson)
            ?? throw new InvalidDataException("The saved Structure result has no owner disclosure evidence.");
        Validate(evidence);
        return evidence;
    }

    private static void Validate(ProjectStructureDisclosureEvidence evidence) {
        if (string.IsNullOrWhiteSpace(evidence.ToolName) || evidence.DatabaseProfileId == Guid.Empty || evidence.AgentId == Guid.Empty ||
                !Enum.IsDefined(evidence.State) || evidence.Targets.IsDefault || evidence.DirectAccessProjectIds.IsDefault ||
                evidence.Targets.Any(target => target is null || target.DatabaseProfileId != evidence.DatabaseProfileId) ||
                evidence.Targets.Select(target => target.ProjectId).Distinct().Count() != evidence.Targets.Length ||
                evidence.DirectAccessProjectIds.Any(id => !evidence.Targets.Any(target => target.ProjectId == id)) ||
                evidence.DirectAccessProjectIds.Distinct().Count() != evidence.DirectAccessProjectIds.Length) {
            throw new InvalidDataException("The saved Structure result authority is invalid.");
        }
    }
}

internal sealed class ProjectStructureResultEvidenceScope(
    string toolName, Guid databaseProfileId, Guid agentId, Func<Guid, CancellationToken, Task<ProjectWriteAdmission?>> capture,
    IReadOnlyDictionary<Guid, ProjectWriteAdmission> initial, bool capturedCollection) {
    private static readonly AsyncLocal<ProjectStructureResultEvidenceScope?> Current = new();
    private readonly Dictionary<Guid, ProjectWriteAdmission> original = new(initial);
    private readonly HashSet<Guid> requested = [];
    private readonly HashSet<Guid> directAccess = [];
    private readonly HashSet<Guid> observed = [];
    private ProjectStructureDisclosureState state;
    private bool hasResult;

    internal IReadOnlySet<Guid> TargetIds => requested;

    internal IDisposable Bind() {
        var previous = Current.Value;
        Current.Value = this;
        return new RestoreBinding(this, previous);
    }

    internal async Task CaptureAsync(Guid? projectId, CancellationToken cancellationToken, bool requiresDirectAccess = true) {
        if (projectId is not { } id || id == Guid.Empty) {
            return;
        }
        if (requiresDirectAccess) {
            directAccess.Add(id);
        }
        if (!requested.Add(id) || original.ContainsKey(id)) {
            return;
        }
        if (!capturedCollection && await capture(id, cancellationToken) is { } admission) {
            original.Add(id, admission);
        }
    }

    internal static Task CaptureProjectAsync(Guid? projectId, CancellationToken cancellationToken)
        => Current.Value?.CaptureAsync(projectId, cancellationToken) ?? Task.CompletedTask;

    internal static void RecordAdmission(ProjectWriteAdmission admission) => Current.Value?.Record(admission);

    internal static void RecordReservation(ProjectCreationReservation reservation) {
        if (Current.Value is not { } current) {
            return;
        }
        current.Record(new(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId));
        if (reservation.ParentProjectId is { } parent && reservation.ParentLifetimeId is { } lifetime) {
            current.Record(new(reservation.DatabaseProfileId, parent, lifetime));
        }
    }

    internal static void RecordResult(object? result) => Current.Value?.Observe(result);

    internal static void RecordWorkflowPreview(IEnumerable<ProjectStructureNode> nodes) {
        if (Current.Value is not { } current || current.ToolName != ProjectStructureToolPolicy.ProjectStructureNodeWorkflowAddOptions) {
            return;
        }
        foreach (var node in nodes) {
            if (node.RelatedProjectId is { } related) {
                current.observed.Add(related);
            }
            current.AddNodeKeys([node.Id, node.ParentId]);
        }
    }

    private string ToolName => toolName;

    internal void Record(ProjectWriteAdmission admission) {
        requested.Add(admission.ProjectId);
        directAccess.Add(admission.ProjectId);
        if (admission.DatabaseProfileId != databaseProfileId ||
                original.TryGetValue(admission.ProjectId, out var prior) && prior != admission) {
            MarkUnavailable(ProjectStructureDisclosureState.LifetimeChangedDuringOperation);
            return;
        }
        original[admission.ProjectId] = admission;
    }

    internal void RecordKnownFailure() {
        hasResult = true;
    }

    internal ProjectStructureDisclosureEvidence Finish(IReadOnlyDictionary<Guid, ProjectWriteAdmission> current) {
        if (!hasResult) {
            MarkUnavailable(ProjectStructureDisclosureState.UnsupportedResult);
        }
        requested.UnionWith(observed);
        var retained = ImmutableArray.CreateBuilder<ProjectWriteAdmission>();
        foreach (var id in requested.Order()) {
            if (!original.TryGetValue(id, out var admission)) {
                MarkUnavailable(ProjectStructureDisclosureState.MissingOriginalLifetime);
                continue;
            }
            retained.Add(admission);
            if (!current.TryGetValue(id, out var now) || now != admission) {
                MarkUnavailable(ProjectStructureDisclosureState.LifetimeChangedDuringOperation);
            }
        }
        return new(toolName, databaseProfileId, agentId, state, retained.ToImmutable(),
            directAccess.Where(original.ContainsKey).Order().ToImmutableArray());
    }

    private void Observe(object? result) {
        hasResult = true;
        switch (result) {
            case IReadOnlyList<ProjectSummary> summaries:
                observed.UnionWith(summaries.Select(project => project.Id));
                directAccess.UnionWith(summaries.Select(project => project.Id));
                break;
            case ProjectSummary project:
                observed.Add(project.Id);
                break;
            case ProjectHierarchySnapshot hierarchy:
                observed.Add(hierarchy.ProjectId);
                observed.UnionWith(hierarchy.ParentProjects.Concat(hierarchy.ChildProjects).Select(project => project.Id));
                break;
            case ProjectStructureReadToolData read:
                observed.Add(read.ProjectId);
                AddNodeKeys(read.Nodes.Select(node => node.Id).Concat(read.Nodes.Select(node => node.ParentId))
                    .Concat(read.Links.SelectMany(link => new[] { link.SourceId, link.TargetId })));
                if (read.Source != ProjectStructureReadSource.CanonicalCurrent) {
                    MarkUnavailable(ProjectStructureDisclosureState.HistoricalSourceUnavailable);
                }
                break;
            case ProjectStructureNodeSummary node:
                AddNode(node);
                break;
            case ProjectStructureChecklistResponse checklist:
                observed.Add(checklist.ProjectId);
                AddNodeKeys(checklist.Items.Select(item => item.NodeId).Concat(checklist.Items.Select(item => item.ParentNodeId))
                    .Concat(checklist.Items.SelectMany(item => item.Prerequisites.Select(parent => parent.NodeId))));
                break;
            case ProjectStructureDependencyResponse dependencies:
                observed.Add(dependencies.ProjectId);
                AddNodeKeys(dependencies.Items.Select(item => item.NodeId).Concat(dependencies.Items.Select(item => item.ParentNodeId))
                    .Concat(dependencies.Items.SelectMany(item =>
                    item.Prerequisites.Concat(item.Dependents).Select(relation => relation.NodeId))));
                break;
            case ProjectStructureNodesCopyResult copy:
                observed.Add(copy.ProjectId);
                AddNodeKeys(copy.SourceRootNodeIds.Concat(copy.CopiedRootNodeIds).Append(copy.DestinationParentNodeId)
                    .Concat(copy.NodeMappings.SelectMany(node => new[] { node.SourceNodeId, node.CopiedNodeId }))
                    .Concat(copy.OmittedBoundaryLinks.SelectMany(link => new[] { link.SourceNodeId, link.TargetNodeId })));
                break;
            case ProjectStructureNodesToSubprojectResult transfer:
                observed.Add(transfer.SourceProjectId);
                observed.Add(transfer.TargetProjectId);
                AddNodeKeys(transfer.RequestedNodeIds.Concat(transfer.MovedNodeIds)
                    .Concat(transfer.RemovedBoundaryLinks.SelectMany(link => new[] { link.SourceNodeId, link.TargetNodeId })));
                break;
            case ProjectStructureSubprojectTransferResult transfer:
                observed.Add(transfer.TargetProjectId);
                AddNodeKeys(transfer.MovedNodeIds.Concat(transfer.RemovedBoundaryLinks.SelectMany(link => new[] { link.SourceNodeId, link.TargetNodeId })));
                break;
            case ProjectStructureWorkflowAddOptionsResult options:
                observed.Add(options.ProjectId);
                AddNode(options.ParentNode);
                break;
            case ProjectStructureWorkflowNodeCreateResult created:
                observed.Add(created.ProjectId);
                AddNode(created.Node);
                break;
            case ProjectStructureWorkflowNodeStartResult start:
                observed.Add(start.ProjectId);
                break;
            case ProjectStructureProcessSubprocessLaunchResult child:
                observed.Add(child.ProjectId);
                break;
            case ProjectStructureAssetDescriptor asset:
                observed.Add(asset.ProjectId);
                break;
            case ProjectStructureAssetContentDescriptor content:
                observed.Add(content.Asset.ProjectId);
                break;
            case ProjectStructureAssetTextDescriptor text:
                observed.Add(text.Asset.ProjectId);
                break;
            case ProjectStructureAssetImageAnalysisDescriptor analysis:
                observed.Add(analysis.Asset.ProjectId);
                break;
            case ProjectPlanSummary plan:
                observed.Add(plan.ProjectId);
                break;
            case ProjectStructureImportResult imported:
                observed.Add(imported.ProjectId);
                AddNodeKeys(imported.CreatedNodeIds.Append(imported.ContainerNodeId).Append(imported.SourceNodeId));
                break;
            case ProjectStructureAgentAnalyticsResponse analytics:
                if (analytics.Entries.Any(entry => entry.ProjectId.HasValue)) {
                    MarkUnavailable(ProjectStructureDisclosureState.HistoricalSourceUnavailable);
                }
                break;
            case ProjectStructureSubtreeRecompositionResult recomposed:
                AddNodeKeys([recomposed.RootNodeId]);
                break;
            case ProjectStructureLinkChangeResult link:
                AddNodeKeys([link.Link.SourceId, link.Link.TargetId]);
                break;
            case ProjectStructureWorkflowRunStatus:
            case ProjectStructureTaskCreateResult:
            case ProjectStructureGanttMutationResult:
            case ProjectStructureTaskResourceAttachResult:
            case ProjectStructureNodeCatalogResponse:
            case ProjectManagementGuidanceResponse:
            case ProjectStructureLeaseSnapshot:
            case OperationAck:
            case OperationCount:
            case ArtifactReference:
            case null:
                break;
            default:
                MarkUnavailable(ProjectStructureDisclosureState.UnsupportedResult);
                break;
        }
        requested.UnionWith(observed);
    }

    private void AddNode(ProjectStructureNodeSummary node) {
        if (node.RelatedProjectId is { } related) {
            observed.Add(related);
        }
        AddNodeKeys([node.Id, node.ParentId]);
    }

    private void AddNodeKeys(IEnumerable<string?> keys) {
        foreach (var key in keys) {
            if (key is not null && ProjectWorkbenchGraphConventions.TryResolveProjectHierarchyNode(key, out _, out var projectId)) {
                observed.Add(projectId);
            }
        }
    }

    private void MarkUnavailable(ProjectStructureDisclosureState reason) {
        if (state == ProjectStructureDisclosureState.Complete) {
            state = reason;
        }
    }

    private sealed class RestoreBinding(ProjectStructureResultEvidenceScope owner, ProjectStructureResultEvidenceScope? previous) : IDisposable {
        public void Dispose() {
            if (ReferenceEquals(Current.Value, owner)) {
                Current.Value = previous;
            }
        }
    }
}
