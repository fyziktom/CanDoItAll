using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanAgentAuthority(ProjectTaskPlanAgentCommand original,
    IAgentCatalogReadLeaseStore catalog, AgentPlanningPublicationExpectation expectedSnapshot,
    AgentPlanningLiveWork work, AgentToolApprovalBinding binding, ProjectAgentSourceMutationAuthority source)
    : IProjectTaskPlanSavedEffectAuthority, IProjectTaskPlanActiveWorkAuthority {
    private IAgentPlanningPublicationSnapshotLease? held;
    private int disposed;
    private int acquired;
    public ImmutableArray<ProjectWriteAdmission> AdditionalProjects { get; } = original.Target.Agent.SourceProject is { } origin ? [origin] : [];

    public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource expected, ProjectWriteAdmission target,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (expected != original.Command.Source || target != original.Target.Project || Interlocked.Exchange(ref acquired, 1) != 0) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.SourceChanged);
        }
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        held = await catalog.AcquirePlanningAuthoritySnapshotAsync(expectedSnapshot, cancellationToken);
        return this;
    }

    public Task RequireCurrentAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        RequireActive();
        try {
            source.RequireSource(original.Target.Admission, ProjectTaskPlanAgentParticipant.MutationTarget(original.Target), held!);
        } catch (ProjectStructureAgentException) {
            throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.AuthorityDenied);
        }
        return Task.CompletedTask;
    }

    public Task RequireEnlistedAsync(CancellationToken cancellationToken) => RequireCurrentAsync(cancellationToken);

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
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
            await held.DisposeAsync();
        }
    }
}
