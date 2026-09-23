using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Process_rework_requires_the_exact_accepted_execution_outcome_and_resolved_file_journal(bool acceptExactResult, bool ownerReceipt) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var original = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var policy = NativeBackgroundPolicy(services, fixture, clock);
        var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), backgroundSources: [policy]);
        original = original with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(original, "Original Process work.") };
        var key = ExecutionRunSourceKey.ForBackground(original.SourceKind, original.SourceId, original.CorrelationId, original.CausationId);
        var first = await store.ReserveBackgroundExecutionRunAsync(key, new(original, null, [], []),
            await journal.PrepareBackgroundReservationAsync(original));
        Assert.Equal(ExecutionRunSourceDisposition.Created, first.Disposition);
        AgentToolBusinessIntentId originalIntent;
        await using (var lease = await journal.AcquireRunAsync(original.ToolAdmission!.Session.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Payload(ownerReceipt
                ? AgentToolProposalRecovery.OwnerReceipt : AgentToolProposalRecovery.ReconcileBeforeRetry);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest,
                CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), [new("effect", payload, false)], default)).Batches);
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "effect", payload, default);
            originalIntent = claim.Proposal.IntentId;
            await journal.CompleteInvocationAsync(claim, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope("{\"nativeId\":41}"),
                ownerReceipt ? AgentToolEffectState.Committed : AgentToolEffectState.Unknown, default);
        }
        await store.UpdateExecutionRunDetailAsync(original.Id, detail => detail with { Run = detail.Run with {
            State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded, CompletedAtUtc = clock.Now,
            ResultSummary = "The native effect completed before Process accepted its result.", Revision = detail.Run.Revision + 1
        } });
        var next = await ReworkClaimAsync(fixture, clock, original, acceptExactResult);
        var observed = await policy.ObserveReservationAsync(next, NativeProfile(services));
        Assert.Equal(acceptExactResult, observed.AcknowledgedExecutionRunIds.Contains(original.Id));
        next = next with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(next, "Explicit Process rework.") };
        var reservation = await store.ReserveBackgroundExecutionRunAsync(key, new(next, null, [], []),
            await journal.PrepareBackgroundReservationAsync(next));
        Assert.Equal(acceptExactResult ? ExecutionRunSourceDisposition.Created : ExecutionRunSourceDisposition.SourceReconciliationRequired,
            reservation.Disposition);
        Assert.Equal(acceptExactResult ? next.Id : original.Id, reservation.Run.Id);
        var retained = (await store.GetExecutionRunAsync(original.Id))!;
        Assert.Equal(originalIntent, Assert.Single(retained.ToolAdmission!.Batches[0].Proposals).IntentId);
        Assert.Equal(ownerReceipt ? AgentToolEffectState.Committed : AgentToolEffectState.Unknown,
            retained.ToolAdmission.Batches[0].Proposals[0].EffectState);
        Assert.Equal(acceptExactResult, await store.GetExecutionRunAsync(next.Id) is not null);
        await using var restarted = fixture.Context();
        var owner = fixture.UnitOfWork(restarted);
        var state = (await owner.LoadAsync(new(Guid.Parse(original.ProcessRunId))))!;
        Assert.Equal(acceptExactResult, state.AppliedResults.Any(item => item.ExecutionRunId?.Value == original.Id));
        Assert.Equal(Guid.Parse(next.ProcessStepId), Assert.Single(state.Steps).StepInstanceId.Value);
        Assert.Equal(DispatchClaimStatus.Reclaimed, state.Claims.Single(item => item.ClaimToken.Value ==
            Guid.Parse(JsonSerializer.Deserialize<Dictionary<string, string>>(next.MetadataJson)!["agentProcessDispatchClaimIdentity"])).Status);
    }

    [Fact]
    public async Task Same_claim_failure_remains_recoverable_but_current_expiry_denies_new_effects() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var run = await CreateClaimedExecutionAsync(scope.ServiceProvider, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var policy = NativeBackgroundPolicy(scope.ServiceProvider, fixture, clock);
        var profile = NativeProfile(scope.ServiceProvider);
        var journal = new AgentToolAdmissionJournal(store, profile, backgroundSources: [policy]);
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Original recovery input.") };
        await store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        await store.UpdateExecutionRunDetailAsync(run.Id, detail => detail with { Run = detail.Run with {
            State = ExecutionState.Failed, Outcome = RunOutcome.Failed, CompletedAtUtc = clock.Now, Revision = detail.Run.Revision + 1
        } });
        var failed = (await store.GetExecutionRunAsync(run.Id))!;
        var current = await policy.ObserveAsync(failed, profile);
        Assert.True(current.ReadAllowed);
        Assert.True(current.DispatchAllowed);
        clock.Now = clock.Now.AddHours(2);
        var expired = await policy.ObserveAsync(failed, profile);
        Assert.True(expired.ReadAllowed);
        Assert.False(expired.DispatchAllowed);
        Assert.Equal(current.OwnerFingerprint, expired.OwnerFingerprint);
        Assert.Equal(run.ToolAdmission!.Session.Reference, failed.ToolAdmission!.Session.Reference);
    }

    private static async Task<ExecutionRunRecord> ReworkClaimAsync(Fixture fixture, NativeClock clock, ExecutionRunRecord original,
        bool acceptExactResult) {
        await using var context = fixture.Context();
        var unit = fixture.UnitOfWork(context);
        var engine = new ProcessRuntimeEngine(unit);
        var runId = new ProcessRunId(Guid.Parse(original.ProcessRunId));
        var stepId = new ProcessStepInstanceId(Guid.Parse(original.ProcessStepId));
        var state = (await unit.LoadAsync(runId))!;
        var active = state.Claims.Single(item => item.ClaimToken == Assert.Single(state.Steps).ActiveClaimToken);
        if (acceptExactResult) {
            var result = new StrategyResultEnvelope(new("strategy.admission-test.execute"), "1.0.0", Guid.NewGuid(),
                StrategyOutcome.NeedsManager, [], [], [], [], "sha256:" + new string('c', 64)) { ExecutionRunId = new(original.Id) };
            var accepted = await engine.SubmitStrategyResultAsync(state, ReservationCommand(clock.Now),
                new(stepId, active.OwnerId, active.ClaimToken, new(result.IdempotencyKey), result));
            Assert.True(accepted.Succeeded, string.Join("; ", accepted.Diagnostics.Select(item => item.Message)));
            state = accepted.State;
            Assert.Equal(original.Id, Assert.Single(state.AppliedResults).ExecutionRunId!.Value.Value);
        } else {
            clock.Now = clock.Now.AddHours(2);
            var expired = await engine.ExpireClaimsAsync(state, ReservationCommand(clock.Now), new(clock.Now));
            Assert.True(expired.Succeeded);
            state = expired.State;
            Assert.Empty(state.AppliedResults);
        }
        clock.Now = clock.Now.AddSeconds(1);
        var rework = await engine.RequestStepReworkAsync(state, ReservationCommand(clock.Now), new(stepId, "Explicit reviewed Process rework."));
        Assert.True(rework.Succeeded, string.Join("; ", rework.Diagnostics.Select(item => item.Message)));
        var token = new DispatchClaimToken(Guid.NewGuid());
        clock.Now = clock.Now.AddSeconds(1);
        var reclaimed = await engine.ReclaimClaimAsync(rework.State, ReservationCommand(clock.Now),
            new(stepId, new DispatcherOwnerId("native-fixture"), token, clock.Now.AddHours(1)));
        Assert.True(reclaimed.Succeeded, string.Join("; ", reclaimed.Diagnostics.Select(item => item.Message)));
        return original with { Id = Guid.NewGuid(), ToolAdmission = null, State = ExecutionState.Preparing, Outcome = null,
            Revision = 1, CreatedAtUtc = clock.Now, UpdatedAtUtc = clock.Now, CompletedAtUtc = null,
            MetadataJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["agentProcessDispatchClaimIdentity"] = token.Value.ToString("D") }) };
    }

    private static RuntimeCommandContext ReservationCommand(DateTimeOffset now)
        => new(RuntimeCommandId.New(), new(ProcessEventActorKind.User, new ProcessActorId("reservation-fixture")),
            new ProcessCorrelationId("reservation-fixture"), now);
}
