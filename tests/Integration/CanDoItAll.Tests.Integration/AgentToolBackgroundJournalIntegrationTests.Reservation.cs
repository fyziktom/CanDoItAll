using System.Diagnostics;
using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolBackgroundJournalIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_rechecks_original_authority_after_waiting_for_file_keys(bool changedFingerprint) {
        await RunReservationRaceAsync(async race => {
            var policy = new SourcePolicy(race.Source);
            var candidate = await PrepareReservationAsync(race.Source, policy);
            await race.HoldAsync(candidate.Detail.Run);
            using var wait = new ReservationPublicationWait(1);
            var pending = race.Track(race.Source.NewStore().ReserveBackgroundExecutionRunAsync(
                BackgroundKey(policy), candidate.Detail, candidate.Reservation, race.Token));
            await wait.Entered.Task.WaitAsync(race.Token);
            Assert.Equal(0, policy.ActivePublications);
            if (changedFingerprint) {
                policy.OwnerFingerprint = new(new string('b', 64));
            } else {
                policy.DispatchAllowed = false;
            }
            await race.ReleaseAsync();
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => race.ObserveAsync(pending));
            Assert.Equal(0, policy.ActivePublications);
            Assert.Null(await race.Source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id, race.Token));
            await race.RequireKeysAvailableAsync(candidate.Detail.Run);
        });
    }

    [Fact]
    public async Task Original_reservation_replay_does_not_acquire_fresh_publication_authority() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var candidate = await PrepareReservationAsync(source, policy);
        var created = await source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.Created, created.Disposition);
        Assert.Equal(0, policy.ActivePublications);
        policy.DispatchAllowed = false;
        var replay = await source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.ExistingActive, replay.Disposition);
        Assert.Equal(created.Run.Id, replay.Run.Id);
        Assert.Equal(created.Run.ToolAdmission!.Session, replay.Run.ToolAdmission!.Session);
        Assert.Equal(0, policy.ActivePublications);
    }

    [Fact]
    public async Task Publication_and_authority_release_failures_preserve_both_original_errors_and_recoverable_journal() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var release = new IOException("Original publication authority release failed.");
        var policy = new SourcePolicy(source) { PublicationReleaseFailure = release };
        var candidate = await PrepareReservationAsync(source, policy);
        var write = new IOException("The new execution journal was persisted before acknowledgement.");
        var store = new FileSandboxWorkspaceStore(source.WorkspaceRoot, source.StorageScope,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: stage => {
                Assert.Equal(1, policy.ActivePublications);
                if (stage == GenericNewRunCommitStage.JournalPersisted) {
                    throw write;
                }
            }, jsonReadDiagnostics: null);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => store.ReserveBackgroundExecutionRunAsync(
            BackgroundKey(policy), candidate.Detail, candidate.Reservation));
        Assert.Contains(write, failure.Flatten().InnerExceptions);
        Assert.Contains(release, failure.Flatten().InnerExceptions);
        Assert.Equal(0, policy.ActivePublications);
        policy.DispatchAllowed = false;
        var replay = await source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.ExistingActive, replay.Disposition);
        Assert.Equal(candidate.Detail.Run.ToolAdmission!.Session, replay.Run.ToolAdmission!.Session);
    }

    [Fact]
    public async Task Actual_same_source_retry_recovers_the_failed_execution_and_original_input_after_restart() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var key = BackgroundKey(policy);
        var request = BackgroundRequest(source, policy, "Original admitted work.");
        AgentRunFailedException failed;
        using (var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default)) {
            var store = source.NewStore();
            var journal = Journal(source, store, policy);
            using var service = Service(source, store, journal, new Runtime(journal, failAfterCheckpoint: true), cache);
            failed = await Assert.ThrowsAsync<AgentRunFailedException>(() => service.ExecuteSameSourceRunAsync(key, request));
        }
        Assert.Equal("Interrupted after retained background SDK checkpoint.", Assert.IsType<IOException>(failed.InnerException).Message);
        var reopened = source.NewStore();
        Assert.Equal(ExecutionState.Failed, (await reopened.GetExecutionRunAsync(failed.ExecutionRunId))!.State);
        var resumedJournal = Journal(source, reopened, policy);
        var runtime = new Runtime(resumedJournal);
        using var resumedCache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var resumed = Service(source, reopened, resumedJournal, runtime, resumedCache);
        var retry = await resumed.ExecuteSameSourceRunAsync(key, request with { InitialActivityOperationId = AgentExecutionOperationId.New(), Prompt = "A newly composed retry prompt must not replace saved input." });
        Assert.Equal(ExecutionRunSourceDisposition.ExistingAdmittedFailure, retry.Disposition);
        Assert.Equal(failed.ExecutionRunId, retry.Run.Id);
        Assert.Null(retry.CreatedExecutionResult);
        Assert.Equal(0, runtime.Calls);
        var completed = await resumed.RecoverExecutionRunAsync(retry.Run.Id, AgentExecutionOperationId.New());
        Assert.Equal(retry.Run.Id, completed.ExecutionRunId);
        Assert.Equal("Original admitted work.", runtime.LastRequest!.Prompt);
        Assert.Equal(1, runtime.Calls);
        var replay = await resumed.ExecuteSameSourceRunAsync(key, request with { InitialActivityOperationId = AgentExecutionOperationId.New() });
        Assert.Equal(ExecutionRunSourceDisposition.ReusedCompleted, replay.Disposition);
        Assert.Equal(completed.ExecutionRunId, replay.Run.Id);
        Assert.Equal(1, runtime.Calls);
        Assert.Single(await reopened.ListExecutionRunsAsync(), key.MatchesBackgroundLineage);
    }

    [Fact]
    public Task Independent_workspace_instances_reserve_exactly_one_execution_for_the_same_claim() => RunReservationRaceAsync(async race => {
        var source = race.Source;
        var policy = new SourcePolicy(source);
        var first = await PrepareReservationAsync(source, policy);
        var second = await PrepareReservationAsync(source, policy);
        await race.HoldAsync(first.Detail.Run);
        using var wait = new ReservationPublicationWait(2);
        var committed = 0;
        FileSandboxWorkspaceStore Store() => new(source.WorkspaceRoot, source.StorageScope,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: stage => {
                if (stage == GenericNewRunCommitStage.JournalPersisted) {
                    Interlocked.Increment(ref committed);
                }
            }, jsonReadDiagnostics: null);
        var left = race.Track(Store().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), first.Detail, first.Reservation, race.Token));
        var right = race.Track(Store().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), second.Detail, second.Reservation, race.Token));
        await wait.Entered.Task.WaitAsync(race.Token);
        Assert.False(left.IsCompleted);
        Assert.False(right.IsCompleted);
        Assert.Equal(0, Volatile.Read(ref committed));
        await race.RequireWorkspaceAvailableAsync();
        Assert.False(File.Exists(race.Layout.RunPath(first.Detail.Run.Id)));
        Assert.False(File.Exists(race.Layout.RunPath(second.Detail.Run.Id)));
        await race.ReleaseAsync();
        var results = new[] { await race.ObserveAsync(left), await race.ObserveAsync(right) };
        var created = Assert.Single(results, result => result.Disposition == ExecutionRunSourceDisposition.Created);
        var replay = Assert.Single(results, result => result.Disposition == ExecutionRunSourceDisposition.ExistingActive);
        Assert.Equal(1, Volatile.Read(ref committed));
        Assert.Equal(created.Run.Id, replay.Run.Id);
        Assert.Equal(created.Run.ToolAdmission!.Session, replay.Run.ToolAdmission!.Session);
        Assert.Equal(created.Run.ToolAdmission.BackgroundInput, replay.Run.ToolAdmission.BackgroundInput);
        Assert.Equal(policy.OwnerFingerprint, replay.Run.ToolAdmission.Session.Reference.BackgroundSource!.OwnerFingerprint);
        replay.Run.ToolAdmission.Validate();
        var winner = created.Run.Id == first.Detail.Run.Id ? first.Detail.Run : second.Detail.Run;
        var loser = created.Run.Id == first.Detail.Run.Id ? second.Detail.Run : first.Detail.Run;
        Assert.Equal(winner.ToolAdmission!.Session, replay.Run.ToolAdmission.Session);
        Assert.False(File.Exists(race.Layout.RunPath(loser.Id)));
        Assert.Single(await source.NewStore().ListExecutionRunsAsync(race.Token), BackgroundKey(policy).MatchesBackgroundLineage);
        await race.RequireKeysAvailableAsync(first.Detail.Run, second.Detail.Run);
    });

    [Fact]
    public async Task Reservation_conflict_without_a_complete_original_claim_does_not_retry_or_recover() {
        foreach (var evidence in Enum.GetValues<ReservationConflictEvidence>()) {
            await RunReservationRaceAsync(async race => {
                var policy = new SourcePolicy(race.Source);
                var candidate = await PrepareReservationAsync(race.Source, policy);
                await race.HoldAsync(candidate.Detail.Run);
                using var wait = new ReservationPublicationWait(1);
                var pending = race.Track(race.Source.NewStore().ReserveBackgroundExecutionRunAsync(
                    BackgroundKey(policy), candidate.Detail, candidate.Reservation, race.Token));
                await wait.Entered.Task.WaitAsync(race.Token);
                Assert.False(pending.IsCompleted);
                await race.RequireWorkspaceAvailableAsync();
                var checkpoint = await race.Source.NewStore().UpdateExecutionRunDetailAsync(race.Source.Detail.Run.Id,
                    current => current with { Run = current.Run with {
                        ResultSummary = "Independent checkpoint while reservation keys wait.", Revision = current.Run.Revision + 1
                    } }, race.Token);
                var catalog = await File.ReadAllBytesAsync(race.Layout.CatalogPath, race.Token);
                var index = await File.ReadAllBytesAsync(race.Layout.WorkspaceIndexPath, race.Token);
                var checkpointBytes = await File.ReadAllBytesAsync(race.Layout.RunPath(checkpoint.Run.Id), race.Token);
                var retained = "Unresolved publication evidence must remain untouched."u8.ToArray();
                var marker = race.Marker(evidence, candidate.Detail.Run.Id);
                if (marker is not null) {
                    Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                    await File.WriteAllBytesAsync(marker, retained, race.Token);
                }
                await race.ReleaseAsync();
                var failure = await Record.ExceptionAsync(() => race.ObserveAsync(pending));
                if (evidence == ReservationConflictEvidence.UnrelatedCheckpoint) {
                    Assert.IsType<SandboxWorkspaceConcurrencyException>(failure);
                } else {
                    Assert.IsType<SandboxWorkspacePublicationConflictException>(failure);
                    Assert.Equal(retained, await File.ReadAllBytesAsync(marker!, race.Token));
                }
                Assert.False(File.Exists(race.Layout.RunPath(candidate.Detail.Run.Id)));
                Assert.Equal(catalog, await File.ReadAllBytesAsync(race.Layout.CatalogPath, race.Token));
                Assert.Equal(index, await File.ReadAllBytesAsync(race.Layout.WorkspaceIndexPath, race.Token));
                Assert.Equal(checkpointBytes, await File.ReadAllBytesAsync(race.Layout.RunPath(checkpoint.Run.Id), race.Token));
                await race.RequireKeysAvailableAsync(candidate.Detail.Run);
            });
        }
    }

    [Theory]
    [InlineData((int)GenericNewRunCommitStage.JournalPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionSlicesPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ExecutionIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.UsageIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.WorkspaceIndexPersisted)]
    [InlineData((int)GenericNewRunCommitStage.ChatIndexPersisted)]
    public async Task Lost_new_execution_ack_preserves_the_exact_background_identity(int stageValue) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var prepared = await PrepareReservationAsync(source, policy);
        var failure = new IOException("Injected background reservation acknowledgement loss.");
        var store = new FileSandboxWorkspaceStore(source.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox,
            chatBackedRunCommitBoundary: null, existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: stage => {
                if (stage == (GenericNewRunCommitStage)stageValue) {
                    throw failure;
                }
            }, jsonReadDiagnostics: null);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => store.ReserveBackgroundExecutionRunAsync(
            BackgroundKey(policy), prepared.Detail, prepared.Reservation)));
        var retry = await PrepareReservationAsync(source, policy);
        var result = await source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), retry.Detail, retry.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.ExistingActive, result.Disposition);
        Assert.Equal(prepared.Detail.Run.Id, result.Run.Id);
        Assert.Equal(prepared.Detail.Run.ToolAdmission!.Session.Reference, result.Run.ToolAdmission!.Session.Reference);
        Assert.Single(await source.NewStore().ListExecutionRunsAsync(), BackgroundKey(policy).MatchesBackgroundLineage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_claim_cannot_replace_a_completed_effect_before_its_exact_Process_outcome_is_acknowledged(bool acknowledgeOtherId) {
        await using var fixture = await Fixture.CreateAsync();
        var intent = await CompleteReservationEffectAsync(fixture);
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        if (acknowledgeOtherId) {
            fixture.Policy.AcknowledgedExecutionRunIds.Add(Guid.NewGuid());
        }
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var result = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.SourceReconciliationRequired, result.Disposition);
        Assert.Equal(fixture.Reference.ExecutionRunId, result.Run.Id);
        Assert.Equal(intent, Assert.Single(result.Run.ToolAdmission!.Batches[0].Proposals).IntentId);
        Assert.Null(await fixture.Source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id));
    }

    [Fact]
    public async Task Exact_acknowledged_terminal_journal_allows_authorized_rework_with_new_dynamic_proposal_identity() {
        await using var fixture = await Fixture.CreateAsync();
        var originalIntent = await CompleteReservationEffectAsync(fixture);
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId);
        fixture.Policy.AcknowledgedExecutionRunIds.Add(fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var result = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.Created, result.Disposition);
        Assert.NotEqual(fixture.Reference.ExecutionRunId, result.Run.Id);
        var journal = fixture.Journal(fixture.Source.NewStore());
        await using var lease = await journal.AcquireRunAsync(result.Run.ToolAdmission!.Session.Reference, default);
        using var current = lease.Bind();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("same-dynamic-call", payload, false)], default)).Batches);
        Assert.NotEqual(originalIntent, Assert.Single(batch.Proposals).IntentId);
        Assert.NotEqual(fixture.Policy.RunId, batch.Proposals[0].IntentId.Value);
        Assert.NotEqual(fixture.Policy.StepId, batch.Proposals[0].IntentId.Value);
    }

    [Theory]
    [InlineData(AgentToolProposalRecovery.OwnerReceipt)]
    [InlineData(AgentToolProposalRecovery.ReconcileBeforeRetry)]
    public async Task Acknowledged_result_does_not_erase_an_unresolved_prior_proposal(AgentToolProposalRecovery recovery) {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var current = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = AgentToolAdmissionJournalFixture.Payload(recovery);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("uncertain", payload, false)], default)).Batches);
            await journal.ClaimInvocationAsync(lease, batch.Id, "uncertain", payload, default);
        }
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId);
        fixture.Policy.AcknowledgedExecutionRunIds.Add(fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var result = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.SourceReconciliationRequired, result.Disposition);
        Assert.Null(await fixture.Source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id));
        Assert.True(result.Run.ToolAdmission!.HasUnresolvedEffects);
    }

    [Fact]
    public async Task Duplicate_preexisting_claim_journals_require_reconciliation_instead_of_timestamp_selection() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var first = await PrepareReservationAsync(source, policy);
        var second = await PrepareReservationAsync(source, policy);
        await source.Store.SaveExecutionRunDetailAsync(first.Detail);
        await source.Store.SaveExecutionRunDetailAsync(second.Detail);
        var candidate = await PrepareReservationAsync(source, policy);
        var error = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => source.NewStore().ReserveBackgroundExecutionRunAsync(
            BackgroundKey(policy), candidate.Detail, candidate.Reservation));
        Assert.Equal("tool-admission.duplicate-source-admission", error.Code);
        Assert.Null(await source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acknowledged_successful_legacy_execution_can_be_intentionally_reworked_without_retrofitting_a_journal(bool succeeded) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var policy = new SourcePolicy(source);
        var legacy = Detached(source, policy) with {
            State = succeeded ? ExecutionState.Completed : ExecutionState.Failed,
            Outcome = succeeded ? RunOutcome.Succeeded : RunOutcome.Failed, CompletedAtUtc = DateTimeOffset.UtcNow
        };
        await source.Store.SaveExecutionRunDetailAsync(new(legacy, null, [], []));
        policy.AcknowledgedExecutionRunIds.Add(legacy.Id);
        var candidate = await PrepareReservationAsync(source, policy);
        var result = await source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(succeeded ? ExecutionRunSourceDisposition.Created : ExecutionRunSourceDisposition.SourceReconciliationRequired,
            result.Disposition);
        Assert.Null((await source.NewStore().GetExecutionRunAsync(legacy.Id))!.ToolAdmission);
        Assert.Equal(succeeded, await source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id) is not null);
    }

    [Fact]
    public async Task Saved_generic_success_can_be_intentionally_reworked_without_claiming_an_atomic_owner_commit() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var current = lease.Bind();
            await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.ReconcileBeforeRetry);
            var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
                [new("unknown", payload, false)], default)).Batches);
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, "unknown", payload, default);
            await journal.CompleteInvocationAsync(claim, AgentToolAdmissionJournalFixture.Envelope("{\"status\":\"success\"}"), AgentToolEffectState.Unknown, default);
        }
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId, succeeded: true);
        fixture.Policy.AcknowledgedExecutionRunIds.Add(fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var rework = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.Created, rework.Disposition);
        Assert.Equal(candidate.Detail.Run.Id, rework.Run.Id);
        var prior = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!;
        Assert.False(prior.ToolAdmission!.HasUnresolvedEffects);
        Assert.Equal(AgentToolEffectState.Unknown, Assert.Single(prior.ToolAdmission.Batches[0].Proposals).EffectState);
        Assert.NotEqual(prior.Id, rework.Run.Id);
    }

    private static ExecutionRunSourceKey BackgroundKey(SourcePolicy policy)
        => ExecutionRunSourceKey.ForBackground(policy.SourceKind, SourcePolicy.StepKey, policy.RunId.ToString("D"), policy.StepId.ToString("D"));

    [Fact]
    public async Task Uncertain_hosted_provider_request_blocks_both_same_claim_recovery_and_later_claim_replacement() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var current = lease.Bind();
            var segment = await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
            await journal.BeginProviderDispatchAsync(lease, segment.Segments[0].Id,
                AgentToolProtocolEnvelope.ComputeDigest("request"), AgentToolProtocolEnvelope.ComputeDigest("native-contract"), [], default);
        }
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId);
        var runtime = new Runtime(journal);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        using var service = Service(fixture.Source, fixture.Source.Store, journal, runtime, cache);
        var error = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => service.RecoverExecutionRunAsync(
            fixture.Reference.ExecutionRunId, AgentExecutionOperationId.New()));
        Assert.Equal("tool-admission.provider-reconciliation-required", error.Code);
        Assert.Equal(0, runtime.Calls);
        fixture.Policy.AcknowledgedExecutionRunIds.Add(fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var blocked = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.SourceReconciliationRequired, blocked.Disposition);
        Assert.True(blocked.Run.ToolAdmission!.HasUnresolvedProviderDispatch);
        Assert.Null(await fixture.Source.NewStore().GetExecutionRunAsync(candidate.Detail.Run.Id));
    }

    [Fact]
    public async Task Acknowledged_native_response_allows_explicit_rework_while_retaining_its_unknown_owner_commit_classification() {
        await using var fixture = await Fixture.CreateAsync();
        var journal = fixture.Journal();
        AgentToolApprovalBinding binding;
        await using (var lease = await journal.AcquireRunAsync(fixture.Reference, default)) {
            using var current = lease.Bind();
            var envelope = AgentToolAdmissionJournalFixture.Envelope();
            var digest = AgentToolProtocolEnvelope.ComputeDigest("native-contract");
            var saved = await journal.BeginSegmentAsync(lease, envelope, null, default);
            var request = await journal.BeginProviderDispatchAsync(lease, saved.Segments[0].Id, digest, digest, [], default);
            var payload = AgentToolAdmissionJournalFixture.Payload(AgentToolProposalRecovery.ReconcileBeforeRetry);
            saved = await journal.CompleteProviderDispatchAsync(lease, request.Id, digest, envelope,
                [new("native-call", payload, true, new("native-server", digest, envelope))], default);
            binding = new(saved.Batches[0].Id, saved.Batches[0].Proposals[0].IntentId, payload.SemanticVersion, payload.Digest);
            var pending = await journal.SaveApprovalCheckpointAsync(lease, saved.Segments[0].Id, envelope, "{}",
                [new("native-approval", "native-call", payload.ToolName, "mcp", "native-server", payload.ArgumentsJson)], default);
            await fixture.Source.Store.UpdateExecutionRunDetailAsync(fixture.Reference.ExecutionRunId, detail => {
                var decisions = pending.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) { ToolAdmission = item.ToolAdmission }).ToArray();
                return detail with { Run = detail.Run with {
                    ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(detail.Run.ToolAdmission!, pending, decisions, false),
                    State = ExecutionState.Running, PendingApprovals = [], Revision = detail.Run.Revision + 1
                } };
            });
            saved = await journal.BeginSegmentAsync(lease, envelope, saved.Segments[0].Id, default);
            var effect = await journal.BeginProviderDispatchAsync(lease, saved.Segments[^1].Id, digest, digest, [binding], default);
            await journal.CompleteProviderDispatchAsync(lease, effect.Id, digest,
                AgentToolAdmissionJournalFixture.Envelope("{\"nativeStatus\":\"succeeded\"}"), [], default);
        }
        await TerminalizeReservationAsync(fixture.Source.Store, fixture.Reference.ExecutionRunId, succeeded: true);
        fixture.Policy.AcknowledgedExecutionRunIds.Add(fixture.Reference.ExecutionRunId);
        fixture.Policy.OwnerFingerprint = new(new string('b', 64));
        var candidate = await PrepareReservationAsync(fixture.Source, fixture.Policy);
        var rework = await fixture.Source.NewStore().ReserveBackgroundExecutionRunAsync(BackgroundKey(fixture.Policy), candidate.Detail, candidate.Reservation);
        Assert.Equal(ExecutionRunSourceDisposition.Created, rework.Disposition);
        Assert.NotEqual(fixture.Reference.ExecutionRunId, rework.Run.Id);
        var prior = (await fixture.Source.NewStore().GetExecutionRunAsync(fixture.Reference.ExecutionRunId))!.ToolAdmission!;
        Assert.All(prior.ProviderDispatches, item => Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted, item.State));
        var completed = AgentToolJournalTransitions.RequireProposal(prior, binding);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(AgentToolEffectState.Unknown, completed.EffectState);
        Assert.False(prior.HasUnresolvedEffects);
        Assert.Equal(binding.IntentId, completed.IntentId);
    }

    private static async Task RunReservationRaceAsync(Func<ReservationRace, Task> action) {
        var source = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
        var race = new ReservationRace(source);
        var failures = new List<Exception>();
        try {
            await action(race);
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            await race.CleanupAsync(failures);
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Background reservation proof and terminal cleanup failed.", failures);
        }
    }

    private enum ReservationConflictEvidence {
        UnrelatedCheckpoint,
        ChatRunJournal,
        NewRunJournal,
        RunUpdateJournal,
        AgentDeletionJournal,
        WorkspacePending,
        RunPending
    }

    private sealed class ReservationRace(AgentToolAdmissionJournalFixture source) {
        private readonly CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
        private readonly DurableFileWriter writer = new(new PhysicalFileSystemPathPolicyFactory());
        private readonly List<Task> pending = [];
        private readonly HashSet<Task> observed = [];
        private IAsyncDisposable? blocker;
        public AgentToolAdmissionJournalFixture Source { get; } = source;
        public FileSandboxWorkspaceStorageLayout Layout { get; } = new(source.WorkspaceRoot, source.StorageScope);
        public CancellationToken Token => deadline.Token;

        public async Task HoldAsync(ExecutionRunRecord run) {
            var keys = FileSandboxWorkspaceWriteSet.Run(null, run, readsAgent: true).LockPaths(Layout);
            blocker = await writer.AcquireCoordinationAsync(Layout.RootPath, keys[0], TimeSpan.FromSeconds(5), false, Token);
        }

        public async Task ReleaseAsync() {
            var owned = blocker;
            blocker = null;
            if (owned is not null) {
                await owned.DisposeAsync();
            }
        }

        public Task<T> Track<T>(Task<T> task) {
            pending.Add(task);
            return task;
        }

        public async Task<T> ObserveAsync<T>(Task<T> task) {
            try {
                var result = await task;
                observed.Add(task);
                return result;
            } catch {
                if (task.IsCompleted) {
                    observed.Add(task);
                }
                throw;
            }
        }

        public async Task RequireWorkspaceAvailableAsync() {
            await using var available = await writer.AcquireCoordinationAsync(Layout.RootPath, Layout.WorkspaceLockPath,
                TimeSpan.FromSeconds(5), false, Token);
            Assert.NotNull(available);
        }

        public async Task RequireKeysAvailableAsync(params ExecutionRunRecord[] runs) {
            foreach (var path in runs.SelectMany(run => FileSandboxWorkspaceWriteSet.Run(null, run, readsAgent: true).LockPaths(Layout))
                    .Append(Layout.WorkspaceLockPath).Distinct()) {
                await using var available = await writer.AcquireCoordinationAsync(Layout.RootPath, path, TimeSpan.FromSeconds(5), false, Token);
                Assert.NotNull(available);
            }
        }

        public string? Marker(ReservationConflictEvidence evidence, Guid runId) => evidence switch {
            ReservationConflictEvidence.UnrelatedCheckpoint => null,
            ReservationConflictEvidence.ChatRunJournal => Path.Combine(Layout.ExecutionStorageRoot, "pending-chat-run-start.json"),
            ReservationConflictEvidence.NewRunJournal => Path.Combine(Layout.ExecutionStorageRoot, "pending-run-start.json"),
            ReservationConflictEvidence.RunUpdateJournal => Path.Combine(Layout.ExecutionStorageRoot, "pending-run-detail-update.json"),
            ReservationConflictEvidence.AgentDeletionJournal => Path.Combine(Layout.ExecutionStorageRoot, "pending-agent-deletion.json"),
            ReservationConflictEvidence.WorkspacePending => Path.Combine(Layout.DataRoot, $".workspace.index.json.{Guid.NewGuid():N}.pending"),
            ReservationConflictEvidence.RunPending => Path.Combine(Layout.RunRoot(runId), $".run.json.{Guid.NewGuid():N}.pending"),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence))
        };

        public async Task CleanupAsync(List<Exception> failures) {
            try {
                await deadline.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                await ReleaseAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            foreach (var task in pending) {
                try {
                    await task;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                } catch (Exception failure) {
                    if (!observed.Contains(task) && !failures.Contains(failure)) {
                        failures.Add(failure);
                    }
                }
            }
            try {
                await Source.DisposeAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            } finally {
                deadline.Dispose();
            }
        }
    }

    private sealed class ReservationPublicationWait : IDisposable {
        private readonly Activity root = new Activity("background-reservation-publication").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        private int arrivals;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ReservationPublicationWait(int expected) {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.publication.keys.wait" &&
                            Interlocked.Increment(ref arrivals) == expected) {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            root.Dispose();
        }
    }

    private static ExecutionRunRequest BackgroundRequest(AgentToolAdmissionJournalFixture source, SourcePolicy policy, string prompt)
        => new(source.Agent.Id, prompt, AgentExecutionOperationId.New(), Context: new(SourceKind: policy.SourceKind, SourceId: SourcePolicy.StepKey,
            CorrelationId: policy.RunId.ToString("D"), CausationId: policy.StepId.ToString("D"), RequestedBy: "process-runtime", RequestedByKind: "system",
            MetadataJson: "{}", ProcessRunId: policy.RunId.ToString("D"), ProcessStepId: policy.StepId.ToString("D")), AutoApprovePendingToolCalls: true);

    private static async Task<(ExecutionRunDetail Detail, AgentToolBackgroundReservation Reservation)> PrepareReservationAsync(
        AgentToolAdmissionJournalFixture source, SourcePolicy policy) {
        var store = source.NewStore();
        var journal = Journal(source, store, policy);
        var run = Detached(source, policy);
        run = run with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(run, "Original admitted work.") };
        return (new(run, null, [], []), await journal.PrepareBackgroundReservationAsync(run));
    }

    private static async Task<AgentToolBusinessIntentId> CompleteReservationEffectAsync(Fixture fixture) {
        var journal = fixture.Journal();
        await using var lease = await journal.AcquireRunAsync(fixture.Reference, default);
        using var current = lease.Bind();
        await journal.BeginSegmentAsync(lease, AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var batch = Assert.Single((await journal.AdmitBatchAsync(lease, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            [new("same-dynamic-call", payload, false)], default)).Batches);
        var claimed = await journal.ClaimInvocationAsync(lease, batch.Id, "same-dynamic-call", payload, default);
        await journal.CompleteInvocationAsync(claimed, AgentToolAdmissionJournalFixture.Envelope("{\"ownerId\":31}"), AgentToolEffectState.Committed, default);
        return claimed.Proposal.IntentId;
    }

    private static Task<ExecutionRunDetail> TerminalizeReservationAsync(FileSandboxWorkspaceStore store, Guid runId, bool succeeded = false)
        => store.UpdateExecutionRunDetailAsync(runId, detail => detail with { Run = detail.Run with {
            State = succeeded ? ExecutionState.Completed : ExecutionState.Failed,
            Outcome = succeeded ? RunOutcome.Succeeded : RunOutcome.Failed, CompletedAtUtc = DateTimeOffset.UtcNow,
            ResultSummary = "The Process owner must accept this exact terminal result.", Revision = detail.Run.Revision + 1
        } });
}
