using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Created_definition_reconciliation_uses_original_prior_creations_in_interrupted_and_cancelled_claims(
        bool process, bool cancelled) {
        var clock = new NativeClock();
        await using var application = await TestApplication.CreateAsync(ToolProducerHarness(clock));
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seed = await SeedHistoricalReceiptsAsync(services, clock, process, receiptCount: 5,
            createdDefinitions: true, unresolvedLast: true);
        var original = seed.Run.ToolAdmission!;
        var pending = original.Batches[^1].Proposals[0];
        Assert.Equal(AgentToolProposalState.Executing, pending.State);
        if (cancelled) {
            await seed.Store.UpdateExecutionRunDetailAsync(seed.Run.Id, detail => detail with {
                Run = detail.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, Revision = detail.Run.Revision + 1 }
            });
        }
        var journal = new AgentToolAdmissionJournal(seed.Store, NativeProfile(services), clock,
            [NativeBackgroundPolicy(services, seed.Owner, clock)]);
        var recovery = services.GetRequiredService<ProjectDefinitionToolReceiptRecovery>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        if (cancelled) {
            await using var lease = await journal.AcquireCancelledReconciliationAsync(seed.Session, deadline.Token);
            using var bound = lease.Bind();
            await journal.ReconcileCancelledAsync(lease, [recovery], true, deadline.Token);
        } else {
            await using var lease = await journal.AcquireInterruptedReconciliationAsync(seed.Session, deadline.Token);
            using var bound = lease.Bind();
            var result = await journal.ReconcileInterruptedAsync(lease, [recovery], (_, _) => new(true), deadline.Token);
            Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
            Assert.Equal(pending.IntentId, Assert.Single(result.Observations).Binding.IntentId);
            Assert.True(result.Observations[0].Persisted);
        }
        var saved = (await seed.Store.GetExecutionRunAsync(seed.Run.Id, deadline.Token))!.ToolAdmission!;
        Assert.Equal(JsonSerializer.Serialize(original.Batches[..^1]), JsonSerializer.Serialize(saved.Batches[..^1]));
        var observed = saved.Batches[^1].Proposals[0];
        Assert.Equal(pending.Payload, observed.Payload);
        Assert.Equal(pending.DispatchClaimId, observed.DispatchClaimId);
        Assert.Null(observed.Result);
        if (cancelled) {
            Assert.Equal(AgentToolProposalState.Cancelled, observed.State);
            Assert.Equal(AgentToolCancellationDisposition.ReceiptCommitted, observed.Cancellation!.Disposition);
            Assert.Equal(AgentToolEffectState.Committed, observed.EffectState);
        } else {
            Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, observed.State);
            Assert.Equal(pending.IntentId.Value.ToString("D"), observed.OwnerReceipt!.Effect.SourceId);
        }
        await AssertHistoricalReceiptsUnchangedAsync(services, seed);
    }
}
