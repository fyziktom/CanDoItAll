using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentChatSessionRejectionIntegrationTests {
    [Theory]
    [InlineData(false, ExecutionState.Failed)]
    [InlineData(true, ExecutionState.Failed)]
    [InlineData(false, ExecutionState.Running)]
    [InlineData(true, ExecutionState.Running)]
    public async Task Thread_with_unresolved_work_keeps_original_receipt_and_rejects_new_prompt_after_reopen(
        bool remoteDispatch, ExecutionState state) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var journal = fixture.NewJournal();
        AgentToolBusinessIntentId committedIntent;
        var acknowledgedResult = AgentToolAdmissionJournalFixture.Envelope("{\"acknowledged\":true}");
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var saved = await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = AgentToolAdmissionJournalFixture.Payload();
            saved = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("acknowledged-operation", payload, false)], default);
            var claim = await journal.ClaimInvocationAsync(lease, saved.Batches[0].Id, "acknowledged-operation", payload, default);
            committedIntent = claim.Proposal.IntentId;
            await journal.CompleteInvocationAsync(claim, acknowledgedResult, AgentToolEffectState.Committed, default);
            if (remoteDispatch) {
                await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, payload.Digest, payload.Digest, [], default);
            } else {
                var read = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.RevalidateAndRead, "unresolved-read");
                await journal.AdmitBatchAsync(lease, read.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                    [new("unresolved-read", read, false)], default);
            }
        }
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            detail => detail with { Run = detail.Run with {
                State = state,
                Outcome = state == ExecutionState.Failed ? RunOutcome.Failed : detail.Run.Outcome,
                CompletedAtUtc = state == ExecutionState.Failed ? DateTimeOffset.UtcNow : null,
                Revision = detail.Run.Revision + 1
            } });
        var before = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        Assert.Equal(state, before.State);
        Assert.Empty(before.PendingApprovals);
        Assert.True(before.ToolAdmission!.HasUnresolvedEffects);
        var originalJournal = JsonSerializer.Serialize(before.ToolAdmission);
        var originalSession = (await fixture.NewStore().GetChatSessionAsync(before.ChatSessionId!.Value))!;
        var originalRuns = (await fixture.NewStore().LoadExecutionAsync()).ExecutionRuns.Select(run => run.Id).ToArray();
        var store = fixture.NewStore();
        var catalog = await store.LoadCatalogSnapshotAsync();
        var startFactoryCalls = 0;
        var blocked = Assert.IsType<ChatBackedRunBlocked>(await store.BeginChatBackedRunAsync(
            new(fixture.Agent.Id, fixture.Agent.ProviderProfileId!.Value, catalog.Revision, originalSession.Id),
            _ => {
                startFactoryCalls++;
                throw new InvalidOperationException("A blocked thread must never call its new-run factory.");
            }));
        Assert.Equal(before.Id, blocked.BlockingRun.Id);
        Assert.Equal(0, startFactoryCalls);

        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        var runtime = new NoDispatchRuntime();
        using var service = CreateService(fixture, store, runtime, cache);
        var failure = await Assert.ThrowsAsync<AgentChatSessionBlockedException>(() => service.SendMessageAsync(
            fixture.Agent.Id, originalSession.Id, "Read current settings without retrying prior work.",
            new AgentChatRunOptions(AgentExecutionOperationId.New())));
        Assert.Equal(state == ExecutionState.Running
            ? AgentChatSessionBlockReason.ActiveExecution
            : AgentChatSessionBlockReason.UnresolvedEffects, failure.Reason);
        Assert.Equal(before.AgentId, failure.AgentId);
        Assert.Equal(originalSession.Id, failure.ChatSessionId);
        Assert.Equal(before.Id, failure.ExecutionRunId);
        if (state == ExecutionState.Failed) {
            Assert.Contains("New thread", failure.Message, StringComparison.Ordinal);
        } else {
            Assert.Contains("Wait for it to finish", failure.Message, StringComparison.Ordinal);
        }
        Assert.Null(failure.InnerException);
        Assert.Equal(0, runtime.Calls);

        var afterStore = fixture.NewStore();
        var after = (await afterStore.GetExecutionRunAsync(before.Id))!;
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(state, after.State);
        Assert.Equal(originalJournal, JsonSerializer.Serialize(after.ToolAdmission));
        var receipt = after.ToolAdmission!.Batches.SelectMany(batch => batch.Proposals)
            .Single(proposal => proposal.IntentId == committedIntent);
        Assert.Equal(AgentToolProposalState.Completed, receipt.State);
        Assert.Equal(AgentToolEffectState.Committed, receipt.EffectState);
        Assert.Equal(acknowledgedResult, receipt.Result);
        Assert.Equal(JsonSerializer.Serialize(originalSession), JsonSerializer.Serialize(await afterStore.GetChatSessionAsync(originalSession.Id)));
        Assert.Equal(originalRuns, (await afterStore.LoadExecutionAsync()).ExecutionRuns.Select(run => run.Id).ToArray());
    }

    private static AgentFrameworkWorkspaceService CreateService(AgentToolAdmissionJournalFixture fixture,
        FileSandboxWorkspaceStore store, NoDispatchRuntime runtime, AgentExecutionPreparationCache cache) {
        var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
            AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        var identity = new AgentExecutionActivityWorkspaceIdentity(fixture.Profile.ProfileId, fixture.StorageScope, fixture.Profile.Generation);
        return new(store, new ZipAgentPackageService(fixture.WorkspaceRoot, fixture.StorageScope), runtime, runtime, runtime, runtime,
            new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), NullLogger<AgentFrameworkWorkspaceService>.Instance,
            coordinator, identity, cache, new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new NoProcessLeases(),
            new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: fixture.NewJournal(store));
    }

    private sealed class NoDispatchRuntime : IAgentExecutionRuntime, IAgentContinuationRuntime,
        IProviderDiagnosticsRuntime, IProviderModelAdministrationRuntime {
        public int Calls { get; private set; }

        private Exception UnexpectedDispatch() {
            Calls++;
            return new InvalidOperationException("No provider or tool may run for a rejected chat start.");
        }

        public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default)
            => throw UnexpectedDispatch();
        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
            => throw UnexpectedDispatch();
        public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default)
            => throw UnexpectedDispatch();
        public Task<ProviderTestChatResult> RunProbeAsync(ProviderProfile provider, ProviderTestChatRequest request,
            CancellationToken cancellationToken = default) => throw UnexpectedDispatch();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw UnexpectedDispatch();
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }
}
