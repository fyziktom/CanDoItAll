using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    public async Task<WorkflowLaunchOrigin.ProcessDispatchAssignment> CaptureAsync(WorkflowProcessRunId runId,
        WorkflowProcessAssignmentId assignmentId, Guid claimToken, string contractHash, CancellationToken cancellationToken = default) {
        var dispatch = await RequireMappedReader().ReadAsync(new(new(runId.Value), new(assignmentId.Value), new(claimToken), contractHash), cancellationToken);
        if (!dispatch.IsCurrent) {
            throw Denied("The mapped Workflow no longer owns the original Process dispatch claim.");
        }
        await using var held = await AcquireMappedSourceAsync(dispatch, mutation: false, cancellationToken);
        var workflow = dispatch.Assignment.WorkflowBinding!;
        return new(new(canonicalDatabase.Profile.Profile.Id, new(dispatch.RootRunId.Value), runId, assignmentId, claimToken,
            dispatch.OwnerFingerprint, contractHash, new(workflow.WorkflowId.Value),
            workflow.WorkflowVersionId is { } version ? new WorkflowVersionId(version.Value) : null, MappedInputFingerprint(dispatch)), new(runId.Value)) {
            AuthorizationScope = WorkspaceScopeDescriptor.Process(runId.Value.ToString("D")),
            AuthorizationPolicyFingerprint = WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
        };
    }

    public async Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowLaunchOrigin.ProcessDispatchAssignment origin,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(origin);
        var saved = origin.Dispatch;
        saved.Validate();
        if (saved.ProfileId != canonicalDatabase.Profile.Profile.Id || origin.StructureAuthority is not null ||
                origin.AuthorizationScope != WorkspaceScopeDescriptor.Process(saved.ProcessRun.Value.ToString("D")) ||
                origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint) {
            throw Denied("Mapped Workflow admission requires its original profile and Process scope without manufactured Structure authority.");
        }
        var current = await RequireMappedReader().ReadAsync(new(new(saved.ProcessRun.Value), new(saved.Assignment.Value),
            new(saved.ClaimToken), saved.ContractHash), cancellationToken);
        if (!current.IsCurrent || current.RootRunId.Value != saved.RootRun.Value || current.OwnerFingerprint != saved.OwnerFingerprint ||
                current.Assignment.WorkflowBinding is not { } workflow || workflow.WorkflowId.Value != saved.WorkflowId.Value ||
                workflow.WorkflowVersionId?.Value != saved.RequestedVersionId?.Value || MappedInputFingerprint(current) != saved.InputFingerprint) {
            throw Denied("The mapped Workflow's original Process claim, executor or sealed contract changed before admission.");
        }
        return await AcquireMappedSourceAsync(current, mutation: true, cancellationToken);
    }

    public async Task<IWorkflowMappedProcessContinuationLease> AcquireForContinuationAsync(WorkflowRunSnapshot originalChild,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(originalChild);
        var origin = originalChild.Origin ?? throw Denied("The retained Workflow source is missing.");
        var request = origin switch {
            WorkflowLaunchOrigin.ProcessDispatchAssignment mapped => new ProcessWorkflowContinuationRequest(
                new(mapped.Dispatch.ProcessRun.Value), new(mapped.Dispatch.Assignment.Value), new(originalChild.RunId.Value),
                new(mapped.Dispatch.ClaimToken), mapped.Dispatch.ContractHash),
            WorkflowLaunchOrigin.ProcessAssignment legacy => new ProcessWorkflowContinuationRequest(
                new(legacy.ProcessRun.Value), new(legacy.Assignment.Value), new(originalChild.RunId.Value)),
            _ => throw Denied("The retained Workflow has no original mapped Process assignment.")
        };
        if (originalChild.State != WorkflowRunState.WaitingForInput || origin.StructureAuthority is not null ||
                origin.AuthorizationScope != WorkspaceScopeDescriptor.Process(request.RunId.Value.ToString("D")) ||
                origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint) {
            throw Denied("Mapped Workflow continuation requires the original waiting child and its saved Process scope.");
        }
        var current = await RequireMappedReader().ReadContinuationAsync(request, cancellationToken);
        var dispatch = current.Dispatch;
        if (origin is WorkflowLaunchOrigin.ProcessAssignment && dispatch.SourceAuthority is null) {
            throw Denied("This pre-cutover Process parent has no retained original profile and source authority. Its child remains readable; inspect or cancel it, then explicitly launch a newly reviewed Process occurrence if required.");
        }
        var binding = dispatch.Assignment.WorkflowBinding!;
        if (current.ProjectAdmission is { } originalProject && originalProject.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id ||
                binding.WorkflowId.Value != originalChild.WorkflowId.Value ||
                binding.WorkflowVersionId is { } requested && requested.Value != originalChild.VersionId.Value ||
                originalChild.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment saved &&
                    (saved.Dispatch.ProfileId != canonicalDatabase.Profile.Profile.Id || saved.Dispatch.RootRun.Value != dispatch.RootRunId.Value ||
                     saved.Dispatch.OwnerFingerprint != dispatch.OwnerFingerprint || saved.Dispatch.InputFingerprint != MappedInputFingerprint(dispatch) ||
                     saved.Dispatch.WorkflowId != originalChild.WorkflowId || saved.Dispatch.RequestedVersionId?.Value != binding.WorkflowVersionId?.Value)) {
            throw Denied("The retained Workflow no longer matches its original Process executor, input, profile or source.");
        }
        IAgentCatalogReadLease? held = null;
        try {
            if (dispatch.SourceAuthority?.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await RequireCatalog().AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireMappedSource(dispatch, held, mutation: true);
            return new MappedProcessContinuationLease(this, current, held, canonicalDatabase.Profile.Profile.Id);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private static string MappedInputFingerprint(ProcessWorkflowDispatchAuthority dispatch) {
        return WorkflowMappedProcessInputFingerprint.Compute(MappedInputJson(dispatch));
    }

    private static string MappedInputJson(ProcessWorkflowDispatchAuthority dispatch) {
        var assignment = dispatch.Assignment;
        var input = new WorkflowProcessAssignmentInputEnvelope(WorkflowProcessAssignmentInputEnvelope.CurrentSchemaVersion,
            new(assignment.RunId.Value), new(assignment.StepInstanceId.Value), assignment.StepKey, assignment.RoleKey,
            assignment.Prompt, dispatch.Request.ContractHash, assignment.LaunchVariables);
        return JsonSerializer.Serialize(input, AgentOutputJson.SerializerOptions);
    }

    private IProcessWorkflowDispatchAuthorityReader RequireMappedReader() => mappedProcessReader
        ?? throw new InvalidOperationException("Mapped Workflow launch requires the Process owner's direct dispatch reader.");

    private async Task<MappedProcessSourceLease> AcquireMappedSourceAsync(ProcessWorkflowDispatchAuthority dispatch,
        bool mutation, CancellationToken cancellationToken) {
        IAgentCatalogReadLease? held = null;
        try {
            if (dispatch.SourceAuthority?.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await RequireCatalog().AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireMappedSource(dispatch, held, mutation);
            if (!mutation && dispatch.SourceAuthority?.ProjectAdmission is { } project) {
                await RequireProjectAdmissions().RequireCurrentAsync(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), cancellationToken);
            }
            return new(this, dispatch, held, mutation);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private void RequireMappedSource(ProcessWorkflowDispatchAuthority dispatch, IAgentCatalogReadLease? held, bool mutation) {
        if (dispatch.SourceAuthority is { } original) {
            (processObservation as ProjectProcessLaunchAuthorityService
                ?? throw new InvalidOperationException("Mapped Workflow admission requires the original Process source owner's held policy."))
                .RequireHeldWorkflowSource(original, held, mutation);
        }
    }

    private Task RequireMappedDispatchAsync(ProcessWorkflowDispatchAuthority dispatch, CancellationToken cancellationToken)
        => (mappedProcessGuard ?? throw new InvalidOperationException("Mapped Workflow launch requires its actual Process owner transaction guard."))
            .RequireForMutationAsync(dispatch, cancellationToken);

    private Task RequireMappedContinuationAsync(ProcessWorkflowContinuationAuthority proof, CancellationToken cancellationToken)
        => (mappedProcessGuard ?? throw new InvalidOperationException("Mapped Workflow continuation requires its Process owner fence."))
            .RequireForContinuationAsync(proof, cancellationToken);

    private sealed class MappedProcessSourceLease(ProjectStructureWorkflowAuthorityService owner, ProcessWorkflowDispatchAuthority dispatch,
        IAgentCatalogReadLease? held, bool mutation) : IWorkflowStructureSourceAuthorityLease {
        private int disposed;

        public async Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            if (!mutation) {
                throw new InvalidOperationException("Mapped Workflow observation cannot authorize a new admission.");
            }
            owner.RequireMappedSource(dispatch, held, mutation: true);
            await owner.RequireMappedDispatchAsync(dispatch, cancellationToken);
            if (dispatch.SourceAuthority?.ProjectAdmission is { } project) {
                await owner.RequireProjectAdmissions().RequireManyUnderMutationGatesAsync(
                    [new ProjectWriteAdmission(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)], cancellationToken);
            }
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }

    private sealed class MappedProcessContinuationLease(ProjectStructureWorkflowAuthorityService owner, ProcessWorkflowContinuationAuthority proof,
        IAgentCatalogReadLease? held, Guid databaseProfileId) : IWorkflowMappedProcessContinuationLease {
        private int disposed;
        public Guid DatabaseProfileId { get; } = databaseProfileId;
        public string OriginalInputJson { get; } = MappedInputJson(proof.Dispatch);
        public WorkflowDefinitionSelection OriginalSelection { get; } = proof.Dispatch.Assignment.WorkflowBinding is { WorkflowVersionId: { } version } binding
            ? new WorkflowDefinitionSelection.ExactSavedVersion(new(binding.WorkflowId.Value), new(version.Value))
            : new WorkflowDefinitionSelection.LatestActive(new(proof.Dispatch.Assignment.WorkflowBinding!.WorkflowId.Value));

        public async Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            owner.RequireMappedSource(proof.Dispatch, held, mutation: true);
            await owner.RequireMappedContinuationAsync(proof, cancellationToken);
            if (proof.ProjectAdmission is { } project) {
                await owner.RequireProjectAdmissions().RequireManyUnderMutationGatesAsync(
                    [new ProjectWriteAdmission(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)], cancellationToken);
            }
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
