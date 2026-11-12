using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal sealed class AgentInheritedParentStopFixture : ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceExecutionRunMutationStore, ISandboxWorkspaceExecutionRunLeaseStore {
    internal AgentInheritedParentStopFixture() {
        var now = AgentInheritedPlanningTestData.Start;
        var run = new ExecutionRunRecord(Guid.NewGuid(), Guid.NewGuid(), null, "Original input", AgentInheritedPlanningTestData.SourceKind,
            "original-step", "correlation", "cause", "fixture", "system",
            AgentPlanningRunMetadata.ApplyInherited("{}", AgentPlanningOwner.ProjectTaskPlan,
                AgentPlanningDecisionTestData.Envelope().Digest, WorkspaceScopeDescriptor.Sandbox),
            "Original input", string.Empty, string.Empty, string.Empty, ExecutionState.Preparing, null,
            now, now, now, null, string.Empty, null, [], Revision: 1);
        var source = new AgentToolBackgroundSourceBinding(run.SourceKind, run.SourceId,
            AgentPlanningDecisionTestData.Envelope("{\"owner\":1}").Digest, AgentBackgroundExecutionIdentity.Fingerprint(run));
        var session = new AgentToolSessionAdmission(new(run.Id, Guid.Empty, default, source), run.AgentId,
            AgentRuntimeContextPurpose.GovernedProcessAutomation, new(Guid.NewGuid(), "parent-stop", new(1)));
        var journal = AgentInheritedPlanningTestData.Create(session);
        Detail = new(run with { ToolAdmission = journal }, null, [], []);
        Original = journal.PlanningDecision!.InheritedOrigin!;
        Parent = new(Original, (_, token) => {
            token.ThrowIfCancellationRequested();
            ParentChecks++;
            OnParentCheck?.Invoke(ParentChecks);
            return Task.CompletedTask;
        });
        Owner = new(this, this, this, Registry, Clock);
    }

    internal ExecutionRunDetail? Detail { get; set; }
    internal AgentPlanningInheritedOrigin Original { get; }
    internal AgentInheritedParentStop Parent { get; }
    internal AgentInheritedPlanningStopper Owner { get; }
    internal AgentExecutionCancellationRegistry Registry { get; } = new(NullLogger<AgentExecutionCancellationRegistry>.Instance);
    internal StopClock Clock { get; } = new();
    internal Action<int>? OnParentCheck { get; set; }
    internal Action? OnLease { get; set; }
    internal Action? OnWrite { get; set; }
    internal int ParentChecks { get; private set; }
    internal int Writes { get; private set; }
    internal int Leases { get; private set; }
    internal bool LeaseDisposed { get; private set; }

    internal void SetJournal(AgentToolJournalRecord journal) {
        journal.Validate();
        Detail = Detail! with { Run = Detail.Run with { ToolAdmission = journal,
            UpdatedAtUtc = journal.PlanningDecision!.Work.LastObservedAtUtc } };
        Clock.Now = Detail.Run.UpdatedAtUtc;
    }

    internal Task<AgentParentStopOutcome> StopAsync(CancellationToken token = default)
        => Owner.StopAsync(Detail!.Run.Id, Parent, token);

    public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.True(Detail is null || Detail.Run.Id == executionRunId);
        return Task.FromResult(Detail);
    }

    public ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.Equal(Detail!.Run.Id, executionRunId);
        Leases++;
        LeaseDisposed = false;
        OnLease?.Invoke();
        return ValueTask.FromResult<IAsyncDisposable>(new Lease(this));
    }

    public async Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update, Func<CancellationToken, Task> requireCurrentAuthority, CancellationToken cancellationToken) {
        Assert.Equal(Detail!.Run.Id, executionRunId);
        Assert.False(LeaseDisposed);
        OnWrite?.Invoke();
        var next = update(Detail);
        await requireCurrentAuthority(cancellationToken);
        if (!ReferenceEquals(Detail, next)) {
            AgentToolJournalTransitions.ValidatePersistence(Detail.Run.ToolAdmission, next.Run.ToolAdmission);
            Writes++;
            Detail = next;
        }
        return Detail;
    }

    public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Selected stopping cannot enumerate executions.");
    public Task<ExecutionRunRecord?> GetExecutionRunAsync(Guid executionRunId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("The selected owner uses the complete journal detail.");
    public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(ExecutionRunDetail detail, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Stopping must use the guarded current-state writer.");
    public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId, Func<ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default) => throw new InvalidOperationException("Stopping must recheck its parent before publication.");
    public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId,
        Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Selected stopping cannot mutate the catalog.");

    internal sealed class StopClock : TimeProvider {
        internal DateTimeOffset Now { get; set; } = AgentInheritedPlanningTestData.Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Lease(AgentInheritedParentStopFixture owner) : IAsyncDisposable {
        public ValueTask DisposeAsync() {
            owner.LeaseDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
