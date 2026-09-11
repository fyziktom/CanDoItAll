using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class AgentToolBackgroundJournalIntegrationTests {
    [Fact]
    public async Task Actual_non_chat_entry_and_restart_recovery_keep_original_input_source_and_execution() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var firstStore = source.NewStore();
        var firstJournal = Journal(source, firstStore, policy);
        var firstRuntime = new Runtime(firstJournal, failAfterCheckpoint: true);
        AgentRunFailedException first;
        using (var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default)) {
            using var service = Service(source, firstStore, firstJournal, firstRuntime, cache);
            first = await Assert.ThrowsAsync<AgentRunFailedException>(() => service.ExecuteRunAsync(new(source.Agent.Id, "Original detached Process instruction.", AgentExecutionOperationId.New(),
                Context: new(SourceKind: policy.SourceKind, SourceId: SourcePolicy.StepKey,
                    CorrelationId: policy.RunId.ToString("D"), CausationId: policy.StepId.ToString("D"),
                    RequestedBy: "process-runtime", RequestedByKind: "system", MetadataJson: "{}",
                    ProcessRunId: policy.RunId.ToString("D"), ProcessStepId: policy.StepId.ToString("D")),
                AutoApprovePendingToolCalls: true)));
        }
        Assert.Equal("Interrupted after retained background SDK checkpoint.", Assert.IsType<IOException>(first.InnerException).Message);
        var saved = (await source.NewStore().GetExecutionRunDetailAsync(first.ExecutionRunId))!;
        Assert.Equal(ExecutionState.Failed, saved.Run.State);
        Assert.Null(saved.ChatSession);
        Assert.Null(saved.Run.ChatSessionId);
        Assert.Null(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(saved.Run.MetadataJson));
        Assert.Equal("Original detached Process instruction.", saved.Run.ToolAdmission!.BackgroundInput!.Content);
        Assert.Null(saved.Run.ToolAdmission.OriginalInput);
        Assert.Equal(AgentToolJournalRecord.BackgroundSchemaVersion, saved.Run.ToolAdmission.SchemaVersion);
        Assert.Single(saved.Run.ToolAdmission.Segments);
        var original = saved.Run.ToolAdmission.Session.Reference;

        var store = source.NewStore();
        var journal = Journal(source, store, policy);
        var runtime = new Runtime(journal);
        using var recoveryCache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var recoveredService = Service(source, store, journal, runtime, recoveryCache);
        var recovered = await recoveredService.RecoverExecutionRunAsync(first.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(first.ExecutionRunId, recovered.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, recovered.State);
        Assert.Equal(original, runtime.LastRequest!.ExecutionOptions!.AdmittedToolSession);
        Assert.Null(runtime.LastRequest.ExecutionOptions.Governance);
        Assert.Equal("Original detached Process instruction.", runtime.LastRequest.Prompt);
        Assert.Equal(original.BackgroundSource!.OwnerFingerprint.Value, runtime.LastRequest.ExecutionOptions.AuthorityPolicyFingerprint);
        policy.DispatchAllowed = false;
        var observed = await recoveredService.RecoverExecutionRunAsync(first.ExecutionRunId, AgentExecutionOperationId.New());
        Assert.Equal(recovered.ResponseText, observed.ResponseText);
        Assert.Equal(1, runtime.Calls);
        policy.ReadAllowed = false;
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => recoveredService.RecoverExecutionRunAsync(first.ExecutionRunId, AgentExecutionOperationId.New()));
    }

    [Fact]
    public async Task Identical_dynamic_proposals_have_distinct_intents_and_keep_serial_order() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using var lease = await journal.AcquireRunAsync(fixture.Reference, default);
        using var bound = lease.Bind();
        var payload = AgentToolAdmissionJournalFixture.Payload();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var saved = await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("first", payload, false), new("second", payload, false)], default);
        var batch = Assert.Single(saved.Batches);
        Assert.NotEqual(batch.Proposals[0].IntentId, batch.Proposals[1].IntentId);
        Assert.All(batch.Proposals, item => {
            Assert.NotEqual(fixture.Policy.RunId, item.IntentId.Value);
            Assert.NotEqual(fixture.Policy.StepId, item.IntentId.Value);
            Assert.NotEqual(fixture.Reference.ExecutionRunId, item.IntentId.Value);
        });
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.ClaimInvocationAsync(lease, batch.Id, "second", payload, default));
        var first = await journal.ClaimInvocationAsync(lease, batch.Id, "first", payload, default);
        await journal.CompleteInvocationAsync(first, AgentToolAdmissionJournalFixture.Envelope("{\"id\":1}"), AgentToolEffectState.Committed, default);
        var second = await journal.ClaimInvocationAsync(lease, batch.Id, "second", payload, default);
        Assert.Equal(batch.Proposals[1].IntentId, second.Proposal.IntentId);
    }

    [Theory]
    [InlineData((int)ExistingRunDetailCommitStage.JournalPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.RunPersisted)]
    [InlineData((int)ExistingRunDetailCommitStage.WorkspaceIndexPersisted)]
    public async Task Lost_checkpoint_ack_keeps_exact_background_batch_identity(int stageValue) {
        await using var fixture = await Fixture.CreateAsync();
        var armed = false;
        var failure = new IOException("Injected detached batch acknowledgement loss.");
        var store = fixture.Source.NewStore(stage => {
            if (armed && stage == (ExistingRunDetailCommitStage)stageValue) {
                armed = false;
                throw failure;
            }
        });
        var journal = fixture.Journal(store);
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = AgentToolAdmissionJournalFixture.Payload();
            armed = true;
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => journal.AdmitBatchAsync(lease, payload.Digest,
                AgentToolAdmissionJournalFixture.Envelope(), [new("only", payload, false)], default)));
        }
        var recovered = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!.ToolAdmission!;
        var reopened = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(fixture.Reference, recovered.Session.Reference);
        Assert.Equal(Assert.Single(recovered.Batches).Id, Assert.Single(reopened.Batches).Id);
        Assert.Equal(Assert.Single(recovered.Batches[0].Proposals).IntentId, Assert.Single(reopened.Batches[0].Proposals).IntentId);
    }

    [Theory]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt, false)]
    [InlineData(AgentToolProposalRecovery.RevalidateAndRead, false)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry, true)]
    public async Task Restart_keeps_proposal_identity_and_does_not_redispatch_unknown_non_idempotent_effects(
        AgentToolProposalRecovery recovery, bool blocked) {
        await using var fixture = await Fixture.CreateAsync();
        var payload = AgentToolAdmissionJournalFixture.Payload(recovery);
        AgentToolBatchRecord batch;
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("call", payload, false)], default)).Batches);
            await journal.ClaimInvocationAsync(lease, batch.Id, "call", payload, default);
        }
        var resumed = fixture.Journal(fixture.Source.NewStore());
        await using var next = await resumed.AcquireRunAsync(fixture.Reference, default);
        using var current = next.Bind();
        if (blocked) {
            var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await resumed.ClaimInvocationAsync(next, batch.Id, "call", payload, default));
            Assert.Equal("tool-admission.reconciliation-required", failure.Code);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => resumed.AdmitBatchAsync(next, payload.Digest,
                AgentToolAdmissionJournalFixture.Envelope(), [new("replacement", payload, false)], default));
        } else {
            var claim = await resumed.ClaimInvocationAsync(next, batch.Id, "call", payload, default);
            Assert.Equal(Assert.Single(batch.Proposals).IntentId, claim.Proposal.IntentId);
        }
    }

    [Fact]
    public async Task Saved_background_approval_keeps_exact_digest_and_cannot_approve_changed_arguments() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using var lease = await journal.AcquireRunAsync(fixture.Reference, default);
        using var current = lease.Bind();
        var segment = await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("call", payload, true)], default)).Batches);
        var pending = await journal.SaveApprovalCheckpointAsync(lease, segment.Segments[0].Id,
            AgentToolAdmissionJournalFixture.Envelope("{\"approval\":\"saved\"}"), "{\"sdk\":\"pending\"}",
            [new("saved", "call", payload.ToolName, "function", "Review", payload.ArgumentsJson)], default);
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Source.Store).UpdateExecutionRunDetailAsync(fixture.Reference.ExecutionRunId, detail => {
            var decisions = pending.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) { ToolAdmission = item.ToolAdmission }).ToArray();
            return detail with { Run = detail.Run with { ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(detail.Run.ToolAdmission!, pending, decisions, false),
                State = ExecutionState.Running, PendingApprovals = [], Revision = detail.Run.Revision + 1 } };
        });
        var saved = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(pending[0].ToolAdmission!.IntentId, Assert.Single(saved.Batches[0].Proposals).IntentId);
        Assert.Equal(payload.Digest, Assert.Single(saved.Batches[0].Proposals).ApprovedDigest);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.ClaimInvocationAsync(lease, batch.Id, "call",
            AgentToolAdmissionJournalFixture.Payload(value: "changed"), default));
    }

    [Fact]
    public async Task Revocation_after_commit_does_not_erase_outcome_but_blocks_new_dispatch_and_disclosure_when_read_is_denied() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using var lease = await journal.AcquireRunAsync(fixture.Reference, default);
        using var bound = lease.Bind();
        var payload = AgentToolAdmissionJournalFixture.Payload();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("committed", payload, false), new("not-started", payload, false)], default)).Batches);
        var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "committed", payload, default);
        fixture.Policy.DispatchAllowed = false;
        var receipt = AgentToolAdmissionJournalFixture.Envelope("{\"originalId\":19}");
        await journal.CompleteInvocationAsync(claim, receipt, AgentToolEffectState.Committed, default);
        var replay = await journal.ClaimInvocationAsync(lease, batch.Id, "committed", payload, default);
        Assert.Equal(receipt, replay.Proposal.Result);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.ClaimInvocationAsync(lease, batch.Id, "not-started", payload, default));
        fixture.Policy.ReadAllowed = false;
        var stored = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!;
        Assert.False(await journal.CanReadBackgroundResultAsync(stored));
        Assert.Equal(AgentToolProposalState.Completed, stored.ToolAdmission!.Batches[0].Proposals[0].State);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.ReadAsync(lease, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_source_policy_or_changed_original_owner_fingerprint_blocks_reentry(bool changed) {
        await using var fixture = await Fixture.CreateAsync();
        AgentToolAdmissionJournal journal;
        if (changed) {
            fixture.Policy.OwnerFingerprint = new(new string('c', 64));
            journal = fixture.Journal();
        } else {
            journal = new(fixture.Source.NewStore(), fixture.Source.Profile);
        }
        await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await journal.AcquireRunAsync(fixture.Reference, default));
        Assert.Empty((await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!.ToolAdmission!.Batches);
    }

    [Fact]
    public async Task Already_saved_legacy_execution_cannot_be_given_a_new_tool_identity() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var policy = new SourcePolicy(source);
        var run = Detached(source, policy);
        await source.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Journal(source, source.Store, policy)
            .CreateForNewBackgroundRunAsync(run, "Original input."));
        Assert.Equal("tool-admission.legacy-run", failure.Code);
        Assert.Null((await source.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_retains_original_intent_and_checks_current_owner_read_before_disclosure(bool canRead) {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        var payload = AgentToolAdmissionJournalFixture.Payload();
        AgentToolBusinessIntentId intent;
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var bound = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("original", payload, false)], default)).Batches);
            intent = Assert.Single(batch.Proposals).IntentId;
            await journal.ClaimInvocationAsync(lease, batch.Id, "original", payload, default);
        }
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Source.Store).UpdateExecutionRunDetailAsync(fixture.Reference.ExecutionRunId,
            detail => detail with { Run = detail.Run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                CompletedAtUtc = DateTimeOffset.UtcNow, Revision = detail.Run.Revision + 1 } });
        fixture.Policy.DispatchAllowed = false;
        fixture.Policy.ReadAllowed = canRead;
        var observer = new ReceiptReader(intent);
        await using var reconciliation = await journal.AcquireCancelledReconciliationAsync(fixture.Reference, default);
        using var current = reconciliation.Bind();
        var result = await journal.ReconcileCancelledAsync(reconciliation, [observer], canRead, default);
        Assert.Equal(intent, Assert.Single(result.Outcomes).IntentId);
        Assert.Equal(canRead ? 1 : 0, observer.Calls);
        Assert.Equal(canRead ? AgentToolCancellationDisposition.ReceiptCommitted : AgentToolCancellationDisposition.CancelledUnreconciled,
            Assert.Single(result.Outcomes).Cancellation!.Disposition);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => journal.BeginSegmentAsync(reconciliation,
            AgentToolAdmissionJournalFixture.Envelope(), null, default));
    }

    private static ExecutionRunRecord Detached(AgentToolAdmissionJournalFixture source, SourcePolicy policy)
        => source.Detail.Run with { Id = Guid.NewGuid(), ChatSessionId = null, SourceKind = policy.SourceKind, SourceId = SourcePolicy.StepKey,
            CorrelationId = policy.RunId.ToString("D"), CausationId = policy.StepId.ToString("D"), RequestedBy = "process-runtime", RequestedByKind = "system",
            ProcessRunId = policy.RunId.ToString("D"), ProcessStepId = policy.StepId.ToString("D"), MetadataJson = "{}", ToolAdmission = null,
            State = ExecutionState.Preparing, Outcome = null, Revision = 1, PendingApprovals = [] };

    private static AgentToolAdmissionJournal Journal(AgentToolAdmissionJournalFixture fixture, FileSandboxWorkspaceStore store, SourcePolicy policy)
        => new(store, fixture.Profile, backgroundSources: [policy]);

    private sealed class Fixture(AgentToolAdmissionJournalFixture source, SourcePolicy policy, AgentToolSessionReference reference) : IAsyncDisposable {
        public AgentToolAdmissionJournalFixture Source { get; } = source;
        public SourcePolicy Policy { get; } = policy;
        public AgentToolSessionReference Reference { get; } = reference;
        public AgentToolAdmissionJournal Journal(FileSandboxWorkspaceStore? store = null)
            => AgentToolBackgroundJournalIntegrationTests.Journal(Source, store ?? Source.Store, Policy);
        public static async Task<Fixture> CreateAsync() {
            var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
            try {
                var policy = new SourcePolicy(source);
                var run = Detached(source, policy);
                var journal = AgentToolBackgroundJournalIntegrationTests.Journal(source, source.Store, policy);
                run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Original detached instruction.") };
                await source.Store.SaveExecutionRunDetailAsync(new(run, null, [], []));
                return new(source, policy, run.ToolAdmission.Session.Reference);
            } catch {
                await source.DisposeAsync();
                throw;
            }
        }
        public ValueTask DisposeAsync() => Source.DisposeAsync();
    }

    private sealed class SourcePolicy(AgentToolAdmissionJournalFixture fixture) : IAgentToolBackgroundReservationPolicy {
        public HashSet<Guid> AcknowledgedExecutionRunIds { get; } = [];
        public ValueTask<AgentToolBackgroundReservationObservation> ObserveReservationAsync(ExecutionRunRecord candidate,
            AgentToolProfileBinding profile, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new AgentToolBackgroundReservationObservation(OwnerFingerprint, AcknowledgedExecutionRunIds));
        public const string StepKey = "original-step";
        public string SourceKind => "process-step";
        public Guid RunId { get; } = Guid.NewGuid();
        public Guid StepId { get; } = Guid.NewGuid();
        public bool ReadAllowed { get; set; } = true;
        public bool DispatchAllowed { get; set; } = true;
        public AgentToolSemanticDigest OwnerFingerprint { get; set; } = new(new string('a', 64));
        public ValueTask<AgentToolBackgroundSourceObservation> ObserveAsync(ExecutionRunRecord execution, AgentToolProfileBinding profile,
            CancellationToken cancellationToken = default) {
            Assert.Equal(fixture.Profile, profile);
            Assert.Equal(fixture.Agent.Id, execution.AgentId);
            Assert.Equal(RunId, Guid.Parse(execution.ProcessRunId));
            Assert.Equal(StepId, Guid.Parse(execution.ProcessStepId));
            Assert.Equal(StepKey, execution.SourceId);
            return ValueTask.FromResult(new AgentToolBackgroundSourceObservation(OwnerFingerprint, ReadAllowed, DispatchAllowed));
        }
    }

    private static AgentFrameworkWorkspaceService Service(AgentToolAdmissionJournalFixture fixture, FileSandboxWorkspaceStore store,
        AgentToolAdmissionJournal journal, Runtime runtime, AgentExecutionPreparationCache cache) {
        var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
            AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        var identity = new AgentExecutionActivityWorkspaceIdentity(fixture.Profile.ProfileId, WorkspaceScopeDescriptor.Sandbox, fixture.Profile.Generation);
        return new(store, new ZipAgentPackageService(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox), runtime, runtime, runtime, runtime,
            new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), NullLogger<AgentFrameworkWorkspaceService>.Instance,
            coordinator, identity, cache, new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation), new NoProcessLeases(),
            new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: journal);
    }

    private sealed class Runtime(AgentToolAdmissionJournal journal, bool failAfterCheckpoint = false)
        : IAgentExecutionRuntime, IAgentContinuationRuntime, IProviderDiagnosticsRuntime, IProviderModelAdministrationRuntime {
        public int Calls { get; private set; }
        public AgentRuntimeExecutionRequest? LastRequest { get; private set; }
        public async Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            LastRequest = request;
            var reference = Assert.IsType<AgentToolSessionReference>(request.ExecutionOptions!.AdmittedToolSession);
            Assert.NotNull(reference.BackgroundSource);
            Assert.Null(request.ExecutionOptions.Governance);
            await using var acquired = AgentToolRunLease.Current is null ? await journal.AcquireRunAsync(reference, cancellationToken) : null;
            using var current = acquired?.Bind();
            await journal.RequireSessionAsync(reference, cancellationToken);
            if (failAfterCheckpoint) {
                await journal.BeginSegmentAsync(AgentToolRunLease.Current!, AgentToolAdmissionJournalFixture.Envelope(), null, cancellationToken);
                throw new IOException("Interrupted after retained background SDK checkpoint.");
            }
            await request.ProgressCallback(ExecutionState.Running, "Recovery", "Recovered the original background invocation.");
            return new("Recovered background result.", 8, 5, 0, string.Empty, null, []);
        }
        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Recovery restores the original saved SDK segment.");
        public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProviderTestChatResult> RunProbeAsync(ProviderProfile provider, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }

    private sealed class ReceiptReader(AgentToolBusinessIntentId expected) : IAgentToolReceiptReconciliationProvider {
        public int Calls { get; private set; }
        public bool Supports(string toolName) => toolName == "admission_fixture_create";
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            var admitted = await claim.RequireAsync(cancellationToken);
            Assert.Equal(expected, admitted.Binding.IntentId);
            Calls++;
            return new(new("fixture-background-owner", expected.Value.ToString("D")), AgentToolAdmissionJournalFixture.Envelope("{\"retained\":true}"));
        }
    }
}
