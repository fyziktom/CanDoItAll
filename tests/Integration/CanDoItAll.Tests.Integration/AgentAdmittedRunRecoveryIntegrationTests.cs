using CanDoItAll.Infrastructure;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class AgentAdmittedRunRecoveryIntegrationTests {
    [Fact]
    public async Task Owner_recovery_reuses_original_run_input_context_and_activity_then_returns_the_saved_completion() {
        var context = new AgentRuntimeTransientContext("Original admitted context.",
            WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")));
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(transientContext: context, includeRecoveryInput: true);
        await PrepareInterruptedAsync(fixture);
        var restartedStore = fixture.NewStore();
        var journal = fixture.NewJournal(restartedStore);
        var authority = new CurrentAuthority(fixture);
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, restartedStore, journal, authority, runtime, cache);
        var first = await service.RecoverExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(fixture.Session.ExecutionRunId, first.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, first.State);
        Assert.Equal(1, runtime.Calls);
        Assert.Equal(1, authority.Calls);
        Assert.Equal(fixture.Detail.Run.ToolAdmission!.OriginalInput!.Content, runtime.Request!.Prompt);
        Assert.Equal(context.Content, runtime.Request.ExecutionOptions!.TransientContext!.Content);
        Assert.Equal(fixture.Session, runtime.Request.ExecutionOptions.AdmittedToolSession);
        Assert.Equal(fixture.Session.ChatSessionId, runtime.Request.Session.Id);
        Assert.Equal(fixture.Session.AuthorityId, runtime.Request.ExecutionOptions.Governance!.AuthorityId);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(first.ExecutionRunId))!;
        Assert.Single(await restartedStore.ListExecutionRunsAsync());
        Assert.Single(saved.ChatSession!.Messages, message => message.Role == ChatMessageRole.User);
        Assert.Single(saved.ChatSession.Messages, message => message.Role == ChatMessageRole.Assistant);
        Assert.Equal(fixture.Detail.ChatSession!.Messages[0], saved.ChatSession.Messages[0]);

        var second = await service.RecoverExecutionRunAsync(first.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(first.ExecutionRunId, second.ExecutionRunId);
        Assert.Equal(first.ResponseText, second.ResponseText);
        Assert.Equal(1, runtime.Calls);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var released = await ((ISandboxWorkspaceExecutionRunLeaseStore)fixture.NewStore())
            .AcquireToolDispatchLeaseAsync(first.ExecutionRunId, timeout.Token);
    }

    [Theory]
    [InlineData(AuthorityChange.None, true)]
    [InlineData(AuthorityChange.MutationRevoked, true)]
    [InlineData(AuthorityChange.ReadRevoked, false)]
    [InlineData(AuthorityChange.ProfileChanged, false)]
    [InlineData(AuthorityChange.CapabilityRestricted, false)]
    [InlineData(AuthorityChange.ScopeChanged, false)]
    public async Task Completed_recovery_rechecks_current_project_read_without_requiring_mutation_or_replaying_the_runtime(
        AuthorityChange change, bool expectedReadable) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            transientContext: new("Original completed project context.", WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))));
        await PrepareInterruptedAsync(fixture);
        var originalStore = fixture.NewStore();
        var originalJournal = fixture.NewJournal(originalStore);
        var originalRuntime = new RecoveryRuntime(originalJournal, fixture);
        ExecutionRunResult completed;
        using (var originalCache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default)) {
            using var originalService = CreateService(fixture, originalStore, originalJournal, new CurrentAuthority(fixture), originalRuntime, originalCache);
            completed = await originalService.RecoverExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        }
        var before = (await fixture.NewStore().GetExecutionRunDetailAsync(completed.ExecutionRunId))!;
        var restartedStore = fixture.NewStore();
        var restartedJournal = fixture.NewJournal(restartedStore);
        var currentAuthority = new CurrentAuthority(fixture, change);
        var restartedRuntime = new RecoveryRuntime(restartedJournal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, restartedStore, restartedJournal, currentAuthority, restartedRuntime, cache);

        if (expectedReadable) {
            var observed = await service.RecoverExecutionRunAsync(completed.ExecutionRunId, AgentExecutionOperationId.New());
            Assert.Equal(completed.ExecutionRunId, observed.ExecutionRunId);
            Assert.Equal(completed.ResponseText, observed.ResponseText);
            Assert.Equal(ExecutionState.Completed, observed.State);
        } else {
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(
                completed.ExecutionRunId, AgentExecutionOperationId.New()));
            Assert.Equal("tool-admission.result-read-denied", denied.Code);
            Assert.DoesNotContain(completed.ResponseText, denied.Message, StringComparison.Ordinal);
        }

        Assert.Equal(1, originalRuntime.Calls);
        Assert.Equal(0, restartedRuntime.Calls);
        Assert.Equal(1, currentAuthority.Calls);
        var after = (await fixture.NewStore().GetExecutionRunDetailAsync(completed.ExecutionRunId))!;
        Assert.Equal(ExecutionState.Completed, after.Run.State);
        Assert.Equal(JsonSerializer.Serialize(before.Run.ToolAdmission), JsonSerializer.Serialize(after.Run.ToolAdmission));
        Assert.Equal(JsonSerializer.Serialize(before.ChatSession), JsonSerializer.Serialize(after.ChatSession));
    }

    [Theory]
    [InlineData(AuthorityChange.MutationRevoked)]
    [InlineData(AuthorityChange.ProfileChanged)]
    [InlineData(AuthorityChange.CapabilityRestricted)]
    public async Task Current_authority_denies_saved_execution_before_runtime_or_provider_replay(AuthorityChange change) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        await PrepareInterruptedAsync(fixture);
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var authority = new CurrentAuthority(fixture, change);
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, authority, runtime, cache);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(
            fixture.Session.ExecutionRunId, AgentExecutionOperationId.New()));
        Assert.Equal("tool-admission.authority-changed", failure.Code);
        Assert.Equal(0, runtime.Calls);
        var retained = (await store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!;
        Assert.Equal(fixture.Detail.Run.ToolAdmission!.OriginalInput, retained.Run.ToolAdmission!.OriginalInput);
        Assert.Equal(fixture.Detail.ChatSession!.Messages, retained.ChatSession!.Messages);
        Assert.Equal(RunOutcome.Failed, retained.Run.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unsupported_legacy_or_uncertain_non_idempotent_run_cannot_enter_runtime(bool uncertainEffect) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var id = fixture.Session.ExecutionRunId;
        if (uncertainEffect) {
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.ReconcileBeforeRetry);
            var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("call", payload, false)], default);
            await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "call", payload, default);
        } else {
            id = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            await store.SaveExecutionRunDetailAsync(fixture.Detail with {
                Run = fixture.Detail.Run with { Id = id, ChatSessionId = chatId, ToolAdmission = null },
                ChatSession = fixture.Detail.ChatSession! with { Id = chatId, LatestExecutionRunId = id }
            });
        }

        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture), runtime, cache);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(id, AgentExecutionOperationId.New()));
        Assert.Equal(uncertainEffect ? "tool-admission.reconciliation-required" : "tool-admission.legacy-run", failure.Code);
        Assert.Equal(0, runtime.Calls);
        if (!uncertainEffect) {
            Assert.Null((await fixture.NewStore().GetExecutionRunAsync(id))!.ToolAdmission);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_preserves_admitted_recovery_and_skips_an_independent_live_dispatch_lease(bool liveDispatch) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var journal = fixture.NewJournal();
        await using (var admission = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = admission.Bind();
            await journal.BeginSegmentAsync(admission, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        }

        await using var live = liveDispatch ? await journal.AcquireRunAsync(fixture.Session, default) : null;
        var before = (await fixture.Store.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISandboxWorkspaceExecutionRunStore>(fixture.NewStore());
        services.AddSingleton<IWorkspaceExecutionRunProcessLeaseCleaner, NoProcessLeases>();
        await using var provider = services.BuildServiceProvider();
        var type = typeof(AgentFrameworkModuleAssemblyMarker).Assembly.GetType(
            "CanDoItAll.Modules.AgentFramework.AgentFrameworkExecutionRecoveryService", throwOnError: true)!;
        var recovery = ActivatorUtilities.CreateInstance(provider, type);
        var method = type.GetMethod("RecoverInterruptedRunsAsync", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: [typeof(DateTimeOffset), typeof(CancellationToken)], modifiers: null)!;
        var repaired = await (Task<int>)method.Invoke(recovery, [DateTimeOffset.UtcNow.AddSeconds(1), CancellationToken.None])!;
        var after = (await fixture.NewStore().GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId))!;
        Assert.Equal(liveDispatch ? 0 : 1, repaired);
        Assert.Equal(JsonSerializer.Serialize(before.Run.ToolAdmission), JsonSerializer.Serialize(after.Run.ToolAdmission));
        Assert.Equal(JsonSerializer.Serialize(before.ChatSession), JsonSerializer.Serialize(after.ChatSession));
        Assert.Equal(before.Run.PendingApprovals, after.Run.PendingApprovals);
        Assert.Equal(before.Run.SerializedSessionStateJson, after.Run.SerializedSessionStateJson);
        Assert.Equal(liveDispatch ? before.Run.State : ExecutionState.Failed, after.Run.State);
        Assert.Equal(liveDispatch ? before.Run.Outcome : RunOutcome.Failed, after.Run.Outcome);
        Assert.NotEqual(RunOutcome.Cancelled, after.Run.Outcome);
    }

    [Fact]
    public async Task Active_cancellation_rejects_undispatched_proposals_in_the_existing_terminal_writer() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        await PrepareInterruptedAsync(fixture);
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        using var cancellation = new CancellationTokenSource();
        var runtime = new RecoveryRuntime(journal, fixture, async (_, token) => {
            var payload = AgentToolAdmissionJournalFixture.Payload();
            await journal.AdmitBatchAsync(AgentToolRunLease.Current!, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("not-dispatched", payload, true)], token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
        });
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture), runtime, cache);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RecoverExecutionRunAsync(
            fixture.Session.ExecutionRunId, AgentExecutionOperationId.New(), cancellation.Token));
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        var proposal = Assert.Single(Assert.Single(saved.ToolAdmission!.Batches).Proposals);
        Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
        Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
        Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
        Assert.Equal(AgentToolCancellationDisposition.NotDispatched, proposal.Cancellation!.Disposition);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        Assert.Null(proposal.DispatchClaimId);
        Assert.False(ExecutionRunSessionConcurrencyPolicy.BlocksSession(saved));
        Assert.Empty(saved.PendingApprovals);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(
            saved.Id, AgentExecutionOperationId.New()));
        Assert.Equal("tool-admission.cancelled-reconciliation-required", denied.Code);
        Assert.Equal(1, runtime.Calls);
    }

    [Theory]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry)]
    public async Task Cancelled_uncertainty_is_terminal_without_replay_and_later_receipts_do_not_replace_a_new_chat_turn(
        AgentToolProposalRecovery recovery) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            transientContext: new("Original cancelled project context.", WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))));
        await PrepareCancelledAsync(fixture, AgentToolAdmissionJournalFixture.Payload(recovery));
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var reader = new ReceiptReader();
        var runtime = new RecoveryRuntime(journal, fixture, requireOriginal: false);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture, AuthorityChange.MutationRevoked),
            runtime, cache, [reader]);
        var first = await service.ReconcileCancelledExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        var uncertain = Assert.Single(first.Outcomes);
        Assert.True(first.HasUnknownEffects);
        Assert.Equal(AgentToolEffectState.Unknown, uncertain.EffectState);
        Assert.Equal(AgentToolCancellationDisposition.CancelledUnreconciled, uncertain.Cancellation!.Disposition);
        Assert.Equal(recovery == AgentToolProposalRecovery.OwnerReceipt ? AgentToolCancellationReason.ReceiptNotObserved :
            AgentToolCancellationReason.NoReceiptProtocol, uncertain.Cancellation.Reason);
        Assert.Equal(recovery == AgentToolProposalRecovery.OwnerReceipt ? 1 : 0, reader.Calls);
        Assert.Equal(0, runtime.Calls);
        Assert.False(ExecutionRunSessionConcurrencyPolicy.BlocksSession((await store.GetExecutionRunAsync(fixture.Session.ExecutionRunId))!));

        var next = await service.SendMessageAsync(fixture.Agent.Id, fixture.Session.ChatSessionId, "A separate legitimate new turn.",
            new(AgentExecutionOperationId.New()));
        Assert.NotEqual(fixture.Session.ExecutionRunId, next.ExecutionRunId);
        Assert.Equal(1, runtime.Calls);
        var currentChat = (await store.GetExecutionRunDetailAsync(next.ExecutionRunId))!.ChatSession!;
        reader.Observation = new(new("fixture-definition", "original-definition"), AgentToolAdmissionJournalFixture.Envelope());
        var second = await service.ReconcileCancelledExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(recovery != AgentToolProposalRecovery.OwnerReceipt, second.HasUnknownEffects);
        Assert.Equal(recovery == AgentToolProposalRecovery.OwnerReceipt ? AgentToolEffectState.Committed : AgentToolEffectState.Unknown,
            Assert.Single(second.Outcomes).EffectState);
        var retainedChat = (await fixture.NewStore().GetExecutionRunDetailAsync(next.ExecutionRunId))!.ChatSession!;
        Assert.Equal(next.ExecutionRunId, retainedChat.LatestExecutionRunId);
        Assert.Equal(JsonSerializer.Serialize(currentChat), JsonSerializer.Serialize(retainedChat));
        Assert.Equal(1, runtime.Calls);
        Assert.Equal(RunOutcome.Cancelled, (await store.GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.Outcome);
    }

    [Fact]
    public async Task Current_read_revocation_records_uncertainty_without_calling_the_owner() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        await PrepareCancelledAsync(fixture, AgentToolAdmissionJournalFixture.Payload());
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var reader = new ReceiptReader();
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, new CurrentAuthority(fixture, AuthorityChange.ReadRevoked), runtime, cache, [reader]);
        var result = await service.ReconcileCancelledExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(AgentToolCancellationReason.CurrentAccessDenied, Assert.Single(result.Outcomes).Cancellation!.Reason);
        Assert.True(result.HasUnknownEffects);
        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, runtime.Calls);
    }

    [Fact]
    public async Task Cached_cancellation_receipt_is_not_disclosed_after_project_read_revocation_and_is_retained_for_restored_access() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true,
            transientContext: new("Original cancelled project context.", WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))));
        await PrepareCancelledAsync(fixture, AgentToolAdmissionJournalFixture.Payload());
        var store = fixture.NewStore();
        var journal = fixture.NewJournal(store);
        var authority = new CurrentAuthority(fixture);
        var reader = new ReceiptReader {
            Observation = new(new("fixture-definition", "private-original-definition"), AgentToolAdmissionJournalFixture.Envelope())
        };
        var runtime = new RecoveryRuntime(journal, fixture);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = CreateService(fixture, store, journal, authority, runtime, cache, [reader]);
        var original = await service.ReconcileCancelledExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        var originalOutcome = Assert.Single(original.Outcomes);
        Assert.Equal(AgentToolEffectState.Committed, originalOutcome.EffectState);
        Assert.Equal(1, reader.Calls);

        authority.Change = AuthorityChange.ReadRevoked;
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.ReconcileCancelledExecutionRunAsync(
            fixture.Session.ExecutionRunId, AgentExecutionOperationId.New()));
        Assert.Equal("tool-admission.receipt-read-denied", denied.Code);
        Assert.DoesNotContain("private-original-definition", denied.Message, StringComparison.Ordinal);
        Assert.Equal(1, reader.Calls);
        var retained = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!;
        var retainedProposal = Assert.Single(Assert.Single(retained.ToolAdmission!.Batches).Proposals);
        Assert.Equal(AgentToolEffectState.Committed, retainedProposal.EffectState);
        Assert.Equal(originalOutcome.Cancellation, retainedProposal.Cancellation);

        authority.Change = AuthorityChange.MutationRevoked;
        var restored = await service.ReconcileCancelledExecutionRunAsync(fixture.Session.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(originalOutcome, Assert.Single(restored.Outcomes));
        Assert.False(restored.HasUnknownEffects);
        Assert.Equal(2, reader.Calls);
        Assert.Equal(0, runtime.Calls);
        Assert.Equal(RunOutcome.Cancelled, (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.Outcome);
    }

    [Fact]
    public async Task Reconciliation_lease_cannot_admit_claim_or_authorize_a_tool_invocation() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        await PrepareCancelledAsync(fixture, payload);
        var journal = fixture.NewJournal(fixture.NewStore());
        await using var lease = await journal.AcquireCancelledReconciliationAsync(fixture.Session, default);
        using var bound = lease.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.RequireSessionAsync(fixture.Session, default).AsTask());
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default));
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.AdmitBatchAsync(lease, payload.Digest,
            AgentToolAdmissionJournalFixture.Envelope(), [new("new-effect", payload, false)], default));
        var batch = Assert.Single((await fixture.Store.GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.ClaimInvocationAsync(lease, batch.Id, "original-call", payload, default));
        var reader = new ReceiptReader();
        await journal.ReconcileCancelledAsync(lease, [reader], true, default);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Independent_reconciliation_waits_for_the_actual_live_dispatch_file_lease() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        await PrepareCancelledAsync(fixture, AgentToolAdmissionJournalFixture.Payload());
        var journal = fixture.NewJournal(fixture.NewStore());
        await using (var live = await ((ISandboxWorkspaceExecutionRunLeaseStore)fixture.NewStore())
            .AcquireToolDispatchLeaseAsync(fixture.Session.ExecutionRunId, default)) {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                await using var blocked = await journal.AcquireCancelledReconciliationAsync(fixture.Session, timeout.Token);
            });
        }

        await using var released = await journal.AcquireCancelledReconciliationAsync(fixture.Session, default);
    }

    private static async Task PrepareCancelledAsync(AgentToolAdmissionJournalFixture fixture, AgentToolPreparedPayload payload) {
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var admitted = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("original-call", payload, false)], default);
            await journal.ClaimInvocationAsync(lease, admitted.Batches[0].Id, "original-call", payload, default);
        }

        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            current => current with { Run = current.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                CompletedAtUtc = DateTimeOffset.UtcNow, Revision = current.Run.Revision + 1 } });
    }

    private sealed class ReceiptReader : IAgentToolReceiptReconciliationProvider {
        public int Calls { get; private set; }
        public AgentToolReceiptObservation Observation { get; set; } = AgentToolReceiptObservation.NotObserved;
        public bool Supports(string toolName) => toolName == "admission_fixture_create";
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            var admitted = await claim.RequireAsync(cancellationToken);
            Assert.NotEqual(Guid.Empty, admitted.Binding.IntentId.Value);
            return Observation;
        }
    }

    private static async Task PrepareInterruptedAsync(AgentToolAdmissionJournalFixture fixture) {
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        }

        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId,
            current => current with { Run = current.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow, Revision = current.Run.Revision + 1 } });
    }

    private static AgentFrameworkWorkspaceService CreateService(AgentToolAdmissionJournalFixture fixture,
        FileSandboxWorkspaceStore store, AgentToolAdmissionJournal journal, CurrentAuthority authority,
        RecoveryRuntime runtime, AgentExecutionPreparationCache cache, IEnumerable<IAgentToolReceiptReconciliationProvider>? receiptProviders = null) {
        var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
            AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        var identity = new AgentExecutionActivityWorkspaceIdentity(fixture.Profile.ProfileId, WorkspaceScopeDescriptor.Sandbox, fixture.Profile.Generation);
        return new(store, new ZipAgentPackageService(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox),
            runtime, runtime, runtime, runtime, new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()),
            NullLogger<AgentFrameworkWorkspaceService>.Instance, coordinator, identity, cache,
            new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new NoProcessLeases(),
            new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: journal, executionAuthorityResolver: authority,
            receiptReconciliationProviders: receiptProviders);
    }

    public enum AuthorityChange { None, MutationRevoked, ProfileChanged, CapabilityRestricted, ReadRevoked, ScopeChanged }

    private sealed class CurrentAuthority(AgentToolAdmissionJournalFixture fixture, AuthorityChange change = AuthorityChange.None)
        : IAgentExecutionAuthorityResolver {
        public int Calls { get; private set; }
        public AuthorityChange Change { get; set; } = change;

        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            Calls++;
            Assert.Equal(fixture.Agent.Id, request.AgentId);
            Assert.Equal(fixture.Profile.Generation, request.ExpectedDatabaseProfileGeneration);
            Assert.Null(request.UiAccessHint);
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Detail.Run.MetadataJson)!;
            Assert.Equal(original.WorkspaceScope, request.ObservedWorkspaceScope);
            Assert.Equal(source.SourceKind, request.SourceKind);
            Assert.Equal(source.SourceId, request.SourceId);
            return ValueTask.FromResult(new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), fixture.Agent.Id,
                Change == AuthorityChange.ProfileChanged ? Guid.NewGuid() : fixture.Profile.ProfileId,
                fixture.Profile.Generation, Change == AuthorityChange.ScopeChanged ? WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) : original.WorkspaceScope,
                Change != AuthorityChange.ReadRevoked, Change is not (AuthorityChange.MutationRevoked or AuthorityChange.ReadRevoked),
                original.PolicyVersion, Change == AuthorityChange.MutationRevoked ? original.PolicyFingerprint + ":read-only" : original.PolicyFingerprint,
                DateTimeOffset.UtcNow, allowedCapabilityKeys: Change == AuthorityChange.CapabilityRestricted ? ["different-capability"] : null));
        }
    }

    private sealed class RecoveryRuntime(AgentToolAdmissionJournal journal, AgentToolAdmissionJournalFixture fixture,
        Func<AgentRuntimeExecutionRequest, CancellationToken, Task>? beforeResult = null, bool requireOriginal = true)
        : IAgentExecutionRuntime, IAgentContinuationRuntime, IProviderDiagnosticsRuntime, IProviderModelAdministrationRuntime {
        public int Calls { get; private set; }
        public AgentRuntimeExecutionRequest? Request { get; private set; }

        public async Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            Request = request;
            if (requireOriginal) {
                Assert.NotNull(AgentToolRunLease.Current);
                Assert.Equal(fixture.Session, AgentToolRunLease.Current.Session);
                await journal.RequireSessionAsync(fixture.Session, cancellationToken);
            }

            if (beforeResult is not null) {
                await beforeResult(request, cancellationToken);
            }
            await request.ProgressCallback(ExecutionState.Running, "Recovery", "Continuing the original admitted run.");
            return new("Recovered original execution.", 8, 5, 0, string.Empty, null, []);
        }

        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Recovery must restore its original SDK segment through the execution port.");
        public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProviderTestChatResult> RunProbeAsync(ProviderProfile provider, ProviderTestChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }
}
