using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    internal async Task<WorkflowReadSourceLease> AcquireReadAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken) {
        if (run.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment or WorkflowLaunchOrigin.ProcessAssignment) {
            return await AcquireMappedReadAsync(run, cancellationToken);
        }
        var authority = run.Origin?.StructureAuthority ?? throw new WorkflowStructureLegacyLineageException();
        if (authority.ProjectScope is null) {
            throw new WorkflowStructureLegacyLineageException();
        }
        var dispatch = await ReadProcessDispatchAsync(authority, WorkflowStructureAuthorityUse.Disclosure, cancellationToken);
        IAgentCatalogReadLease? held = null;
        try {
            if (authority.ProcessAuthority?.ToolInvocation is { } tool) {
                var ids = authority.AgentGovernance is { } original
                    ? new[] { original.AgentId, tool.ExecutorAgentId }.Distinct().ToArray() : new[] { tool.ExecutorAgentId };
                held = await RequireCatalog().AcquireAgentsReadLeaseAsync(ids, cancellationToken);
            } else if (authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution) {
                held = await RequireCatalog().AcquireAgentReadLeaseAsync(authority.AgentGovernance?.AgentId
                    ?? throw Denied("The Workflow source has no saved Agent governance."), cancellationToken);
            }
            async Task RequireSourceAsync(CancellationToken ct) {
                RequireCurrentSource(authority, WorkflowStructureAuthorityUse.Disclosure, null, held, checkAdmissionTargets: false);
                RequireHeldProcessSource(dispatch, held, WorkflowStructureAuthorityUse.Disclosure);
                if (authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution &&
                        !AgentProjectStructureAccessMetadata.Read(held!.Agent!.ConfigurationJson).CanRead) {
                    throw Denied("Current Agent policy no longer permits Workflow project reads.");
                }
                if (authority.SchedulerAuthority is { } scheduler) {
                    await scheduledAuthority.RequireCurrentAsync(scheduler, ct);
                }
            }
            await RequireSourceAsync(cancellationToken);
            return new(this, held, RequireSourceAsync, target => AllowsReadTarget(authority, held, target));
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private static bool AllowsReadTarget(WorkflowStructureAuthority authority, IAgentCatalogReadLease? held, ProjectWriteAdmission target) {
        var saved = authority.ProjectScope!.Find(target.ProjectId);
        if (target.DatabaseProfileId != authority.DatabaseProfileId ||
                saved is not null && ToProjectAdmission(saved) != target || saved is null && !authority.AllProjects ||
                authority.ProjectId != Guid.Empty && authority.ProjectId != target.ProjectId ||
                !authority.AllProjects && authority.ProjectId == Guid.Empty && !authority.ProjectIds.Contains(target.ProjectId)) {
            return false;
        }
        if (authority.Channel != WorkflowStructureAuthorityChannel.AgentExecution) {
            return true;
        }
        var governance = authority.AgentGovernance!;
        if (governance.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
                (!Guid.TryParse(governance.WorkspaceScope.Key, out var sourceProject) || sourceProject != target.ProjectId)) {
            return false;
        }
        var current = AgentProjectStructureAccessMetadata.Read(held!.Agent!.ConfigurationJson);
        return current.CanRead && (current.AllowAllProjects || current.AllowedProjectIds.Contains(target.ProjectId) &&
            current.AllowedProjectLifetimes.Contains(new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId)));
    }

    private async Task<WorkflowReadSourceLease> AcquireMappedReadAsync(WorkflowRunSnapshot run, CancellationToken cancellationToken) {
        var original = await ReadMappedChildForReadAsync(run, cancellationToken);
        var project = original.SourceAuthority?.ProjectAdmission
            ?? throw Denied("The retained Process source has no original project lifetime for Workflow reads. Inspect or cancel the child, then explicitly launch a newly reviewed occurrence if required.");
        IAgentCatalogReadLease? held = null;
        try {
            if (original.SourceAuthority!.Principal is ProcessLaunchPrincipal.AgentExecution agent) {
                held = await RequireCatalog().AcquireAgentReadLeaseAsync(agent.Ceiling.AgentId, cancellationToken);
            }
            async Task RequireSourceAsync(CancellationToken ct) {
                var current = await ReadMappedChildForReadAsync(run, ct);
                if (current.OwnerFingerprint != original.OwnerFingerprint || current.SourceAuthority?.ProjectAdmission != project) {
                    throw Denied("The retained Process source changed before this Workflow read.");
                }
                RequireMappedSource(original, held, mutation: false);
                await RequireProjectAdmissions().RequireCurrentAsync(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), ct);
            }
            await RequireSourceAsync(cancellationToken);
            return new(this, held, RequireSourceAsync, target => target.DatabaseProfileId == project.DatabaseProfileId &&
                target.ProjectId == project.ProjectId && target.LifetimeId == project.LifetimeId);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private async Task<ProcessWorkflowDispatchAuthority> ReadMappedChildForReadAsync(WorkflowRunSnapshot child, CancellationToken cancellationToken) {
        var origin = child.Origin!;
        ProcessWorkflowDispatchAuthority dispatch;
        if (origin is WorkflowLaunchOrigin.ProcessDispatchAssignment mapped) {
            var saved = mapped.Dispatch;
            saved.Validate();
            if (saved.ProfileId != canonicalDatabase.Profile.Profile.Id) {
                throw Denied("The retained Workflow belongs to another original Process profile.");
            }
            dispatch = await RequireMappedReader().ReadAsync(new(new(saved.ProcessRun.Value), new(saved.Assignment.Value),
                new(saved.ClaimToken), saved.ContractHash), cancellationToken);
            if (!dispatch.IsCurrent) {
                dispatch = (await RequireMappedReader().ReadContinuationAsync(new(new(saved.ProcessRun.Value), new(saved.Assignment.Value),
                    new(child.RunId.Value), new(saved.ClaimToken), saved.ContractHash), cancellationToken)).Dispatch;
            }
            if (saved.RootRun.Value != dispatch.RootRunId.Value || saved.OwnerFingerprint != dispatch.OwnerFingerprint ||
                    saved.InputFingerprint != MappedInputFingerprint(dispatch) || saved.WorkflowId != child.WorkflowId ||
                    saved.RequestedVersionId?.Value != dispatch.Assignment.WorkflowBinding?.WorkflowVersionId?.Value) {
                throw Denied("The Workflow read no longer matches its original mapped Process admission.");
            }
        } else if (origin is WorkflowLaunchOrigin.ProcessAssignment legacy) {
            dispatch = (await RequireMappedReader().ReadContinuationAsync(new(new(legacy.ProcessRun.Value), new(legacy.Assignment.Value),
                new(child.RunId.Value)), cancellationToken)).Dispatch;
        } else {
            throw Denied("The Workflow read has no supported mapped Process source.");
        }
        if (origin.StructureAuthority is not null || dispatch.SourceAuthority is not { } source ||
                source.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id ||
                origin.AuthorizationScope != WorkspaceScopeDescriptor.Process(dispatch.Request.RunId.Value.ToString("D")) ||
                origin.AuthorizationPolicyFingerprint != WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint ||
                dispatch.Assignment.WorkflowBinding is not { } binding || binding.WorkflowId.Value != child.WorkflowId.Value ||
                binding.WorkflowVersionId is { } version && version.Value != child.VersionId.Value) {
            throw Denied("The retained Workflow has no complete original Process source, profile and version authority for this read.");
        }
        var children = mappedWorkflowChildren
            ?? throw new InvalidOperationException("Workflow reads require the receiving owner's original mapped child query.");
        var savedChildren = await children.FindAsync(new(dispatch.Request.RunId.Value), new(dispatch.Request.StepInstanceId.Value), cancellationToken);
        if (savedChildren.Count != 1 || savedChildren[0].RunId != child.RunId) {
            throw Denied("The original Process assignment has missing or ambiguous Workflow children; reconcile the original run.");
        }
        if (!dispatch.IsCurrent) {
            WorkflowDefinitionSelection selection = binding.WorkflowVersionId is { } exact
                ? new WorkflowDefinitionSelection.ExactSavedVersion(new(binding.WorkflowId.Value), new(exact.Value))
                : new WorkflowDefinitionSelection.LatestActive(new(binding.WorkflowId.Value));
            await children.RequireRetainedChildAsync(child, source.DatabaseProfileId, selection, MappedInputJson(dispatch), cancellationToken);
        }
        return dispatch;
    }

    private ProjectWriteSelectionQuery RequireProjectSelections() => projectSelections
        ?? throw new InvalidOperationException("Workflow project reads require the Projects owner selection query.");

    internal sealed class WorkflowReadSourceLease(ProjectStructureWorkflowAuthorityService owner, IAgentCatalogReadLease? held,
        Func<CancellationToken, Task> requireSource, Func<ProjectWriteAdmission, bool> allowsTarget) : IAsyncDisposable {
        private int disposed;

        public async Task<IReadOnlyList<ProjectWriteAdmission>> ListTargetsAsync(CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            var query = owner.RequireProjectSelections();
            var candidates = await query.ListAsync(cancellationToken: cancellationToken);
            return candidates.Select(project => project.Admission).Where(allowsTarget).ToArray();
        }

        public async Task<ProjectWriteAdmission> RequireTargetAsync(Guid projectId, CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            var target = await owner.RequireProjectAdmissions().CaptureAsync(projectId, cancellationToken);
            if (target is null || !allowsTarget(target)) {
                throw Denied("The requested Workflow project is outside its original and current exact read grants.");
            }
            return target;
        }

        public async Task RequireCurrentAsync(IReadOnlyList<ProjectWriteAdmission> targets, CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            await requireSource(cancellationToken);
            foreach (var target in targets) {
                if (!allowsTarget(target)) {
                    throw Denied("The requested Workflow project is outside its original and current exact read grants.");
                }
            }
            if (targets.Count > 0) {
                await owner.RequireProjectAdmissions().RequireManyCurrentAsync(targets, cancellationToken);
            }
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
