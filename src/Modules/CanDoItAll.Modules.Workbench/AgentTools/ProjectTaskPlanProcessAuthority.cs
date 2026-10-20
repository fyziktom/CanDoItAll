using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessAuthority(ProjectTaskPlanProcessCommand original,
    IAgentCatalogReadLeaseStore catalog, AgentPlanningPublicationExpectation expectedSnapshot,
    AgentPlanningLiveWork work, AgentToolApprovalBinding binding, ProjectProcessLaunchAuthorityService source,
    IProcessDecisionPublicationFence fence, IAgentExecutionProfileGenerationSource generations)
    : IProjectTaskPlanSavedEffectAuthority, IProjectTaskPlanActiveWorkAuthority {
    private IAgentPlanningPublicationSnapshotLease? held;
    private ProcessDecisionChildMaterialBinding? parent;
    private int disposed;
    private int acquired;
    public ImmutableArray<ProjectWriteAdmission> AdditionalProjects { get; } =
        ProjectProcessReadAuthorityPolicy.ResolveSourceProject(original.Target.Source.SourceAuthority!) is { } origin &&
            new ProjectWriteAdmission(origin.DatabaseProfileId, origin.ProjectId, origin.LifetimeId) is var project && project != original.Target.Project
            ? [project] : [];

    public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource expected, ProjectWriteAdmission target,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        var sourceId = (original.Target.Source.SourceAuthority?.Principal as ProcessLaunchPrincipal.AgentExecution)?.Ceiling.AgentId;
        if (expected != original.Command.Source || target != original.Target.Project ||
            expectedSnapshot.Origin != AgentPlanningPublicationOrigin.Inherited || expectedSnapshot.Session != original.Target.Session ||
            expectedSnapshot.AdditionalAgentId != (sourceId == original.Target.Session.AgentId ? null : sourceId) ||
            Interlocked.Exchange(ref acquired, 1) != 0) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        try {
            held = await catalog.AcquirePlanningAuthoritySnapshotAsync(expectedSnapshot, cancellationToken);
        } catch (SandboxWorkspacePublicationConflictException) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        return this;
    }

    public Task RequireCurrentAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActive();
        var executor = held!.Agent;
        if (original.Target.Source.SourceAuthority is not { CanCreateTasks: true } authority ||
            !original.Target.Executor.CanUseTools || !original.Target.Executor.CanWriteTasks ||
            executor.Id != original.Target.Session.AgentId || executor.IsTemplate || executor.Status != AgentLifecycleStatus.Active ||
            !executor.Permissions.CanUseTools ||
            !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(AgentProjectStructureAccessMetadata.Read(executor.ConfigurationJson))) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.AuthorityDenied);
        }
        try {
            source.RequireHeldSource(authority, held, mutation: true);
        } catch (ProcessLaunchAuthorityRejectedException) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.AuthorityDenied);
        }
        return Task.CompletedTask;
    }

    public async Task RequireEnlistedAsync(CancellationToken cancellationToken) {
        await RequireCurrentAsync(cancellationToken);
        var origin = held!.OriginalRun.ToolAdmission?.PlanningDecision?.InheritedOrigin
            ?? throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        parent ??= ProjectTaskPlanProcessCodec.Parent(origin);
        try {
            await fence.RequireSelectedAsync(parent, original.Target.Source, ProjectTaskPlanProcessContract.Execution,
                ProjectTaskPlanProcessContract.Content, ProcessDecisionPublicationKind.Dispatch, cancellationToken);
        } catch (ProcessExecutionAuthorityMismatchException) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
    }

    public void RequireActiveWork(ProjectTaskPlanSource expected, DecisionDataPolicy policy) {
        RequireActive();
        if (expected != original.Command.Source || policy != original.Command.Policy) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        try {
            _ = work.RequirePublication(held!, binding);
        } catch (AgentPlanningAdmissionException exception) {
            throw new ProjectTaskPlanAuthorityException(exception.Reason == AgentPlanningAdmissionRefusal.WorkBudgetExhausted
                ? ProjectTaskPlanRefusal.BudgetElapsed : ProjectTaskPlanRefusal.SourceChanged);
        }
    }

    private void RequireActive() {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        (held ?? throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged)).RequireActive();
        if (original.Target.Session.Profile.Generation != generations.GetGeneration()) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
            await held.DisposeAsync();
        }
    }
}
