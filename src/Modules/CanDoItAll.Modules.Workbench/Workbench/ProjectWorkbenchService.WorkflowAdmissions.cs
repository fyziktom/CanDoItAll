using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectWorkflowDeliveryState {
    Prepared,
    Pending,
    Applied,
    Superseded,
    TargetChanged,
    TargetDeleted,
    LegacyObservation,
    AuthorityBlocked
}

public sealed record ProjectWorkflowAdmission(
    Guid ProjectId,
    string NodeId,
    WorkflowStructureAdmissionBinding Binding,
    WorkflowDefinition Definition,
    WorkflowLaunchIntent LaunchIntent,
    bool CallerSuppliedIntent,
    ProjectWorkflowDeliveryState Delivery,
    ProjectStructureWorkflowRunStatus? RecordedStatus) {
    [System.Text.Json.Serialization.JsonIgnore]
    public RetainedEvidenceImport? ImportedHistory { get; init; }
}

public sealed partial class ProjectWorkbenchService {
    public async Task<ProjectWorkflowAdmission?> FindWorkflowAdmissionAsync(Guid intentId,
        CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntentId == intentId, cancellationToken);
        return row is null ? null : ReadAdmission(row);
    }

    public async Task<ProjectWorkflowAdmission?> FindSelectedWorkflowAdmissionAsync(Guid projectId, string nodeId,
        CancellationToken cancellationToken = default) {
        var project = await mutationScopes.CaptureProjectObservationAsync(projectId, cancellationToken);
        if (project is null) {
            return null;
        }
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.NodeId == nodeId &&
                (item.DatabaseProfileId == project.Admission.DatabaseProfileId && item.ProjectLifetimeId == project.Admission.LifetimeId ||
                 item.DatabaseProfileId == null && item.ProjectLifetimeId == null && !project.HasRetiredLifetime))
            .OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ReadAdmission(row);
    }

    public async Task<ProjectWorkflowAdmission> PrepareWorkflowAdmissionAsync(Guid projectId, string nodeId,
        Guid intentId, bool callerSuppliedIntent, WorkflowDefinition definition, string inputJson,
        WorkflowRuntimeBackendKind? requestedBackend, WorkflowPreviewSimulationPlan simulation,
        WorkflowStructureAuthority authority, ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(authority);
        if (intentId == Guid.Empty || authority.ProjectId != projectId) {
            throw new ArgumentException("The workflow admission intent and authority must match the requested project.");
        }

        var target = authority.ProjectScope?.Find(projectId) ?? throw new WorkflowStructureLegacyLineageException();
        authority.ProjectScope!.Validate(authority);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var scope = await mutationScopes.BeginBindingWriteAsync(database, [
            ProjectStructureSerializableMutationScope.ForProject(projectId),
            $"workbench:workflow-admission:{intentId:N}"
        ], cancellationToken, [ProjectStructureWorkflowAuthorityService.ToProjectAdmission(target)],
            workflowAdmission: new(authority, target, WorkflowStructureAuthorityUse.StructureAdmission));
        var node = await database.Set<ProjectObjectRecord>().SingleOrDefaultAsync(
            item => item.ProjectId == projectId && item.NodeKey == nodeId, cancellationToken)
            ?? throw new ProjectStructureAgentException(404, "NodeNotFound", "The workflow admission target no longer exists.");
        var bindingFingerprint = await WorkflowAdmissionFingerprintAsync(database, node, requestedBackend, simulation, target, cancellationToken);
        var existing = await database.Set<ProjectWorkflowAdmissionRecord>()
            .SingleOrDefaultAsync(item => item.IntentId == intentId, cancellationToken);
        if (existing is not null) {
            var admission = ReadAdmission(existing);
            EnsureAdmissionMatches(admission, projectId, nodeId, authority, bindingFingerprint);
            return admission;
        }

        var sequence = checked((await database.Set<ProjectWorkflowAdmissionRecord>()
            .Where(item => item.ProjectId == projectId && item.NodeId == nodeId)
            .MaxAsync(item => (long?)item.Sequence, cancellationToken) ?? 0) + 1);
        var binding = new WorkflowStructureAdmissionBinding(intentId, WorkflowRunId.New(), sequence,
            node.Id, bindingFingerprint, new WorkflowProjectStructureNodeId(node.NodeKey),
            WorkflowStructureOutputRole.RequiredResult, authority) {
            LeaseOwner = new(agent.AgentId, agent.AgentName, agent.MachineName, agent.RepositoryRoot, agent.BranchName, agent.SessionId)
        };
        var origin = new WorkflowLaunchOrigin.ProjectStructureNode(projectId,
            new WorkflowProjectStructureNodeId(nodeId), authority.Principal, new WorkflowLaunchSessionId(agent.SessionId),
            new WorkflowLaunchCorrelationId(intentId)) {
            StructureAdmission = binding,
            StructureAuthority = authority
        };
        var launch = new WorkflowLaunchIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            simulation.HasSteps ? WorkflowLaunchMode.Preview : WorkflowLaunchMode.Production,
            origin, inputJson, WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(new WorkflowLaunchIdempotencyKey($"structure-admission:{intentId:N}"))) {
            RequestedBackend = requestedBackend,
            PreviewSimulationPlan = simulation
        };
        var result = new ProjectWorkflowAdmission(projectId, nodeId, binding, definition, launch,
            callerSuppliedIntent, ProjectWorkflowDeliveryState.Prepared, null);
        database.Add(new ProjectWorkflowAdmissionRecord {
            IntentId = intentId,
            ProjectId = projectId,
            DatabaseProfileId = target.DatabaseProfileId,
            ProjectLifetimeId = target.LifetimeId,
            NodeId = nodeId,
            NativeNodeId = node.Id,
            RunId = binding.RunId.Value,
            Sequence = sequence,
            AdmissionJson = JsonSerializer.Serialize(result, WorkflowContributionJson),
            NextAttemptAtUtc = clock.GetUtcNow()
        });
        await database.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return result;
    }

    public async Task ValidateWorkflowAdmissionReplayAsync(ProjectWorkflowAdmission admission,
        Guid projectId, string nodeId, WorkflowStructureAuthority authority,
        WorkflowRuntimeBackendKind? requestedBackend, WorkflowPreviewSimulationPlan simulation,
        CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking()
            .SingleAsync(item => item.IntentId == admission.Binding.IntentId, cancellationToken);
        RetainedEvidenceImport.RequireNative(row.ImportedHistory);
        var saved = ReadAdmission(row);
        EnsureAdmissionMatches(saved, admission.ProjectId, admission.NodeId, admission.Binding.Authority, admission.Binding.BindingFingerprint);
        EnsureAdmissionMatches(admission, projectId, nodeId, authority, admission.Binding.BindingFingerprint);
        if (admission.LaunchIntent.RequestedBackend != requestedBackend ||
                JsonSerializer.Serialize(admission.LaunchIntent.PreviewSimulationPlan, WorkflowContributionJson) !=
                    JsonSerializer.Serialize(simulation, WorkflowContributionJson)) {
            throw new ProjectStructureAgentException(409, "WorkflowAdmissionConflict",
                "The workflow launch intent was already prepared with different execution settings.");
        }
    }

    public async Task<IReadOnlyList<ProjectWorkflowAdmission>> ListWorkflowAdmissionsForDeliveryAsync(int take,
        CancellationToken cancellationToken = default) {
        if (take is < 1 or > 128) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var rows = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking()
            .Where(item => item.ImportedHistory == null && !item.DeliveryFinished && item.NextAttemptAtUtc <= now)
            .OrderBy(item => item.NextAttemptAtUtc).ThenBy(item => item.IntentId)
            .Take(take).ToListAsync(cancellationToken);
        return rows.Select(ReadAdmission).ToList();
    }

    public async Task DeferWorkflowAdmissionDeliveryAsync(Guid intentId, CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projectId = await database.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking()
            .Where(item => item.IntentId == intentId).Select(item => item.ProjectId).SingleAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(database,
            ProjectStructureSerializableMutationScope.ForProject(projectId), cancellationToken);
        var row = await database.Set<ProjectWorkflowAdmissionRecord>().SingleAsync(item => item.IntentId == intentId, cancellationToken);
        RetainedEvidenceImport.RequireNative(row.ImportedHistory);
        row.NextAttemptAtUtc = clock.GetUtcNow().AddSeconds(30);
        await database.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task<ProjectWorkflowDeliveryState> DeliverWorkflowStatusAsync(ProjectWorkflowAdmission admission,
        ProjectStructureWorkflowRunStatus status, WorkflowRunSnapshot? run, bool outputsComplete,
        CancellationToken cancellationToken = default) {
        if (status.RunId != admission.Binding.RunId || run is not null && run.RunId != admission.Binding.RunId) {
            throw new InvalidOperationException("Workflow status delivery must name its exact admitted run.");
        }
        try {
            return await DeliverCurrentWorkflowStatusAsync(admission, status, run, outputsComplete, cancellationToken);
        } catch (Exception exception) when (exception is WorkflowStructureLegacyLineageException or
                ProjectWriteAdmissionRejectedException or ProjectStructureAgentException { StatusCode: 403 }) {
            return await RecordBlockedWorkflowStatusAsync(admission, status, run,
                exception is WorkflowStructureLegacyLineageException ? ProjectWorkflowDeliveryState.LegacyObservation :
                    exception is ProjectWriteAdmissionRejectedException ? ProjectWorkflowDeliveryState.TargetDeleted :
                        ProjectWorkflowDeliveryState.AuthorityBlocked, cancellationToken);
        }
    }

    private async Task<ProjectWorkflowDeliveryState> DeliverCurrentWorkflowStatusAsync(ProjectWorkflowAdmission admission,
        ProjectStructureWorkflowRunStatus status, WorkflowRunSnapshot? run, bool outputsComplete,
        CancellationToken cancellationToken) {
        if (status.RunId != admission.Binding.RunId || run is not null && run.RunId != admission.Binding.RunId) {
            throw new InvalidOperationException("Workflow status delivery must name its exact admitted run.");
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var target = admission.Binding.Authority.ProjectScope?.Find(admission.ProjectId)
            ?? throw new WorkflowStructureLegacyLineageException();
        await using var scope = await mutationScopes.BeginBindingWriteAsync(database,
            [ProjectStructureSerializableMutationScope.ForProject(admission.ProjectId)], cancellationToken,
            [ProjectStructureWorkflowAuthorityService.ToProjectAdmission(target)],
            workflowAdmission: new(admission.Binding.Authority, target, WorkflowStructureAuthorityUse.StatusProjection));
        var row = await database.Set<ProjectWorkflowAdmissionRecord>()
            .SingleAsync(item => item.IntentId == admission.Binding.IntentId, cancellationToken);
        RetainedEvidenceImport.RequireNative(row.ImportedHistory);
        var saved = ReadAdmission(row);
        if (saved.Binding.IntentId != admission.Binding.IntentId || saved.Binding.RunId != admission.Binding.RunId || saved.Binding.BindingFingerprint != admission.Binding.BindingFingerprint) {
            throw new InvalidOperationException("The saved workflow admission differs from its delivery request.");
        }

        row.NextAttemptAtUtc = clock.GetUtcNow().AddSeconds(10);
        var selectedSequence = await database.Set<ProjectWorkflowAdmissionRecord>()
            .Where(item => item.ProjectId == row.ProjectId && item.NodeId == row.NodeId)
            .MaxAsync(item => item.Sequence, cancellationToken);
        if (selectedSequence == row.Sequence && run is not null &&
            (row.ObservedRunUpdatedAtUtc > run.UpdatedAtUtc || row.DeliveryFinished && row.Delivery == ProjectWorkflowDeliveryState.Applied)) {
            return row.Delivery;
        }
        var node = await database.Set<ProjectObjectRecord>().SingleOrDefaultAsync(item =>
            item.Id == row.NativeNodeId && item.ProjectId == row.ProjectId && item.NodeKey == row.NodeId, cancellationToken);
        var state = selectedSequence != row.Sequence ? ProjectWorkflowDeliveryState.Superseded
            : node is null ? ProjectWorkflowDeliveryState.TargetDeleted
            : await WorkflowAdmissionFingerprintAsync(database, node, saved.LaunchIntent.RequestedBackend, saved.LaunchIntent.PreviewSimulationPlan, target, cancellationToken) != saved.Binding.BindingFingerprint
                ? ProjectWorkflowDeliveryState.TargetChanged
                : outputsComplete && run is not null ? ProjectWorkflowDeliveryState.Applied : ProjectWorkflowDeliveryState.Pending;
        var recorded = status with { Delivery = state, IntentId = row.IntentId, AdmissionSequence = row.Sequence };
        if (state is ProjectWorkflowDeliveryState.Applied or ProjectWorkflowDeliveryState.Pending) {
            var metadata = ProjectObjectMetadataSerializer.Parse(node!.MetadataJson);
            var workflow = metadata.Workflow ?? throw new InvalidOperationException("The selected workflow node lost its workflow binding.");
            workflow.LastRunId = status.RunId;
            workflow.LastRunState = status.State;
            workflow.LastRunSummary = status.Summary.RunSummary;
            workflow.LastCreatedNodeIds = status.Summary.CreatedNodeIds;
            workflow.LastCreatedAssetIds = status.Summary.CreatedAssetIds;
            workflow.LastCreatedFilePaths = status.Summary.CreatedFilePaths;
            workflow.LastStepIndex = status.CurrentStepIndex;
            workflow.LastStepCount = status.StepCount;
            workflow.LastStartedAtUtc = run?.CreatedAtUtc;
            workflow.LastUpdatedAtUtc = run?.UpdatedAtUtc;
            node.MetadataJson = ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(node.MetadataJson, metadata);
            node.Status = status.Status;
            node.ProgressMode = status.ProgressMode;
            node.ProgressPercent = status.ProgressPercent;
            var markers = ProjectNodeMarkerState.Parse(node.MarkersJson)
                .Where(marker => marker.Icon is not ("alert" or "pause" or "stop")).ToList();
            var marker = ProjectObjectMetadataSerializer.NormalizeMarker(status.MarkerIcon, status.MarkerTone, status.MarkerLabel);
            if (marker is not null) {
                markers.Add(marker);
            }

            node.MarkersJson = ProjectNodeMarkerState.Serialize(ProjectObjectMetadataSerializer.NormalizeMarkers(markers));
            node.UpdatedAtUtc = clock.GetUtcNow();
        }

        row.Delivery = state;
        row.ObservedRunUpdatedAtUtc = run?.UpdatedAtUtc;
        row.StatusJson = JsonSerializer.Serialize(recorded, WorkflowContributionJson);
        row.DeliveryFinished = state is ProjectWorkflowDeliveryState.Superseded or ProjectWorkflowDeliveryState.TargetChanged or ProjectWorkflowDeliveryState.TargetDeleted ||
            state == ProjectWorkflowDeliveryState.Applied && run!.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;
        await database.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return state;
    }

    private async Task<ProjectWorkflowDeliveryState> RecordBlockedWorkflowStatusAsync(ProjectWorkflowAdmission admission,
        ProjectStructureWorkflowRunStatus status, WorkflowRunSnapshot? run, ProjectWorkflowDeliveryState state,
        CancellationToken cancellationToken) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(database,
            ProjectStructureSerializableMutationScope.ForProject(admission.ProjectId), cancellationToken);
        var row = await database.Set<ProjectWorkflowAdmissionRecord>()
            .SingleAsync(item => item.IntentId == admission.Binding.IntentId, cancellationToken);
        RetainedEvidenceImport.RequireNative(row.ImportedHistory);
        var saved = ReadAdmission(row);
        if (saved.Binding != admission.Binding) {
            if (JsonSerializer.Serialize(saved.Binding, WorkflowContributionJson) != JsonSerializer.Serialize(admission.Binding, WorkflowContributionJson)) {
                throw new InvalidOperationException("The saved Workflow admission differs from its blocked observation.");
            }
        }
        if (run is not null && row.ObservedRunUpdatedAtUtc > run.UpdatedAtUtc) {
            return row.Delivery;
        }
        var selected = await database.Set<ProjectWorkflowAdmissionRecord>()
            .Where(item => item.ProjectId == row.ProjectId && item.NodeId == row.NodeId)
            .MaxAsync(item => item.Sequence, cancellationToken);
        if (selected != row.Sequence) {
            state = ProjectWorkflowDeliveryState.Superseded;
        }
        row.Delivery = state;
        row.StatusJson = JsonSerializer.Serialize(status with { Delivery = state, IntentId = row.IntentId,
            AdmissionSequence = row.Sequence }, WorkflowContributionJson);
        row.ObservedRunUpdatedAtUtc = run?.UpdatedAtUtc;
        row.DeliveryFinished = state is ProjectWorkflowDeliveryState.Superseded or ProjectWorkflowDeliveryState.TargetDeleted or ProjectWorkflowDeliveryState.LegacyObservation;
        row.NextAttemptAtUtc = clock.GetUtcNow().AddSeconds(30);
        await database.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return state;
    }

    private static void EnsureAdmissionMatches(ProjectWorkflowAdmission admission, Guid projectId, string nodeId,
        WorkflowStructureAuthority authority, string bindingFingerprint) {
        RetainedEvidenceImport.RequireNative(admission.ImportedHistory);
        if (admission.ProjectId != projectId || admission.NodeId != nodeId ||
            admission.Binding.Authority.Channel != authority.Channel ||
            admission.Binding.Authority.Principal != authority.Principal ||
            admission.Binding.Authority.DatabaseProfileId != authority.DatabaseProfileId ||
            WorkflowStructureAuthorityFingerprint.Create(admission.Binding.Authority) != WorkflowStructureAuthorityFingerprint.Create(authority) ||
            admission.Binding.BindingFingerprint != bindingFingerprint) {
            throw new ProjectStructureAgentException(409, "WorkflowAdmissionConflict",
                "The workflow launch intent was already prepared for different authority, settings, or target binding.");
        }
    }

    private async Task<string> WorkflowAdmissionFingerprintAsync(WorkbenchDbContext database, ProjectObjectRecord node,
        WorkflowRuntimeBackendKind? requestedBackend, WorkflowPreviewSimulationPlan simulation,
        WorkflowProjectLifetime? target, CancellationToken cancellationToken) {
        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).Workflow
            ?? throw new ProjectStructureAgentException(400, "WorkflowNodeRequired", "The selected node has no workflow binding.");
        var payload = JsonSerializer.Serialize(new {
            Target = await ReadWorkflowTargetBindingAsync(database, node.ProjectId, node.NodeKey, cancellationToken),
            metadata.WorkflowId,
            metadata.WorkflowVersionId,
            metadata.InputSettings,
            requestedBackend,
            simulation
        }, WorkflowContributionJson);
        return ProjectWorkflowContributionFingerprint.Hash(target is null ? payload :
            "workflow-admission-project-lifetime-v1\n" + JsonSerializer.Serialize(new { target, payload }, WorkflowContributionJson));
    }

    private static ProjectWorkflowAdmission ReadAdmission(ProjectWorkflowAdmissionRecord row) {
        var admission = JsonSerializer.Deserialize<ProjectWorkflowAdmission>(row.AdmissionJson, WorkflowContributionJson)
            ?? throw new InvalidOperationException("The saved workflow admission is invalid.");
        if (admission.Binding.IntentId != row.IntentId || admission.Binding.RunId.Value != row.RunId ||
            admission.Binding.Sequence != row.Sequence || admission.Binding.NativeNodeId != row.NativeNodeId ||
            admission.ProjectId != row.ProjectId || admission.NodeId != row.NodeId ||
            admission.Binding.Authority.ProjectScope?.Find(row.ProjectId) is { } target &&
                (target.DatabaseProfileId != row.DatabaseProfileId || target.LifetimeId != row.ProjectLifetimeId) ||
            admission.Binding.Authority.ProjectScope is null && (row.DatabaseProfileId.HasValue || row.ProjectLifetimeId.HasValue)) {
            throw new InvalidOperationException("The saved workflow admission key is inconsistent.");
        }

        return admission with {
            ImportedHistory = row.ImportedHistory,
            Delivery = row.Delivery,
            RecordedStatus = string.IsNullOrEmpty(row.StatusJson) ? null :
                JsonSerializer.Deserialize<ProjectStructureWorkflowRunStatus>(row.StatusJson, WorkflowContributionJson)
                    ?? throw new InvalidOperationException("The saved workflow delivery status is invalid.")
        };
    }
}

public sealed class ProjectWorkflowAdmissionRecord {
    public Guid IntentId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? DatabaseProfileId { get; set; }
    public Guid? ProjectLifetimeId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public Guid NativeNodeId { get; set; }
    public Guid RunId { get; set; }
    public long Sequence { get; set; }
    public string AdmissionJson { get; set; } = string.Empty;
    public ProjectWorkflowDeliveryState Delivery { get; set; }
    public string StatusJson { get; set; } = string.Empty;
    public bool DeliveryFinished { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? ObservedRunUpdatedAtUtc { get; set; }
    public RetainedEvidenceImport? ImportedHistory { get; set; }
}

internal sealed class ProjectWorkflowAdmissionRecordConfiguration : IEntityTypeConfiguration<ProjectWorkflowAdmissionRecord> {
    public void Configure(EntityTypeBuilder<ProjectWorkflowAdmissionRecord> builder) {
        builder.ToTable("Workbench_WorkflowAdmissions");
        builder.Property(row => row.ImportedHistory).HasRetainedEvidenceImportConversion();
        builder.HasKey(row => row.IntentId);
        builder.Property(row => row.ImportedHistory).HasRetainedEvidenceImportConversion();
        builder.Property(row => row.NodeId).HasMaxLength(240).IsRequired();
        builder.Property(row => row.AdmissionJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.StatusJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => row.RunId).IsUnique();
        builder.HasIndex(row => new { row.ProjectId, row.NodeId, row.Sequence }).IsUnique();
        builder.HasIndex(row => new { row.DeliveryFinished, row.NextAttemptAtUtc, row.IntentId });
    }
}
