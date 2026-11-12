using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentBackgroundParentStopTests {
    [Theory]
    [InlineData(AgentToolJournalRecord.BackgroundSchemaVersion)]
    [InlineData(AgentToolJournalRecord.EffectJournalSchemaVersion)]
    public async Task Idle_stop_retains_original_input_and_replays_without_another_write(int schema) {
        var (store, owner, parent) = Create();
        SetJournal(store, store.Detail!.Run.ToolAdmission! with { SchemaVersion = schema });
        var original = store.Detail!.Run.ToolAdmission!;
        Assert.Equal(AgentParentStopOutcome.Stopped, await owner.StopAsync(store.Detail.Run.Id, parent));
        var stopped = store.Detail.Run;
        Assert.Equal(RunOutcome.Cancelled, stopped.Outcome);
        Assert.Equal(ExecutionState.Failed, stopped.State);
        Assert.Equal(original.Session, stopped.ToolAdmission!.Session);
        Assert.Equal(original.BackgroundInput, stopped.ToolAdmission.BackgroundInput);
        Assert.Equal(schema, stopped.ToolAdmission.SchemaVersion);
        Assert.Equal(original.Revision + 1, stopped.ToolAdmission.Revision);
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await owner.StopAsync(stopped.Id, parent));
        Assert.Same(stopped, store.Detail.Run);
        Assert.Equal(1, store.Writes);
        Assert.True(store.LeaseDisposed);
    }

    [Fact]
    public async Task Live_registration_is_signalled_without_a_write_or_lease() {
        var (store, owner, parent) = Create();
        var original = store.Detail!;
        using var registration = store.Registry.Register(original.Run, default);
        Assert.Equal(AgentParentStopOutcome.Signalled, await owner.StopAsync(original.Run.Id, parent));
        Assert.True(registration.Token.IsCancellationRequested);
        Assert.Same(original, store.Detail);
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Leases);
    }

    [Theory]
    [InlineData(ExecutionState.Preparing, null)]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded)]
    [InlineData(ExecutionState.Failed, RunOutcome.Cancelled)]
    public async Task Request_scoped_input_without_durable_invocation_evidence_remains_unresolved(ExecutionState state, RunOutcome? outcome) {
        var (store, owner, parent) = Create();
        SetJournal(store, store.Detail!.Run.ToolAdmission! with { Support = AgentToolAdmissionSupport.RequestScopedInput });
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = state, Outcome = outcome,
            CompletedAtUtc = outcome is null ? null : Start } };
        var original = store.Detail;
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(original.Run.Id, parent));
        Assert.Same(original, store.Detail);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task A_registration_from_another_admitted_profile_is_not_signalled() {
        var (store, owner, parent) = Create();
        var original = store.Detail!.Run;
        var journal = original.ToolAdmission!;
        var profile = journal.Session.Profile;
        var foreign = original with { ToolAdmission = journal with { Session = journal.Session with {
            Profile = new(Guid.NewGuid(), profile.Fingerprint, profile.Generation)
        } } };
        using var registration = store.Registry.Register(foreign, default);
        Assert.Equal(AgentParentStopOutcome.Stopped, await owner.StopAsync(original.Id, parent));
        Assert.False(registration.Token.IsCancellationRequested);
        Assert.Equal(RunOutcome.Cancelled, store.Detail.Run.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Revoked_parent_before_signal_after_lease_or_at_publication_is_refused(int boundary) {
        var checks = 0;
        var (store, owner, parent) = Create((_, _) => {
            if (++checks == boundary) {
                throw new UnauthorizedAccessException("Original parent barrier changed.");
            }
            return Task.CompletedTask;
        });
        var original = store.Detail!;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.StopAsync(original.Run.Id, parent));
        Assert.Same(original, store.Detail);
        Assert.Equal(0, store.Writes);
        Assert.Equal(boundary, checks);
        Assert.Equal(boundary > 1, store.LeaseDisposed);
    }

    [Theory]
    [InlineData(AgentToolProviderDispatchState.Started)]
    [InlineData(AgentToolProviderDispatchState.CancelledUnreconciled)]
    public async Task An_unacknowledged_provider_request_is_not_terminalized(AgentToolProviderDispatchState state) {
        var (store, owner, parent) = Create();
        SetJournal(store, Provider(store.Detail!.Run.ToolAdmission!, state));
        var before = JsonSerializer.Serialize(store.Detail);
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(before, JsonSerializer.Serialize(store.Detail));
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(AgentToolProposalState.Executing, AgentToolEffectState.Unknown, AgentParentStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.ReconciliationRequired, AgentToolEffectState.Unknown, AgentParentStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Unknown, AgentParentStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Committed, AgentParentStopOutcome.CompletionPending)]
    public async Task Dispatched_effects_retain_their_original_resolution_owner(AgentToolProposalState state,
        AgentToolEffectState effect, AgentParentStopOutcome expected) {
        var (store, owner, parent) = Create();
        SetJournal(store, WithProposal(store.Detail!.Run.ToolAdmission!, state, effect));
        var before = JsonSerializer.Serialize(store.Detail);
        Assert.Equal(expected, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(before, JsonSerializer.Serialize(store.Detail));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Never_dispatched_proposal_is_cancelled_without_losing_its_provider_response() {
        var (store, owner, parent) = Create();
        var original = WithProposal(store.Detail!.Run.ToolAdmission!, AgentToolProposalState.Prepared, AgentToolEffectState.Unknown);
        SetJournal(store, original);
        Assert.Equal(AgentParentStopOutcome.Stopped, await owner.StopAsync(store.Detail.Run.Id, parent));
        var journal = store.Detail.Run.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(journal.Batches).Proposals);
        Assert.Equal(original.Batches[0].Id, journal.Batches[0].Id);
        Assert.Equal(original.Batches[0].Response, journal.Batches[0].Response);
        Assert.Equal(original.ProviderDispatches, journal.ProviderDispatches);
        Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.Equal(AgentToolCancellationDisposition.NotDispatched, proposal.Cancellation!.Disposition);
    }

    [Theory]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded)]
    [InlineData(ExecutionState.Failed, RunOutcome.Failed)]
    [InlineData(ExecutionState.Completed, RunOutcome.Cancelled)]
    [InlineData(ExecutionState.Failed, RunOutcome.Cancelled)]
    public async Task A_consistent_terminal_record_replays_without_a_write(ExecutionState state, RunOutcome outcome) {
        var (store, owner, parent) = Create();
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = state, Outcome = outcome, CompletedAtUtc = Start } };
        var original = store.Detail;
        Assert.Equal(AgentParentStopOutcome.AlreadyClosed, await owner.StopAsync(original.Run.Id, parent));
        Assert.Same(original, store.Detail);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(ExecutionState.Completed, null, true)]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, false)]
    [InlineData(ExecutionState.Failed, RunOutcome.Succeeded, true)]
    [InlineData(ExecutionState.Preparing, RunOutcome.Cancelled, true)]
    public async Task Inconsistent_terminal_evidence_is_refused(ExecutionState state, RunOutcome? outcome, bool completed) {
        var (store, owner, parent) = Create();
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = state, Outcome = outcome,
            CompletedAtUtc = completed ? Start : null } };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(false, AgentToolJournalRecord.BackgroundSchemaVersion)]
    [InlineData(true, AgentToolJournalRecord.BackgroundSchemaVersion)]
    [InlineData(true, AgentToolJournalRecord.EffectJournalSchemaVersion)]
    public async Task Legacy_invocation_without_provider_evidence_remains_pending(bool segment, int schema) {
        var (store, owner, parent) = Create();
        var journal = store.Detail!.Run.ToolAdmission!;
        SetJournal(store, journal with {
            SchemaVersion = schema,
            ActiveDispatchLeaseId = segment ? null : Guid.NewGuid(),
            Segments = segment ? [new(Guid.NewGuid(), 0, 0, Envelope(), null, null, [])] : []
        });
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded)]
    [InlineData(ExecutionState.Failed, RunOutcome.Cancelled)]
    public async Task A_terminal_status_cannot_close_legacy_invocation_without_original_provider_evidence(ExecutionState state, RunOutcome outcome) {
        var (store, owner, parent) = Create();
        SetJournal(store, store.Detail!.Run.ToolAdmission! with {
            Segments = [new(Guid.NewGuid(), 0, 0, Envelope(), null, null, [])]
        });
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = state, Outcome = outcome, CompletedAtUtc = Start } };
        var original = store.Detail;
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(original.Run.Id, parent));
        Assert.Same(original, store.Detail);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rebound_original_before_or_after_lease_is_refused(bool afterLease) {
        var (store, owner, parent) = Create();
        void Rebind() => store.Detail = store.Detail! with { Run = store.Detail.Run with { ProcessRunId = Guid.NewGuid().ToString("D") } };
        if (afterLease) {
            store.OnLease = Rebind;
        } else {
            Rebind();
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.StopAsync(store.Detail!.Run.Id, parent));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task A_late_provider_dispatch_at_publication_remains_unresolved() {
        var (store, owner, parent) = Create();
        store.OnWrite = () => SetJournal(store, Provider(store.Detail!.Run.ToolAdmission!, AgentToolProviderDispatchState.Started));
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(store.Detail!.Run.Id, parent));
        Assert.True(store.Detail.Run.ToolAdmission!.HasUnresolvedProviderDispatch);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task An_inherited_planning_journal_cannot_use_the_ordinary_stop_policy() {
        var store = new AgentInheritedParentStopFixture();
        var owner = new AgentBackgroundExecutionStopper(store, store, store, store.Registry, store.Clock);
        var parent = new AgentBackgroundParentStop(store.Detail!.Run.ToolAdmission!.Session, (_, _) => Task.CompletedTask);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_record_or_journal_is_explicit_pending_evidence(bool missingRecord) {
        var (store, owner, parent) = Create();
        var id = store.Detail!.Run.Id;
        store.Detail = missingRecord ? null : store.Detail with { Run = store.Detail.Run with { ToolAdmission = null } };
        Assert.Equal(AgentParentStopOutcome.MissingJournal, await owner.StopAsync(id, parent));
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Leases);
    }

    [Fact]
    public async Task Backwards_clock_and_pre_cancelled_call_cannot_publish_a_stop() {
        var (store, owner, parent) = Create();
        store.Clock.Now = Start.AddTicks(-1);
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.StopAsync(store.Detail!.Run.Id, parent));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.StopAsync(store.Detail!.Run.Id, parent, cancellation.Token));
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(ExecutionState.Running, null, AgentParentStopOutcome.Stopped)]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, AgentParentStopOutcome.AlreadyClosed)]
    [InlineData(ExecutionState.Failed, RunOutcome.Cancelled, AgentParentStopOutcome.AlreadyClosed)]
    public async Task Local_proposal_contract_closes_without_inventing_a_hosted_dispatch(ExecutionState state,
        RunOutcome? outcome, AgentParentStopOutcome expected) {
        var (store, owner, parent) = Create();
        SetJournal(store, store.Detail!.Run.ToolAdmission! with {
            ActiveDispatchLeaseId = Guid.NewGuid(),
            Segments = [new(Guid.NewGuid(), 0, 0, Envelope(), PendingApprovals: [],
                ProviderEffectScope: AgentToolProviderEffectScope.LocalProposalsOnly)]
        });
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = state, Outcome = outcome,
            CompletedAtUtc = outcome is null ? null : Start } };
        var before = store.Detail.Run.ToolAdmission!;
        Assert.Equal(expected, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Equal(before.Segments, store.Detail.Run.ToolAdmission!.Segments);
        Assert.True(store.Detail.Run.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
        Assert.Equal(expected == AgentParentStopOutcome.Stopped ? 1 : 0, store.Writes);
        Assert.Equal(1, store.Leases);
        Assert.True(store.LeaseDisposed);
    }

    [Theory]
    [InlineData(AgentToolProviderEffectScope.Unknown)]
    [InlineData(AgentToolProviderEffectScope.HostedTools)]
    public async Task A_terminal_invocation_with_unproven_provider_scope_stays_unresolved(AgentToolProviderEffectScope scope) {
        var (store, owner, parent) = Create();
        SetJournal(store, store.Detail!.Run.ToolAdmission! with {
            Segments = [new(Guid.NewGuid(), 0, 0, Envelope(), PendingApprovals: [], ProviderEffectScope: scope)]
        });
        store.Detail = store.Detail! with { Run = store.Detail.Run with { State = ExecutionState.Failed,
            Outcome = RunOutcome.Cancelled, CompletedAtUtc = Start } };
        var before = store.Detail;
        Assert.Equal(AgentParentStopOutcome.UnresolvedEffects, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Same(before, store.Detail);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(AgentToolProposalState.Executing, AgentToolEffectState.Unknown, AgentParentStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Unknown, AgentParentStopOutcome.UnresolvedEffects)]
    [InlineData(AgentToolProposalState.Completed, AgentToolEffectState.Committed, AgentParentStopOutcome.CompletionPending)]
    public async Task Local_provider_scope_does_not_erase_dispatched_local_effects(AgentToolProposalState state,
        AgentToolEffectState effect, AgentParentStopOutcome expected) {
        var (store, owner, parent) = Create();
        var journal = WithProposal(store.Detail!.Run.ToolAdmission!, state, effect);
        SetJournal(store, journal with {
            SchemaVersion = AgentToolJournalRecord.BackgroundSchemaVersion,
            Segments = [journal.Segments[0] with { ProviderEffectScope = AgentToolProviderEffectScope.LocalProposalsOnly }],
            Batches = [journal.Batches[0] with { ProviderDispatchId = null }], ProviderDispatches = []
        });
        var before = store.Detail;
        Assert.Equal(expected, await owner.StopAsync(store.Detail.Run.Id, parent));
        Assert.Same(before, store.Detail);
        Assert.Equal(0, store.Writes);
    }

    private static (AgentInheritedParentStopFixture Store, AgentBackgroundExecutionStopper Owner, AgentBackgroundParentStop Parent) Create(
        Func<ExecutionRunRecord, CancellationToken, Task>? requireCurrent = null) {
        var store = new AgentInheritedParentStopFixture();
        var run = store.Detail!.Run with { MetadataJson = "{}", ProcessRunId = Guid.NewGuid().ToString("D"), ToolAdmission = null };
        var source = new AgentToolBackgroundSourceBinding(run.SourceKind, run.SourceId, Envelope().Digest, AgentBackgroundExecutionIdentity.Fingerprint(run));
        var session = new AgentToolSessionAdmission(new(run.Id, Guid.Empty, default, source), run.AgentId,
            AgentRuntimeContextPurpose.GovernedProcessAutomation, store.Original.Profile);
        var journal = new AgentToolJournalRecord(AgentToolJournalRecord.BackgroundSchemaVersion, 1, session, [], [], BackgroundInput: new("Original input"));
        journal.Validate();
        store.Detail = new(run with { ToolAdmission = journal }, null, [], []);
        return (store, new(store, store, store, store.Registry, store.Clock), new(session, requireCurrent ?? ((_, _) => Task.CompletedTask)));
    }

    private static void SetJournal(AgentInheritedParentStopFixture store, AgentToolJournalRecord journal) {
        journal.Validate();
        store.Detail = store.Detail! with { Run = store.Detail.Run with { ToolAdmission = journal } };
    }

    private static AgentToolJournalRecord Provider(AgentToolJournalRecord journal, AgentToolProviderDispatchState state) {
        var segment = new AgentToolInvocationSegment(Guid.NewGuid(), 0, 0, Envelope(), null, null, []);
        return journal with { SchemaVersion = AgentToolJournalRecord.ProviderDispatchSchemaVersion, Segments = [segment],
            ProviderDispatches = [new(new(Guid.NewGuid()), 0, segment.Id, Envelope().Digest, Envelope().Digest, [], state)] };
    }

    private static AgentToolJournalRecord WithProposal(AgentToolJournalRecord journal, AgentToolProposalState state, AgentToolEffectState effect) {
        var current = Provider(journal, AgentToolProviderDispatchState.Started);
        var dispatch = current.ProviderDispatches[0];
        var batchId = new AgentToolBatchId(Guid.NewGuid());
        var payload = new AgentToolPreparedPayload("original_mutation", 1, Envelope().Digest, "{}", AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt);
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-call", payload, false, state,
            ExecutionApprovalStatus.Approved, payload.Digest, DispatchClaimId: state == AgentToolProposalState.Prepared ? null : Guid.NewGuid(),
            Result: state == AgentToolProposalState.Completed ? Envelope("{\"completed\":true}") : null, EffectState: effect);
        return current with { ProviderDispatches = [dispatch with { State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = batchId }],
            Batches = [new(batchId, 0, dispatch.RequestDigest, Envelope(), [proposal], dispatch.Id)] };
    }
}
