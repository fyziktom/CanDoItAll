using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(AgentToolEffectState.Unknown)]
    [InlineData(AgentToolEffectState.Committed)]
    public async Task Explicit_owner_uncertainty_blocks_new_Process_rework_even_after_its_exact_execution_result_was_accepted(
        AgentToolEffectState effect) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var original = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
        var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var profile = NativeProfile(services);
        var policy = NativeBackgroundPolicy(services, fixture, clock);
        var journal = new AgentToolAdmissionJournal(store, profile, backgroundSources: [policy]);
        original = original with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(original, "Original owner work.") };
        var key = ExecutionRunSourceKey.ForBackground(original.SourceKind, original.SourceId, original.CorrelationId, original.CausationId);
        var first = await store.ReserveBackgroundExecutionRunAsync(key, new(original, null, [], []),
            await journal.PrepareBackgroundReservationAsync(original));
        Assert.Equal(ExecutionRunSourceDisposition.Created, first.Disposition);
        var checkpoint = CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope("{\"admittedRun\":47,\"observation\":\"pending\"}");
        await using (var lease = await journal.AcquireRunAsync(original.ToolAdmission!.Session.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.OwnerReceipt);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest,
                CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), [new("effect", payload, false)], default)).Batches);
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "effect", payload, default);
            await journal.CompleteInvocationAsync(claim, checkpoint, effect, default, requiresOwnerReconciliation: true);
        }
        await store.UpdateExecutionRunDetailAsync(original.Id, detail => detail with { Run = detail.Run with {
            State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded, CompletedAtUtc = clock.Now,
            ResultSummary = "Protocol result received; original owner observation remains pending.", Revision = detail.Run.Revision + 1
        } });
        var next = await ReworkClaimAsync(fixture, clock, original, acceptExactResult: true);
        var observed = await policy.ObserveReservationAsync(next, profile);
        Assert.Contains(original.Id, observed.AcknowledgedExecutionRunIds);
        next = next with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(next, "Explicit new rework.") };
        var reservation = await store.ReserveBackgroundExecutionRunAsync(key, new(next, null, [], []),
            await journal.PrepareBackgroundReservationAsync(next));
        Assert.Equal(ExecutionRunSourceDisposition.SourceReconciliationRequired, reservation.Disposition);
        Assert.Equal(original.Id, reservation.Run.Id);
        Assert.Null(await store.GetExecutionRunAsync(next.Id));
        var retained = (await store.GetExecutionRunAsync(original.Id))!.ToolAdmission!;
        Assert.True(retained.HasUnresolvedEffects);
        var proposal = Assert.Single(retained.Batches[0].Proposals);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, proposal.State);
        Assert.Equal(effect, proposal.EffectState);
        Assert.Equal(checkpoint, proposal.Result);
    }
}
